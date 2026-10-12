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
[Authorize(Roles = "TEACHER,ADMIN")]
public class ResultsController : ControllerBase
{
    private readonly IResultService _resultService;

    public ResultsController(IResultService resultService)
    {
        _resultService = resultService;
    }

    /// <summary>
    /// [TEACHER] Láº¥y danh sÃ¡ch káº¿t quáº£ chá» review cá»§a má»™t Exam Session.
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
    /// [TEACHER] Láº¥y chi tiáº¿t káº¿t quáº£ Ä‘á»ƒ review.
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
    /// [TEACHER] Review vÃ  chá»‰nh sá»­a Ä‘iá»ƒm/feedback tá»«ng cÃ¢u.
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
    /// [TEACHER] Approve & Publish Final Result (cÃ´ng bá»‘ káº¿t quáº£ chÃ­nh thá»©c).
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
