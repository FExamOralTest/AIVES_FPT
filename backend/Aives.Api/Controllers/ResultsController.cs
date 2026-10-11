using System.Security.Claims;
using Aives.Application.Results.DTOs;
using Aives.Application.Results.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/results")]
[Authorize(Roles = "TEACHER")]
public class ResultsController : ControllerBase
{
    private readonly IResultService _resultService;

    public ResultsController(IResultService resultService) => _resultService = resultService;

    [HttpGet("session/{sessionId:int}")]
    public async Task<IActionResult> GetResultsForReview(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();
        try { return Ok(await _resultService.GetResultsForReviewAsync(sessionId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("{resultId:int}")]
    public async Task<IActionResult> GetResultForReview(int resultId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();
        try { return Ok(await _resultService.GetResultForReviewAsync(resultId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{resultId:int}/review")]
    public async Task<IActionResult> ReviewResult(int resultId, [FromBody] ReviewResultRequest request)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();
        try { return Ok(await _resultService.ReviewResultAsync(resultId, request, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("{resultId:int}/publish")]
    public async Task<IActionResult> PublishResult(int resultId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();
        try { return Ok(await _resultService.PublishResultAsync(resultId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
