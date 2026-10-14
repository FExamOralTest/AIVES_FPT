using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aives.Application.AI.DTOs;
using Aives.Application.AI;
using Aives.Application.AI.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.AI;

public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiServiceClient> _logger;

    public AiServiceClient(HttpClient httpClient, ILogger<AiServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SpeechToTextResponse> TranscribeAudioAsync(
        Stream audioStream, string fileName, string examSessionId, string language = "vi-VN")
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var streamContent = new StreamContent(audioStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            form.Add(streamContent, "audio_file", fileName);
            form.Add(new StringContent(examSessionId), "exam_session_id");
            form.Add(new StringContent(language), "language");

            var response = await _httpClient.PostAsync("/ai/speech/transcribe", form);

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
            var request = new TextToSpeechRequest
            {
                Text = text,
                Language = language,
                Voice = voice
            };

            var response = await _httpClient.PostAsJsonAsync("/ai/speech/synthesize", request);

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
            var response = await _httpClient.PostAsJsonAsync("/ai/interview/follow-up", request);

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
            using var response = await _httpClient.PostAsJsonAsync("/ai/evaluate/session", request, cancellationToken);

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

    private static EvaluateSessionResponse EvaluationFailure(string code, string message) => new()
    {
        Success = false,
        ErrorCode = code,
        Message = message
    };
}
