using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aives.Application.AI.DTOs;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Contracts.Stt;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.AI;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Queue;
using Aives.Infrastructure.Repositories;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 1.4 + 1.5 — integration tests chứng minh 2 contract được <b>thực thi</b>
/// trên đường chạy thật, chứ không chỉ là class helper không ai gọi.
/// <list type="bullet">
///   <item>Job metadata vi phạm contract → bị từ chối trước khi enqueue (HangfireQueueService).</item>
///   <item>Job metadata vi phạm contract → job abort, không chạy pipeline (AnswerProcessingJob).</item>
///   <item>STT response vi phạm contract → dead-letter, không retry (AnswerOrchestrator).</item>
///   <item>STT response hợp lệ → transcript + language + duration được persist đúng.</item>
/// </list>
/// </summary>
public class ContractEnforcementTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly AnswerRepository _answerRepo;
    private readonly ResultRepository _resultRepo;
    private readonly FakeStorageService _storage;

    public ContractEnforcementTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"Contract_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);
        _answerRepo = new AnswerRepository(_db);
        _resultRepo = new ResultRepository(_db);
        _storage = new FakeStorageService();
    }

    public void Dispose()
    {
        _storage.Clear();
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ===================================================================
    //  Task 1.4 — Queue Job Contract enforced on the enqueue path
    // ===================================================================

    [Fact]
    public void Enqueue_InvalidMetadata_Throws_AndDoesNotCreateJob()
    {
        var storage = new MemoryStorage();
        var svc = new HangfireQueueService(
            new Hangfire.BackgroundJobClient(storage), NullLogger<HangfireQueueService>.Instance);

        // Attempt = 0 vi phạm contract (attempt là 1-based).
        var metadata = new QueueJobMetadata
        {
            AnswerId = 42,
            ResultId = 1,
            ObjectKey = "k",
            Attempt = 0
        };

        Assert.Throws<ArgumentException>(() => svc.EnqueueAnswerProcessing(metadata));

        using var connection = storage.GetConnection();
        Assert.Empty(connection.GetAllItemsFromSet("jobs"));
    }

    [Fact]
    public void Enqueue_MetadataWithoutAnswerId_Throws()
    {
        var storage = new MemoryStorage();
        var svc = new HangfireQueueService(
            new Hangfire.BackgroundJobClient(storage), NullLogger<HangfireQueueService>.Instance);

        var metadata = new QueueJobMetadata { AnswerId = 0, Attempt = 1 };

        Assert.Throws<ArgumentException>(() => svc.EnqueueAnswerProcessing(metadata));
    }

    [Fact]
    public void Enqueue_ValidMetadata_CreatesJob_AndSetsJobId()
    {
        var storage = new MemoryStorage();
        var svc = new HangfireQueueService(
            new Hangfire.BackgroundJobClient(storage), NullLogger<HangfireQueueService>.Instance);

        var metadata = QueueJobContract.Create(42, 7, "exams/1/sessions/2/q.webm", 1);

        var jobId = svc.EnqueueAnswerProcessing(metadata);

        Assert.False(string.IsNullOrEmpty(jobId));
        Assert.Equal(jobId, metadata.JobId);
        Assert.Equal(QueueJobStatus.Pending, metadata.Status);
    }

    // ===================================================================
    //  Task 1.4 — Queue Job Contract enforced on the execution path
    // ===================================================================

    [Fact]
    public async Task ExecuteAsync_InvalidMetadata_AbortsWithoutRunningPipeline()
    {
        var orchestrator = new RecordingOrchestrator();
        var repo = new RecordingAnswerRepository();
        var job = new AnswerProcessingJob(orchestrator, repo, NullLogger<AnswerProcessingJob>.Instance);

        var metadata = new QueueJobMetadata
        {
            JobId = "test",
            AnswerId = 1,
            ResultId = 1,
            ObjectKey = "k",
            Attempt = 0 // vi phạm contract
        };

        await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(metadata, CancellationToken.None));

        Assert.Equal(0, orchestrator.CallCount);
        Assert.Equal(0, repo.IncrementCalls);
        Assert.Equal(QueueJobStatus.DeadLetter, metadata.Status);
    }

    [Fact]
    public async Task ExecuteAsync_ValidMetadata_RunsPipeline()
    {
        var orchestrator = new RecordingOrchestrator();
        var repo = new RecordingAnswerRepository();
        var job = new AnswerProcessingJob(orchestrator, repo, NullLogger<AnswerProcessingJob>.Instance);

        var metadata = QueueJobContract.Create(1, 1, "k", 1);

        await job.ExecuteAsync(metadata, CancellationToken.None);

        Assert.Equal(1, orchestrator.CallCount);
        Assert.Equal(1, repo.IncrementCalls);
        Assert.Equal(QueueJobStatus.Completed, metadata.Status);
    }

    // ===================================================================
    //  Task 1.5 — STT Response Contract enforced on the STT path
    // ===================================================================

    [Fact]
    public async Task Stt_ContractViolation_ResponseIsRejected_NotPersisted()
    {
        var (orchestrator, ctx) = await BuildOrchestratorAsync(
            sttResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = "ok",
                LanguageDetected = "vietnamese", // không phải ISO-639-1
                Duration = 1.0
            });

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        // Contract violation = AI trả sai → retry cũng ra kết quả y hệt → không retry.
        Assert.False(result.SttError!.Retryable);

        var dbAnswer = await _db.Answers.FindAsync(ctx.AnswerId);
        Assert.Null(dbAnswer!.Transcript);
    }

    [Fact]
    public async Task Stt_ValidResponse_PersistsTranscriptLanguageAndDuration()
    {
        var (orchestrator, ctx) = await BuildOrchestratorAsync(
            sttResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = "Tôi trả lời về kiến trúc phần mềm",
                LanguageDetected = "vi-VN",
                Duration = 7.25
            },
            evalResponse: new EvaluateSessionResponse { Success = false, Message = "eval disabled" });

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, 1, CancellationToken.None);

        var dbAnswer = await _db.Answers.FindAsync(ctx.AnswerId);
        Assert.Equal("Tôi trả lời về kiến trúc phần mềm", dbAnswer!.Transcript);
        // Ngôn ngữ được chuẩn hoá về ISO-639-1 trước khi lưu.
        Assert.Equal("vi", dbAnswer.DetectedLanguage);
        Assert.Equal(7.25f, dbAnswer.DurationSeconds);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Stt_FailureWithUnknownErrorCode_IsMappedToInternalError()
    {
        var (orchestrator, ctx) = await BuildOrchestratorAsync(
            sttResponse: new SpeechToTextResponse
            {
                Success = false,
                Message = "AI1 blew up",
                Error = new SttErrorPayload
                {
                    Code = "SOMETHING_NEW",
                    Message = "AI1 blew up",
                    Retryable = true
                }
            });

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        // Code lạ → Unknown (retry mặc định vì chưa biết là lỗi tạm thời hay vĩnh viễn).
        Assert.Equal(SttErrorCode.Unknown, result.SttError!.Code);
        Assert.True(result.SttError.Retryable);
    }

    [Fact]
    public async Task Stt_EmptyTranscript_IsBusinessFailure_NotContractViolation()
    {
        var (orchestrator, ctx) = await BuildOrchestratorAsync(
            sttResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = string.Empty,
                LanguageDetected = "vi",
                Duration = 2.0
            });

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        Assert.Equal(SttErrorCode.EmptyTranscript, result.SttError!.Code);
        // Im lặng là lỗi vĩnh viễn → không retry.
        Assert.False(result.SttError.Retryable);
    }

    // ===================================================================
    //  Helpers
    // ===================================================================

    private async Task<(AnswerOrchestrator orchestrator, SeedContext ctx)> BuildOrchestratorAsync(
        SpeechToTextResponse sttResponse,
        EvaluateSessionResponse? evalResponse = null)
    {
        var ctx = await SeedAsync();

        // Answer phải có ObjectKey + audio trong storage để orchestrator đi qua đường STT
        // (nếu không có audio, transcript lấy từ StudentAnswer và AI1 không được gọi).
        var objectKey = $"contract/sessions/{ctx.ResultId}/q_{ctx.AnswerId}.wav";
        await _storage.UploadAudioAsync(objectKey, new MemoryStream(new byte[] { 0x52, 0x49, 0x46, 0x46 }),
            "audio/wav");
        var answer = await _db.Answers.FindAsync(ctx.AnswerId);
        answer!.ObjectKey = objectKey;
        answer.AudioUrl = objectKey;
        answer.StudentAnswer = null;
        await _db.SaveChangesAsync();

        var handler = new StubHandler(request =>
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;

            // Audio download từ fake storage.
            if (uri.StartsWith("http://localhost:9999/storage/", StringComparison.Ordinal))
            {
                var key = "/" + Uri.UnescapeDataString(uri["http://localhost:9999/storage/".Length..].Split('?')[0]);
                var stream = _storage.TryGetStream(key);
                return Task.FromResult(stream == null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(stream) });
            }

            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("transcribe"))
            {
                // Endpoint "transcribe-object" dùng schema StoredSpeechToTextResponse
                // (answerId + language), endpoint "transcribe" dùng schema SpeechToTextResponse.
                if (path.Contains("transcribe-object"))
                {
                    return Task.FromResult(Json(HttpStatusCode.OK, new StoredSpeechToTextResponse
                    {
                        Success = sttResponse.Success,
                        AnswerId = ctx.AnswerId,
                        Transcript = sttResponse.Transcript,
                        Language = sttResponse.LanguageDetected,
                        Duration = sttResponse.Duration,
                        Message = sttResponse.Message,
                        Error = sttResponse.Error
                    }));
                }

                return Task.FromResult(Json(HttpStatusCode.OK, sttResponse));
            }

            return Task.FromResult(Json(HttpStatusCode.OK,
                evalResponse ?? new EvaluateSessionResponse { Success = false, Message = "no eval mock" }));
        });

        var aiClient = BuildAiClient(handler);
        var orchestrator = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storage, aiClient,
            NullLogger<AnswerOrchestrator>.Instance, handler);

        return (orchestrator, ctx);
    }

    private static HttpResponseMessage Json<T>(HttpStatusCode status, T payload) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
    };

    private static IAiServiceClient BuildAiClient(HttpMessageHandler handler)
    {
        var options = Options.Create(new AiServiceOptions
        {
            BaseUrl = "http://fake-ai",
            TimeoutSeconds = 30,
            ApiKey = "test-key"
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://fake-ai") };
        return new AiServiceClient(http, options, NullLogger<AiServiceClient>.Instance);
    }

    private async Task<SeedContext> SeedAsync()
    {
        var course = new Course { CourseCode = "CT", CourseName = "Contract Test" };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var student = new User
        {
            UserId = 8801, Username = "ct1", Email = "ct1@a.com", FullName = "CT S",
            Role = UserRole.STUDENT, PasswordHash = "x"
        };
        _db.Users.Add(student);

        var exam = new Exam { ExamCode = "CT-E", Title = "CT", CourseId = course.CourseId, DurationMinutes = 30 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "CT-S",
            SessionName = "CT Session",
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

        var qb = new QuestionBank { CourseId = course.CourseId, QuestionText = "Hỏi", MaxScore = 10 };
        _db.QuestionBanks.Add(qb);
        await _db.SaveChangesAsync();

        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = qb.QuestionId,
            Status = AnswerStatus.PROCESSING,
            StudentAnswer = "x"
        };
        _db.Answers.Add(answer);
        await _db.SaveChangesAsync();

        return new SeedContext(answer.AnswerId, result.ResultId);
    }

    private record SeedContext(int AnswerId, int ResultId);

    private class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => _handler(request);
    }

    /// <summary>Orchestrator ghi lại số lần được gọi — verify pipeline có/không chạy.</summary>
    private class RecordingOrchestrator : IAnswerOrchestrator
    {
        public int CallCount { get; private set; }

        public Task<OrchestrationResult> ProcessAnswerAsync(int answerId, int attempt, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new OrchestrationResult { Success = true });
        }
    }

    /// <summary>Repository giả — chỉ cần đếm lần gọi IncrementAttemptAsync.</summary>
    private class RecordingAnswerRepository : IAnswerRepository
    {
        public int IncrementCalls { get; private set; }

        public Task<int> IncrementAttemptAsync(int answerId, string? jobId, CancellationToken cancellationToken = default)
        {
            IncrementCalls++;
            return Task.FromResult(1);
        }

        public Task<Domain.Entities.Answer?> GetByIdWithDetailsAsync(int answerId, CancellationToken cancellationToken = default)
            => Task.FromResult<Domain.Entities.Answer?>(null);

        public Task UpdateTranscriptAsync(int answerId, string transcript, AnswerStatus status,
            CancellationToken cancellationToken = default, string? detectedLanguage = null, float? durationSeconds = null)
            => Task.CompletedTask;

        public Task UpdateAiEvaluationAsync(int answerId, float? aiScore, string? aiFeedback, string? aiAnalysisJson,
            AnswerStatus status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkFailedAsync(int answerId, string errorMessage, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ResetForRetryAsync(int answerId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAudioUrlAsync(int answerId, string audioUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetJobIdAsync(int answerId, string? jobId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
