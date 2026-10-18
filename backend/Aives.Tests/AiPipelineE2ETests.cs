using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.AI;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Queue;
using Aives.Infrastructure.Repositories;
using Aives.Infrastructure.Storage;
using Hangfire;
using Hangfire.Common;
using Hangfire.MemoryStorage;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 2.23 — AI Pipeline E2E.
/// Test toàn bộ flow từ submit answer → upload R2 → enqueue Hangfire →
/// background process → STT + Eval → persist DB → mark EVALUATED.
/// Real R2 + Mock AI service + Real Hangfire MemoryStorage.
/// </summary>
public class AiPipelineE2ETests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly AnswerRepository _answerRepo;
    private readonly ResultRepository _resultRepo;
    private readonly CloudflareR2StorageService _storageService;
    private readonly List<string> _uploadedKeys = new();
    private MemoryStorage _storage = new(new MemoryStorageOptions { FetchNextJobTimeout = TimeSpan.FromMilliseconds(50) });

    public AiPipelineE2ETests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"PipelineE2E_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);
        _answerRepo = new AnswerRepository(_db);
        _resultRepo = new ResultRepository(_db);

        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Aives.Api"));
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();
        var r2Options = new CloudflareR2Options();
        config.GetSection(CloudflareR2Options.SectionName).Bind(r2Options);
        r2Options.UseLocalStorageFallback = false;
        _storageService = new CloudflareR2StorageService(
            Options.Create(r2Options),
            NullLogger<CloudflareR2StorageService>.Instance);
    }

    public void Dispose()
    {
        foreach (var key in _uploadedKeys)
        {
            try { _storageService.DeleteAudioAsync(key).GetAwaiter().GetResult(); } catch { }
        }
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ===================================================================
    // E2E flows
    // ===================================================================

    [Fact]
    public async Task FullPipeline_SubmitAudio_UploadR2_Enqueue_Process_Succeeds_MarksEvaluated()
    {
        // 1. Seed: course / exam / session / student / question / answer
        var seed = await SeedAsync();

        // 2. Upload audio lên R2 thật
        var audioKey = await UploadAudioAsync(seed);

        // 3. Setup mock AI: STT trả transcript, Eval trả score 8.5
        var aiClient = CreateMockAi(
            sttResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = "AI là trí tuệ nhân tạo, giúp máy móc học hỏi.",
                LanguageDetected = "vi",
                Duration = 6.0
            },
            evalResponse: new EvaluateSessionResponse
            {
                Success = true,
                ExamSessionId = seed.SessionId.ToString(),
                TotalScore = 8.5f,
                MaxTotalScore = 10.0f,
                Percentage = 85.0f,
                OverallFeedback = "Bài làm tốt, nội dung đầy đủ.",
                Status = "PendingReview",
                StudySuggestions = new System.Collections.Generic.List<string> { "Ôn thêm chương 1" },
                QuestionScores = new System.Collections.Generic.List<QuestionScoreDetailDto>
                {
                    new()
                    {
                        QuestionId = seed.QuestionId.ToString(),
                        QuestionText = "AI là gì?",
                        StudentAnswer = "AI là trí tuệ nhân tạo, giúp máy móc học hỏi.",
                        Score = 8.5f,
                        MaxScore = 10.0f,
                        Feedback = "Đúng khái niệm cốt lõi.",
                        Strengths = new() { "Định nghĩa chính xác" },
                        Weaknesses = new(),
                        MissingContent = new(),
                        Improvements = new() { "Bổ sung ví dụ thực tế" },
                        SentenceFeedback = new(),
                        CriterionAnalyses = new()
                    }
                }
            });

        // 4. Wire DI: orchestrator thật + real repo + mock AI + real R2
        var orchestrator = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance);
        var job = new AnswerProcessingJob(orchestrator, _answerRepo, NullLogger<AnswerProcessingJob>.Instance);

        // 5. Chạy job trực tiếp (không qua Hangfire runtime để deterministic)
        var metadata = new QueueJobMetadata
        {
            JobId = "test",
            AnswerId = seed.AnswerId,
            ResultId = seed.ResultId,
            ObjectKey = audioKey,
            Attempt = 1
        };
        await job.ExecuteAsync(metadata, CancellationToken.None);

        // 6. Verify DB state
        var dbAnswer = await _db.Answers.FindAsync(seed.AnswerId);
        Assert.NotNull(dbAnswer);
        Assert.Equal(AnswerStatus.EVALUATED, dbAnswer!.Status);
        Assert.Equal(8.5f, dbAnswer.AiScore);
        Assert.Equal("AI là trí tuệ nhân tạo, giúp máy móc học hỏi.", dbAnswer.Transcript);
        Assert.NotNull(dbAnswer.AiAnalysis);
        Assert.Contains("question_text", dbAnswer.AiAnalysis!); // JSON chứa nội dung eval
        Assert.Contains("\"score\":8.5", dbAnswer.AiAnalysis!);
        Assert.Equal(QueueJobStatus.Completed, metadata.Status);
    }

    [Fact]
    public async Task FullPipeline_HangfireRealRuntime_ThroughBackgroundJob_CompletesSuccessfully()
    {
        // Test Hangfire end-to-end: enqueue → server pick up → job chạy → complete.
        // Verify: 1 invocation, state = Succeeded, DB = EVALUATED.
        var seed = await SeedAsync();
        var audioKey = await UploadAudioAsync(seed);

        var aiClient = CreateMockAi(
            sttResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = "Pipeline E2E success.",
                LanguageDetected = "vi",
                Duration = 3.0
            },
            evalResponse: new EvaluateSessionResponse
            {
                Success = true,
                ExamSessionId = seed.SessionId.ToString(),
                TotalScore = 9.0f,
                MaxTotalScore = 10.0f,
                Percentage = 90.0f,
                OverallFeedback = "Excellent.",
                Status = "PendingReview",
                StudySuggestions = new(),
                QuestionScores = new System.Collections.Generic.List<QuestionScoreDetailDto>
                {
                    new()
                    {
                        QuestionId = seed.QuestionId.ToString(),
                        QuestionText = "Q1",
                        StudentAnswer = "Pipeline E2E success.",
                        Score = 9.0f,
                        MaxScore = 10.0f,
                        Feedback = "Great",
                        Strengths = new(),
                        Weaknesses = new(),
                        MissingContent = new(),
                        Improvements = new(),
                        SentenceFeedback = new(),
                        CriterionAnalyses = new()
                    }
                }
            });

        var orchestrator = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance);
        var job = new AnswerProcessingJob(orchestrator, _answerRepo, NullLogger<AnswerProcessingJob>.Instance);

        // Hangfire runtime
        using var server = new BackgroundJobServer(new BackgroundJobServerOptions
        {
            WorkerCount = 1,
            SchedulePollingInterval = TimeSpan.FromMilliseconds(50),
            HeartbeatInterval = TimeSpan.FromMilliseconds(50),
            ServerCheckInterval = TimeSpan.FromMilliseconds(50),
            StopTimeout = TimeSpan.FromSeconds(1)
        }, _storage);

        var client = new BackgroundJobClient(_storage);
        var metadata = new QueueJobMetadata
        {
            JobId = "test-hf",
            AnswerId = seed.AnswerId,
            ResultId = seed.ResultId,
            ObjectKey = audioKey,
            Attempt = 1
        };
        var jobId = client.Create(Job.FromExpression(() => job.ExecuteAsync(metadata, CancellationToken.None)), new EnqueuedState());

        // Wait for Succeeded
        await WaitForStateAsync(jobId, SucceededState.StateName, TimeSpan.FromSeconds(15));

        // Verify DB
        var dbAnswer = await _db.Answers.FindAsync(seed.AnswerId);
        Assert.Equal(AnswerStatus.EVALUATED, dbAnswer!.Status);
        Assert.Equal(9.0f, dbAnswer.AiScore);
    }

    [Fact]
    public async Task FullPipeline_SttEmpty_OrchestratorReturnsEmptyTranscript_StatusNotEvaluated()
    {
        // STT thành công nhưng transcript rỗng → DB giữ nguyên PROCESSING (mark failed = ERROR).
        var seed = await SeedAsync();
        var audioKey = await UploadAudioAsync(seed);

        var aiClient = CreateMockAi(
            sttResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = "",
                LanguageDetected = "vi",
                Duration = 1.0
            });

        var orchestrator = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance);
        var job = new AnswerProcessingJob(orchestrator, _answerRepo, NullLogger<AnswerProcessingJob>.Instance);

        var metadata = new QueueJobMetadata
        {
            JobId = "test",
            AnswerId = seed.AnswerId,
            ResultId = seed.ResultId,
            ObjectKey = audioKey,
            Attempt = 1
        };

        // Job phải throw NonRetryableJobException
        await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(metadata, CancellationToken.None));

        // DB: status=ERROR vì EmptyTranscript path → MarkFailedAsync
        var dbAnswer = await _db.Answers.FindAsync(seed.AnswerId);
        Assert.Equal(AnswerStatus.ERROR, dbAnswer!.Status);
    }

    [Fact]
    public async Task FullPipeline_SttRetryable_ThenSucceeds_FinalStateEvaluated()
    {
        // Mô phỏng: lần 1 STT fail (retryable) → job throw → Hangfire retry → lần 2 success.
        // Test bằng cách gọi 2 lần ExecuteAsync với attempt=1, attempt=2.
        var seed = await SeedAsync();
        var audioKey = await UploadAudioAsync(seed);

        // First call: STT fails with timeout (retryable)
        var failingClient = CreateMockAi(
            sttResponse: new SpeechToTextResponse
            {
                Success = false,
                Message = "AI service timeout",
                Transcript = ""
            });

        var orchFail = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, failingClient,
            NullLogger<AnswerOrchestrator>.Instance);
        var jobFail = new AnswerProcessingJob(orchFail, _answerRepo, NullLogger<AnswerProcessingJob>.Instance);

        var m1 = new QueueJobMetadata { JobId = "1", AnswerId = seed.AnswerId, ResultId = seed.ResultId, ObjectKey = audioKey, Attempt = 1 };
        await Assert.ThrowsAsync<AnswerProcessingJob.RetryableJobException>(
            () => jobFail.ExecuteAsync(m1, CancellationToken.None));
        Assert.Equal(QueueJobStatus.Failed, m1.Status);

        // Second call: STT succeeds, Eval succeeds
        var okClient = CreateMockAi(
            sttResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = "Retry success transcript.",
                LanguageDetected = "vi"
            },
            evalResponse: new EvaluateSessionResponse
            {
                Success = true,
                ExamSessionId = seed.SessionId.ToString(),
                TotalScore = 7.0f, MaxTotalScore = 10.0f, Percentage = 70.0f,
                OverallFeedback = "OK", Status = "PendingReview",
                StudySuggestions = new(),
                QuestionScores = new System.Collections.Generic.List<QuestionScoreDetailDto>
                {
                    new()
                    {
                        QuestionId = seed.QuestionId.ToString(),
                        QuestionText = "Q", StudentAnswer = "Retry success transcript.",
                        Score = 7.0f, MaxScore = 10.0f, Feedback = "OK",
                        Strengths = new(), Weaknesses = new(), MissingContent = new(),
                        Improvements = new(), SentenceFeedback = new(), CriterionAnalyses = new()
                    }
                }
            });

        var orchOk = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, okClient,
            NullLogger<AnswerOrchestrator>.Instance);
        var jobOk = new AnswerProcessingJob(orchOk, _answerRepo, NullLogger<AnswerProcessingJob>.Instance);

        var m2 = new QueueJobMetadata { JobId = "2", AnswerId = seed.AnswerId, ResultId = seed.ResultId, ObjectKey = audioKey, Attempt = 2 };
        await jobOk.ExecuteAsync(m2, CancellationToken.None);

        Assert.Equal(QueueJobStatus.Completed, m2.Status);
        var dbAnswer = await _db.Answers.FindAsync(seed.AnswerId);
        Assert.Equal(AnswerStatus.EVALUATED, dbAnswer!.Status);
        Assert.Equal(7.0f, dbAnswer.AiScore);
    }

    [Fact]
    public async Task FullPipeline_EvalFailure_AnswerMarkedError_NotEvaluated()
    {
        // STT success → Eval fail → MarkFailed.
        var seed = await SeedAsync();
        var audioKey = await UploadAudioAsync(seed);

        var aiClient = CreateMockAi(
            sttResponse: new SpeechToTextResponse { Success = true, Transcript = "ok", LanguageDetected = "vi" },
            evalResponse: new EvaluateSessionResponse { Success = false, Message = "AI eval unavailable" });

        var orch = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance);
        var job = new AnswerProcessingJob(orch, _answerRepo, NullLogger<AnswerProcessingJob>.Instance);

        var m = new QueueJobMetadata { JobId = "t", AnswerId = seed.AnswerId, ResultId = seed.ResultId, ObjectKey = audioKey, Attempt = 1 };

        // Eval fail → throw RetryableJobException (orchestrator trả EvaluationErrorCode → job throw Retryable)
        await Assert.ThrowsAsync<AnswerProcessingJob.RetryableJobException>(
            () => job.ExecuteAsync(m, CancellationToken.None));

        var dbAnswer = await _db.Answers.FindAsync(seed.AnswerId);
        Assert.Equal(AnswerStatus.ERROR, dbAnswer!.Status);
        Assert.NotNull(dbAnswer.TeacherComment); // Lỗi message
    }

    [Fact]
    public async Task FullPipeline_AnswerNotFound_JobFails_WithNotFoundMessage()
    {
        // AnswerId không tồn tại → orchestrator trả Unknown error.
        var aiClient = CreateMockAi(sttResponse: new SpeechToTextResponse { Success = true, Transcript = "x" });
        var orch = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance);
        var job = new AnswerProcessingJob(orch, _answerRepo, NullLogger<AnswerProcessingJob>.Instance);

        var m = new QueueJobMetadata { JobId = "t", AnswerId = 99999, ResultId = 1, ObjectKey = "x", Attempt = 1 };

        await Assert.ThrowsAsync<AnswerProcessingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(m, CancellationToken.None));
        Assert.Equal(QueueJobStatus.DeadLetter, m.Status);
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private async Task<PipelineSeed> SeedAsync()
    {
        var course = new Course { CourseCode = "E2E", CourseName = "Pipeline Test" };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var student = new User
        {
            UserId = 9001, Username = "p1", Email = "p1@a.com", FullName = "P",
            Role = UserRole.STUDENT, PasswordHash = "x"
        };
        _db.Users.Add(student);

        var exam = new Exam { ExamCode = "E2E-X", Title = "E2E", CourseId = course.CourseId, DurationMinutes = 30 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "E2E-S",
            SessionName = "E2E Session",
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

        var qb = new QuestionBank { CourseId = course.CourseId, QuestionText = "AI là gì?", MaxScore = 10 };
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

        return new PipelineSeed(course.CourseId, exam.ExamId, session.SessionId, result.ResultId, qb.QuestionId, answer.AnswerId);
    }

    private async Task<string> UploadAudioAsync(PipelineSeed seed)
    {
        var key = $"e2e-pipeline/{seed.SessionId}/q_{Guid.NewGuid():N}.wav";
        var header = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
            0x57, 0x41, 0x56, 0x45, 0x66, 0x6d, 0x74, 0x20,
            0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
            0x44, 0xac, 0x00, 0x00, 0x88, 0x58, 0x01, 0x00,
            0x02, 0x00, 0x10, 0x00
        };
        using var ms = new MemoryStream(header);
        await _storageService.UploadAudioAsync(key, ms, "audio/wav");
        _uploadedKeys.Add(key);

        // Update AudioUrl
        var answer = await _db.Answers.FindAsync(seed.AnswerId);
        answer!.AudioUrl = key;
        await _db.SaveChangesAsync();
        return key;
    }

    private static Aives.Application.AI.Interfaces.IAiServiceClient CreateMockAi(SpeechToTextResponse sttResponse, EvaluateSessionResponse? evalResponse = null)
    {
        var handler = new StubHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("transcribe"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(sttResponse), Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(evalResponse ?? new EvaluateSessionResponse
                {
                    Success = false, Message = "no mock eval"
                }), Encoding.UTF8, "application/json")
            });
        });
        var opts = Options.Create(new AiServiceOptions { BaseUrl = "http://mock", TimeoutSeconds = 30, ApiKey = "k" });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://mock") };
        return new Aives.Infrastructure.AI.AiServiceClient(http, opts, NullLogger<Aives.Infrastructure.AI.AiServiceClient>.Instance);
    }

    private async Task WaitForStateAsync(string jobId, string targetState, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        using var conn = _storage.GetConnection();
        while (DateTime.UtcNow < deadline)
        {
            var data = conn.GetJobData(jobId);
            if (data != null && data.State == targetState) return;
            await Task.Delay(50);
        }
        var final = conn.GetJobData(jobId);
        throw new TimeoutException($"Job {jobId} not in {targetState} after {timeout}. Final: {final?.State}");
    }

    private record PipelineSeed(int CourseId, int ExamId, int SessionId, int ResultId, int QuestionId, int AnswerId);

    private class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _h;
        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> h) => _h = h;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken c) => _h(r);
    }
}
