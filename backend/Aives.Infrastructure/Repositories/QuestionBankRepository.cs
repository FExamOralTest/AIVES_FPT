using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation của IQuestionBankRepository.
/// Dùng để quản lý, truy vấn và ngẫu nhiên hóa câu hỏi từ QuestionBank.
/// </summary>
public class QuestionBankRepository : IQuestionBankRepository
{
    private readonly AivesDbContext _db;

    public QuestionBankRepository(AivesDbContext db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────
    // Các method từ nhánh HEAD
    // ──────────────────────────────────────────────

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

    // ──────────────────────────────────────────────
    // Các method từ nhánh Thinh_Dev
    // ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<QuestionBank>> GetRandomByLevelAsync(
        int courseId, int level, int count)
    {
        if (count <= 0) return [];

        // EF.Functions.Random() → random() tại PostgreSQL, không load toàn bộ bảng
        return await _db.QuestionBanks
            .AsNoTracking()
            .Where(q => q.CourseId == courseId && q.Level == level)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync();
    }
}