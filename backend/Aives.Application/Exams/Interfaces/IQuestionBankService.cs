using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IQuestionBankService
{
    Task<QuestionBankPageDto> GetQuestionsAsync(int courseId, QuestionBankQuery query, int teacherId);
    Task<QuestionBankDto> GetQuestionAsync(int courseId, int questionId, int teacherId);
    Task<QuestionBankDto> CreateQuestionAsync(int courseId, WriteQuestionRequest request, int teacherId);
    Task<QuestionBankDto> UpdateQuestionAsync(int courseId, int questionId, WriteQuestionRequest request, int teacherId);
    Task DeleteQuestionAsync(int courseId, int questionId, int teacherId);
}
