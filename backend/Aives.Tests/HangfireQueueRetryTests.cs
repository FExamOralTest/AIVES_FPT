using System.Linq.Expressions;
using System.Reflection;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Contracts.Stt;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Queue;
using Aives.Infrastructure.Repositories;
using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Test cho <see cref="AnswerProcessingJob"/> retry pipeline (Task 2.18):
/// <list type="number">
///   <item><b>A. Direct job logic</b> — success / retryable / non-retryable, không qua Hangfire.</item>
///   <item><b>B. Hangfire retry thật</b> — MemoryStorage + BackgroundJobServer, verify số attempt.</item>
///   <item><b>C. HangfireQueueService dedup</b> — enqueue 2 lần cùng answerId.</item>
///   <item><b>D. AutomaticRetry attribute</b> — config tĩnh (3 attempts, 30/120/300, Delete).</item>
///   <item><b>E. Real DB + real AnswerRepository</b> — verify job không tự sửa Answer.Status.</item>
/// </list>
/// </summary>
public class HangfireQueueRetryTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly Mock<IAnswerOrchestrator> _orchestratorMock = new();
    private readonly Mock<IAnswerRepository> _repoMock = new();
    private readonly AnswerProcessingJob _job;

    public HangfireQueueRetryTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"HangfireRetry_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);

        _repoMock.Setup(r => r.MarkFailedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _job = new AnswerProcessingJob(
            _orchestratorMock.Object,
            _repoMock.Object,
            NullLogger<AnswerProcessingJob>.Instance);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        // Reset static counters between tests
        TestRetryJob.ResetCounters();
    }

    // ===================================================================
    // A. Direct job logic
    // ===================================================================

    [Fact]
    public async Task A1_ExecuteAsync_Success_SetsMetadataStatus_Completed_DoesNotThrow()
    {
        var metadata = NewMetadata();
        _orchestratorMock.Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult { Success = true });

        await _job.ExecuteAsync(metadata, CancellationToken.None);

        Assert.Equal(QueueJobStatus.Completed, metadata.Status);
        Assert.Null(metadata.LastError);
    }

    [Fact]
    public async Task A2_ExecuteAsync_RetryableError_ThrowsRetryableJobException_SetsStatusFailed()
    {
        var metadata = NewMetadata();
        _orchestratorMock.Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult
            {
                Success = false,
                SttError = new SttError
                {
                    Code = SttErrorCode.Timeout,
                    Message = "AI STT timed out.",
                    Retryable = true
                }
            });

        var ex = await Assert.ThrowsAsync<AnswerProcessingJob.RetryableJobException>(
            () => _job.ExecuteAsync(metadata, CancellationToken.None));
        Assert.Contains("Retryable error", ex.Message);
        Assert.Equal(QueueJobStatus.Failed, metadata.Status);
        Assert.Equal("AI STT timed out.", metadata.LastError);
    }

    [Fact]
    public async Task A3_ExecuteAsync_NonRetryableSttError_ThrowsNonRetryableJobException_SetsStatusDeadLetter()
    {
        var metadata = NewMetadata();
        _orchestratorMock.Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult
            {
                Success = false,
                SttError = new SttError
                {
                    Code = SttErrorCode.AudioCorrupted,
                    Message = "Audio file is corrupted.",
                    Retryable = false
                }
            });

        var ex = await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => _job.ExecuteAsync(metadata, CancellationToken.None));
        Assert.Contains("AudioCorrupted", ex.Message);
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);
        Assert.Equal("Audio file is corrupted.", metadata.LastError);
    }

    [Fact]
    public async Task A4_ExecuteAsync_NonRetryableEmptyTranscript_ThrowsNonRetryableJobException()
    {
        // Empty transcript path — đặc biệt cho trường hợp audio im lặng.
        var metadata = NewMetadata();
        _orchestratorMock.Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult
            {
                Success = false,
                SttError = new SttError
                {
                    Code = SttErrorCode.EmptyTranscript,
                    Message = "Empty transcript after STT.",
                    Retryable = false
                }
            });

        await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => _job.ExecuteAsync(metadata, CancellationToken.None));
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);
    }

    [Fact]
    public async Task A5_ExecuteAsync_EvaluationError_ThrowsNonRetryable_DeadLetter()
    {
        // Path lỗi Eval (không qua STT) → NonRetryable + dead-letter.
        // Lý do: AI2 evaluation trả Success=false là business-level failure
        // (không phải transient như timeout). Retry sẽ cho cùng kết quả.
        var metadata = NewMetadata();
        _orchestratorMock.Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult
            {
                Success = false,
                EvaluationErrorCode = "internal_error",
                Message = "Eval crash"
            });

        await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => _job.ExecuteAsync(metadata, CancellationToken.None));
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);
        Assert.Equal("Eval crash", metadata.LastError);
    }

    [Fact]
    public async Task A6_ExecuteAsync_Success_DoesNotCallMarkFailed()
    {
        var metadata = NewMetadata();
        _orchestratorMock.Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult { Success = true });

        await _job.ExecuteAsync(metadata, CancellationToken.None);

        _repoMock.Verify(r => r.MarkFailedAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void A7_ExceptionTypes_AreDistinctClasses()
    {
        // Hai exception phải là type khác nhau — Hangfire filter dùng `is` để phân biệt.
        Assert.False(
            typeof(AnswerProcessingJob.RetryableJobException)
                .IsAssignableTo(typeof(AnswerProcessingJob.NonRetryableJobException)));
        Assert.False(
            typeof(AnswerProcessingJob.NonRetryableJobException)
                .IsAssignableTo(typeof(AnswerProcessingJob.RetryableJobException)));
    }

    // ===================================================================
    // D. AutomaticRetry attribute config
    // ===================================================================

    [Fact]
    public void D1_AutomaticRetryAttribute_HasCorrectConfiguration()
    {
        var attr = typeof(AnswerProcessingJob)
            .GetCustomAttribute<AutomaticRetryAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(3, attr!.Attempts);
        Assert.Equal(new[] { 30, 120, 300 }, attr.DelaysInSeconds);
        Assert.Equal(AttemptsExceededAction.Delete, attr.OnAttemptsExceeded);
    }

    // ===================================================================
    // B. Real Hangfire retry pipeline (MemoryStorage)
    // ===================================================================

    [Fact]
    public async Task B1_Hangfire_NonRetryable_StopsAtFirstAttempt_NoRetry()
    {
        UseFreshMemoryStorage();
        using var server = StartServer();

        var client = new BackgroundJobClient(_currentStorage);
        var jobId = client.Create(Job.FromExpression(() => TestRetryJob.ThrowsNonRetryable()), new EnqueuedState());

        await WaitForJobStateAsync(jobId, FailedState.StateName, TimeSpan.FromSeconds(10));

        // NonRetryableException → Hangfire filter skip retry.
        Assert.Equal(1, TestRetryJob.CallCount);
    }

    [Fact]
    public async Task B2_Hangfire_Retryable_RetriesUpToMaxAttempts_ThenFails()
    {
        UseFreshMemoryStorage();
        using var server = StartServer();

        var client = new BackgroundJobClient(_currentStorage);
        var jobId = client.Create(Job.FromExpression(() => TestRetryJob.ThrowsGenericRetryable()), new EnqueuedState());

        await WaitForJobStateAsync(jobId, FailedState.StateName, TimeSpan.FromSeconds(15));

        // Hangfire's `AutomaticRetry(Attempts=N)` thực sự là N lần RETRY, tổng attempts = N+1.
        // 1 initial + 3 retries = 4 calls. Delays 0s nên gần như tức thì.
        Assert.Equal(4, TestRetryJob.CallCount);

        // Verify job state = Failed (vượt max attempts).
        using var connection = _currentStorage.GetConnection();
        var jobData = connection.GetJobData(jobId);
        Assert.Equal(FailedState.StateName, jobData.State);
    }

    [Fact]
    public async Task B3_Hangfire_JobSucceeds_StateIsSucceeded()
    {
        UseFreshMemoryStorage();
        using var server = StartServer();

        var client = new BackgroundJobClient(_currentStorage);
        var jobId = client.Create(Job.FromExpression(() => TestRetryJob.ReturnsOk()), new EnqueuedState());

        await WaitForJobStateAsync(jobId, SucceededState.StateName, TimeSpan.FromSeconds(10));

        Assert.Equal(1, TestRetryJob.CallCount);
    }

    // ===================================================================
    // C. HangfireQueueService dedup (real Hangfire)
    // ===================================================================

    [Fact]
    public void C1_HangfireQueueService_Dedup_PreventsDoubleEnqueue_SameAnswerId()
    {
        UseFreshMemoryStorage();
        // Không start server để job đầu nằm yên trong Enqueued state → query thấy.
        var jobClient = new BackgroundJobClient(_currentStorage);
        var svc = new HangfireQueueService(
            jobClient, NullLogger<HangfireQueueService>.Instance);

        var m1 = new QueueJobMetadata { AnswerId = 7777, ResultId = 1, ObjectKey = "k", Attempt = 1 };
        var m2 = new QueueJobMetadata { AnswerId = 7777, ResultId = 1, ObjectKey = "k", Attempt = 1 };

        // Set JobStorage.Current để HangfireQueueService (singleton) có thể resolve IMonitoringApi.
        // C2 test StartServer cũng tự set Current — wrap trong try/finally phòng concurrent tests.
        JobStorage.Current = _currentStorage;
        try
        {
            var id1 = svc.EnqueueAnswerProcessing(m1);
            var id2 = svc.EnqueueAnswerProcessing(m2);

            Assert.False(string.IsNullOrEmpty(id1), "First enqueue should succeed.");
            Assert.True(string.IsNullOrEmpty(id2), "Second enqueue (same answer) should be deduped.");
        }
        finally
        {
            // Reset về null để test khác tự setup (tránh pollution giữa tests).
            try { JobStorage.Current = null!; } catch { /* ignore */ }
        }
    }

    [Fact]
    public void C2_HangfireQueueService_DifferentAnswerIds_BothEnqueued()
    {
        UseFreshMemoryStorage();
        using var server = StartServer();

        var jobClient = new BackgroundJobClient(_currentStorage);
        var svc = new HangfireQueueService(
            jobClient, NullLogger<HangfireQueueService>.Instance);

        var m1 = new QueueJobMetadata { AnswerId = 1001, ResultId = 1, ObjectKey = "k", Attempt = 1 };
        var m2 = new QueueJobMetadata { AnswerId = 1002, ResultId = 1, ObjectKey = "k", Attempt = 1 };

        var id1 = svc.EnqueueAnswerProcessing(m1);
        var id2 = svc.EnqueueAnswerProcessing(m2);

        Assert.False(string.IsNullOrEmpty(id1));
        Assert.False(string.IsNullOrEmpty(id2));
        Assert.NotEqual(id1, id2);
    }

    // ===================================================================
    // E. Real DB + real AnswerRepository
    // ===================================================================

    [Fact]
    public async Task E1_RealAnswerProcessingJob_RealDb_NonRetryable_AnswerStatusUnchanged()
    {
        // Khi AnswerProcessingJob fail non-retryable, job KHÔNG tự sửa Answer.Status
        // (status sẽ được controller khác xử lý / user click retry).
        // Verify: Answer.Status giữ nguyên PENDING sau khi job throw.
        var seed = await SeedRealAnswerAsync();
        var metadata = new QueueJobMetadata
        {
            JobId = "test",
            AnswerId = seed.AnswerId,
            ResultId = seed.ResultId,
            ObjectKey = "k",
            Attempt = 1,
            Status = QueueJobStatus.Pending
        };

        var realRepo = new AnswerRepository(_db);
        var orchestratorMock = new Mock<IAnswerOrchestrator>();
        orchestratorMock.Setup(o => o.ProcessAnswerAsync(seed.AnswerId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult
            {
                Success = false,
                SttError = new SttError
                {
                    Code = SttErrorCode.AudioCorrupted,
                    Message = "bad",
                    Retryable = false
                }
            });

        var job = new AnswerProcessingJob(orchestratorMock.Object, realRepo, NullLogger<AnswerProcessingJob>.Instance);

        await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(metadata, CancellationToken.None));

        var dbAnswer = await _db.Answers.FindAsync(seed.AnswerId);
        Assert.Equal(AnswerStatus.PROCESSING, dbAnswer!.Status);
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static QueueJobMetadata NewMetadata() => new()
    {
        JobId = $"test-{Guid.NewGuid():N}",
        AnswerId = 1,
        ResultId = 1,
        ObjectKey = "k",
        Attempt = 1,
        Status = QueueJobStatus.Pending,
        EnqueuedAt = DateTime.UtcNow
    };

    private async Task<(int AnswerId, int ResultId)> SeedRealAnswerAsync()
    {
        var course = new Course { CourseCode = "HF", CourseName = "Hangfire Test" };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var student = new User
        {
            UserId = 5001, Username = "s1", Email = "s1@a.com", FullName = "S",
            Role = UserRole.STUDENT, PasswordHash = "x"
        };
        _db.Users.Add(student);

        var exam = new Exam { ExamCode = "HF-E", Title = "HF", CourseId = course.CourseId, DurationMinutes = 30 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "HF-S",
            SessionName = "HF Session",
            Status = SessionStatus.IN_PROGRESS,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(1),
            ScheduledBy = 1
        };
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = new Result
        {
            SessionId = session.SessionId,
            StudentId = student.UserId,
            Status = SubmissionStatus.PENDING_REVIEW,
            SubmittedAt = DateTime.UtcNow
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var qb = new QuestionBank { CourseId = course.CourseId, QuestionText = "Q", MaxScore = 10 };
        _db.QuestionBanks.Add(qb);
        await _db.SaveChangesAsync();

        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = qb.QuestionId,
            Status = AnswerStatus.PROCESSING,
            StudentAnswer = "abc",
            AudioUrl = "exams/1/sessions/1/students/5001/q_1.webm"
        };
        _db.Answers.Add(answer);
        await _db.SaveChangesAsync();

        return (answer.AnswerId, result.ResultId);
    }

    private static void UseFreshMemoryStorage()
    {
        // Hangfire.MemoryStorage stateless — instance mới = data mới.
        // Ta tạo storage riêng cho mỗi test để job từ test cũ không nhiễu.
        // Storage được pass trực tiếp vào BackgroundJobServer/Client (không qua JobStorage.Current).
        _currentStorage = new MemoryStorage(new MemoryStorageOptions
        {
            FetchNextJobTimeout = TimeSpan.FromMilliseconds(50)
        });
        TestRetryJob.ResetCounters();
    }

    private static MemoryStorage _currentStorage = new(new MemoryStorageOptions());

    private static BackgroundJobServer StartServer() =>
        new(new BackgroundJobServerOptions
        {
            WorkerCount = 1,
            SchedulePollingInterval = TimeSpan.FromMilliseconds(50),
            HeartbeatInterval = TimeSpan.FromMilliseconds(50),
            ServerCheckInterval = TimeSpan.FromMilliseconds(50),
            StopTimeout = TimeSpan.FromSeconds(1)
        }, _currentStorage);

    private static async Task WaitForJobStateAsync(string jobId, string targetState, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        using var connection = _currentStorage.GetConnection();
        while (DateTime.UtcNow < deadline)
        {
            var data = connection.GetJobData(jobId);
            if (data != null && data.State == targetState) return;
            await Task.Delay(50);
        }
        var finalData = connection.GetJobData(jobId);
        throw new TimeoutException(
            $"Job {jobId} did not reach state '{targetState}' within {timeout}. Final state: {finalData?.State ?? "null"}");
    }
}

/// <summary>
/// Job body thuần cho Hangfire retry thật (Task 2.18) — dùng static counter.
/// </summary>
public class TestRetryJob
{
    public static int CallCount { get; private set; }

    public static void ResetCounters() => CallCount = 0;

    /// <summary>
    /// Throw exception mà Hangfire skip retry (Attempts=0).
    /// Mô phỏng <c>NonRetryableJobException</c> thật trong AnswerProcessingJob:
    /// ngoài đời, exception type riêng + JobFilter sẽ chặn retry.
    /// Ở đây dùng attribute đơn giản vì cùng ý nghĩa.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public static void ThrowsNonRetryable()
    {
        CallCount++;
        throw new InvalidOperationException("non-retryable for test");
    }

    /// <summary>Throw exception generic → Hangfire retry theo attribute (3 attempts).</summary>
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 0, 0, 0 })]
    public static void ThrowsGenericRetryable()
    {
        CallCount++;
        throw new InvalidOperationException("retryable for test");
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 0, 0, 0 })]
    public static void ReturnsOk()
    {
        CallCount++;
    }
}
