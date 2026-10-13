using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aives.Application.AI.DTOs;
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

    public async Task<EvaluateSessionResponse> EvaluateSessionAsync(EvaluateSessionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/ai/evaluate/session", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Session evaluation failed with status {StatusCode}: {Error}", response.StatusCode, error);
                return new EvaluateSessionResponse
                {
                    Success = false,
                    Message = $"AI Evaluation service returned status {response.StatusCode}: {error}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<EvaluateSessionResponse>();
            return result ?? new EvaluateSessionResponse { Success = false, Message = "Empty evaluation response." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling AI Session evaluation service at {BaseAddress}", _httpClient.BaseAddress);
            return new EvaluateSessionResponse
            {
                Success = false,
                Message = $"Error connecting to AI Evaluation: {ex.Message}"
            };
        }
    }
}
