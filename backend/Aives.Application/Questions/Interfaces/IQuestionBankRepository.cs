using Aives.Domain.Entities;

namespace Aives.Application.Questions.Interfaces;

public interface IQuestionBankRepository
{
    Task<IEnumerable<QuestionBank>> GetByCourseIdAsync(int courseId, int limit = 50);
    Task<List<int>> GetValidQuestionIdsForCourseAsync(int courseId, IEnumerable<int> questionIds);
    Task<QuestionBank?> GetByIdAsync(int questionId);
    Task<IEnumerable<QuestionBank>> GetRandomByLevelAsync(int courseId, int level, int count);
    Task AddRangeAsync(IEnumerable<QuestionBank> questions);
    Task SaveChangesAsync();
}
