using Aives.Domain.Entities;

using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IQuestionBankRepository
{
    Task<QuestionBankPageDto> GetPageAsync(int courseId, QuestionBankQuery query);
    Task<List<QuestionBank>> GetSuggestionsAsync(int courseId, string? focusTopic, int maxCount);
    Task<List<QuestionBank>> GetByIdsForCourseAsync(int courseId, IEnumerable<int> questionIds);
    Task<bool> IsInUseAsync(int questionId);
    Task AddAsync(QuestionBank question);
    void Remove(QuestionBank question);
    Task<IEnumerable<QuestionBank>> GetByCourseIdAsync(int courseId, int limit = 50);
    Task<List<int>> GetValidQuestionIdsForCourseAsync(int courseId, IEnumerable<int> questionIds);
    Task<QuestionBank?> GetByIdAsync(int questionId);
    Task<IEnumerable<QuestionBank>> GetRandomByLevelAsync(int courseId, int level, int count);
    Task SaveGeneratedAsync(IEnumerable<QuestionBank> questions, CancellationToken cancellationToken = default);
    Task SaveChangesAsync();
}
