using System.Text.Json;
using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Contracts.Stt;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Orchestrator thực thi pipeline AI cho 1 Answer:
/// <list type="number">
///   <item>Download audio từ R2 (nếu AudioUrl là object key).</item>
///   <item>STT qua AI1 (Faster-Whisper).</item>
///   <item>Eval qua AI2 (nếu transcript không rỗng).</item>
///   <item>Persist kết quả vào Answer + Result.</item>
/// </list>
/// Được wrap bởi AnswerProcessingJob (Hangfire) và cũng có thể gọi sync từ Submit API.
/// </summary>
public class AnswerOrchestrator : IAnswerOrchestrator
{
    /// <summary>Timeout mỗi lần gọi AI service. Ngắn hơn Hangfire Retry filter để fail sớm, retry nhanh hơn.</summary>
    private static readonly TimeSpan AiCallTimeout = TimeSpan.FromMinutes(2);

    private readonly IAnswerRepository _answerRepository;
    private readonly IResultRepository _resultRepository;
    private readonly IStorageService _storageService;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly ILogger<AnswerOrchestrator> _logger;

    public AnswerOrchestrator(
        IAnswerRepository answerRepository,
        IResultRepository resultRepository,
        IStorageService storageService,
        IAiServiceClient aiServiceClient,
        ILogger<AnswerOrchestrator> logger)
    {
        _answerRepository = answerRepository;
        _resultRepository = resultRepository;
        _storageService = storageService;
        _aiServiceClient = aiServiceClient;
        _logger = logger;
    }

    public async Task<OrchestrationResult> ProcessAnswerAsync(
        int answerId, int attempt, CancellationToken cancellationToken)
    {
        var answer = await _answerRepository.GetByIdWithDetailsAsync(answerId, cancellationToken);
        if (answer == null)
        {
            _logger.LogError("Answer {AnswerId} not found (attempt {Attempt})", answerId, attempt);
            return new OrchestrationResult
            {
                Success = false,
                SttError = new SttError
                {
                    Code = SttErrorCode.Unknown,
                    Message = $"Answer {answerId} not found.",
                    Retryable = false
                },
                Message = "Answer not found."
            };
        }

        // 1. Nếu Answer không có audio (student chỉ nhập text) → dùng StudentAnswer làm transcript.
        string transcript = answer.Transcript ?? string.Empty;
        if (string.IsNullOrWhiteSpace(transcript))
        {
            // Case A: có AudioUrl trên R2 → STT
            if (!string.IsNullOrWhiteSpace(answer.AudioUrl) && !IsAbsoluteHttpUrl(answer.AudioUrl!))
            {
                var sttResult = await TranscribeFromR2Async(answer, attempt, cancellationToken);
                if (!sttResult.Success)
                    return sttResult;
                transcript = sttResult.Transcript ?? string.Empty;
            }
            // Case B: audio trống, dùng text answer
            else
            {
                transcript = answer.StudentAnswer ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            // Transcript rỗng → KHÔNG retry (sẽ lặp vô ích), đánh dấu ERROR để staff biết.
            _logger.LogWarning("Answer {AnswerId} has empty transcript after STT (attempt {Attempt})", answerId, attempt);
            await _answerRepository.MarkFailedAsync(answerId, "Empty transcript: student did not speak or audio is silent.", cancellationToken);
            return new OrchestrationResult
            {
                Success = false,
                SttError = new SttError
                {
                    Code = SttErrorCode.EmptyTranscript,
                    Message = "Empty transcript after STT.",
                    Retryable = false
                },
                Message = "Empty transcript."
            };
        }

        // 2. Persist transcript trước (idempotent — nếu job retry, transcript không bị ghi đè sai).
        await _answerRepository.UpdateTranscriptAsync(answerId, transcript, AnswerStatus.PROCESSING, cancellationToken);

        // 3. AI Eval (chỉ eval answer này — evaluation theo session là task khác).
        var evaluation = await EvaluateAnswerAsync(answer, transcript, attempt, cancellationToken);
        if (!evaluation.Success)
        {
            await _answerRepository.MarkFailedAsync(answerId, evaluation.Message ?? "Evaluation failed.", cancellationToken);
            return evaluation;
        }

        // 4. Persist AI score.
        await _answerRepository.UpdateAiEvaluationAsync(
            answerId,
            evaluation.AiScore,
            evaluation.AiFeedback,
            evaluation.AiAnalysisJson,
            AnswerStatus.EVALUATED,
            cancellationToken);

        _logger.LogInformation("Answer {AnswerId} processed successfully on attempt {Attempt}: score={Score}",
            answerId, attempt, evaluation.AiScore);
        return new OrchestrationResult { Success = true };
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private async Task<OrchestrationResult> TranscribeFromR2Async(
        Answer answer, int attempt, CancellationToken cancellationToken)
    {
        // AudioUrl có thể là:
        //   - Object key R2: "exams/1/sessions/2/students/3/q_4.webm"
        //   - URL fallback local: "/uploads/audio/file.webm"
        //   - URL external: "https://..."  (không xảy ra với upload của mình nhưng defensive)
        //
        // Để download từ R2 trong cùng service: dùng cùng Storage service.
        // Vì IStorageService hiện không có DownloadAsync, ta dùng presigned URL + HttpClient.
        // (Tránh truy cập trực tiếp S3 client vì nó nằm private trong service.)

        try
        {
            using var http = new HttpClient { Timeout = AiCallTimeout };
            var presignedUrl = _storageService.ResolvePlaybackUrl(
                answer.AudioUrl,
                TimeSpan.FromMinutes(10));

            if (string.IsNullOrWhiteSpace(presignedUrl) || !Uri.TryCreate(presignedUrl, UriKind.Absolute, out var uri))
            {
                return SttFail(SttErrorCode.UnsupportedFormat, "Audio URL is not downloadable.", retryable: false);
            }

            // Chỉ download khi là R2 (scheme http/https). Local fallback path trả "/uploads/..." → không phải absolute URI.
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return SttFail(SttErrorCode.UnsupportedFormat,
                    "Local fallback audio is not supported for background processing.", retryable: false);
            }

            using var audioResp = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!audioResp.IsSuccessStatusCode)
            {
                return SttFail(SttErrorCode.AudioCorrupted,
                    $"Audio download failed with status {(int)audioResp.StatusCode}.", retryable: false);
            }

            await using var audioStream = await audioResp.Content.ReadAsStreamAsync(cancellationToken);
            using var aiCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            aiCts.CancelAfter(AiCallTimeout);

            var stt = await _aiServiceClient.TranscribeAudioAsync(
                audioStream,
                Path.GetFileName(answer.AudioUrl!),
                answer.Result.SessionId.ToString());

            if (!stt.Success)
            {
                return SttFail(SttErrorCode.InternalError,
                    stt.Message ?? "AI STT returned a failure response.",
                    retryable: true); // Có thể là AI1 quá tải / lỗi downstream → retry.
            }

            if (string.IsNullOrWhiteSpace(stt.Transcript))
            {
                // STT thành công nhưng audio im lặng / Whisper không nghe được gì.
                // Không retry: audio này đã rõ, retry cũng cho transcript rỗng.
                return SttFail(SttErrorCode.EmptyTranscript,
                    stt.Message ?? "Audio has no speech content.",
                    retryable: false);
            }

            return new OrchestrationResult
            {
                Success = true,
                Transcript = stt.Transcript,
                Message = stt.Transcript
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout từ aiCts — retryable vì có thể lần sau nhanh hơn.
            return SttFail(SttErrorCode.Timeout, "STT request timed out.", retryable: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during STT for Answer {AnswerId}", answer.AnswerId);
            return SttFail(SttErrorCode.InternalError, ex.Message, retryable: true);
        }
    }

    private async Task<OrchestrationResult> EvaluateAnswerAsync(
        Answer answer, string transcript, int attempt, CancellationToken cancellationToken)
    {
        // Hiện AI2 chấm theo SESSION, không theo answer đơn lẻ.
        // Orchestrator chỉ chấm answer này dùng rubric → gọi EvaluateSessionAsync với 1 question.
        // Teacher sẽ re-evaluate full session ở bước Review cuối.

        var question = answer.Question;
        if (question == null)
        {
            return EvalFail("question_not_found", "Question reference missing on answer.");
        }

        var evalRequest = new EvaluateSessionRequest
        {
            ExamSessionId = answer.Result.SessionId.ToString(),
            StudentId = answer.Result.StudentId.ToString(),
            CourseName = answer.Result.Session?.Exam?.Course?.CourseName ?? string.Empty,
            MaxScorePerQuestion = question.MaxScore ?? 1.0f,
            QuestionAnswers = new List<QuestionAnswerPairDto>
            {
                new()
                {
                    QuestionId = question.QuestionId.ToString(),
                    QuestionText = question.QuestionText,
                    StudentAnswer = transcript,
                    IsFollowUp = answer.IsFollowUp ?? false,
                    SampleAnswer = question.SampleAnswer,
                    LearningOutcome = question.LearningOutcome,
                    Topic = question.Topic,
                    MaxScore = question.MaxScore,
                    Rubric = question.Rubric?.Select(r => new AnswerRubricCriterionDto
                    {
                        CriterionId = r.CriterionId.ToString(),
                        Description = r.Description,
                        MaxScore = (float)r.MaxScore
                    }).ToList() ?? new()
                }
            }
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(AiCallTimeout);

            var response = await _aiServiceClient.EvaluateSessionAsync(evalRequest, cts.Token);

            if (!response.Success || response.QuestionScores.Count == 0)
            {
                return EvalFail(
                    response.ErrorCode ?? "evaluation_failed",
                    response.Message ?? "AI evaluation failed without a message.");
            }

            var score = response.QuestionScores[0];
            return new OrchestrationResult
            {
                Success = true,
                AiScore = score.Score,
                AiFeedback = score.Feedback,
                AiAnalysisJson = JsonSerializer.Serialize(score)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EvalFail("timeout", "AI evaluation timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during evaluation for Answer {AnswerId}", answer.AnswerId);
            return EvalFail("internal_error", ex.Message);
        }
    }

    private static OrchestrationResult SttFail(SttErrorCode code, string message, bool retryable) =>
        new()
        {
            Success = false,
            SttError = new SttError { Code = code, Message = message, Retryable = retryable },
            Message = message
        };

    private static OrchestrationResult EvalFail(string code, string message) =>
        new() { Success = false, EvaluationErrorCode = code, Message = message };

    private static bool IsAbsoluteHttpUrl(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
         value.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
