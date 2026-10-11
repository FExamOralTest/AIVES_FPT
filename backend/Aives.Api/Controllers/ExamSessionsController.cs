using System.Security.Claims;
using Aives.Application.Exams.DTOs;
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

    public ExamSessionsController(IExamSessionService sessionService) => _sessionService = sessionService;

    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] CreateExamSessionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try
        {
            var session = await _sessionService.CreateSessionAsync(request, teacherId.Value);
            return CreatedAtAction(nameof(GetSessionById), new { sessionId = session.SessionId }, session);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{sessionId:int}")]
    public async Task<IActionResult> GetSessionById(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try { return Ok(await _sessionService.GetSessionByIdAsync(sessionId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("teacher/{teacherId:int}")]
    public async Task<IActionResult> GetSessionsByTeacher(int teacherId)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null) return Unauthorized(new { message = "Invalid token." });
        if (currentUserId.Value != teacherId) return Forbid();

        return Ok(await _sessionService.GetSessionsByTeacherAsync(teacherId));
    }

    [HttpPut("{sessionId:int}/status")]
    public async Task<IActionResult> UpdateSessionStatus(
        int sessionId, [FromBody] UpdateSessionStatusRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try { return Ok(await _sessionService.UpdateSessionStatusAsync(sessionId, request, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{sessionId:int}/enrollments")]
    public async Task<IActionResult> GetEnrollments(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try { return Ok(await _sessionService.GetSessionEnrollmentsAsync(sessionId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("{sessionId:int}/monitor")]
    public async Task<IActionResult> GetSessionMonitor(int sessionId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try { return Ok(await _sessionService.GetSessionMonitorAsync(sessionId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
