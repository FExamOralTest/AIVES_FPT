using Aives.Application.Queue.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Tra cứu trạng thái xử lý của answer/job và bài chấm — chỉ đọc, không thay đổi dữ liệu.
/// Tách khỏi controller để endpoint chỉ còn phần parse claim và map lỗi HTTP.
/// </summary>
public interface IProcessingStatusService
{
    /// <summary>
    /// Trạng thái STT + evaluation của một Answer.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Answer không tồn tại.</exception>
    /// <exception cref="UnauthorizedAccessException">Người gọi không có quyền xem answer.</exception>
    Task<AnswerProcessingStatusDto> GetAnswerStatusAsync(
        int answerId,
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trạng thái chấm cả bài của một Result, kèm tóm tắt từng answer.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Result không tồn tại.</exception>
    /// <exception cref="UnauthorizedAccessException">Người gọi không có quyền xem result.</exception>
    Task<ResultGradingStatusDto> GetResultStatusAsync(
        int resultId,
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trạng thái bài nộp của chính sinh viên trong một ca thi.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Sinh viên chưa có Result trong ca thi.</exception>
    Task<SubmissionStatusDto> GetSubmissionStatusAsync(
        int sessionId,
        int studentId,
        CancellationToken cancellationToken = default);
}
