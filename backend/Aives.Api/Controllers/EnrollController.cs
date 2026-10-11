using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Student Enroll vào Exam Session bằng Enroll Code (SessionCode).
/// </summary>
[ApiController]
[Route("api/enroll")]
[Authorize(Roles = "STUDENT")]
public class EnrollController : ControllerBase
{
    private readonly IExamSessionService _sessionService;

    public EnrollController(IExamSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    /// <summary>
    /// [STUDENT] Nhập Enroll Code để tham gia ca thi.
    /// </summary>
    [HttpPost("session")]
    [ProducesResponseType(typeof(EnrollSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnrollSession([FromBody] EnrollSessionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var result = await _sessionService.EnrollStudentAsync(request, studentId.Value);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}