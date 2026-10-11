using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation của IExamSessionRepository.
/// Thao tác bảng exam_sessions và eager-load các quan hệ cần thiết.
/// </summary>
public class ExamSessionRepository : IExamSessionRepository
{
    private readonly AivesDbContext _db;

    public ExamSessionRepository(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<ExamSession> CreateAsync(ExamSession session)
    {
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    public async Task<ExamSession?> GetByIdAsync(int sessionId)
    {
        return await _db.ExamSessions
            .Include(s => s.Exam)
                .ThenInclude(e => e.Course)
            .Include(s => s.ScheduledByNavigation)
            .Include(s => s.SessionEnrollments)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task<ExamSession?> GetByIdWithEnrollmentsAsync(int sessionId)
    {
        return await _db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.Student)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task<ExamSession?> GetBySessionCodeAsync(string sessionCode)
    {
        return await _db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionEnrollments)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
    }

    public async Task<IEnumerable<ExamSession>> GetByTeacherIdAsync(int teacherId)
    {
        return await _db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionEnrollments)
            .Where(s => s.ScheduledBy == teacherId)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
    }

    public async Task UpdateStatusAsync(int sessionId, SessionStatus status)
    {
        var session = await _db.ExamSessions
            .FirstOrDefaultAsync(s => s.SessionId == sessionId)
            ?? throw new KeyNotFoundException($"ExamSession with ID {sessionId} not found.");

        session.Status = status;
    }

    public async Task<SessionEnrollment?> GetEnrollmentAsync(int sessionId, int studentId)
    {
        return await _db.SessionEnrollments
            .FirstOrDefaultAsync(se => se.SessionId == sessionId && se.StudentId == studentId);
    }

    public async Task AddEnrollmentAsync(SessionEnrollment enrollment)
    {
        _db.SessionEnrollments.Add(enrollment);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(ExamSession session)
    {
        _db.ExamSessions.Update(session);
    }

    public async Task<bool> SessionCodeExistsAsync(string sessionCode)
    {
        return await _db.ExamSessions.AnyAsync(s => s.SessionCode == sessionCode);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}