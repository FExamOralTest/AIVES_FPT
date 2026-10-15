using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IExamService
{
    Task<ExamDto> CreateExamAsync(CreateExamRequest request, int teacherId);
    Task<ExamDto> GetExamByIdAsync(int examId, int teacherId);
    Task<IEnumerable<ExamDto>> GetExamsByTeacherAsync(int teacherId);
    Task<SuggestedQuestionsDto> GetSuggestedQuestionsAsync(SuggestQuestionsRequest request, int teacherId);
    Task<ExamDto> UpdateExamQuestionsAsync(int examId, List<ExamQuestionItem> questions, int teacherId);
}
