using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation của IExamRepository.
/// Sử dụng AivesDbContext để thao tác với bảng exams và exam_questions.
/// </summary>
public class ExamRepository : IExamRepository
{
    private readonly AivesDbContext _db;

    public ExamRepository(AivesDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<Exam> CreateAsync(Exam exam)
    {
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();
        return exam;
    }

    /// <inheritdoc/>
    public async Task<Exam?> GetByIdAsync(int examId)
    {
        return await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.CreatedByNavigation)
            .FirstOrDefaultAsync(e => e.ExamId == examId);
    }

    /// <inheritdoc/>
    public async Task<Exam?> GetByIdWithQuestionsAsync(int examId)
    {
        return await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.CreatedByNavigation)
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.ExamId == examId);
    }

    /// <inheritdoc/>
    public async Task<bool> ExamCodeExistsAsync(string examCode)
    {
        return await _db.Exams.AnyAsync(e => e.ExamCode == examCode);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Exam>> GetByTeacherIdAsync(int teacherId)
    {
        return await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions)
            .Where(e => e.CreatedBy == teacherId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task AddQuestionsAsync(IEnumerable<ExamQuestion> questions)
    {
        await _db.ExamQuestions.AddRangeAsync(questions);
    }

    /// <inheritdoc/>
    public async Task<bool> IsOwnerAsync(int examId, int teacherId)
    {
        return await _db.Exams
            .AnyAsync(e => e.ExamId == examId && e.CreatedBy == teacherId);
    }

    /// <inheritdoc/>
    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
