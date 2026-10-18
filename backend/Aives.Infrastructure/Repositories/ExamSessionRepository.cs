using Aives.Application.Exams.DTOs;
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
            .Include(s => s.Results)
                .ThenInclude(r => r.Answers)
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

    public async Task<IEnumerable<StudentSessionEnrollmentDto>> GetStudentEnrollmentsAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        return await _db.SessionEnrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.Session.StartTime)
            .ThenByDescending(e => e.SessionEnrollmentId)
            .Select(e => new StudentSessionEnrollmentDto
            {
                SessionEnrollmentId = e.SessionEnrollmentId,
                SessionId = e.SessionId,
                SessionName = e.Session.SessionName,
                ExamTitle = e.Session.Exam.Title,
                CourseName = e.Session.Exam.Course.CourseName,
                Room = e.Session.Room,
                StartTime = e.Session.StartTime,
                EndTime = e.Session.EndTime,
                EnrolledAt = e.EnrolledAt,
                SessionStatus = e.Session.Status ?? SessionStatus.NOT_STARTED,
                EnrollmentStatus = e.Status ?? SessionStatus.NOT_STARTED,
                IsPublished = e.Session.Results.Any(r =>
                    r.StudentId == studentId && r.Status == SubmissionStatus.PUBLISHED)
            })
            .ToListAsync(cancellationToken);
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

    public async Task AddEnrollmentsRangeAsync(IEnumerable<SessionEnrollment> enrollments)
    {
        _db.SessionEnrollments.AddRange(enrollments);
        // Caller sẽ SaveChangesAsync trong cùng transaction (ExamUnitOfWork).
        await _db.SaveChangesAsync();
    }

    public async Task RemoveEnrollmentAsync(SessionEnrollment enrollment)
    {
        _db.SessionEnrollments.Remove(enrollment);
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlySet<int>> GetEnrolledStudentIdsAsync(int sessionId)
    {
        var ids = await _db.SessionEnrollments
            .AsNoTracking()
            .Where(se => se.SessionId == sessionId)
            .Select(se => se.StudentId)
            .ToListAsync();
        return ids.ToHashSet();
    }

    public async Task<IReadOnlyList<User>> GetUsersByIdsAsync(IEnumerable<int> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return Array.Empty<User>();
        return await _db.Users
            .Where(u => idList.Contains(u.UserId))
            .ToListAsync();
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

    public async Task RemoveAsync(ExamSession session)
    {
        _db.ExamSessions.Remove(session);
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<ExamSession>> GetAllAsync(SessionStatus? status)
    {
        var query = _db.ExamSessions
            .Include(s => s.Exam)
            .Include(s => s.SessionEnrollments)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
    }
}
