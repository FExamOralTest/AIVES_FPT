using Aives.Application.Viva.DTOs;

namespace Aives.Application.Viva.Interfaces;

/// <summary>
/// Chấm một bài thi đã nộp. Hangfire gọi dịch vụ này từ hàng đợi <c>grading</c>,
/// mỗi lần một bài, sau khi sinh viên đã nộp xong.
/// </summary>
public interface ISessionGradingService
{
    /// <summary>
    /// Chấm bài <paramref name="resultId"/>. Khi <paramref name="finalAttempt"/> là false và lỗi
    /// có thể thử lại, trạng thái bài được giữ ở <c>PENDING</c>.
    /// </summary>
    Task<SessionGradingResult> GradeAsync(int resultId, bool finalAttempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận bài không chấm được sau khi hết số lần thử hoặc job gặp lỗi ngoài dự kiến.
    /// </summary>
    Task MarkFailedAsync(int resultId, string error, CancellationToken cancellationToken = default);
}

public interface IVivaSessionService
{
    Task<StartVivaSessionResponse> StartSessionAsync(int sessionId, int studentId);
    Task<VivaQuestionDto> GetCurrentQuestionAsync(int sessionId, int studentId);
    Task<SubmitVivaAnswerResponse> SubmitAnswerAsync(
        int sessionId, int studentId, SubmitVivaAnswerRequest request,
        CancellationToken cancellationToken = default);
    Task<FinishVivaSessionResponse> FinishSessionAsync(int sessionId, int studentId, CancellationToken cancellationToken = default);
}
