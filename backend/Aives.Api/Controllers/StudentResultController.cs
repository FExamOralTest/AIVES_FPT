using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Student xem kết quả thi Viva sau khi giảng viên đã duyệt và công bố.
/// </summary>
[ApiController]
[Route("api/student/results")]
[Authorize(Roles = "STUDENT,ADMIN")]
public class StudentResultController : ControllerBase
{
    private readonly IResultService _resultService;

    public StudentResultController(IResultService resultService)
    {
        _resultService = resultService;
    }

    /// <summary>
    /// [STUDENT] Lấy danh sách kết quả đã publish của chính mình.
    /// BR-11: Chỉ xem kết quả thuộc về mình và có status = PUBLISHED.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ResultForReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyPublishedResults()
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        var results = await _resultService.GetStudentPublishedResultsAsync(studentId.Value);
        return Ok(results);
    }

    /// <summary>
    /// [STUDENT] Xem kết quả thi của sinh viên cho một Exam Session.
    /// BR-10, BR-11: Chỉ hiển thị chi tiết khi trạng thái là PUBLISHED.
    /// </summary>
    [HttpGet("{sessionId:int}")]
    [ProducesResponseType(typeof(StudentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentResult(int sessionId)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var result = await _resultService.GetStudentResultAsync(sessionId, studentId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
