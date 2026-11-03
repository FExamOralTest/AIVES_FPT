using Aives.Application.AI.DTOs;

namespace Aives.Application.AI.Interfaces;

public interface IAiServiceClient
{
    Task<SpeechToTextResponse> TranscribeAudioAsync(Stream audioStream, string fileName, string examSessionId, string language = "vi-VN", CancellationToken cancellationToken = default);
    Task<SpeechToTextResponse> TranscribeStoredAudioAsync(int answerId, string objectKey, CancellationToken cancellationToken = default);
    Task<TextToSpeechResponse> SynthesizeSpeechAsync(string text, string language = "vi-VN", string? voice = null);
    Task<FollowUpResponse> GenerateFollowUpAsync(FollowUpRequest request);
    Task<EvaluateSessionResponse> EvaluateSessionAsync(EvaluateSessionRequest request, CancellationToken cancellationToken = default);
}
