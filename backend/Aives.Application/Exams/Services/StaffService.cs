using System.Security.Cryptography;
using Aives.Application.DTOs;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

/// <summary>
/// Nghiệp vụ Staff: quản lý phòng thi, lịch thu xếp ca, xếp lớp / phân công giảng viên,
/// quản lý enrollment và theo dõi tiến độ ca thi.
/// Admin vẫn dùng được (bypass ownership check).
/// TODO: khi có bảng `rooms` riêng → chuyển ExamSession.Room (string) thành FK int.
/// </summary>
public class StaffService : IStaffService
{
    private readonly IExamSessionRepository _sessionRepository;
    private readonly IExamRepository _examRepository;
    private readonly IExamUnitOfWork _unitOfWork;
    private readonly ICourseService _courseService;
    private readonly IUserRepository _userRepository;

    public StaffService(
        IExamSessionRepository sessionRepository,
        IExamRepository examRepository,
        IExamUnitOfWork unitOfWork,
        ICourseService courseService,
        IUserRepository userRepository)
    {
        _sessionRepository = sessionRepository;
        _examRepository = examRepository;
        _unitOfWork = unitOfWork;
        _courseService = courseService;
        _userRepository = userRepository;
    }

    // ═══════════════════════════════════════════════════════════════
    //  SESSIONS / ROOMS
    // ═══════════════════════════════════════════════════════════════

    public async Task<IEnumerable<ExamSessionSummaryDto>> GetAllSessionsAsync(StaffSessionQuery query, int staffId)
    {
        var sessions = await _sessionRepository.GetAllAsync(query?.Status);
        var actor = await _userRepository.GetByIdAsync(staffId)
            ?? throw new UnauthorizedAccessException("Staff account was not found.");

        if (actor.Role != UserRole.ADMIN)
            sessions = sessions.Where(s => s.Exam?.Course?.TeacherId == staffId);

        return sessions.Select(MapToSummary);
    }

    public async Task<ExamSessionDto> GetSessionByIdAsync(int sessionId, int staffId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"ExamSession {sessionId} not found.");
        await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);
        return MapToFull(session, session.Exam);
    }

    public Task<ExamSessionDto> CreateSessionAsync(
        StaffCreateSessionRequest request, int staffId)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(request.SessionName))
                throw new ArgumentException("SessionName is required.");
            if (request.StartTime >= request.EndTime)
                throw new InvalidOperationException("StartTime must be earlier than EndTime.");
            if (request.StartTime < DateTime.UtcNow.AddMinutes(-5))
                throw new InvalidOperationException("StartTime cannot be in the past.");
            if (request.MaxCapacity < 1)
                throw new ArgumentException("MaxCapacity must be at least 1.");

            var exam = await _examRepository.GetByIdWithQuestionsAsync(request.ExamId)
                ?? throw new KeyNotFoundException($"Exam {request.ExamId} not found.");

            await EnsureStaffCanManageCourseAsync(exam.CourseId, staffId, exam);

            if (exam.ExamQuestions is null || exam.ExamQuestions.Count == 0)
                throw new InvalidOperationException("Add questions to the exam before creating a session.");

            var sessionCode = await GenerateUniqueSessionCodeAsync();

            var session = new ExamSession
            {
                ExamId = request.ExamId,
                ScheduledBy = staffId,
                SessionName = request.SessionName.Trim(),
                Room = request.Room?.Trim(),
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                MaxCapacity = request.MaxCapacity,
                CurrentEnrollment = 0,
                Status = SessionStatus.NOT_STARTED,
                SessionCode = sessionCode,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _sessionRepository.CreateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            var full = await _sessionRepository.GetByIdAsync(created.SessionId)
                ?? throw new InvalidOperationException("Session was created but could not be retrieved.");
            return MapToFull(full, exam);
        });

    public Task<ExamSessionDto> UpdateSessionAsync(
        int sessionId, StaffUpdateSessionRequest request, int staffId)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId)
                ?? throw new KeyNotFoundException($"ExamSession {sessionId} not found.");

            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);

            if (request.SessionName is not null)
            {
                if (string.IsNullOrWhiteSpace(request.SessionName))
                    throw new ArgumentException("SessionName cannot be empty.");
                session.SessionName = request.SessionName.Trim();
            }
            if (request.Room is not null)
                session.Room = string.IsNullOrWhiteSpace(request.Room) ? null : request.Room.Trim();

            var newStart = request.StartTime ?? session.StartTime;
            var newEnd   = request.EndTime   ?? session.EndTime;
            if (newStart >= newEnd)
                throw new InvalidOperationException("StartTime must be earlier than EndTime.");
            session.StartTime = newStart;
            session.EndTime   = newEnd;

            if (request.MaxCapacity.HasValue)
            {
                if (request.MaxCapacity.Value < session.CurrentEnrollment)
                    throw new InvalidOperationException(
                        $"MaxCapacity cannot be lower than current enrolled students ({session.CurrentEnrollment}).");
                session.MaxCapacity = request.MaxCapacity.Value;
            }

            if (request.Status.HasValue && request.Status.Value != session.Status)
                ValidateStatusTransition(session.Status, request.Status.Value);

            await _sessionRepository.UpdateAsync(session);
            if (request.Status.HasValue)
                await _sessionRepository.UpdateStatusAsync(sessionId, request.Status.Value);
            await _sessionRepository.SaveChangesAsync();

            var updated = await _sessionRepository.GetByIdAsync(sessionId)
                ?? throw new InvalidOperationException("Session could not be retrieved after update.");
            return MapToFull(updated, updated.Exam);
        });

    public async Task DeleteSessionAsync(int sessionId, int staffId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"ExamSession {sessionId} not found.");

            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);

        if (session.Status == SessionStatus.IN_PROGRESS)
            throw new InvalidOperationException("Cannot delete a session that is in progress.");
        if (session.Status == SessionStatus.COMPLETED)
            throw new InvalidOperationException("Cannot delete a completed session.");
        if (session.SessionEnrollments is { Count: > 0 })
            throw new InvalidOperationException(
                "Cannot delete a session that has enrolled students. Remove enrollments first.");

        await _sessionRepository.RemoveAsync(session);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CLASS / TEACHER ASSIGNMENT
    // ═══════════════════════════════════════════════════════════════

    public async Task<CourseDto> AssignTeacherToClassAsync(
        int courseId, StaffAssignTeacherRequest request, int staffId)
    {
        var teacher = await _userRepository.GetByIdAsync(request.TeacherId)
            ?? throw new KeyNotFoundException($"User {request.TeacherId} not found.");
        if (teacher.Role != UserRole.STAFF)
            throw new InvalidOperationException(
                $"User {request.TeacherId} is not academic staff (role={teacher.Role}).");

        return await _courseService.AssignTeacherAsync(courseId, new AssignTeacherRequest(request.TeacherId));
    }

    // ═══════════════════════════════════════════════════════════════
    //  ENROLLMENTS / MONITOR
    // ═══════════════════════════════════════════════════════════════

    public Task<StaffBulkEnrollmentResult> AddStudentsToSessionAsync(
        int sessionId, StaffAddEnrollmentRequest request, int staffId)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);

            if (session.Status == SessionStatus.COMPLETED || session.Status == SessionStatus.CANCELLED)
                throw new InvalidOperationException("Session is no longer open for enrollment.");

            // Chuẩn hoá + de-dup input ngay từ client side.
            var requestedIds = (request.StudentIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (requestedIds.Count == 0)
                throw new ArgumentException("StudentIds is empty.");
            if (requestedIds.Count > 500)
                throw new ArgumentException("Cannot enroll more than 500 students in a single request.");

            // Lấy user có role STUDENT trong 1 query (tránh N+1).
            var students = (await _userRepository.GetByRoleAsync(UserRole.STUDENT))
                .Where(u => requestedIds.Contains(u.UserId))
                .ToDictionary(u => u.UserId);

            // Lấy enrollment hiện có trong 1 query.
            var existing = await _sessionRepository.GetEnrolledStudentIdsAsync(sessionId);

            var result = new StaffBulkEnrollmentResult();
            var now = DateTime.UtcNow;
            var newEnrollments = new List<SessionEnrollment>();

            foreach (var id in requestedIds)
            {
                if (!students.TryGetValue(id, out var student))
                {
                    result.NotFound.Add(id);
                    continue;
                }
                if (existing.Contains(id))
                {
                    result.AlreadyEnrolled.Add(id);
                    continue;
                }

                newEnrollments.Add(new SessionEnrollment
                {
                    SessionId = sessionId,
                    StudentId = id,
                    Status = SessionStatus.NOT_STARTED,
                    EnrolledAt = now
                });

                result.Added.Add(new SessionEnrollmentDto
                {
                    SessionId = sessionId,
                    StudentId = student.UserId,
                    StudentName = student.FullName,
                    StudentEmail = student.Email ?? string.Empty,
                    Status = SessionStatus.NOT_STARTED.ToString(),
                    EnrolledAt = now,
                    CurrentQuestionIndex = 0,
                    TotalQuestions = session.Exam?.MaxQuestions ?? 0
                });
            }

            // Kiểm tra capacity SAU khi loại bỏ trùng / not-found.
            if (session.CurrentEnrollment + newEnrollments.Count > session.MaxCapacity)
                throw new InvalidOperationException(
                    $"Adding {newEnrollments.Count} students would exceed MaxCapacity " +
                    $"({session.CurrentEnrollment}/{session.MaxCapacity}).");

            if (newEnrollments.Count == 0)
                return result; // tất cả đều trùng hoặc không tồn tại

            await _sessionRepository.AddEnrollmentsAsync(newEnrollments);
            session.CurrentEnrollment += newEnrollments.Count;
            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return result;
        });

    public Task<StaffBulkRemoveResult> RemoveStudentsFromSessionAsync(
        int sessionId, StaffRemoveEnrollmentRequest request, int staffId)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);

            if (session.Status == SessionStatus.IN_PROGRESS)
                throw new InvalidOperationException(
                    "Cannot remove students from a session that is in progress.");
            if (session.Status == SessionStatus.COMPLETED)
                throw new InvalidOperationException(
                    "Cannot remove students from a completed session.");

            var requestedIds = (request.StudentIds ?? new List<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (requestedIds.Count == 0)
                throw new ArgumentException("StudentIds is empty.");
            if (requestedIds.Count > 500)
                throw new ArgumentException("Cannot remove more than 500 students in a single request.");

            // Lấy enrollment tương ứng trong 1 query.
            var enrollments = await _sessionRepository.GetEnrollmentsByStudentIdsAsync(sessionId, requestedIds);
            var foundIds = enrollments.Select(e => e.StudentId).ToHashSet();

            var result = new StaffBulkRemoveResult
            {
                Removed = enrollments.Select(e => e.StudentId).ToList(),
                NotEnrolled = requestedIds.Where(id => !foundIds.Contains(id)).ToList()
            };

            if (enrollments.Count == 0)
                return result; // không có gì để xoá

            await _sessionRepository.RemoveEnrollmentsAsync(enrollments);
            session.CurrentEnrollment = Math.Max(0, session.CurrentEnrollment - enrollments.Count);
            await _sessionRepository.UpdateAsync(session);
            await _sessionRepository.SaveChangesAsync();

            return result;
        });

    public async Task<SessionMonitorDto> GetSessionMonitorAsync(int sessionId, int staffId)
    {
        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);

        var exam = await _examRepository.GetByIdAsync(session.ExamId);

        return new SessionMonitorDto
        {
            SessionId = session.SessionId,
            SessionName = session.SessionName,
            ExamTitle = exam?.Title ?? string.Empty,
            ExamCode = exam?.ExamCode ?? string.Empty,
            SessionCode = session.SessionCode,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Status = session.Status?.ToString() ?? string.Empty,
            MaxCapacity = session.MaxCapacity,
            CurrentEnrollment = session.CurrentEnrollment,
            Students = session.SessionEnrollments.Select(e =>
            {
                var studentResult = session.Results.FirstOrDefault(r => r.StudentId == e.StudentId);
                var answeredCount = studentResult?.Answers?.Count ?? 0;
                return new StudentProgressDto
                {
                    SessionEnrollmentId = e.SessionEnrollmentId,
                    StudentId = e.StudentId,
                    StudentName = e.Student?.FullName ?? string.Empty,
                    StudentEmail = e.Student?.Email ?? string.Empty,
                    EnrollmentStatus = e.Status?.ToString() ?? string.Empty,
                    EnrolledAt = e.EnrolledAt,
                    StartedAt = e.StartedAt,
                    LastActivityAt = e.LastActivityAt,
                    CurrentQuestionIndex = e.CurrentQuestionIndex ?? 0,
                    TotalQuestions = session.Exam?.MaxQuestions ?? 0,
                    AnsweredQuestions = answeredCount
                };
            }).ToList()
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

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
            throw new InvalidOperationException(
                $"Cannot transition from '{current}' to '{target}'. " +
                $"Allowed transitions: [{string.Join(", ", allowed)}].");
    }

    private async Task EnsureStaffCanManageCourseAsync(
        int examId, int staffId, Exam? examAlreadyLoaded = null)
    {
        var exam = examAlreadyLoaded
            ?? await _examRepository.GetByIdAsync(examId)
            ?? throw new KeyNotFoundException($"Exam {examId} not found.");

        var actor = await _userRepository.GetByIdAsync(staffId)
            ?? throw new UnauthorizedAccessException("Staff account was not found.");

        if (actor.Role == UserRole.ADMIN)
            return;

        if (actor.Role != UserRole.STAFF || exam.Course?.TeacherId != staffId)
            throw new UnauthorizedAccessException(
                "You are not assigned to the course of this exam.");
    }

    private static ExamSessionDto MapToFull(ExamSession s, Exam? exam) => new()
    {
        SessionId = s.SessionId,
        ExamId = s.ExamId,
        ExamTitle = exam?.Title ?? s.Exam?.Title ?? string.Empty,
        ExamCode = exam?.ExamCode ?? s.Exam?.ExamCode ?? string.Empty,
        SessionName = s.SessionName,
        Room = s.Room,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        DurationMinutes = (int)(s.EndTime - s.StartTime).TotalMinutes,
        Status = s.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),
        ScheduledBy = s.ScheduledBy,
        ScheduledByName = s.ScheduledByNavigation?.FullName,
        CreatedAt = s.CreatedAt,
        EnrolledStudents = s.SessionEnrollments?.Count ?? 0,
        SessionCode = s.SessionCode,
        MaxCapacity = s.MaxCapacity
    };

    private static ExamSessionSummaryDto MapToSummary(ExamSession s) => new()
    {
        SessionId = s.SessionId,
        SessionName = s.SessionName,
        ExamTitle = s.Exam?.Title ?? string.Empty,
        ExamCode = s.Exam?.ExamCode ?? string.Empty,
        Room = s.Room,
        StartTime = s.StartTime,
        EndTime = s.EndTime,
        Status = s.Status?.ToString() ?? SessionStatus.NOT_STARTED.ToString(),
        EnrolledStudents = s.SessionEnrollments?.Count ?? 0
    };
}
