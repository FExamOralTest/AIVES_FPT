using Aives.Domain.Entities;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Repository interface cho thao tác dữ liệu Question Bank.
/// </summary>
public interface IQuestionBankRepository
{
    Task<IEnumerable<QuestionBank>> GetByCourseIdAsync(int courseId, int limit = 50);

    Task<List<int>> GetValidQuestionIdsForCourseAsync(int courseId, IEnumerable<int> questionIds);

    Task<QuestionBank?> GetByIdAsync(int questionId);

    Task AddRangeAsync(IEnumerable<QuestionBank> questions);

    Task SaveChangesAsync();
}