using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IQuestionBankService
{
    Task<QuestionBankPageDto> GetQuestionsAsync(int courseId, QuestionBankQuery query, int teacherId);
    Task<QuestionBankDto> GetQuestionAsync(int courseId, int questionId, int teacherId);
    Task<QuestionBankDto> CreateQuestionAsync(int courseId, WriteQuestionRequest request, int teacherId);
    Task<QuestionBankDto> UpdateQuestionAsync(int courseId, int questionId, WriteQuestionRequest request, int teacherId);
    Task DeleteQuestionAsync(int courseId, int questionId, int teacherId);
    Task<QuestionBankDto> ApproveQuestionAsync(int courseId, int questionId, int staffId);
    Task<QuestionBankDto> RejectQuestionAsync(int courseId, int questionId, string reason, int staffId);
}
