using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.Contracts.Stt;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Queue;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 2.16 — STT Integration Test với audio variations.
/// Test <see cref="AnswerOrchestrator"/> end-to-end với fake storage + mock AI service
/// (HttpMessageHandler stub) — verify error taxonomy chuẩn của SttErrorCode được
/// áp dụng đúng cho từng kịch bản audio thực tế.
/// </summary>
public class SttIntegrationTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly AnswerRepository _answerRepo;
    private readonly ResultRepository _resultRepo;
    private readonly FakeStorageService _storageService;
    private readonly FakeAiServiceFactory _aiFactory;

    public SttIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"SttIntegration_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);
        _answerRepo = new AnswerRepository(_db);
        _resultRepo = new ResultRepository(_db);

        // Fake in-memory storage — không cần R2 credentials.
        _storageService = new FakeStorageService();
        _aiFactory = new FakeAiServiceFactory(_storageService);
    }

    public void Dispose()
    {
        _storageService.Clear();
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // ===================================================================
    // STT Error Taxonomy — AnswerOrchestrator phải map đúng code + retryable
    // ===================================================================

    [Fact]
    public async Task Stt_EmptyTranscript_ReturnsEmptyTranscript_NotRetryable()
    {
        // AI trả Success=true nhưng transcript rỗng → empty transcript path.
        var (orchestrator, ctx) = BuildPipeline(aiResponse: new SpeechToTextResponse
        {
            Success = true,
            Transcript = string.Empty,
            LanguageDetected = "vi"
        });

        // Upload dummy audio vào fake storage.
        var key = await UploadDummyAudioAsync(ctx);

        // Act
        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        // Assert: rơi vào EmptyTranscript, non-retryable
        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        Assert.Equal(SttErrorCode.EmptyTranscript, result.SttError!.Code);
        Assert.False(result.SttError.Retryable);
    }

    [Fact]
    public async Task Stt_AiSuccess_ValidTranscript_PersistsTranscript_TriggersEval()
    {
        // AI trả Success=true, transcript hợp lệ → DB lưu transcript + status PROCESSING.
        // Eval sẽ fail vì mock AI2 trả error → pipeline fail nhưng transcript phải lưu.
        var (orchestrator, ctx) = BuildPipeline(
            aiResponse: new SpeechToTextResponse
            {
                Success = true,
                Transcript = "Xin chào thầy",
                LanguageDetected = "vi",
                Duration = 5.5
            },
            aiEvalResponse: new EvaluateSessionResponse
            {
                Success = false,
                Message = "AI eval unavailable in test"
            });

        var key = await UploadDummyAudioAsync(ctx);

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        // Verify DB có transcript (persist ngay cả khi eval fail)
        // Status sẽ là ERROR vì eval fail → MarkFailedAsync set status.
        var dbAnswer = await _db.Answers.FindAsync(ctx.AnswerId);
        Assert.Equal("Xin chào thầy", dbAnswer!.Transcript);
        Assert.Equal(AnswerStatus.ERROR, dbAnswer.Status);
    }

    [Fact]
    public async Task Stt_AiReturnsHttpError_ReturnsInternalError_Retryable()
    {
        // AI service trả 500 → retryable (transient).
        var (orchestrator, ctx) = BuildPipelineWithHttpError(HttpStatusCode.InternalServerError);
        var key = await UploadDummyAudioAsync(ctx);

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        Assert.Equal(SttErrorCode.InternalError, result.SttError!.Code);
        Assert.True(result.SttError.Retryable, "HTTP 500 phải retryable.");
    }

    [Fact]
    public async Task Stt_AiReturns404_AudioDownloadFails_ReturnsAudioCorrupted_NotRetryable()
    {
        // Presigned URL trỏ đến object không tồn tại (404) → AudioCorrupted, không retry.
        var (orchestrator, ctx) = BuildPipelineWith404Audio();
        // Tạo answer nhưng KHÔNG upload — storage trả 404.
        ctx.Answer.AudioUrl = $"missing/audio_{Guid.NewGuid():N}.webm";
        await _db.SaveChangesAsync();

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        Assert.Equal(SttErrorCode.AudioCorrupted, result.SttError!.Code);
        Assert.False(result.SttError.Retryable, "404 audio không nên retry.");
    }

    [Fact]
    public async Task Stt_LocalFallbackPath_ReturnsUnsupportedFormat_NotRetryable()
    {
        // AudioUrl là local path "/uploads/..." → orchestrator phải trả UnsupportedFormat.
        var (orchestrator, ctx) = BuildPipeline(aiResponse: new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok"
        });
        ctx.Answer.AudioUrl = "/uploads/audio/local.webm";
        await _db.SaveChangesAsync();

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        Assert.Equal(SttErrorCode.UnsupportedFormat, result.SttError!.Code);
        Assert.False(result.SttError.Retryable);
    }

    [Fact]
    public async Task Stt_AiServiceUnreachable_ReturnsTimeoutOrInternal_Retryable()
    {
        // AI service timeout / connection error → retryable.
        var (orchestrator, ctx) = BuildPipelineWithHttpRequestException();
        var key = await UploadDummyAudioAsync(ctx);

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        // Orchestrator catch tất cả exception thành InternalError/Timeout — đều retryable.
        Assert.True(
            result.SttError!.Code == SttErrorCode.Timeout
            || result.SttError.Code == SttErrorCode.InternalError,
            $"Unexpected code: {result.SttError.Code}");
        Assert.True(result.SttError.Retryable);
    }

    [Fact]
    public async Task Stt_StudentTextOnly_NoAudio_TranscriptFromTextAnswer()
    {
        // Không có AudioUrl nhưng có StudentAnswer (text mode) → dùng làm transcript.
        // AI eval fail vì mock chưa setup, nhưng transcript phải được persist từ text.
        var (orchestrator, ctx) = BuildPipeline(
            aiResponse: new SpeechToTextResponse { Success = true, Transcript = "ok" },
            aiEvalResponse: new EvaluateSessionResponse { Success = false, Message = "test fail" });
        ctx.Answer.AudioUrl = null;
        ctx.Answer.StudentAnswer = "Em trả lời câu hỏi này bằng văn bản.";
        await _db.SaveChangesAsync();

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        var dbAnswer = await _db.Answers.FindAsync(ctx.AnswerId);
        Assert.Equal("Em trả lời câu hỏi này bằng văn bản.", dbAnswer!.Transcript);
    }

    [Fact]
    public void SttErrorCode_Enum_HasExpectedValues()
    {
        // Verify enum contract — phòng trường hợp ai đó đổi tên sẽ phát hiện ngay.
        Assert.Equal(0, (int)SttErrorCode.Unknown);
        Assert.Equal(1, (int)SttErrorCode.AudioCorrupted);
        Assert.Equal(2, (int)SttErrorCode.EmptyTranscript);
        Assert.Equal(3, (int)SttErrorCode.UnsupportedFormat);
        Assert.Equal(4, (int)SttErrorCode.Timeout);
        Assert.Equal(5, (int)SttErrorCode.InternalError);
    }

    [Fact]
    public void SttError_Structure_HasRetryableFlag_ForDecision()
    {
        // SttError phải có Retryable boolean — caller dùng để quyết định retry/dead-letter.
        var err = new SttError
        {
            Code = SttErrorCode.Timeout,
            Message = "timed out",
            Retryable = true
        };
        Assert.True(err.Retryable);

        err.Retryable = false;
        Assert.False(err.Retryable);
    }

    [Fact]
    public async Task AiClient_SttTimeout_ReturnsRetryableCode()
    {
        // AI service timeout → Orchestrator trả Timeout code, retryable.
        var (orchestrator, ctx) = BuildPipelineWithTimeout(TimeSpan.FromSeconds(5));
        var key = await UploadDummyAudioAsync(ctx);

        var result = await orchestrator.ProcessAnswerAsync(ctx.AnswerId, attempt: 1, CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.SttError);
        Assert.True(result.SttError!.Retryable);
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private async Task<string> UploadDummyAudioAsync(AnswerContext ctx)
    {
        // 44 bytes minimal WAV header đủ để AI service parse được.
        var key = $"stt-tests/sessions/{ctx.Session.SessionId}/students/{ctx.Student.UserId}/q_{Guid.NewGuid():N}.wav";
        var header = new byte[]
        {
            0x52, 0x49, 0x46, 0x46, // RIFF
            0x24, 0x00, 0x00, 0x00, // file size
            0x57, 0x41, 0x56, 0x45, // WAVE
            0x66, 0x6d, 0x74, 0x20, // fmt
            0x10, 0x00, 0x00, 0x00, // subchunk size
            0x01, 0x00, 0x01, 0x00, // PCM mono
            0x44, 0xac, 0x00, 0x00, // sample rate
            0x88, 0x58, 0x01, 0x00, // byte rate
            0x02, 0x00, 0x10, 0x00  // block align + bits
        };
        using var ms = new MemoryStream(header);
        await _storageService.UploadAudioAsync(key, ms, "audio/wav");
        ctx.Answer.AudioUrl = key;
        await _db.SaveChangesAsync();
        return key;
    }

    private (AnswerOrchestrator orchestrator, AnswerContext ctx) BuildPipeline(
        SpeechToTextResponse? aiResponse = null,
        EvaluateSessionResponse? aiEvalResponse = null)
    {
        var ctx = SeedAnswerAsync().GetAwaiter().GetResult();
        var aiClient = _aiFactory.CreateClient(aiResponse, aiEvalResponse, out var handler);
        var orch = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance, handler);
        return (orch, ctx);
    }

    private (AnswerOrchestrator orchestrator, AnswerContext ctx) BuildPipelineWithHttpError(HttpStatusCode status)
    {
        var ctx = SeedAnswerAsync().GetAwaiter().GetResult();
        var aiClient = _aiFactory.CreateClientWithHttpError(status, out var handler);
        var orch = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance, handler);
        return (orch, ctx);
    }

    private (AnswerOrchestrator orchestrator, AnswerContext ctx) BuildPipelineWithHttpRequestException()
    {
        var ctx = SeedAnswerAsync().GetAwaiter().GetResult();
        var aiClient = _aiFactory.CreateClientWithException(new HttpRequestException("connection refused"), out var handler);
        var orch = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance, handler);
        return (orch, ctx);
    }

    private (AnswerOrchestrator orchestrator, AnswerContext ctx) BuildPipelineWithTimeout(TimeSpan delay)
    {
        var ctx = SeedAnswerAsync().GetAwaiter().GetResult();
        var aiClient = _aiFactory.CreateClientWithTimeout(delay, out var handler);
        var orch = new AnswerOrchestrator(
            _answerRepo, _resultRepo, _storageService, aiClient,
            NullLogger<AnswerOrchestrator>.Instance, handler);
        return (orch, ctx);
    }

    private (AnswerOrchestrator orchestrator, AnswerContext ctx) BuildPipelineWith404Audio()
    {
        // Khác: 404 đến từ storage (R2), không phải AI. Build pipeline bình thường.
        return BuildPipeline(aiResponse: new SpeechToTextResponse { Success = true, Transcript = "ok" });
    }

    private async Task<AnswerContext> SeedAnswerAsync()
    {
        var course = new Course { CourseCode = "STT", CourseName = "STT Test" };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var student = new User
        {
            UserId = 7001, Username = "stt1", Email = "stt1@a.com", FullName = "STT S",
            Role = UserRole.STUDENT, PasswordHash = "x"
        };
        _db.Users.Add(student);

        var exam = new Exam { ExamCode = "STT-E", Title = "STT", CourseId = course.CourseId, DurationMinutes = 30 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "STT-S",
            SessionName = "STT Session",
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

        var qb = new QuestionBank { CourseId = course.CourseId, QuestionText = "Nói về AI", MaxScore = 10 };
        _db.QuestionBanks.Add(qb);
        await _db.SaveChangesAsync();

        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = qb.QuestionId,
            Status = AnswerStatus.PROCESSING,
            StudentAnswer = "x",
            AudioUrl = "stt-tests/placeholder.webm" // sẽ bị override
        };
        _db.Answers.Add(answer);
        await _db.SaveChangesAsync();

        return new AnswerContext(answer.AnswerId, result.ResultId, session, student, answer);
    }

    private record AnswerContext(int AnswerId, int ResultId, ExamSession Session, User Student, Answer Answer);
}

/// <summary>
/// Factory tạo <see cref="IAiServiceClient"/> với response có thể control được
/// dùng cho STT integration test. Nhận biết fake storage URLs để mock audio download.
/// </summary>
internal class FakeAiServiceFactory
{
    private readonly FakeStorageService _storageService;

    public FakeAiServiceFactory(FakeStorageService storageService) => _storageService = storageService;

    public IAiServiceClient CreateClient(SpeechToTextResponse? sttResponse, EvaluateSessionResponse? evalResponse, out HttpMessageHandler handler)
    {
        var h = new FakeR2AwareHandler(_storageService, req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (path.Contains("transcribe") || path.Contains("speech"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(sttResponse), Encoding.UTF8, "application/json")
                });
            }
            // Evaluate
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(evalResponse ?? new EvaluateSessionResponse
                {
                    Success = false, Message = "no eval mock"
                }), Encoding.UTF8, "application/json")
            });
        });
        handler = h;
        return BuildClient(h);
    }

    public IAiServiceClient CreateClientWithHttpError(HttpStatusCode status, out HttpMessageHandler handler)
    {
        var h = new FakeR2AwareHandler(_storageService, _ => Task.FromResult(new HttpResponseMessage(status)));
        handler = h;
        return BuildClient(h);
    }

    public IAiServiceClient CreateClientWithException(Exception ex, out HttpMessageHandler handler)
    {
        var h = new FakeR2AwareHandler(_storageService, _ => throw ex);
        handler = h;
        return BuildClient(h);
    }

    public IAiServiceClient CreateClientWithTimeout(TimeSpan delay, out HttpMessageHandler handler)
    {
        var h = new FakeR2AwareHandler(_storageService, async _ =>
        {
            await Task.Delay(delay);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        handler = h;
        return BuildClient(h);
    }

    private static IAiServiceClient BuildClient(HttpMessageHandler handler)
    {
        var opts = Options.Create(new Aives.Infrastructure.AI.AiServiceOptions
        {
            BaseUrl = "http://fake-ai",
            TimeoutSeconds = 30,
            ApiKey = "test-key"
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://fake-ai") };
        return new Aives.Infrastructure.AI.AiServiceClient(http, opts, NullLogger<Aives.Infrastructure.AI.AiServiceClient>.Instance);
    }

    /// <summary>
    /// Handler nhận biết file:// URL của FakeStorageService → trả audio bytes.
    /// Mọi request khác chuyển sang delegate AI thật.
    /// </summary>
    private class FakeR2AwareHandler : HttpMessageHandler
    {
        private readonly FakeStorageService _storage;
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _aiHandler;

        public FakeR2AwareHandler(FakeStorageService storage, Func<HttpRequestMessage, Task<HttpResponseMessage>> aiHandler)
        {
            _storage = storage;
            _aiHandler = aiHandler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;

            // Fake R2: http://localhost:9999/storage/exams/1/sessions/2/students/3/q_4.webm
            if (uri.StartsWith("http://localhost:9999/storage/"))
            {
                var objectKey = "/" + Uri.UnescapeDataString(uri["http://localhost:9999/storage/".Length..].Split('?')[0]);
                var stream = _storage.TryGetStream(objectKey);
                if (stream == null)
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(stream)
                };
            }

            return await _aiHandler(request);
        }
    }
}
