using System.Text.Json;
using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.Contracts.Stt;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

public class AnswerOrchestrator : IAnswerOrchestrator
{
    private static readonly TimeSpan AiCallTimeout = TimeSpan.FromMinutes(2);

    private readonly IAnswerRepository _answerRepository;
    private readonly IStorageService _storageService;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly ILogger<AnswerOrchestrator> _logger;
    private readonly HttpMessageHandler? _downloadHandler;

    public AnswerOrchestrator(
        IAnswerRepository answerRepository,
        IResultRepository resultRepository,
        IStorageService storageService,
        IAiServiceClient aiServiceClient,
        ILogger<AnswerOrchestrator> logger,
        HttpMessageHandler? downloadHandler = null)
    {
        _answerRepository = answerRepository;
        _storageService = storageService;
        _aiServiceClient = aiServiceClient;
        _logger = logger;
        _downloadHandler = downloadHandler;
    }

    public async Task<OrchestrationResult> ProcessAnswerAsync(
        int answerId, int attempt, CancellationToken cancellationToken)
    {
        var answer = await _answerRepository.GetByIdWithDetailsAsync(answerId, cancellationToken);
        if (answer == null)
            return SttFail(SttErrorCode.Unknown, $"Answer {answerId} not found.", false);

        if (answer.Status == AnswerStatus.EVALUATED && answer.AiScore.HasValue)
            return new OrchestrationResult { Success = true };

        var transcript = answer.Transcript ?? string.Empty;
        string? detectedLanguage = null;
        float? durationSeconds = null;
        if (string.IsNullOrWhiteSpace(transcript))
        {
            if (!string.IsNullOrWhiteSpace(answer.ObjectKey) || !string.IsNullOrWhiteSpace(answer.AudioUrl))
            {
                var stt = await TranscribeFromR2Async(answer, cancellationToken);
                if (!stt.Success)
                {
                    await _answerRepository.MarkFailedAsync(answerId, stt.Message ?? "STT failed.", cancellationToken);
                    return stt;
                }
                transcript = stt.Transcript ?? string.Empty;
                detectedLanguage = stt.DetectedLanguage;
                durationSeconds = stt.DurationSeconds;
            }
            else
            {
                transcript = answer.StudentAnswer ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(transcript))
        {
            const string message = "Empty transcript: student did not speak or audio is silent.";
            await _answerRepository.MarkFailedAsync(answerId, message, cancellationToken);
            return SttFail(SttErrorCode.EmptyTranscript, message, false);
        }

        // Keep STT output available even when evaluation fails or is retried.
        await _answerRepository.UpdateTranscriptAsync(answerId, transcript, AnswerStatus.PROCESSING,
            cancellationToken, detectedLanguage, durationSeconds);

        var evaluation = await EvaluateAnswerAsync(answer, transcript, cancellationToken);
        if (!evaluation.Success)
        {
            await _answerRepository.MarkFailedAsync(answerId, evaluation.Message ?? "Evaluation failed.", cancellationToken);
            return evaluation;
        }

        await _answerRepository.UpdateAiEvaluationAsync(answerId, evaluation.AiScore, evaluation.AiFeedback,
            evaluation.AiAnalysisJson, AnswerStatus.EVALUATED, cancellationToken);

        _logger.LogInformation("Answer {AnswerId} processed on attempt {Attempt}: score={Score}",
            answerId, attempt, evaluation.AiScore);
        evaluation.Transcript = transcript;
        return evaluation;
    }

    private async Task<OrchestrationResult> TranscribeFromR2Async(Answer answer, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(AiCallTimeout);
            SpeechToTextResponse stt;
            if (!string.IsNullOrWhiteSpace(answer.ObjectKey))
            {
                stt = await _aiServiceClient.TranscribeStoredAudioAsync(answer.AnswerId, answer.ObjectKey, cts.Token);
            }
            else
            {
                // Support recordings saved before ObjectKey was introduced.
                using var http = _downloadHandler != null
                    ? new HttpClient(_downloadHandler, disposeHandler: false)
                    : new HttpClient();
                var playbackUrl = _storageService.ResolvePlaybackUrl(answer.AudioUrl, TimeSpan.FromMinutes(10));
                if (!Uri.TryCreate(playbackUrl, UriKind.Absolute, out var uri)
                    || uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    return SttFail(SttErrorCode.UnsupportedFormat, "Audio URL is not downloadable.", false);

                using var audio = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!audio.IsSuccessStatusCode)
                    return SttFail(SttErrorCode.AudioCorrupted,
                        $"Audio download failed with status {(int)audio.StatusCode}.", false);

                await using var stream = await audio.Content.ReadAsStreamAsync(cts.Token);
                stt = await _aiServiceClient.TranscribeAudioAsync(stream, Path.GetFileName(answer.AudioUrl!),
                    answer.Result.SessionId.ToString(), cancellationToken: cts.Token);
            }

            if (!stt.Success)
            {
                var code = stt.Error?.Code?.ToUpperInvariant() switch
                {
                    "EMPTY_AUDIO" or "AUDIO_CORRUPTED" => SttErrorCode.AudioCorrupted,
                    "NO_SPEECH" or "EMPTY_TRANSCRIPT" => SttErrorCode.EmptyTranscript,
                    "UNSUPPORTED_FORMAT" => SttErrorCode.UnsupportedFormat,
                    "TIMEOUT" => SttErrorCode.Timeout,
                    _ => SttErrorCode.InternalError
                };
                return SttFail(code, stt.Error?.Message ?? stt.Message ?? "STT failed.",
                    stt.Error?.Retryable ?? (code is SttErrorCode.Timeout or SttErrorCode.InternalError));
            }

            if (string.IsNullOrWhiteSpace(stt.Transcript))
                return SttFail(SttErrorCode.EmptyTranscript, "Audio has no speech content.", false);

            if (!double.IsFinite(stt.Duration) || stt.Duration < 0 || stt.Duration > float.MaxValue
                || string.IsNullOrWhiteSpace(stt.LanguageDetected) || stt.LanguageDetected.Length > 20)
                return SttFail(SttErrorCode.InternalError, "STT returned invalid audio metadata.", false);

            return new OrchestrationResult
            {
                Success = true,
                Transcript = stt.Transcript,
                DetectedLanguage = stt.LanguageDetected,
                DurationSeconds = (float)stt.Duration
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SttFail(SttErrorCode.Timeout, "STT request timed out.", true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STT failed for Answer {AnswerId}", answer.AnswerId);
            return SttFail(SttErrorCode.InternalError, "Could not transcribe answer audio.", true);
        }
    }

    private async Task<OrchestrationResult> EvaluateAnswerAsync(
        Answer answer, string transcript, CancellationToken cancellationToken)
    {
        var question = answer.Question;
        if (question == null)
            return EvalFail("question_not_found", "Question reference missing on answer.");

        var context = new QuestionAnswerPairDto
        {
            QuestionId = question.QuestionId.ToString(),
            QuestionText = question.QuestionText,
            StudentAnswer = transcript,
            IsFollowUp = answer.IsFollowUp ?? false,
            SampleAnswer = question.SampleAnswer,
            LearningOutcome = question.LearningOutcome,
            Topic = question.Topic,
            MaxScore = question.MaxScore,
            ExpectedKeywords = question.ExpectedKeywords.ToList(),
            Rubric = question.Rubric.Select(r => new AnswerRubricCriterionDto
            {
                CriterionId = r.CriterionId.ToString(),
                Description = r.Description,
                MaxScore = (float)r.MaxScore
            }).ToList()
        };
        var request = new EvaluateSessionRequest
        {
            ExamSessionId = answer.Result.SessionId.ToString(),
            StudentId = answer.Result.StudentId.ToString(),
            CourseName = answer.Result.Session?.Exam?.Course?.CourseName ?? string.Empty,
            MaxScorePerQuestion = question.MaxScore ?? 1.0f,
            QuestionAnswers = new() { context }
        };

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(AiCallTimeout);
            var response = await _aiServiceClient.EvaluateSessionAsync(request, cts.Token);
            if (!response.Success)
                return EvalFail(response.ErrorCode ?? "evaluation_failed", response.Message ?? "AI evaluation failed.");

            Aives.Application.AI.EvaluationContract.Validate(request, response);
            var score = response.QuestionScores.Single(s => s.QuestionId == context.QuestionId);
            return new OrchestrationResult
            {
                Success = true,
                AiScore = score.Score,
                AiFeedback = score.Feedback,
                AiAnalysisJson = JsonSerializer.Serialize(new StoredAnswerAnalysisDto { Context = context, Evaluation = score })
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return EvalFail("timeout", "AI evaluation timed out.");
        }
        catch (InvalidDataException)
        {
            return EvalFail("invalid_response", "AI evaluation returned an invalid response.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Evaluation failed for Answer {AnswerId}", answer.AnswerId);
            return EvalFail("internal_error", "Could not evaluate answer.");
        }
    }

    private static OrchestrationResult SttFail(SttErrorCode code, string message, bool retryable) => new()
    {
        SttError = new SttError { Code = code, Message = message, Retryable = retryable },
        Message = message
    };

    private static OrchestrationResult EvalFail(string code, string message) =>
        new() { EvaluationErrorCode = code, Message = message };
}
