using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IExamService
{
    Task<ExamDto> CreateExamAsync(CreateExamRequest request, int teacherId);
    Task<ExamDto> GetExamByIdAsync(int examId, int teacherId);
    Task<IEnumerable<ExamDto>> GetExamsByTeacherAsync(int teacherId);
    Task<AiSuggestedQuestionsDto> GetAiSuggestedQuestionsAsync(AiSuggestQuestionsRequest request, int teacherId);
}
