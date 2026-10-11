using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Teacher Review & Publish Final Results.
/// </summary>
[ApiController]
[Route("api/results")]
[Authorize(Roles = "TEACHER")]
public class ResultsController : ControllerBase
{
    private readonly IResultService _resultService;

    public ResultsController(IResultService resultService)
    {
        _resultService = resultService;
    }

    /// <summary>
    /// [TEACHER] Lấy danh sách kết quả chờ review của một Exam Session.
    /// </summary>
    [HttpGet("session/{sessionId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ResultForReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultsForReview(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try 
        { 
            return Ok(await _resultService.GetResultsForReviewAsync(sessionId, teacherId.Value)); 
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// [TEACHER] Lấy chi tiết kết quả để review.
    /// </summary>
    [HttpGet("{resultId:int}")]
    [ProducesResponseType(typeof(ResultForReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultForReview(int resultId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try 
        { 
            return Ok(await _resultService.GetResultForReviewAsync(resultId, teacherId.Value)); 
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// [TEACHER] Review và chỉnh sửa điểm/feedback từng câu.
    /// </summary>
    [HttpPut("{resultId:int}/review")]
    [ProducesResponseType(typeof(ResultForReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewResult(int resultId, [FromBody] ReviewResultRequest request)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try 
        { 
            return Ok(await _resultService.ReviewResultAsync(resultId, request, teacherId.Value)); 
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [TEACHER] Approve & Publish Final Result (công bố kết quả chính thức).
    /// </summary>
    [HttpPost("{resultId:int}/publish")]
    [ProducesResponseType(typeof(ResultForReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PublishResult(int resultId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try 
        { 
            return Ok(await _resultService.PublishResultAsync(resultId, teacherId.Value)); 
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}