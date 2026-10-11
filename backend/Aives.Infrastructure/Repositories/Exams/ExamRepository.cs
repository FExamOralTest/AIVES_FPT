using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories.Exams;

public class ExamRepository : IExamRepository
{
    private readonly AivesDbContext _db;

    public ExamRepository(AivesDbContext db) => _db = db;

    public async Task<Exam> CreateAsync(Exam exam)
    {
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();
        return exam;
    }

    public async Task<Exam?> GetByIdAsync(int examId)
        => await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.CreatedByNavigation)
            .FirstOrDefaultAsync(e => e.ExamId == examId);

    public async Task<Exam?> GetByIdWithQuestionsAsync(int examId)
        => await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.CreatedByNavigation)
            .Include(e => e.ExamQuestions).ThenInclude(eq => eq.Question)
            .FirstOrDefaultAsync(e => e.ExamId == examId);

    public async Task<bool> ExamCodeExistsAsync(string examCode)
        => await _db.Exams.AnyAsync(e => e.ExamCode == examCode);

    public async Task<IEnumerable<Exam>> GetByTeacherIdAsync(int teacherId)
        => await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions)
            .Where(e => e.CreatedBy == teacherId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

    public async Task<IEnumerable<Exam>> GetByCourseIdAsync(int courseId)
        => await _db.Exams.AsNoTracking()
            .Include(e => e.Course)
            .Where(e => e.CourseId == courseId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

    public async Task AddQuestionsAsync(IEnumerable<ExamQuestion> questions)
        => await _db.ExamQuestions.AddRangeAsync(questions);

    public async Task<bool> IsOwnerAsync(int examId, int teacherId)
        => await _db.Exams.AnyAsync(e => e.ExamId == examId && e.CreatedBy == teacherId);

    public async Task<Exam?> GetWithSessionsAsync(int examId)
        => await _db.Exams.AsNoTracking()
            .Include(e => e.Course)
            .Include(e => e.ExamSessions)
                .ThenInclude(s => s.Results).ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(e => e.ExamId == examId);

    public async Task<ExamSession?> GetSessionWithResultsAsync(int sessionId)
        => await _db.ExamSessions.AsNoTracking()
            .Include(s => s.Exam).ThenInclude(e => e.Course)
            .Include(s => s.Results).ThenInclude(r => r.Student)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

    public async Task<Result?> GetResultDetailAsync(int resultId)
        => await _db.Results.AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers).ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(r => r.ResultId == resultId);

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
