using System.Security.Cryptography;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

/// <summary>
/// Triển khai nghiệp vụ quản lý ExamSession (Ca thi / Phòng thi).
/// Tự động sinh SessionCode bảo mật và kiểm tra phân quyền Teacher.
/// </summary>
public class ExamSessionService : IExamSessionService
{
    private readonly IExamSessionRepository _sessionRepository;
    private readonly IExamRepository _examRepository;

    public ExamSessionService(
        IExamSessionRepository sessionRepository,
        IExamRepository examRepository)
    {
        _sessionRepository = sessionRepository;
        _examRepository = examRepository;
    }

    /// <inheritdoc/>
    public async Task<ExamSessionDto> CreateSessionAsync(
        CreateExamSessionRequest request, int teacherId)
    {
        // ── Business Rule Validations ─────────────────────────────────────────

        var exam = await _examRepository.GetByIdAsync(request.ExamId)
            ?? throw new KeyNotFoundException($"Exam with ID {request.ExamId} not found.");

        if (exam.CreatedBy != teacherId)
            throw new UnauthorizedAccessException(
                "You do not have permission to create a session for this exam.");

        if (request.StartTime >= request.EndTime)
            throw new InvalidOperationException(
                "StartTime must be earlier than EndTime.");

        if (request.StartTime < DateTime.UtcNow.AddMinutes(-5))
            throw new InvalidOperationException(
                "StartTime cannot be in the past.");

        // ── Generate SessionCode (Enroll Code) ─────────────────────────────────
        var sessionCode = await GenerateUniqueSessionCodeAsync();

        var session = new ExamSession
        {
            ExamId = request.ExamId,
            ScheduledBy = teacherId,
            SessionName = request.SessionName,
            Room = request.Room,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = SessionStatus.NOT_STARTED,
            CreatedAt = DateTime.UtcNow,
            SessionCode = sessionCode,
            MaxCapacity = request.MaxCapacity ?? 50,
            CurrentEnrollment = 0
        };

        var created = await _sessionRepository.CreateAsync(session);
        await _sessionRepository.SaveChangesAsync();

        var fullSession = await _sessionRepository.GetByIdAsync(created.SessionId)
            ?? throw new InvalidOperationException("Session was created but could not be retrieved.");

        return MapToSessionDto(fullSession, exam);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ExamSessionSummaryDto>> GetSessionsByTeacherAsync(int teacherId)
    {
        var sessions = await _sessionRepository.GetByTeacherIdAsync(teacherId);
        return sessions.Select(MapToSessionSummaryDto);
    }

    /// <inheritdoc/>
    public async Task<ExamSessionDto> GetSessionByIdAsync(int sessionId, int teacherId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"ExamSession with ID {sessionId} not found.");

        if (session.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException(
                "You do not have permission to view this session.");

        return MapToSessionDto(session, session.Exam);
    }

    /// <inheritdoc/>
    public async Task<ExamSessionDto> UpdateSessionStatusAsync(
        int sessionId, UpdateSessionStatusRequest request, int teacherId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"ExamSession with ID {sessionId} not found.");

        if (session.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException(
                "You do not have permission to update this session.");

        ValidateStatusTransition(session.Status, request.Status);

        await _sessionRepository.UpdateStatusAsync(sessionId, request.Status);
        await _sessionRepository.SaveChangesAsync();

        var updated = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new InvalidOperationException("Session could not be retrieved after update.");

        return MapToSessionDto(updated, updated.Exam);
    }

    // === P0: Student Enroll ===
    public async Task<EnrollSessionResponse> EnrollStudentAsync(EnrollSessionRequest request, int studentId)
    {
        var session = await _sessionRepository.GetBySessionCodeAsync(request.SessionCode)
            ?? throw new KeyNotFoundException("Invalid or expired Enroll Code.");

        if (session.Status != SessionStatus.NOT_STARTED && session.Status != SessionStatus.IN_PROGRESS)
            throw new InvalidOperationException("This session is not open for enrollment.");

        if (session.CurrentEnrollment >= session.MaxCapacity)
            throw new InvalidOperationException("Session has reached maximum capacity.");

        var existing = await _sessionRepository.GetEnrollmentAsync(session.SessionId, studentId);
        if (existing != null)
            throw new InvalidOperationException("You are already enrolled in this session.");

        var enrollment = new SessionEnrollment
        {
            SessionId = session.SessionId,
            StudentId = studentId,
            Status = SessionStatus.NOT_STARTED,
            EnrolledAt = DateTime.UtcNow
        };

        await _sessionRepository.AddEnrollmentAsync(enrollment);

        session.CurrentEnrollment++;
        await _sessionRepository.UpdateAsync(session);
        await _sessionRepository.SaveChangesAsync();

        var exam = await _examRepository.GetByIdAsync(session.ExamId);

        return new EnrollSessionResponse
        {
            SessionEnrollmentId = enrollment.SessionEnrollmentId,
            SessionId = session.SessionId,
            SessionName = session.SessionName,
            ExamTitle = exam?.Title ?? "",
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Status = session.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString()
        };
    }

    // === P0: Teacher gets enrollments ===
    public async Task<IEnumerable<SessionEnrollmentDto>> GetSessionEnrollmentsAsync(int sessionId, int teacherId)
    {
        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        if (session.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException("Not your session.");

        return session.SessionEnrollments.Select(e => new SessionEnrollmentDto
        {
            SessionEnrollmentId = e.SessionEnrollmentId,
            SessionId = e.SessionId,
            StudentId = e.StudentId,
            StudentName = e.Student?.FullName ?? "",
            StudentEmail = e.Student?.Email ?? "",
            Status = e.Status?.ToString() ?? "",
            EnrolledAt = e.EnrolledAt,
            StartedAt = e.StartedAt,
            CurrentQuestionIndex = e.CurrentQuestionIndex,
            TotalQuestions = e.Session?.Exam?.MaxQuestions ?? 0
        }).ToList();
    }

    // === P2: Live Monitor ===
    public async Task<SessionMonitorDto> GetSessionMonitorAsync(int sessionId, int teacherId)
    {
        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        if (session.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException("Not your session.");

        var exam = await _examRepository.GetByIdAsync(session.ExamId);

        return new SessionMonitorDto
        {
            SessionId = session.SessionId,
            SessionName = session.SessionName,
            ExamTitle = exam?.Title ?? "",
            ExamCode = exam?.ExamCode ?? "",
            SessionCode = session.SessionCode,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Status = session.Status?.ToString() ?? "",
            MaxCapacity = session.MaxCapacity,
            CurrentEnrollment = session.CurrentEnrollment,
            Students = session.SessionEnrollments.Select(e => new StudentProgressDto
            {
                SessionEnrollmentId = e.SessionEnrollmentId,
                StudentId = e.StudentId,
                StudentName = e.Student?.FullName ?? "",
                StudentEmail = e.Student?.Email ?? "",
                EnrollmentStatus = e.Status?.ToString() ?? "",
                EnrolledAt = e.EnrolledAt,
                StartedAt = e.StartedAt,
                LastActivityAt = e.LastActivityAt,
                CurrentQuestionIndex = e.CurrentQuestionIndex ?? 0,
                TotalQuestions = session.Exam?.MaxQuestions ?? 0,
                AnsweredQuestions = 0 // TODO: calculate from answers
            }).ToList()
        };
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private async Task<string> GenerateUniqueSessionCodeAsync()
    {
        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateSessionCode();
            if (!await _sessionRepository.SessionCodeExistsAsync(code))
                return code;
        }
        throw new InvalidOperationException(
            "Unable to generate a unique Session Code after multiple attempts.");
    }

    private static string GenerateSessionCode()
    {
        var randomBytes = new byte[4];
        RandomNumberGenerator.Fill(randomBytes);
        return "ESS-" + Convert.ToHexString(randomBytes);
    }

    private static void ValidateStatusTransition(SessionStatus? current, SessionStatus target)
    {
        var allowed = current switch
        {
            SessionStatus.NOT_STARTED => new[] { SessionStatus.IN_PROGRESS, SessionStatus.CANCELLED },
            SessionStatus.IN_PROGRESS => new[] { SessionStatus.COMPLETED, SessionStatus.CANCELLED },
            SessionStatus.COMPLETED   => Array.Empty<SessionStatus>(),
            SessionStatus.CANCELLED   => Array.Empty<SessionStatus>(),
            null                      => new[] { SessionStatus.NOT_STARTED, SessionStatus.IN_PROGRESS, SessionStatus.CANCELLED },
            _ => Array.Empty<SessionStatus>()
        };

        if (!allowed.Contains(target))
        {
            throw new InvalidOperationException(
                $"Cannot transition from '{current}' to '{target}'. " +
                $"Allowed transitions: [{string.Join(", ", allowed)}].");
        }
    }

    // ─── Mappers ──────────────────────────────────────────────────────────────

    private static ExamSessionDto MapToSessionDto(ExamSession session, Domain.Entities.Exam? exam)
    {
        return new ExamSessionDto
        {
            SessionId = session.SessionId,
            ExamId = session.ExamId,
            ExamTitle = exam?.Title ?? session.Exam?.Title ?? string.Empty,
            ExamCode = exam?.ExamCode ?? session.Exam?.ExamCode ?? string.Empty,
            SessionName = session.SessionName,
            Room = session.Room,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            DurationMinutes = (int)(session.EndTime - session.StartTime).TotalMinutes,
            Status = session.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),
            ScheduledBy = session.ScheduledBy,
            ScheduledByName = session.ScheduledByNavigation?.FullName,
            CreatedAt = session.CreatedAt,
            EnrolledStudents = session.SessionEnrollments?.Count ?? 0,
            SessionCode = session.SessionCode,
            MaxCapacity = session.MaxCapacity
        };
    }

    private static ExamSessionSummaryDto MapToSessionSummaryDto(ExamSession session) => new()
    {
        SessionId = session.SessionId,
        SessionName = session.SessionName,
        ExamTitle = session.Exam?.Title ?? string.Empty,
        ExamCode = session.Exam?.ExamCode ?? string.Empty,
        Room = session.Room,
        StartTime = session.StartTime,
        EndTime = session.EndTime,
        Status = session.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),
        EnrolledStudents = session.SessionEnrollments?.Count ?? 0
    };
}