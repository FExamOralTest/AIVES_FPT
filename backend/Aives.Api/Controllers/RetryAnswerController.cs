using System.Security.Claims;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Endpoint retry answer đã failed. Phục vụ 3 role: STUDENT (retry bài mình),
/// STAFF/EXAMINER (retry bài sinh viên trong session mình phụ trách), ADMIN (mọi thứ).
/// </summary>
[ApiController]
[Route("api/answers")]
[Authorize] // Có auth, role check bên trong service.
public class RetryAnswerController : ControllerBase
{
    private readonly IRetryAnswerService _retryService;

    public RetryAnswerController(IRetryAnswerService retryService)
    {
        _retryService = retryService;
    }

    /// <summary>
    /// [STUDENT/STAFF/EXAMINER/ADMIN] Retry 1 answer đã failed (status = ERROR hoặc PROCESSING > 30 phút).
    /// Reset transcript/AI fields, re-enqueue Hangfire job.
    /// </summary>
    [HttpPost("{answerId:int}/retry")]
    [ProducesResponseType(typeof(RetryAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retry(
        int answerId,
        [FromBody] RetryAnswerRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentRole();
        if (userId is null || role is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _retryService.RetryAsync(
                answerId,
                userId,
                role,
                request ?? new RetryAnswerRequest(),
                cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }

    private UserRole? GetCurrentRole()
    {
        var claim = User.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(claim, out var role) ? role : null;
    }
}
