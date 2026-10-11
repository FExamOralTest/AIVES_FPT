using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation của IQuestionBankRepository.
/// Read-only repository dùng để lấy câu hỏi cho AI filtering và validate.
/// </summary>
public class QuestionBankRepository : IQuestionBankRepository
{
    private readonly AivesDbContext _db;

    public QuestionBankRepository(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<QuestionBank>> GetByCourseIdAsync(int courseId, int limit = 50)
    {
        return await _db.QuestionBanks
            .Where(q => q.CourseId == courseId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<int>> GetValidQuestionIdsForCourseAsync(
        int courseId, IEnumerable<int> questionIds)
    {
        var idList = questionIds.ToList();
        return await _db.QuestionBanks
            .Where(q => q.CourseId == courseId && idList.Contains(q.QuestionId))
            .Select(q => q.QuestionId)
            .ToListAsync();
    }

    public async Task<QuestionBank?> GetByIdAsync(int questionId)
    {
        return await _db.QuestionBanks
            .Include(q => q.Course)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId);
    }

    public async Task AddRangeAsync(IEnumerable<QuestionBank> questions)
    {
        await _db.QuestionBanks.AddRangeAsync(questions);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}