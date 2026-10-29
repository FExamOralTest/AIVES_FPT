using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Examiner Review & Publish Final Results.
/// BR-08: AI tạo Result ở trạng thái PENDING_REVIEW.
/// BR-09: Result được gửi cho Examiner review.
/// BR-10: Examiner approve và publish Final Result.
/// BR-11: Final Result được giao cho Student.
/// BR-13: Xác thực JWT và phân quyền theo role.
/// BR-15: Lưu câu hỏi, câu trả lời, transcript, điểm và feedback để đối soát.
/// </summary>
[ApiController]
[Route("api/results")]
[Authorize(Roles = "EXAMINER,ADMIN")]
public class ResultsController : ControllerBase
{
    private readonly IResultService _resultService;

    public ResultsController(IResultService resultService)
    {
        _resultService = resultService;
    }

    /// <summary>
    /// [EXAMINER] Lấy tất cả kết quả PENDING_REVIEW của tất cả sessions do Examiner quản lý.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IEnumerable<ResultForReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllPendingReviewResults()
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        var results = await _resultService.GetAllPendingReviewResultsAsync(teacherId.Value);
        return Ok(results);
    }

    /// <summary>
    /// [EXAMINER] Lấy danh sách kết quả của một Exam Session.
    /// </summary>
    [HttpGet("session/{sessionId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ResultForReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultsForReview(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            return Ok(await _resultService.GetResultsForReviewAsync(sessionId, teacherId.Value));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// [EXAMINER] Lấy chi tiết kết quả để review.
    /// Bao gồm transcript, audio URL, AI score, AI feedback.
    /// BR-15: Lưu đầy đủ dữ liệu để đối soát.
    /// </summary>
    [HttpGet("{resultId:int}")]
    [ProducesResponseType(typeof(ResultForReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultForReview(int resultId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            return Ok(await _resultService.GetResultForReviewAsync(resultId, teacherId.Value));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// [EXAMINER] Review và lưu điểm cuối cùng, feedback; cập nhật status = REVIEWED.
    /// BR-10: Teacher approve.
    /// BR-15: Cập nhật answers.final_score và answers.teacher_comment; không ghi đè ai_score/ai_feedback.
    /// </summary>
    [HttpPut("{resultId:int}/review")]
    [ProducesResponseType(typeof(ResultForReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReviewResult(int resultId, [FromBody] ReviewResultRequest request)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            return Ok(await _resultService.ReviewResultAsync(resultId, request, teacherId.Value));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The attempt changed. Refresh its retake or result status." });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already published") || ex.Message.Contains("Cannot review"))
                return Conflict(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [EXAMINER] Publish kết quả đã approve (REVIEWED) → PUBLISHED.
    /// BR-10: Teacher publish Final Result.
    /// BR-11: Final Result được giao cho Student.
    /// BR-15: Timestamp publish theo UTC.
    /// </summary>
    [HttpPost("{resultId:int}/publish")]
    [ProducesResponseType(typeof(ResultForReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PublishResult(int resultId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            return Ok(await _resultService.PublishResultAsync(resultId, teacherId.Value));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The attempt changed. Refresh its retake or result status." });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already published"))
                return Conflict(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}