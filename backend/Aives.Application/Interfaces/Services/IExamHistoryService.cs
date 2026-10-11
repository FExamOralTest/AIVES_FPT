using Aives.Application.DTOs;

namespace Aives.Application.Interfaces.Services;

public interface IExamHistoryService
{
    /// <summary>Danh sách tất cả bài thi của một course.</summary>
    Task<IEnumerable<ExamSummaryDto>> GetExamsByCourseAsync(int courseId);

    /// <summary>Chi tiết một bài thi — gồm tất cả phiên thi và kết quả từng student.</summary>
    Task<IEnumerable<ExamSessionDetailDto>> GetSessionsByExamAsync(int examId);

    /// <summary>Chi tiết một phiên thi — danh sách student đã thi và điểm.</summary>
    Task<ExamSessionDetailDto> GetSessionDetailAsync(int sessionId);

    /// <summary>Kết quả chi tiết của một student — từng câu hỏi và câu trả lời.</summary>
    Task<StudentResultDetailDto> GetResultDetailAsync(int resultId);
}
