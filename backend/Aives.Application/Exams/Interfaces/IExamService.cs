using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Service interface cho nghiệp vụ quản lý Exam của Teacher.
/// </summary>
public interface IExamService
{
    /// <summary>
    /// Teacher tạo đề thi mới. Tự động sinh ExamCode duy nhất.
    /// Nếu request có câu hỏi, gán luôn vào đề thi.
    /// </summary>
    Task<ExamDto> CreateExamAsync(CreateExamRequest request, int teacherId);

    /// <summary>
    /// Lấy thông tin chi tiết đề thi kèm danh sách câu hỏi.
    /// Chỉ Teacher sở hữu mới được xem chi tiết.
    /// </summary>
    Task<ExamDto> GetExamByIdAsync(int examId, int teacherId);

    /// <summary>Lấy danh sách đề thi của Teacher.</summary>
    Task<IEnumerable<ExamDto>> GetExamsByTeacherAsync(int teacherId);

    /// <summary>
    /// Gợi ý câu hỏi từ AI dựa trên môn học và chủ đề.
    /// Trả về danh sách câu hỏi phù hợp nhất từ Question Bank.
    /// </summary>
    Task<AiSuggestedQuestionsDto> GetAiSuggestedQuestionsAsync(AiSuggestQuestionsRequest request, int teacherId);
}
