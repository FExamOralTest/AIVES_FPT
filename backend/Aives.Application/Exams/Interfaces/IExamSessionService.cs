using Aives.Application.Exams.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Service interface cho nghiệp vụ quản lý ExamSession (Ca thi / Phòng thi).
/// </summary>
public interface IExamSessionService
{
    Task<ExamSessionDto> CreateSessionAsync(CreateExamSessionRequest request, int teacherId);

    Task<IEnumerable<ExamSessionSummaryDto>> GetSessionsByTeacherAsync(int teacherId);

    Task<ExamSessionDto> GetSessionByIdAsync(int sessionId, int teacherId);

    Task<ExamSessionDto> UpdateSessionStatusAsync(int sessionId, UpdateSessionStatusRequest request, int teacherId);

    // === P0: Student Enroll & Teacher Monitor ===
    Task<EnrollSessionResponse> EnrollStudentAsync(EnrollSessionRequest request, int studentId);
    Task<IEnumerable<SessionEnrollmentDto>> GetSessionEnrollmentsAsync(int sessionId, int teacherId);
    Task<SessionMonitorDto> GetSessionMonitorAsync(int sessionId, int teacherId);
}
