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
/// quản lý enrollment (bulk) và theo dõi tiến độ ca thi.
/// Admin vẫn dùng được (bypass ownership check).
/// TODO: khi có bảng `rooms` riêng → chuyển ExamSession.Room (string) thành FK int.
/// TODO: khi có bảng `classes` riêng → bật lại GetAllClassesAsync.
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

    // ═════════════════════════════════════════════════════════════
    //  SESSIONS / ROOMS
    // ═════════════════════════════════════════════════════════════

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
            var newEnd = request.EndTime ?? session.EndTime;
            if (newStart >= newEnd)
                throw new InvalidOperationException("StartTime must be earlier than EndTime.");
            session.StartTime = newStart;
            session.EndTime = newEnd;

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

    // ═════════════════════════════════════════════════════════════
    //  CLASS / TEACHER ASSIGNMENT
    // ═════════════════════════════════════════════════════════════

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

    // ═════════════════════════════════════════════════════════════
    //  ENROLLMENTS / MONITOR
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Bulk add nhiều sinh viên vào ca — 1 transaction.
    /// Khác với teacher-driven `EnrollStudentAsync` (1 SV / lần): staff cần nhập nguyên lớp.
    /// Mỗi studentId được đánh dấu 1 trong 4 trạng thái: added / already_enrolled / not_student / not_found.
    /// Trả về StaffBulkEnrollResponse để FE biết chính xác SV nào được thêm, SV nào bị skip.
    /// </summary>
    public Task<StaffBulkEnrollResponse> AddStudentsToSessionAsync(
        int sessionId, StaffAddEnrollmentsRequest request, int staffId)
        => _unitOfWork.ExecuteAsync(async () =>
        {
            var session = await _sessionRepository.GetByIdAsync(sessionId)
                ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

            await EnsureStaffCanManageCourseAsync(session.ExamId, staffId, session.Exam);

            if (session.Status == SessionStatus.COMPLETED || session.Status == SessionStatus.CANCELLED)
                throw new InvalidOperationException("Session is no longer open for enrollment.");

            // Dedup + bỏ id ≤ 0 (input có thể bị rác)
            var distinctIds = request.StudentIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            if (distinctIds.Count == 0)
                throw new ArgumentException("StudentIds is empty or invalid.");

            // 1. Check capacity trước khi làm gì (UX: fail-fast)
            var existingIds = await _sessionRepository.GetEnrolledStudentIdsAsync(sessionId);
            var toAddCount = distinctIds.Count(id => !existingIds.Contains(id));
            if (session.CurrentEnrollment + toAddCount > session.MaxCapacity)
                throw new InvalidOperationException(
                    $"Adding {toAddCount} new student(s) would exceed MaxCapacity ({session.MaxCapacity}). " +
                    $"Current: {session.CurrentEnrollment}.");

            // 2. Bulk lookup user
            var users = await _sessionRepository.GetUsersByIdsAsync(distinctIds);
            var usersById = users.ToDictionary(u => u.UserId);

            // 3. Build enrollments + results
            var results = new List<StaffEnrollResult>(distinctIds.Count);
            var newEnrollments = new List<SessionEnrollment>();

            foreach (var studentId in distinctIds)
            {
                if (existingIds.Contains(studentId))
                {
                    results.Add(new StaffEnrollResult
                    {
                        StudentId = studentId,
                        Status = "already_enrolled",
                        Reason = "Student is already enrolled in this session."
                    });
                    continue;
                }

                if (!usersById.TryGetValue(studentId, out var user))
                {
                    results.Add(new StaffEnrollResult
                    {
                        StudentId = studentId,
                        Status = "not_found",
                        Reason = "User does not exist."
                    });
                    continue;
                }

                if (user.Role != UserRole.STUDENT)
                {
                    results.Add(new StaffEnrollResult
                    {
                        StudentId = studentId,
                        Status = "not_student",
                        Reason = $"User has role '{user.Role}', not STUDENT."
                    });
                    continue;
                }

                var enrollment = new SessionEnrollment
                {
                    SessionId = sessionId,
                    StudentId = studentId,
                    Status = SessionStatus.NOT_STARTED,
                    EnrolledAt = DateTime.UtcNow
                };
                newEnrollments.Add(enrollment);
                results.Add(new StaffEnrollResult
                {
                    StudentId = studentId,
                    Status = "added",
                    SessionEnrollmentId = null  // sẽ fill sau khi SaveChanges
                });
            }

            // Kiểm tra capacity sau khi đã loại bỏ sinh viên trùng,
            // không tồn tại hoặc không phải sinh viên.
            if (session.CurrentEnrollment + newEnrollments.Count > session.MaxCapacity)
            {
                throw new InvalidOperationException(
                    $"Adding {newEnrollments.Count} students would exceed MaxCapacity " +
                    $"({session.CurrentEnrollment}/{session.MaxCapacity}).");
            }

            // Chỉ thêm enrollment khi có sinh viên hợp lệ.
            if (newEnrollments.Count > 0)
            {
                await _sessionRepository.AddEnrollmentsRangeAsync(newEnrollments);

                var addedByStudentId = newEnrollments
                    .Where(e => e.SessionEnrollmentId > 0)
                    .ToDictionary(e => e.StudentId, e => e.SessionEnrollmentId);

                foreach (var r in results.Where(r => r.Status == "added"))
                {
                    r.SessionEnrollmentId =
                        addedByStudentId.GetValueOrDefault(r.StudentId);
                }

                session.CurrentEnrollment += newEnrollments.Count;
                await _sessionRepository.UpdateAsync(session);
            }

            await _sessionRepository.SaveChangesAsync();

            return new StaffBulkEnrollResponse
            {
                SessionId = sessionId,
                AddedCount = newEnrollments.Count,
                SkippedCount = results.Count(r => r.Status != "added"),
                CurrentEnrollment = session.CurrentEnrollment,
                Results = results
            };
        });

    public async Task RemoveStudentFromSessionAsync(int sessionId, int studentId, int staffId)
    {
        var session = await _sessionRepository.GetByIdWithEnrollmentsAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        if (session.Status == SessionStatus.IN_PROGRESS)
            throw new InvalidOperationException(
                "Cannot remove a student from a session that is in progress.");

        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)
            ?? throw new KeyNotFoundException(
                $"Student {studentId} is not enrolled in session {sessionId}.");

        var toRemove = new[] { enrollment };
        foreach (var e in toRemove)
            await _sessionRepository.RemoveEnrollmentAsync(e);
        if (session.CurrentEnrollment > 0) session.CurrentEnrollment--;
        await _sessionRepository.UpdateAsync(session);
        await _sessionRepository.SaveChangesAsync();
    }

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

    // ═════════════════════════════════════════════════════════════
    //  Helpers
    // ═════════════════════════════════════════════════════════════

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
            SessionStatus.COMPLETED => Array.Empty<SessionStatus>(),
            SessionStatus.CANCELLED => Array.Empty<SessionStatus>(),
            null => new[] { SessionStatus.NOT_STARTED, SessionStatus.IN_PROGRESS, SessionStatus.CANCELLED },
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
