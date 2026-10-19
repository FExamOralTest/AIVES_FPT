using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aives.Application.AI;
using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
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
        Stream audioStream, string fileName, string examSessionId, string language = "vi-VN")
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

            using var response = await _httpClient.PostAsync("/ai/speech/transcribe", form);

            if (IsAuthFailure(response.StatusCode))
            {
                _logger.LogError(
                    "AI STT rejected with {StatusCode} — internal token invalid. Check AI:ApiKey config.",
                    (int)response.StatusCode);
                return AuthFailure("STT");
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("STT failed with status {StatusCode}: {Error}", response.StatusCode, error);
                return new SpeechToTextResponse
                {
                    Success = false,
                    Message = $"AI STT service returned status {response.StatusCode}: {error}",
                    Transcript = string.Empty
                };
            }

            var result = await response.Content.ReadFromJsonAsync<SpeechToTextResponse>();
            return result ?? new SpeechToTextResponse { Success = false, Message = "Empty response from STT service." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling AI STT service at {BaseAddress}", _httpClient.BaseAddress);
            return new SpeechToTextResponse
            {
                Success = false,
                Message = $"Error connecting to AI STT: {ex.Message}",
                Transcript = string.Empty
            };
        }
    }

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
                _logger.LogWarning("Session evaluation failed with status {StatusCode}", response.StatusCode);
                return EvaluationFailure("http_error", $"AI evaluation returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<EvaluateSessionResponse>(cancellationToken);
            if (result == null)
                return EvaluationFailure("invalid_response", "AI evaluation returned an empty response.");
            if (!result.Success)
                return EvaluationFailure("evaluation_failed", "AI service could not evaluate this submission.");

            EvaluationContract.Validate(request, result);
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
            _logger.LogWarning(ex, "AI evaluation returned an invalid response.");
            return EvaluationFailure("invalid_response", "AI evaluation returned an invalid response.");
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
        Message = $"Internal auth failed at {endpoint}. Check AI:ApiKey configuration."
    };

    private static EvaluateSessionResponse EvaluationFailure(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        Message = message
    };
}
