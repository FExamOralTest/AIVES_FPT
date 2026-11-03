using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aives.Application.AI;
using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Queue.Contracts.Stt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aives.Infrastructure.AI;

/// <summary>
/// HTTP client gọi AI service (Faster-Whisper STT, evaluation, follow-up, TTS).
/// Mọi request tự động gắn <c>X-Internal-Token</c> header để AI verify
/// request đến từ backend AIVES (chứ không phải attacker gọi trực tiếp).
/// </summary>
/// <remarks>
/// Triết lý bảo mật:
/// <list type="bullet">
///   <item>Backend và AI là 2 microservice trong cùng cluster → network policy đã cô lập.</item>
///   <item>Token header là defense-in-depth: nếu network policy bị miss, AI vẫn từ chối.</item>
///   <item>Token KHÔNG log ra → tránh lộ qua application log aggregator.</item>
///   <item>Nếu AI trả 401/403 → coi như internal auth failed, fail-fast (không retry).</item>
/// </list>
/// </remarks>
public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly AiServiceOptions _options;
    private readonly ILogger<AiServiceClient> _logger;

    public AiServiceClient(
        HttpClient httpClient,
        IOptions<AiServiceOptions> options,
        ILogger<AiServiceClient> logger)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;

        if (_httpClient.BaseAddress is null)
            throw new InvalidOperationException(
                "HttpClient.BaseAddress must be set via AddHttpClient when registering AiServiceClient, " +
                "or provide BaseAddress in tests.");

        if (!_options.HasApiKey)
        {
            // Không crash app ở dev nếu user chưa set, nhưng warn loudly.
            // Production thì Program.cs sẽ fail-fast khi build options.
            _logger.LogWarning(
                "AI:ApiKey is not configured. Internal Service Token will be EMPTY — AI service may reject all requests.");
        }
    }
    public async Task<SpeechToTextResponse> TranscribeAudioAsync(
        Stream audioStream, string fileName, string examSessionId, string language = "vi-VN",
        CancellationToken cancellationToken = default)
    {
        try
        {
            ApplyInternalToken();

            using var form = new MultipartFormDataContent();
            var streamContent = new StreamContent(audioStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(streamContent, "audio_file", fileName);
            form.Add(new StringContent(examSessionId), "exam_session_id");
            form.Add(new StringContent(language), "language");

            using var response = await _httpClient.PostAsync("/ai/speech/transcribe", form, cancellationToken);

            if (IsAuthFailure(response.StatusCode))
            {
                _logger.LogError(
                    "AI STT rejected with {StatusCode} — internal token invalid. Check AI:ApiKey config.",
                    (int)response.StatusCode);
                return AuthFailure("STT");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("STT failed with status {StatusCode}", response.StatusCode);
                try
                {
                    var failure = await response.Content.ReadFromJsonAsync<SpeechToTextResponse>(cancellationToken);
                    if (failure?.Error is { } error && !string.IsNullOrWhiteSpace(error.Code))
                    {
                        failure.Success = false;
                        failure.Transcript = string.Empty;
                        failure.Message = error.Message;
                        return failure;
                    }
                }
                catch (JsonException)
                {
                    // Older deployments and proxies may return a different error body.
                }

                var code = SttContract.MapHttpStatusToCode((int)response.StatusCode);
                return SttFailure(code, $"AI STT service returned status {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<SpeechToTextResponse>(cancellationToken);
            return result ?? new SpeechToTextResponse { Success = false, Message = "Empty response from STT service." };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SttFailure(SttContract.CodeTimeout, "AI STT request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling AI STT service at {BaseAddress}", _httpClient.BaseAddress);
            return SttFailure(SttContract.CodeInternalError, "Could not complete AI STT request.");
        }
    }

    public async Task<SpeechToTextResponse> TranscribeStoredAudioAsync(
        int answerId, string objectKey, CancellationToken cancellationToken = default)
    {
        try
        {
            ApplyInternalToken();
            using var response = await _httpClient.PostAsJsonAsync("/ai/speech/transcribe-object",
                new { answerId, objectKey }, cancellationToken);

            if (IsAuthFailure(response.StatusCode))
                return AuthFailure("STT");

            if (!response.IsSuccessStatusCode)
            {
                try
                {
                    var failure = await response.Content.ReadFromJsonAsync<SpeechToTextResponse>(cancellationToken);
                    if (failure?.Error is { } error && !string.IsNullOrWhiteSpace(error.Code))
                    {
                        failure.Success = false;
                        failure.Transcript = string.Empty;
                        return failure;
                    }
                }
                catch (JsonException)
                {
                    // Proxies may return an HTML error page instead of the STT contract.
                }

                var code = SttContract.MapHttpStatusToCode((int)response.StatusCode);
                return SttFailure(code, $"AI STT service returned status {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<StoredSpeechToTextResponse>(cancellationToken);
            if (result == null || result.AnswerId != answerId)
                return SttFailure(SttContract.CodeInvalidResponse, "STT response does not match the requested answer.");

            if (!result.Success)
            {
                var error = result.Error ?? new SttErrorPayload
                {
                    Code = SttContract.CodeInternalError,
                    Message = "Stored audio transcription failed.",
                    Retryable = true
                };

                // Chuẩn hoá error code về taxonomy của contract + đồng bộ cờ retryable.
                // Code KHÔNG nhận diện được được giữ nguyên (forward-compatibility) —
                // orchestrator sẽ map về SttErrorCode.Unknown và log cảnh báo để
                // Backend cập nhật taxonomy.
                var normalizedCode = error.Code?.Trim().ToUpperInvariant() ?? string.Empty;
                if (normalizedCode.Length == 0)
                    normalizedCode = SttContract.CodeInternalError;

                var retryable = SttContract.IsKnownCode(normalizedCode)
                    ? SttContract.IsRetryableCode(normalizedCode)
                    : error.Retryable;

                return new SpeechToTextResponse
                {
                    Success = false,
                    Message = result.Error?.Message ?? result.Message ?? "Stored audio transcription failed.",
                    Error = new SttErrorPayload
                    {
                        Code = normalizedCode,
                        Message = error.Message,
                        Retryable = retryable
                    }
                };
            }

            // Task 1.5 — validate response theo STT contract trước khi dùng.
            var mapped = new SpeechToTextResponse
            {
                Success = true,
                Transcript = result.Transcript,
                LanguageDetected = result.Language,
                Duration = result.Duration
            };

            if (!SttContract.TryValidate(mapped, out var contractError))
            {
                _logger.LogWarning(
                    "Stored audio transcription violated the STT contract: {Error}", contractError?.Message);
                return SttFailure(SttContract.CodeInvalidResponse,
                    contractError?.Message ?? "STT returned invalid audio metadata.");
            }

            return mapped;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SttFailure(SttContract.CodeTimeout, "AI STT request timed out.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Stored audio transcription returned an invalid response.");
            return SttFailure(SttContract.CodeInvalidResponse, "STT returned an invalid response.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not connect to the STT service.");
            return SttFailure(SttContract.CodeInternalError, "Could not connect to the STT service.");
        }
    }

    private static SpeechToTextResponse SttFailure(string code, string message) => new()
    {
        Success = false,
        Message = message,
        Error = new SttErrorPayload
        {
            Code = code,
            Message = message,
            // Cờ retryable lấy từ contract (task 1.5) để không lệch với taxonomy.
            Retryable = SttContract.IsRetryableCode(code)
        }
    };

    public async Task<TextToSpeechResponse> SynthesizeSpeechAsync(string text, string language = "vi-VN", string? voice = null)
    {
        try
        {
            ApplyInternalToken();

            var request = new TextToSpeechRequest
            {
                Text = text,
                Language = language,
                Voice = voice
            };

            using var response = await _httpClient.PostAsJsonAsync("/ai/speech/synthesize", request);

            if (IsAuthFailure(response.StatusCode))
                return new TextToSpeechResponse { Success = false, Message = "Internal auth failed (TTS)." };

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("TTS failed with status {StatusCode}: {Error}", response.StatusCode, error);
                return new TextToSpeechResponse
                {
                    Success = false,
                    Message = $"AI TTS service returned status {response.StatusCode}: {error}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<TextToSpeechResponse>();
            return result ?? new TextToSpeechResponse { Success = false, Message = "Empty response from TTS service." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling AI TTS service at {BaseAddress}", _httpClient.BaseAddress);
            return new TextToSpeechResponse
            {
                Success = false,
                Message = $"Error connecting to AI TTS: {ex.Message}"
            };
        }
    }

    public async Task<FollowUpResponse> GenerateFollowUpAsync(FollowUpRequest request)
    {
        try
        {
            ApplyInternalToken();

            using var response = await _httpClient.PostAsJsonAsync("/ai/interview/follow-up", request);

            if (IsAuthFailure(response.StatusCode))
                return new FollowUpResponse
                {
                    Success = false,
                    ShouldFollowUp = false,
                    Message = "Internal auth failed (Follow-up)."
                };

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Follow-up failed with status {StatusCode}: {Error}", response.StatusCode, error);
                return new FollowUpResponse
                {
                    Success = false,
                    ShouldFollowUp = false,
                    Message = $"AI Follow-up service returned status {response.StatusCode}: {error}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<FollowUpResponse>();
            return result ?? new FollowUpResponse { Success = false, ShouldFollowUp = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling AI Follow-up service at {BaseAddress}", _httpClient.BaseAddress);
            return new FollowUpResponse
            {
                Success = false,
                ShouldFollowUp = false,
                Message = $"Error connecting to AI Follow-up: {ex.Message}"
            };
        }
    }

    public async Task<EvaluateSessionResponse> EvaluateSessionAsync(
        EvaluateSessionRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            ApplyInternalToken();

            using var response = await _httpClient.PostAsJsonAsync("/ai/evaluate/session", request, cancellationToken);

            if (IsAuthFailure(response.StatusCode))
                return EvaluationFailure("internal_auth_failed", "AI rejected request: invalid internal token.");

            if (!response.IsSuccessStatusCode)
            {
                // Phân biệt lỗi server (5xx / 429 — tạm thời, retry được) với lỗi client
                // (4xx — request sai, retry cũng ra kết quả y hệt → dead-letter ngay).
                var statusCode = (int)response.StatusCode;
                var code = statusCode >= 500 || statusCode == 429
                    ? "http_error"
                    : "http_client_error";
                var detail = await ReadApiErrorDetailAsync(response, cancellationToken);
                _logger.LogWarning(
                    "Session evaluation failed with status {StatusCode}: {ErrorDetail}",
                    statusCode,
                    detail ?? "No error detail returned.");
                var message = detail == null
                    ? $"AI evaluation returned HTTP {statusCode}."
                    : $"AI evaluation returned HTTP {statusCode}: {detail}";
                return EvaluationFailure(code, message);
            }

            var result = await response.Content.ReadFromJsonAsync<EvaluateSessionResponse>(cancellationToken);
            if (result == null)
                return EvaluationFailure("invalid_response", "AI evaluation returned an empty response.");
            if (!result.Success)
                return EvaluationFailure("evaluation_failed", "AI service could not evaluate this submission.");

            EvaluationContract.Validate(
                request,
                result,
                message => _logger.LogWarning("{EvaluationWarning}", message));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AI session evaluation timed out.");
            return EvaluationFailure("timeout", "AI evaluation timed out. The submission is available for teacher review.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Could not connect to AI evaluation service.");
            return EvaluationFailure("connection_error", "Could not connect to the AI evaluation service.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            _logger.LogWarning(ex, "AI evaluation response validation failed: {ValidationError}", ex.Message);
            return EvaluationFailure("invalid_response", $"AI evaluation response validation failed: {ex.Message}");
        }
    }

    // ────────────────────────────────────────────────────────────────────
    //  Internal helpers
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gắn <c>X-Internal-Token</c> header cho request TIẾP THEO.
    /// Dùng per-request thay vì DefaultRequestHeaders để tránh leak sang HttpClient instances khác
    /// (khi HttpClient được resolve từ pool có thể dùng chung nếu cấu hình sai).
    /// </summary>
    private void ApplyInternalToken()
    {
        if (!_options.HasApiKey)
            return; // Caller đã log warning ở constructor; không spam thêm ở mỗi request.

        // Set trên HttpClient default headers — OK vì AddHttpClient bind 1 HttpClient riêng cho AiServiceClient.
        // Nếu sau này cần multi-tenant, switch sang HttpRequestMessage.Headers trong từng call.
        if (!_httpClient.DefaultRequestHeaders.Contains(AiServiceOptions.TokenHeaderName))
        {
            _httpClient.DefaultRequestHeaders.Add(AiServiceOptions.TokenHeaderName, _options.ApiKey);
        }
    }

    private static bool IsAuthFailure(System.Net.HttpStatusCode statusCode) =>
        statusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden;

    private static SpeechToTextResponse AuthFailure(string endpoint) => new()
    {
        Success = false,
        Message = $"Internal auth failed at {endpoint}. Check AI:ApiKey configuration.",
        Error = new SttErrorPayload
        {
            Code = SttContract.CodeAuthFailed,
            Message = "Internal service authentication failed.",
            Retryable = false
        }
    };

    private static EvaluateSessionResponse EvaluationFailure(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        Message = message
    };

    private static async Task<string?> ReadApiErrorDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("detail", out var detail)
                || detail.ValueKind != JsonValueKind.String)
                return null;

            var message = detail.GetString()?.Trim();
            if (string.IsNullOrEmpty(message))
                return null;
            return message.Length <= 500 ? message : message[..500];
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
