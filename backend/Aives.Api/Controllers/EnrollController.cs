using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/enroll")]
[Authorize(Roles = "STUDENT")]
public class EnrollController : ControllerBase
{
    private readonly IExamSessionService _sessionService;

    public EnrollController(IExamSessionService sessionService) => _sessionService = sessionService;

    [HttpPost("session")]
    public async Task<IActionResult> EnrollSession([FromBody] EnrollSessionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var studentId = GetCurrentUserId();
        if (studentId is null) return Unauthorized(new { message = "Invalid token." });

        try { return Ok(await _sessionService.EnrollStudentAsync(request, studentId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
