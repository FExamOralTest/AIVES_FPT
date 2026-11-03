using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// API quản trị: danh sách exam toàn hệ thống và báo cáo tổng quan cho dashboard.
/// Route gốc: /api/admin
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "ADMIN")]
[Produces("application/json")]
public class AdminReportController(IAdminReportService reportService) : ControllerBase
{
    /// <summary>
    /// [ADMIN] Lấy toàn bộ exam trong hệ thống, có bộ lọc.
    /// </summary>
    /// <remarks>
    /// Trả về danh sách exam kèm số câu hỏi và số phiên thi đã tạo.
    /// Bộ lọc: <c>courseId</c>, <c>examStatus</c> (DRAFT/SCHEDULED/OPEN/CLOSED/CANCELLED), <c>search</c> (title hoặc exam code),
    /// <c>includeInactive</c> (mặc định <c>true</c>; đặt <c>false</c> để chỉ lấy exam chưa soft-delete),
    /// <c>isDescending</c> (mặc định <c>true</c>; sắp xếp <c>created_at</c> giảm dần).
    /// Endpoint không phân trang — trả về toàn bộ bản ghi khớp bộ lọc.
    /// </remarks>
    [HttpGet("exams")]
    [ProducesResponseType(typeof(AdminExamListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetExams([FromQuery] AdminExamQuery query)
    {
        try
        {
            var result = await reportService.GetExamListAsync(query);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [ADMIN] Lấy tổng quan hệ thống: số phiên thi, lượt đăng ký, lượt nộp bài, điểm trung bình và hàng đợi review.
    /// </summary>
    /// <remarks>
    /// Bộ lọc (tùy chọn): <c>fromDate</c>, <c>toDate</c> (UTC, áp dụng cho <c>start_time</c> của session),
    /// <c>courseId</c>, <c>sessionStatus</c>.
    /// </remarks>
    [HttpGet("reports/summary")]
    [ProducesResponseType(typeof(AdminReportSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSummary([FromQuery] AdminReportSummaryQuery query)
    {
        try
        {
            var result = await reportService.GetSummaryAsync(query);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}