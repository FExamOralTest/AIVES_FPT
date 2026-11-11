using System.Collections.Concurrent;
using System.Reflection;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Contracts.Stt;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Queue;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 2 — 100 bài nộp đồng thời không bị nhầm sinh viên.
///
/// Bộ test này khoá các hành vi sau:
/// <list type="bullet">
///   <item><b>Queue riêng</b> — job xử lý answer chạy trên queue <c>answers</c>, tách khỏi
///         <c>grading</c>.</item>
///   <item><b>Ownership</b> — metadata mang <c>StudentId</c>/<c>SessionId</c>/<c>ResultId</c>
///         của chủ sở hữu; worker đối chiếu với DB trước khi chạy pipeline. Metadata gán
///         chéo bị dead-letter mà không đụng tới bài của sinh viên đúng.</item>
///   <item><b>Đồng thời</b> — 100 job chạy song song, mỗi job chỉ chạm đúng answer/result/
///         student của nó.</item>
///   <item><b>Dedup</b> — nhiều request đồng thời cho cùng answer chỉ sinh đúng 1 job.</item>
///   <item><b>Trạng thái job</b> — <see cref="HangfireQueueService.GetJobState"/> đọc được
///         state thật từ Hangfire.</item>
/// </list>
/// </summary>
public class AnswerQueueConcurrencyTests
{
    // ===================================================================
    // A. Queue riêng cho answer processing
    // ===================================================================

    [Fact]
    public void A1_AnswerProcessingJob_UsesAnswersQueue()
    {
        Assert.Equal("answers", AnswerProcessingJob.QueueName);

        var attribute = typeof(AnswerProcessingJob)
            .GetMethod(nameof(AnswerProcessingJob.ExecuteAsync))!
            .GetCustomAttribute<QueueAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(AnswerProcessingJob.QueueName, attribute!.Queue);
    }

    [Fact]
    public void A2_AnswersQueue_IsDifferentFromGradingQueue()
    {
        // Chấm cả bài chạy tuần tự trên queue `grading`; STT/evaluation từng câu phải
        // chạy trên queue riêng để bài đang chấm không chặn audio mới.
        Assert.NotEqual(AnswerProcessingJob.QueueName, SessionGradingJob.QueueName);
    }

    // ===================================================================
    // B. Ownership contract
    // ===================================================================

    [Fact]
    public void B1_MetadataWithBothOwnerIds_IsValid()
    {
        var metadata = QueueJobContract.Create(
            answerId: 10, resultId: 20, objectKey: "k", attempt: 1,
            studentId: 5001, sessionId: 300);

        Assert.Equal(5001, metadata.StudentId);
        Assert.Equal(300, metadata.SessionId);
        Assert.True(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void B2_MetadataWithOnlyStudentId_IsRejected()
    {
        var metadata = new QueueJobMetadata
        {
            AnswerId = 10,
            Attempt = 1,
            StudentId = 5001,
            SessionId = null
        };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("StudentId", error);
        Assert.Contains("SessionId", error);
    }

    [Fact]
    public void B3_MetadataWithOnlySessionId_IsRejected()
    {
        var metadata = new QueueJobMetadata
        {
            AnswerId = 10,
            Attempt = 1,
            StudentId = null,
            SessionId = 300
        };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("StudentId", error);
        Assert.Contains("SessionId", error);
    }

    [Fact]
    public void B4_MetadataWithoutOwnership_IsStillValid_ForLegacyJobs()
    {
        // Job enqueue trước khi nâng cấp không có StudentId/SessionId. Payload phải
        // deserialize và validate được để không phá job đang chờ trong queue.
        var metadata = new QueueJobMetadata { AnswerId = 10, Attempt = 1 };

        Assert.True(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Null(error);
    }

    // ===================================================================
    // C. Ownership enforcement trong AnswerProcessingJob
    // ===================================================================

    [Fact]
    public async Task C1_MatchingOwnership_RunsPipelineAndSyncsMetadata()
    {
        var repository = new FakeAnswerRepository(OwnedAnswer(1, resultId: 11, studentId: 5001, sessionId: 300));
        var orchestrator = new Mock<IAnswerOrchestrator>();
        orchestrator
            .Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult { Success = true });

        var job = NewJob(orchestrator, repository);
        var metadata = NewMetadata(1, resultId: 11, studentId: 5001, sessionId: 300);

        await job.ExecuteAsync(metadata, CancellationToken.None);

        Assert.Equal(QueueJobStatus.Completed, metadata.Status);
        Assert.Equal(5001, metadata.StudentId);
        Assert.Equal(300, metadata.SessionId);
        Assert.Equal(11, metadata.ResultId);
        orchestrator.Verify(
            o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Empty(repository.Failures);
    }

    [Fact]
    public async Task C2_StudentMismatch_IsDeadLettered_WithoutTouchingPipelineOrDbAnswer()
    {
        // Job của sinh viên 9999 nhưng answer thuộc sinh viên 5001.
        var repository = new FakeAnswerRepository(OwnedAnswer(1, resultId: 11, studentId: 5001, sessionId: 300));
        var orchestrator = new Mock<IAnswerOrchestrator>();
        var job = NewJob(orchestrator, repository);
        var metadata = NewMetadata(1, resultId: 11, studentId: 9999, sessionId: 300);

        var ex = await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(metadata, CancellationToken.None));

        Assert.Contains("Ownership mismatch", ex.Message);
        Assert.Contains("9999", ex.Message);
        Assert.Contains("5001", ex.Message);
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);

        // Bài của sinh viên đúng phải nguyên vẹn: không chạy pipeline, không MarkFailed,
        // không tăng attempt counter.
        orchestrator.Verify(
            o => o.ProcessAnswerAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Empty(repository.Failures);
        Assert.Empty(repository.IncrementCalls);
    }

    [Fact]
    public async Task C3_SessionMismatch_IsDeadLettered()
    {
        var repository = new FakeAnswerRepository(OwnedAnswer(1, resultId: 11, studentId: 5001, sessionId: 300));
        var orchestrator = new Mock<IAnswerOrchestrator>();
        var job = NewJob(orchestrator, repository);
        var metadata = NewMetadata(1, resultId: 11, studentId: 5001, sessionId: 999);

        var ex = await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(metadata, CancellationToken.None));

        Assert.Contains("SessionId", ex.Message);
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);
        orchestrator.Verify(
            o => o.ProcessAnswerAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task C4_ResultMismatch_IsDeadLettered()
    {
        var repository = new FakeAnswerRepository(OwnedAnswer(1, resultId: 11, studentId: 5001, sessionId: 300));
        var orchestrator = new Mock<IAnswerOrchestrator>();
        var job = NewJob(orchestrator, repository);

        // Cùng student/session nhưng ResultId trỏ sang bài nộp khác.
        var metadata = NewMetadata(1, resultId: 77, studentId: 5001, sessionId: 300);

        var ex = await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(metadata, CancellationToken.None));

        Assert.Contains("ResultId", ex.Message);
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);
        orchestrator.Verify(
            o => o.ProcessAnswerAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task C5_UnresolvableOwner_IsDeadLettered()
    {
        // Answer bị xoá giữa lúc enqueue và lúc worker chạy.
        var repository = new FakeAnswerRepository();
        var orchestrator = new Mock<IAnswerOrchestrator>();
        var job = NewJob(orchestrator, repository);
        var metadata = NewMetadata(1, resultId: 11, studentId: 5001, sessionId: 300);

        var ex = await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(metadata, CancellationToken.None));

        Assert.Contains("ownership could not be resolved", ex.Message);
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);
        orchestrator.Verify(
            o => o.ProcessAnswerAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task C6_LegacyMetadataWithoutOwnership_SkipsOwnershipCheck()
    {
        // Tương thích ngược: job cũ không có StudentId/SessionId vẫn chạy như trước.
        var repository = new FakeAnswerRepository(OwnedAnswer(1, resultId: 11, studentId: 5001, sessionId: 300));
        var orchestrator = new Mock<IAnswerOrchestrator>();
        orchestrator
            .Setup(o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult { Success = true });

        var job = NewJob(orchestrator, repository);
        var metadata = new QueueJobMetadata
        {
            JobId = "legacy-job",
            AnswerId = 1,
            ResultId = 11,
            ObjectKey = "k",
            Attempt = 1,
            Status = QueueJobStatus.Pending
        };

        await job.ExecuteAsync(metadata, CancellationToken.None);

        Assert.Equal(QueueJobStatus.Completed, metadata.Status);
        orchestrator.Verify(
            o => o.ProcessAnswerAsync(1, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ===================================================================
    // D. 100 bài nộp đồng thời
    // ===================================================================

    [Fact]
    public async Task D1_OneHundredConcurrentJobs_EachProcessesOnlyItsOwnAnswer()
    {
        const int count = 100;

        var answers = Enumerable.Range(1, count)
            .Select(i => OwnedAnswer(
                answerId: i,
                resultId: 1000 + i,
                studentId: 5000 + i,
                sessionId: 300))
            .ToList();

        var repository = new FakeAnswerRepository(answers);
        var orchestrator = new Mock<IAnswerOrchestrator>();

        var processed = new ConcurrentBag<int>();
        var active = 0;
        var maxConcurrent = 0;

        orchestrator
            .Setup(o => o.ProcessAnswerAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async (int answerId, int attempt, CancellationToken ct) =>
            {
                // Ghi nhận mức độ song song thật sự của pipeline.
                var current = Interlocked.Increment(ref active);
                InterlockedExtensions.Max(ref maxConcurrent, current);

                try
                {
                    await Task.Delay(5, ct);
                    processed.Add(answerId);
                    return new OrchestrationResult { Success = true };
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            });

        var job = NewJob(orchestrator, repository);

        // Mỗi bài nộp có metadata riêng, mang đúng chủ sở hữu của answer tương ứng.
        var metadataByAnswer = answers.ToDictionary(
            answer => answer.AnswerId,
            answer => NewMetadata(
                answer.AnswerId,
                answer.ResultId,
                answer.Result.StudentId,
                answer.Result.SessionId));

        var tasks = metadataByAnswer.Values
            .Select(metadata => job.ExecuteAsync(metadata, CancellationToken.None))
            .ToArray();

        await Task.WhenAll(tasks);

        // 1. Tất cả job thành công, không job nào bị dead-letter.
        Assert.All(metadataByAnswer.Values, metadata =>
            Assert.Equal(QueueJobStatus.Completed, metadata.Status));

        // 2. Mỗi answer được xử lý đúng một lần, không trùng, không sót.
        Assert.Equal(count, processed.Count);
        Assert.Equal(count, processed.Distinct().Count());
        Assert.Equal(
            Enumerable.Range(1, count).OrderBy(i => i),
            processed.OrderBy(i => i));

        // 3. Không nhầm sinh viên: metadata sau khi chạy vẫn trỏ đúng chủ sở hữu DB.
        foreach (var answer in answers)
        {
            var metadata = metadataByAnswer[answer.AnswerId];
            Assert.Equal(answer.ResultId, metadata.ResultId);
            Assert.Equal(answer.Result.StudentId, metadata.StudentId);
            Assert.Equal(answer.Result.SessionId, metadata.SessionId);
        }

        // 4. Các job thực sự chạy song song, không bị serialize toàn bộ.
        Assert.True(maxConcurrent > 1, $"Expected concurrent execution, max was {maxConcurrent}.");

        // 5. Không có bài nào bị đánh dấu lỗi.
        Assert.Empty(repository.Failures);
        Assert.Equal(count, repository.IncrementCalls.Count);
    }

    [Fact]
    public async Task D2_OneMismatchedJob_DoesNotAffectTheOtherNinetyNine()
    {
        const int count = 100;

        var answers = Enumerable.Range(1, count)
            .Select(i => OwnedAnswer(
                answerId: i,
                resultId: 1000 + i,
                studentId: 5000 + i,
                sessionId: 300))
            .ToList();

        var repository = new FakeAnswerRepository(answers);
        var orchestrator = new Mock<IAnswerOrchestrator>();
        orchestrator
            .Setup(o => o.ProcessAnswerAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrchestrationResult { Success = true });

        var job = NewJob(orchestrator, repository);

        var metadataByAnswer = answers.ToDictionary(
            answer => answer.AnswerId,
            answer => NewMetadata(
                answer.AnswerId,
                answer.ResultId,
                answer.Result.StudentId,
                answer.Result.SessionId));

        // Answer 100 thuộc sinh viên 5100, nhưng metadata gán sang 5000.
        metadataByAnswer[100].StudentId = 5000;

        var tasks = metadataByAnswer.Values
            .Select(metadata => job.ExecuteAsync(metadata, CancellationToken.None))
            .ToArray();

        var failures = await Record.ExceptionAsync(() => Task.WhenAll(tasks));

        // Task.WhenAll ném exception vì job sai ownership throw NonRetryableJobException.
        Assert.IsType<AnswerProcessingJob.NonRetryableJobException>(failures);

        // 99 bài hợp lệ vẫn hoàn tất bình thường.
        var completed = metadataByAnswer.Values
            .Count(metadata => metadata.Status == QueueJobStatus.Completed);
        Assert.Equal(count - 1, completed);

        // Chỉ job gán chéo bị dead-letter.
        var deadLettered = metadataByAnswer.Values
            .Where(metadata => metadata.Status == QueueJobStatus.DeadLetter)
            .ToList();
        Assert.Single(deadLettered);
        Assert.Equal(100, deadLettered[0].AnswerId);

        // Không bài nào bị MarkFailed hay tăng attempt counter oan.
        Assert.Empty(repository.Failures);
        Assert.Equal(count - 1, repository.IncrementCalls.Count);
        orchestrator.Verify(
            o => o.ProcessAnswerAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Exactly(count - 1));
    }

    // ===================================================================
    // E. HangfireQueueService — 100 enqueue đồng thời + dedup
    // ===================================================================

    [Fact]
    public async Task E1_OneHundredConcurrentEnqueues_LandOnAnswersQueue_WithDistinctJobIds()
    {
        var storage = new MemoryStorage(new MemoryStorageOptions
        {
            FetchNextJobTimeout = TimeSpan.FromMilliseconds(50)
        });
        var client = new BackgroundJobClient(storage);
        var service = new HangfireQueueService(
            client, storage.GetMonitoringApi(), NullLogger<HangfireQueueService>.Instance);

        var metadatas = Enumerable.Range(1, 100)
            .Select(i => NewMetadata(i, resultId: 1000 + i, studentId: 5000 + i, sessionId: 300))
            .ToList();

        var jobIds = await Task.WhenAll(
            metadatas.Select(metadata => Task.Run(() => service.EnqueueAnswerProcessing(metadata))));

        Assert.All(jobIds, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.Equal(100, jobIds.Distinct().Count());

        var monitoring = storage.GetMonitoringApi();
        Assert.Equal(100, monitoring.EnqueuedJobs(AnswerProcessingJob.QueueName, 0, 200).Count);
        Assert.Empty(monitoring.EnqueuedJobs("default", 0, 200));
    }

    [Fact]
    public async Task E2_ConcurrentEnqueuesForSameAnswer_ProduceExactlyOneJob()
    {
        var storage = new MemoryStorage(new MemoryStorageOptions
        {
            FetchNextJobTimeout = TimeSpan.FromMilliseconds(50)
        });
        var client = new BackgroundJobClient(storage);
        var service = new HangfireQueueService(
            client, storage.GetMonitoringApi(), NullLogger<HangfireQueueService>.Instance);

        // 32 request đồng thời cho cùng một answer (user double-click / retry storm).
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            var metadata = NewMetadata(42, resultId: 1042, studentId: 5042, sessionId: 300);
            return service.EnqueueAnswerProcessing(metadata);
        })));

        Assert.Single(results.Where(id => !string.IsNullOrEmpty(id)));
        Assert.Single(
            storage.GetMonitoringApi().EnqueuedJobs(AnswerProcessingJob.QueueName, 0, 200));
    }

    // ===================================================================
    // F. Trạng thái job từ Hangfire monitoring API
    // ===================================================================

    [Fact]
    public void F1_GetJobState_ReturnsEnqueuedStateForQueuedAnswerJob()
    {
        var storage = new MemoryStorage(new MemoryStorageOptions());
        var client = new BackgroundJobClient(storage);
        var service = new HangfireQueueService(
            client, storage.GetMonitoringApi(), NullLogger<HangfireQueueService>.Instance);

        var jobId = service.EnqueueAnswerProcessing(
            NewMetadata(77, resultId: 1077, studentId: 5077, sessionId: 300));

        var state = service.GetJobState(jobId);

        Assert.NotNull(state);
        Assert.Equal(jobId, state!.JobId);
        Assert.Equal("Enqueued", state.State);
        Assert.Equal(QueueJobStatus.Pending, state.Status);
        Assert.Equal(nameof(AnswerProcessingJob), state.JobType);
    }

    [Fact]
    public void F2_GetJobState_UnknownOrEmptyJobId_ReturnsNull()
    {
        var storage = new MemoryStorage(new MemoryStorageOptions());
        var client = new BackgroundJobClient(storage);
        var service = new HangfireQueueService(
            client, storage.GetMonitoringApi(), NullLogger<HangfireQueueService>.Instance);

        Assert.Null(service.GetJobState(string.Empty));
        Assert.Null(service.GetJobState("   "));
        Assert.Null(service.GetJobState("does-not-exist"));
    }

    [Fact]
    public void F3_GetSessionGradingState_ReturnsNullWithoutQueuedJob()
    {
        var storage = new MemoryStorage(new MemoryStorageOptions());
        var client = new BackgroundJobClient(storage);
        var service = new HangfireQueueService(
            client, storage.GetMonitoringApi(), NullLogger<HangfireQueueService>.Instance);

        Assert.Null(service.GetSessionGradingState(0));
        Assert.Null(service.GetSessionGradingState(9999));
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static AnswerProcessingJob NewJob(
        Mock<IAnswerOrchestrator> orchestrator, IAnswerRepository repository) =>
        new(orchestrator.Object, repository, NullLogger<AnswerProcessingJob>.Instance);

    private static QueueJobMetadata NewMetadata(
        int answerId, int resultId, int studentId, int sessionId)
    {
        var metadata = QueueJobContract.Create(
            answerId: answerId,
            resultId: resultId,
            objectKey: $"sessions/300/answers/{answerId}.webm",
            attempt: 1,
            studentId: studentId,
            sessionId: sessionId);
        metadata.JobId = $"job-{answerId}";
        return metadata;
    }

    private static Answer OwnedAnswer(int answerId, int resultId, int studentId, int sessionId) =>
        new()
        {
            AnswerId = answerId,
            ResultId = resultId,
            QuestionId = answerId,
            Status = AnswerStatus.PROCESSING,
            StudentAnswer = "answer",
            Result = new Result
            {
                ResultId = resultId,
                StudentId = studentId,
                SessionId = sessionId,
                Status = SubmissionStatus.PENDING_REVIEW
            }
        };

    private static class InterlockedExtensions
    {
        public static void Max(ref int target, int value)
        {
            int current;
            while (value > (current = Volatile.Read(ref target)))
            {
                if (Interlocked.CompareExchange(ref target, value, current) == current)
                    return;
            }
        }
    }

    /// <summary>
    /// Repository thread-safe cho test đồng thời. Không dùng EF InMemory vì một DbContext
    /// không thread-safe; ở đây chỉ cần mô phỏng đúng dữ liệu ownership trả về cho worker.
    /// </summary>
    private sealed class FakeAnswerRepository : IAnswerRepository
    {
        private readonly ConcurrentDictionary<int, Answer> _answers;
        private readonly ConcurrentDictionary<int, int> _attempts = new();

        public FakeAnswerRepository(params Answer[] answers)
            : this((IEnumerable<Answer>)answers)
        {
        }

        public FakeAnswerRepository(IEnumerable<Answer> answers) =>
            _answers = new ConcurrentDictionary<int, Answer>(
                answers.ToDictionary(answer => answer.AnswerId));

        public ConcurrentBag<(int AnswerId, string? JobId)> IncrementCalls { get; } = new();

        public ConcurrentBag<(int AnswerId, string Error)> Failures { get; } = new();

        public Task<Answer?> GetByIdWithDetailsAsync(
            int answerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_answers.TryGetValue(answerId, out var answer) ? answer : null);

        public Task<int> IncrementAttemptAsync(
            int answerId, string? jobId, CancellationToken cancellationToken = default)
        {
            if (!_answers.ContainsKey(answerId))
                return Task.FromResult(0);

            var attempt = _attempts.AddOrUpdate(answerId, 1, (_, current) => current + 1);
            IncrementCalls.Add((answerId, jobId));
            return Task.FromResult(attempt);
        }

        public Task MarkFailedAsync(
            int answerId, string errorMessage, CancellationToken cancellationToken = default)
        {
            Failures.Add((answerId, errorMessage));
            if (_answers.TryGetValue(answerId, out var answer))
                answer.Status = AnswerStatus.ERROR;
            return Task.CompletedTask;
        }

        public Task UpdateTranscriptAsync(
            int answerId, string transcript, AnswerStatus status,
            CancellationToken cancellationToken = default,
            string? detectedLanguage = null, float? durationSeconds = null) =>
            Task.CompletedTask;

        public Task UpdateAiEvaluationAsync(
            int answerId, float? aiScore, string? aiFeedback, string? aiAnalysisJson,
            AnswerStatus status, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ResetForRetryAsync(
            int answerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task UpdateAudioUrlAsync(
            int answerId, string audioUrl, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetJobIdAsync(
            int answerId, string? jobId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
