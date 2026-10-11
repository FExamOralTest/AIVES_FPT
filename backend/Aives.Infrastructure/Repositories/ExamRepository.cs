using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation của IExamRepository.
/// Sử dụng AivesDbContext để thao tác với bảng exams và các bảng liên quan.
/// </summary>
public class ExamRepository : IExamRepository
{
    private readonly AivesDbContext _db;

    public ExamRepository(AivesDbContext db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────
    // Các method từ nhánh HEAD (Exam Management)
    // ──────────────────────────────────────────────

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

    // ──────────────────────────────────────────────
    // Các method từ nhánh Thinh_Dev (Sessions & Results)
    // ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IEnumerable<Exam>> GetByCourseIdAsync(int courseId)
        => await _db.Exams
            .AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.CourseId == courseId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Exam?> GetWithSessionsAsync(int examId)
        => await _db.Exams
            .AsNoTracking()
            .Include(e => e.Course)
            .Include(e => e.ExamSessions)
                .ThenInclude(s => s.Results)
                    .ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(e => e.ExamId == examId);

    /// <inheritdoc/>
    public async Task<ExamSession?> GetSessionWithResultsAsync(int sessionId)
        => await _db.ExamSessions
            .AsNoTracking()
            .Include(s => s.Exam)
                .ThenInclude(e => e.Course)
            .Include(s => s.Results)
                .ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

    /// <inheritdoc/>
    public async Task<Result?> GetResultDetailAsync(int resultId)
        => await _db.Results
            .AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(r => r.ResultId == resultId);
}