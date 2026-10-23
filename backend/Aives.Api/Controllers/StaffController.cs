using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Staff endpoints — quản lý phòng thi, lịch thu xếp ca, xếp lớp / phân công giảng viên,
/// theo dõi enrollment & tiến độ ca thi.
/// Admin dùng được để dễ kiểm thử (bypass ownership check ở service).
/// Staff KHÔNG đụng vào tài liệu trong Course — chỉ gen đề random qua TeacherDocumentController.
/// </summary>
[ApiController]
[Route("api/staff")]
[Authorize(Roles = "STAFF,ADMIN")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class StaffController : ControllerBase
{
    private readonly IStaffService _staff;

    public StaffController(IStaffService staff) => _staff = staff;

    private int? CallerId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // ═════════════════════════════════════════════════════════════
    //  SESSIONS / ROOMS
    // ═════════════════════════════════════════════════════════════

    /// <summary>Danh sách ca thi (lọc theo Status, ví dụ ?status=NOT_STARTED).</summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IEnumerable<ExamSessionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions([FromQuery] StaffSessionQuery? query)
    {
        var id = CallerId ?? throw new UnauthorizedAccessException();
        var sessions = await _staff.GetAllSessionsAsync(query ?? new StaffSessionQuery(), id);
        return Ok(sessions);
    }

    /// <summary>Chi tiết một ca thi.</summary>
    [HttpGet("sessions/{sessionId:int}")]
    [ProducesResponseType(typeof(ExamSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetSession(int sessionId) => ExecuteAsync(async () =>
    {
        var id = CallerId ?? throw new UnauthorizedAccessException();
        return Ok(await _staff.GetSessionByIdAsync(sessionId, id));
    });

    /// <summary>Tạo ca thi mới (phòng, lịch, capacity). Không cần là người tạo Exam.</summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(ExamSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> CreateSession([FromBody] StaffCreateSessionRequest request) => ExecuteAsync(async () =>
    {
        var id = CallerId ?? throw new UnauthorizedAccessException();
        var session = await _staff.CreateSessionAsync(request, id);
        return CreatedAtAction(nameof(GetSession), new { sessionId = session.SessionId }, session);
    });

    /// <summary>Cập nhật thông tin ca thi (phòng, lịch, capacity, status).</summary>
    [HttpPut("sessions/{sessionId:int}")]
    [ProducesResponseType(typeof(ExamSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> UpdateSession(int sessionId, [FromBody] StaffUpdateSessionRequest request)
        => ExecuteAsync(async () =>
        {
            var id = CallerId ?? throw new UnauthorizedAccessException();
            return Ok(await _staff.UpdateSessionAsync(sessionId, request, id));
        });

    /// <summary>Xoá ca thi (chỉ khi chưa có enrollment và chưa IN_PROGRESS/COMPLETED).</summary>
    [HttpDelete("sessions/{sessionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> DeleteSession(int sessionId) => ExecuteAsync(async () =>
    {
        var id = CallerId ?? throw new UnauthorizedAccessException();
        await _staff.DeleteSessionAsync(sessionId, id);
        return NoContent();
    });

    // ═════════════════════════════════════════════════════════════
    //  CLASS / TEACHER ASSIGNMENT
    //  - Tạm ẩn GET /api/staff/classes: chưa có bảng `classes` riêng,
    //    staff dùng GET /api/courses (đã có sẵn). Sẽ bật lại sau khi update DB.
    // ═════════════════════════════════════════════════════════════

    // [ApiExplorerSettings(IgnoreApi = true)]
    // [HttpGet("classes")]
    // public async Task<IActionResult> GetClasses()
    //     => Ok(await _staff.GetAllClassesAsync());

    /// <summary>Phân công / thay đổi giảng viên (STAFF role) cho một lớp.</summary>
    [HttpPut("classes/{courseId:int}/assign-teacher")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> AssignTeacher(int courseId, [FromBody] StaffAssignTeacherRequest request)
        => ExecuteAsync(async () =>
        {
            var id = CallerId ?? throw new UnauthorizedAccessException();
            return Ok(await _staff.AssignTeacherToClassAsync(courseId, request, id));
        });

    // ═════════════════════════════════════════════════════════════
    //  ENROLLMENTS / MONITOR
    // ═════════════════════════════════════════════════════════════

    /// <summary>
    /// Bulk thêm nhiều sinh viên vào ca thi (≤ 500/lần).
    /// Response chi tiết từng studentId: added / already_enrolled / not_student / not_found.
    /// </summary>
    [HttpPost("sessions/{sessionId:int}/enrollments")]
    [ProducesResponseType(typeof(StaffBulkEnrollResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> AddEnrollments(int sessionId, [FromBody] StaffAddEnrollmentsRequest request)
        => ExecuteAsync(async () =>
        {
            var id = CallerId ?? throw new UnauthorizedAccessException();
            return Ok(await _staff.AddStudentsToSessionAsync(sessionId, request, id));
        });

    /// <summary>Tự động thêm toàn bộ sinh viên đã ghi danh môn học của ca thi.</summary>
    [HttpPost("sessions/{sessionId:int}/enroll-course-students")]
    [ProducesResponseType(typeof(StaffBulkEnrollResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> EnrollCourseStudents(int sessionId) => ExecuteAsync(async () =>
    {
        var id = CallerId ?? throw new UnauthorizedAccessException();
        return Ok(await _staff.ImportCourseStudentsToSessionAsync(sessionId, id));
    });

    /// <summary>Xoá sinh viên khỏi ca thi (chỉ khi ca chưa IN_PROGRESS).</summary>
    [HttpDelete("sessions/{sessionId:int}/enrollments/{studentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> RemoveEnrollment(int sessionId, int studentId) => ExecuteAsync(async () =>
    {
        var id = CallerId ?? throw new UnauthorizedAccessException();
        await _staff.RemoveStudentFromSessionAsync(sessionId, studentId, id);
        return NoContent();
    });

    /// <summary>Theo dõi tiến độ ca thi (danh sách SV, số câu đã trả lời, % hoàn thành).</summary>
    [HttpGet("sessions/{sessionId:int}/monitor")]
    [ProducesResponseType(typeof(SessionMonitorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetMonitor(int sessionId) => ExecuteAsync(async () =>
    {
        var id = CallerId ?? throw new UnauthorizedAccessException();
        return Ok(await _staff.GetSessionMonitorAsync(sessionId, id));
    });

    /// <summary>Trộn và phân bổ ngẫu nhiên, cân bằng các bộ đề cho sinh viên trong ca thi.</summary>
    [HttpPost("sessions/{sessionId:int}/distribute-exams")]
    [ProducesResponseType(typeof(DistributeExamsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DistributeExams(int sessionId, [FromBody] DistributeExamsRequest? request)
        => ExecuteAsync(async () =>
        {
            var id = CallerId ?? throw new UnauthorizedAccessException();
            return Ok(await _staff.DistributeExamsAsync(sessionId, request ?? new DistributeExamsRequest(), id));
        });

    /// <summary>Gán đề thi cụ thể cho một sinh viên trong ca thi.</summary>
    [HttpPut("sessions/{sessionId:int}/enrollments/{studentId:int}/assign-exam")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> AssignStudentExam(int sessionId, int studentId, [FromBody] StaffAssignExamRequest request)
        => ExecuteAsync(async () =>
        {
            var id = CallerId ?? throw new UnauthorizedAccessException();
            await _staff.AssignStudentExamAsync(sessionId, studentId, request, id);
            return NoContent();
        });

    /// <summary>Trộn và phân bổ cân bằng sinh viên vào các kíp thi (time slots) và tự động chia đề thi.</summary>
    [HttpPost("sessions/{sessionId:int}/distribute-slots")]
    [ProducesResponseType(typeof(DistributeSlotsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> DistributeSlots(int sessionId, [FromBody] DistributeSlotsRequest? request)
        => ExecuteAsync(async () =>
        {
            var id = CallerId ?? throw new UnauthorizedAccessException();
            return Ok(await _staff.DistributeSlotsAsync(sessionId, request ?? new DistributeSlotsRequest(), id));
        });

    /// <summary>Gán kíp thi cụ thể cho một sinh viên trong ca thi.</summary>
    [HttpPut("sessions/{sessionId:int}/enrollments/{studentId:int}/assign-slot")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> AssignStudentSlot(int sessionId, int studentId, [FromBody] StaffAssignSlotRequest request)
        => ExecuteAsync(async () =>
        {
            var id = CallerId ?? throw new UnauthorizedAccessException();
            await _staff.AssignStudentSlotAsync(sessionId, studentId, request, id);
            return NoContent();
        });

    // ═════════════════════════════════════════════════════════════
    //  Error mapping — đồng nhất với các controller khác
    // ═════════════════════════════════════════════════════════════

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex)        { return BadRequest(new { message = ex.Message }); }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
