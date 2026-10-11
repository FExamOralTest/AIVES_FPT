using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class ResultRepository : IResultRepository
{
    private readonly AivesDbContext _db;

    public ResultRepository(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<Result?> GetByIdAsync(int resultId)
    {
        return await _db.Results
            .Include(r => r.Session)
                .ThenInclude(s => s.Exam)
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .FirstOrDefaultAsync(r => r.ResultId == resultId);
    }

    public async Task<Result?> GetByIdWithDetailsAsync(int resultId)
    {
        return await _db.Results
            .Include(r => r.Session)
                .ThenInclude(s => s.Exam)
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(r => r.ResultId == resultId);
    }

    public async Task<IEnumerable<Result>> GetBySessionIdWithDetailsAsync(int sessionId)
    {
        return await _db.Results
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();
    }

    public async Task UpdateAsync(Result result)
    {
        _db.Results.Update(result);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}