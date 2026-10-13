using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class QuestionBankRepository : IQuestionBankRepository
{
    private readonly AivesDbContext _db;

    public QuestionBankRepository(AivesDbContext db) => _db = db;

    public async Task<IEnumerable<QuestionBank>> GetByCourseIdAsync(int courseId, int limit = 50)
        => await _db.QuestionBanks
            .Where(q => q.CourseId == courseId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<List<int>> GetValidQuestionIdsForCourseAsync(int courseId, IEnumerable<int> questionIds)
    {
        var idList = questionIds.ToList();
        return await _db.QuestionBanks
            .Where(q => q.CourseId == courseId && idList.Contains(q.QuestionId))
            .Select(q => q.QuestionId)
            .ToListAsync();
    }

    public async Task<QuestionBank?> GetByIdAsync(int questionId)
        => await _db.QuestionBanks
            .Include(q => q.Course)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId);

    public async Task<IEnumerable<QuestionBank>> GetRandomByLevelAsync(int courseId, int level, int count)
    {
        if (count <= 0) return [];
        return await _db.QuestionBanks.AsNoTracking()
            .Where(q => q.CourseId == courseId && q.Level == level)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync();
    }

    public async Task SaveGeneratedAsync(IEnumerable<QuestionBank> questions, CancellationToken cancellationToken = default)
    {
        var batch = questions.ToList();
        try
        {
            await _db.QuestionBanks.AddRangeAsync(batch, cancellationToken);
            // A single SaveChanges commits the entire batch in EF Core's transaction.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var question in batch)
                _db.Entry(question).State = EntityState.Detached;
            throw;
        }
    }

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
