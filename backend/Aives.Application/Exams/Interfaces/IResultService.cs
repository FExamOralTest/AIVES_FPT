using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IResultService
{
    /// <summary>
    /// [TEACHER] Lấy tất cả kết quả PENDING_REVIEW của một Exam Session cụ thể.
    /// </summary>
    Task<IEnumerable<ResultForReviewDto>> GetResultsForReviewAsync(int sessionId, int teacherId);

    /// <summary>
    /// [TEACHER] Lấy tất cả kết quả PENDING_REVIEW của tất cả sessions do Teacher quản lý.
    /// </summary>
    Task<IEnumerable<ResultForReviewDto>> GetAllPendingReviewResultsAsync(int teacherId);

    /// <summary>
    /// [TEACHER] Lấy chi tiết một Result để review.
    /// </summary>
    Task<ResultForReviewDto> GetResultForReviewAsync(int resultId, int teacherId);

    /// <summary>
    /// [TEACHER] Review và lưu điểm cuối cùng, feedback; cập nhật status = REVIEWED.
    /// BR-10: Teacher approve.
    /// </summary>
    Task<ResultForReviewDto> ReviewResultAsync(int resultId, ReviewResultRequest request, int teacherId);

    /// <summary>
    /// [TEACHER] Publish kết quả đã approve (REVIEWED) → PUBLISHED.
    /// BR-10: Teacher publish Final Result. BR-11: Final Result được giao cho Student.
    /// </summary>
    Task<ResultForReviewDto> PublishResultAsync(int resultId, int teacherId);

    /// <summary>
    /// [STUDENT] Xem kết quả một ca thi của chính mình.
    /// </summary>
    Task<StudentResultDto> GetStudentResultAsync(int sessionId, int studentId);

    /// <summary>
    /// [STUDENT] Lấy kết quả đã publish của chính mình.
    /// BR-11: Student chỉ được xem Result thuộc chính mình và có status = PUBLISHED.
    /// </summary>
    Task<IEnumerable<ResultForReviewDto>> GetStudentPublishedResultsAsync(int studentId);
}