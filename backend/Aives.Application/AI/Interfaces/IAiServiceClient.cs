using Aives.Application.AI.DTOs;

namespace Aives.Application.AI.Interfaces;

public interface IAiServiceClient
{
    Task<SpeechToTextResponse> TranscribeAudioAsync(Stream audioStream, string fileName, string examSessionId, string language = "vi-VN");
    Task<TextToSpeechResponse> SynthesizeSpeechAsync(string text, string language = "vi-VN", string? voice = null);
    Task<FollowUpResponse> GenerateFollowUpAsync(FollowUpRequest request);
    Task<EvaluateSessionResponse> EvaluateSessionAsync(EvaluateSessionRequest request);
}
