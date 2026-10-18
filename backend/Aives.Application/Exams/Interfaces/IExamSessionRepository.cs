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

    Task<IEnumerable<ExamSession>> GetByTeacherIdAsync(int teacherId);

    Task<IEnumerable<StudentSessionEnrollmentDto>> GetStudentEnrollmentsAsync(int studentId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(int sessionId, SessionStatus status);

    Task<SessionEnrollment?> GetEnrollmentAsync(int sessionId, int studentId);

    Task AddEnrollmentAsync(SessionEnrollment enrollment);
    Task AddEnrollmentsAsync(IEnumerable<SessionEnrollment> enrollments);
    Task RemoveEnrollmentsAsync(IEnumerable<SessionEnrollment> enrollments);
    Task RemoveEnrollmentAsync(SessionEnrollment enrollment);

    /// <summary>Lấy tất cả enrollment hiện có của 1 session (để check trùng khi bulk insert).</summary>
    Task<IReadOnlySet<int>> GetEnrolledStudentIdsAsync(int sessionId);

    /// <summary>Lấy các enrollment theo danh sách studentId (để bulk remove).</summary>
    Task<IReadOnlyList<SessionEnrollment>> GetEnrollmentsByStudentIdsAsync(int sessionId, IEnumerable<int> studentIds);

    Task UpdateAsync(ExamSession session);

    Task<bool> SessionCodeExistsAsync(string sessionCode);

    Task SaveChangesAsync();

    Task RemoveAsync(ExamSession session);

    // === STAFF: list all sessions with optional filters ===
    Task<IEnumerable<ExamSession>> GetAllAsync(SessionStatus? status);
}
