using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;
[ApiController]
[Route("api/exam-sessions")]
[Authorize(Roles = "TEACHER")]
public class ExamSessionsController : ControllerBase
{
    private readonly IExamSessionService _sessionService;

    public ExamSessionsController(IExamSessionService sessionService)
    {
        _sessionService = sessionService;
    }
    [HttpPost]
    [ProducesResponseType(typeof(ExamSessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSession([FromBody] CreateExamSessionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var session = await _sessionService.CreateSessionAsync(request, teacherId.Value);
            return CreatedAtAction(
                nameof(GetSessionById),
                new { sessionId = session.SessionId },
                session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpGet("{sessionId:int}")]
    [ProducesResponseType(typeof(ExamSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionById(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var session = await _sessionService.GetSessionByIdAsync(sessionId, teacherId.Value);
            return Ok(session);
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
    [HttpGet("teacher/{teacherId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ExamSessionSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSessionsByTeacher(int teacherId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized(new { message = "Invalid token." });
        if (currentUserId.Value != teacherId)
            return Forbid();

        var sessions = await _sessionService.GetSessionsByTeacherAsync(teacherId);
        return Ok(sessions);
    }
    [HttpPut("{sessionId:int}/status")]
    [ProducesResponseType(typeof(ExamSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSessionStatus(
        int sessionId,
        [FromBody] UpdateSessionStatusRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var updated = await _sessionService.UpdateSessionStatusAsync(
                sessionId, request, teacherId.Value);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpGet("{sessionId:int}/monitor")]
    [ProducesResponseType(typeof(SessionMonitorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionMonitor(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var monitor = await _sessionService.GetSessionMonitorAsync(sessionId, teacherId.Value);
            return Ok(monitor);
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

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
