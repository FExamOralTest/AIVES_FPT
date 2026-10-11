using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IExamSessionService
{
    Task<ExamSessionDto> CreateSessionAsync(CreateExamSessionRequest request, int teacherId);
    Task<IEnumerable<ExamSessionSummaryDto>> GetSessionsByTeacherAsync(int teacherId);
    Task<ExamSessionDto> GetSessionByIdAsync(int sessionId, int teacherId);
    Task<ExamSessionDto> UpdateSessionStatusAsync(int sessionId, UpdateSessionStatusRequest request, int teacherId);
    Task<EnrollSessionResponse> EnrollStudentAsync(EnrollSessionRequest request, int studentId);
    Task<IEnumerable<SessionEnrollmentDto>> GetSessionEnrollmentsAsync(int sessionId, int teacherId);
    Task<SessionMonitorDto> GetSessionMonitorAsync(int sessionId, int teacherId);
}
