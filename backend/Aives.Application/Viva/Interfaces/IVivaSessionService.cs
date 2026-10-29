using Aives.Application.Viva.DTOs;

namespace Aives.Application.Viva.Interfaces;

public interface IVivaSessionService
{
    Task<StartVivaSessionResponse> StartSessionAsync(int sessionId, int studentId);
    Task<VivaQuestionDto> GetCurrentQuestionAsync(int sessionId, int studentId);
    Task<SubmitVivaAnswerResponse> SubmitAnswerAsync(
        int sessionId, int studentId, SubmitVivaAnswerRequest request,
        CancellationToken cancellationToken = default);
    Task<FinishVivaSessionResponse> FinishSessionAsync(int sessionId, int studentId, CancellationToken cancellationToken = default);
}
