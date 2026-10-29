using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IRetakeService
{
    Task<RetakeDto> GrantAsync(int resultId, GrantRetakeRequest request, int callerId, CancellationToken ct = default);
    Task<RetakeDto> RescheduleAsync(int retakeId, GrantRetakeRequest request, int callerId, CancellationToken ct = default);
    Task<IReadOnlyList<RetakeDto>> HistoryAsync(int resultId, int callerId, CancellationToken ct = default);
    Task<RetakeStart?> StartAsync(int sessionId, int studentId, CancellationToken ct = default);
    Task EnsureSubmittedAsync(int resultId, CancellationToken ct = default);
    Task EnsureWritableAsync(int sessionId, int studentId, bool checkDeadline = true, CancellationToken ct = default);
}
