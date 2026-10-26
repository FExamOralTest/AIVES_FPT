using Aives.Application.Exams.DTOs;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Repository interface cho thao tác dữ liệu ExamSession.
/// </summary>
public interface IExamSessionRepository
{
    Task<ExamSession> CreateAsync(ExamSession session);

    Task<ExamSession?> GetByIdAsync(int sessionId);

    Task<ExamSession?> GetByIdWithEnrollmentsAsync(int sessionId);

    Task<ExamSession?> GetBySessionCodeAsync(string sessionCode);

    Task<(ExamSession? Session, ExamSessionSlot? Slot)> GetBySessionOrSlotCodeAsync(string code);

    Task<IEnumerable<ExamSession>> GetByTeacherIdAsync(int teacherId);

    Task<IEnumerable<StudentSessionEnrollmentDto>> GetStudentEnrollmentsAsync(int studentId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(int sessionId, SessionStatus status);

    Task<SessionEnrollment?> GetEnrollmentAsync(int sessionId, int studentId);

    Task AddEnrollmentAsync(SessionEnrollment enrollment);

    /// <summary>Bulk add nhiều enrollment cùng lúc — KHÔNG SaveChanges, caller chịu trách nhiệm.</summary>
    Task AddEnrollmentsRangeAsync(IEnumerable<SessionEnrollment> enrollments);

    /// <summary>Xoá 1 enrollment — SaveChanges ngay (dùng cho single remove).</summary>
    Task RemoveEnrollmentAsync(SessionEnrollment enrollment);

    /// <summary>Lấy tập studentId đã enrolled trong session (dùng để check trùng trong bulk add).</summary>
    Task<IReadOnlySet<int>> GetEnrolledStudentIdsAsync(int sessionId);

    /// <summary>Trả về user tồn tại theo danh sách id — bulk lookup tối ưu cho staff import.</summary>
    Task<IReadOnlyList<User>> GetUsersByIdsAsync(IEnumerable<int> ids);

    /// <summary>Bulk lookup user theo danh sách identifier (MSSV / Username, Email, ID).</summary>
    Task<IReadOnlyList<User>> GetUsersByIdentifiersAsync(IEnumerable<string> identifiers);

    /// <summary>Lấy danh sách studentId đã đăng ký vào một môn học (Course).</summary>
    Task<IReadOnlyList<int>> GetEnrolledStudentIdsByCourseIdAsync(int courseId);

    Task UpdateAsync(ExamSession session);

    Task<bool> SessionCodeExistsAsync(string sessionCode);

    Task SaveChangesAsync();

    Task RemoveAsync(ExamSession session);

    // === STAFF: list all sessions with optional filters ===
    Task<IEnumerable<ExamSession>> GetAllAsync(SessionStatus? status);

    // === Multi-Exam & Balanced Distribution ===
    Task SetSessionExamsAsync(int sessionId, IEnumerable<int> examIds);
    Task<List<Exam>> GetExamsBySessionIdAsync(int sessionId);
    Task UpdateEnrollmentAssignedExamAsync(int sessionId, int studentId, int examId);
    Task BulkUpdateEnrollmentsAsync(IEnumerable<SessionEnrollment> enrollments);

    // === Time Slots (Kíp thi) Management ===
    Task SetSessionSlotsAsync(int sessionId, IEnumerable<ExamSessionSlot> slots);
    Task UpdateEnrollmentAssignedSlotAsync(int sessionId, int studentId, int? slotId);
}
