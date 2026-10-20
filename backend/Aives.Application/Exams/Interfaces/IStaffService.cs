using Aives.Application.DTOs;
using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Nghiệp vụ dành cho Staff: quản lý phòng thi (ExamSession.Room), lịch thu xếp ca,
/// xếp lớp / phân công giảng viên, theo dõi enrollment và tiến độ ca thi.
/// Admin vẫn dùng được (bypass ownership check).
/// </summary>
public interface IStaffService
{
    // ── Sessions / Rooms ───────────────────────────────────────
    Task<IEnumerable<ExamSessionSummaryDto>> GetAllSessionsAsync(StaffSessionQuery query, int staffId);
    Task<ExamSessionDto> GetSessionByIdAsync(int sessionId, int staffId);
    Task<ExamSessionDto> CreateSessionAsync(StaffCreateSessionRequest request, int staffId);
    Task<ExamSessionDto> UpdateSessionAsync(int sessionId, StaffUpdateSessionRequest request, int staffId);
    Task DeleteSessionAsync(int sessionId, int staffId);

    // ─── Class / Teacher assignment ──────────────────────────────
    // TODO: khi bổ sung bảng `classes` riêng (ClassId, Name, CourseId, Semester, ...)
    // thì bật lại GetAllClassesAsync trỏ vào bảng mới. Hiện tại staff dùng
    // GET /api/courses có sẵn nên tạm ẩn.
    // Task<IEnumerable<ClassDto>> GetAllClassesAsync();
    Task<CourseDto> AssignTeacherToClassAsync(int courseId, StaffAssignTeacherRequest request, int staffId);

    // ─── Enrollments (Session) ───────────────────────────────────
    Task<StaffBulkEnrollResponse> AddStudentsToSessionAsync(int sessionId, StaffAddEnrollmentsRequest request, int staffId);
    Task RemoveStudentFromSessionAsync(int sessionId, int studentId, int staffId);
    Task<SessionMonitorDto> GetSessionMonitorAsync(int sessionId, int staffId);
}
