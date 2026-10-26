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
            .Include(s => s.Course)
            .Include(s => s.Period)
            .Include(s => s.Exam)
                .ThenInclude(e => e.Course)
            .Include(s => s.SessionExams)
                .ThenInclude(se => se.Exam)
            .Include(s => s.Slots)
            .Include(s => s.ScheduledByNavigation)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.AssignedExam)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.AssignedSlot)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task<ExamSession?> GetByIdWithEnrollmentsAsync(int sessionId)
    {
        return await _db.ExamSessions
            .Include(s => s.Course)
            .Include(s => s.Period)
            .Include(s => s.Exam)
                .ThenInclude(e => e.Course)
            .Include(s => s.SessionExams)
                .ThenInclude(se => se.Exam)
            .Include(s => s.Slots)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.Student)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.AssignedExam)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.AssignedSlot)
            .Include(s => s.Results)
                .ThenInclude(r => r.Answers)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);
    }

    public async Task<ExamSession?> GetBySessionCodeAsync(string sessionCode)
    {
        var normalizedCode = NormalizeSessionCode(sessionCode);
        if (normalizedCode is null)
            return null;

        return await _db.ExamSessions
            .Include(s => s.Course)
            .Include(s => s.Period)
            .Include(s => s.Exam)
            .Include(s => s.SessionExams)
                .ThenInclude(se => se.Exam)
            .Include(s => s.Slots)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.AssignedExam)
            .Include(s => s.SessionEnrollments)
                .ThenInclude(se => se.AssignedSlot)
            .FirstOrDefaultAsync(s => s.SessionCode == normalizedCode);
    }

    public async Task<(ExamSession? Session, ExamSessionSlot? Slot)> GetBySessionOrSlotCodeAsync(string code)
    {
        var normalized = NormalizeSessionCode(code);
        if (normalized is null)
            return (null, null);

        var slot = await _db.ExamSessionSlots
            .Include(sl => sl.Session)
                .ThenInclude(s => s.Course)
            .Include(sl => sl.Session)
                .ThenInclude(s => s.Period)
            .Include(sl => sl.Session)
                .ThenInclude(s => s.Exam)
            .Include(sl => sl.Session)
                .ThenInclude(s => s.SessionExams)
                    .ThenInclude(se => se.Exam)
            .Include(sl => sl.Session)
                .ThenInclude(s => s.Slots)
            .Include(sl => sl.Session)
                .ThenInclude(s => s.SessionEnrollments)
                    .ThenInclude(se => se.AssignedExam)
            .Include(sl => sl.Session)
                .ThenInclude(s => s.SessionEnrollments)
                    .ThenInclude(se => se.AssignedSlot)
            .FirstOrDefaultAsync(sl => sl.SlotCode == normalized);

        if (slot != null)
        {
            return (slot.Session, slot);
        }

        var session = await GetBySessionCodeAsync(normalized);
        return (session, null);
    }

    public async Task<IEnumerable<ExamSession>> GetByTeacherIdAsync(int teacherId)
    {
        return await _db.ExamSessions
            .Include(s => s.Course)
            .Include(s => s.Period)
            .Include(s => s.Exam)
            .Include(s => s.SessionExams)
                .ThenInclude(se => se.Exam)
            .Include(s => s.Slots)
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
                ExamTitle = e.AssignedExam != null ? e.AssignedExam.Title : (e.Session.Exam != null ? e.Session.Exam.Title : ""),
                ExamCode = e.AssignedExam != null ? e.AssignedExam.ExamCode : (e.Session.Exam != null ? e.Session.Exam.ExamCode : ""),
                CourseName = e.Session.Course != null ? e.Session.Course.CourseName : (e.Session.Exam != null ? e.Session.Exam.Course.CourseName : ""),
                Room = e.Session.Room,
                StartTime = e.Session.StartTime,
                EndTime = e.Session.EndTime,
                EnrolledAt = e.EnrolledAt,
                SessionStatus = e.Session.Status ?? SessionStatus.NOT_STARTED,
                EnrollmentStatus = e.Status ?? SessionStatus.NOT_STARTED,
                IsPublished = e.Session.Results.Any(r =>
                    r.StudentId == studentId && r.Status == SubmissionStatus.PUBLISHED),
                AssignedSlotId = e.AssignedSlotId,
                AssignedSlotName = e.AssignedSlot != null ? e.AssignedSlot.SlotName : null,
                SlotStartTime = e.AssignedSlot != null ? e.AssignedSlot.StartTime : null,
                SlotEndTime = e.AssignedSlot != null ? e.AssignedSlot.EndTime : null
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

    public async Task<IReadOnlyList<User>> GetUsersByIdentifiersAsync(IEnumerable<string> identifiers)
    {
        var rawList = identifiers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rawList.Count == 0) return Array.Empty<User>();

        var numericIds = new List<int>();
        var lowerTokens = new List<string>();

        foreach (var token in rawList)
        {
            if (int.TryParse(token, out var id) && id > 0)
            {
                numericIds.Add(id);
            }
            lowerTokens.Add(token.ToLower());
        }

        return await _db.Users
            .Where(u =>
                (numericIds.Count > 0 && numericIds.Contains(u.UserId)) ||
                (u.Username != null && lowerTokens.Contains(u.Username.ToLower())) ||
                (u.Email != null && lowerTokens.Contains(u.Email.ToLower()))
            )
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetEnrolledStudentIdsByCourseIdAsync(int courseId)
    {
        return await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync();
    }

    public async Task UpdateAsync(ExamSession session)
    {
        _db.ExamSessions.Update(session);
    }

    public async Task<bool> SessionCodeExistsAsync(string sessionCode)
    {
        var normalizedCode = NormalizeSessionCode(sessionCode);

        return normalizedCode is not null
            && await _db.ExamSessions
                .AnyAsync(s => s.SessionCode == normalizedCode);
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
            .Include(s => s.Course)
            .Include(s => s.Period)
            .Include(s => s.Exam)
                .ThenInclude(e => e.Course)
            .Include(s => s.SessionExams)
                .ThenInclude(se => se.Exam)
            .Include(s => s.Slots)
            .Include(s => s.SessionEnrollments)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        return await query
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();
    }

    public async Task SetSessionExamsAsync(int sessionId, IEnumerable<int> examIds)
    {
        var existing = await _db.ExamSessionExams.Where(se => se.SessionId == sessionId).ToListAsync();
        _db.ExamSessionExams.RemoveRange(existing);

        var distinctIds = examIds.Distinct();
        foreach (var examId in distinctIds)
        {
            _db.ExamSessionExams.Add(new ExamSessionExam
            {
                SessionId = sessionId,
                ExamId = examId,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<List<Exam>> GetExamsBySessionIdAsync(int sessionId)
    {
        return await _db.ExamSessionExams
            .Where(se => se.SessionId == sessionId)
            .Include(se => se.Exam)
                .ThenInclude(e => e.Course)
            .Select(se => se.Exam)
            .ToListAsync();
    }

    public async Task UpdateEnrollmentAssignedExamAsync(int sessionId, int studentId, int examId)
    {
        var enrollment = await _db.SessionEnrollments
            .FirstOrDefaultAsync(se => se.SessionId == sessionId && se.StudentId == studentId);
        if (enrollment != null)
        {
            enrollment.AssignedExamId = examId;
            await _db.SaveChangesAsync();
        }
    }

    public async Task BulkUpdateEnrollmentsAsync(IEnumerable<SessionEnrollment> enrollments)
    {
        _db.SessionEnrollments.UpdateRange(enrollments);
        await _db.SaveChangesAsync();
    }

    public async Task SetSessionSlotsAsync(int sessionId, IEnumerable<ExamSessionSlot> slots)
    {
        var existing = await _db.ExamSessionSlots.Where(s => s.SessionId == sessionId).ToListAsync();
        _db.ExamSessionSlots.RemoveRange(existing);

        foreach (var slot in slots)
        {
            slot.SessionId = sessionId;
            _db.ExamSessionSlots.Add(slot);
        }
        await _db.SaveChangesAsync();
    }

    public async Task UpdateEnrollmentAssignedSlotAsync(int sessionId, int studentId, int? slotId)
    {
        var enrollment = await _db.SessionEnrollments
            .FirstOrDefaultAsync(se => se.SessionId == sessionId && se.StudentId == studentId);
        if (enrollment != null)
        {
            enrollment.AssignedSlotId = slotId;
            await _db.SaveChangesAsync();
        }
    }

    private static string? NormalizeSessionCode(string? sessionCode)
    {
        var value = sessionCode?.Trim();

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.ToUpperInvariant();
    }
}
