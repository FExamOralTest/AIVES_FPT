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

    Task UpdateStatusAsync(int sessionId, SessionStatus status);

    Task<SessionEnrollment?> GetEnrollmentAsync(int sessionId, int studentId);

    Task AddEnrollmentAsync(SessionEnrollment enrollment);

    Task UpdateAsync(ExamSession session);

    Task<bool> SessionCodeExistsAsync(string sessionCode);

    Task SaveChangesAsync();
}
