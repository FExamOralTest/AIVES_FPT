using System.Security.Claims;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Tra cứu trạng thái xử lý của answer và bài chấm. Chỉ đọc — dùng để client
/// poll tiến độ thay vì đoán trạng thái từ DB.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class ProcessingStatusController : ControllerBase
{
    private readonly IProcessingStatusService _statusService;

    public ProcessingStatusController(IProcessingStatusService statusService)
    {
        _statusService = statusService;
    }

    /// <summary>
    /// [STUDENT/STAFF/EXAMINER/ADMIN] Trạng thái STT + AI evaluation của một Answer.
    /// Kèm trạng thái Hangfire job, số lần thử và khả năng retry.
    /// </summary>
    [HttpGet("answers/{answerId:int}/processing-status")]
    [ProducesResponseType(typeof(AnswerProcessingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnswerStatus(
        int answerId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentRole();
        if (userId is null || role is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            return Ok(await _statusService.GetAnswerStatusAsync(
                answerId, userId.Value, role.Value, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
    }

    /// <summary>
    /// [STUDENT/STAFF/EXAMINER/ADMIN] Trạng thái chấm cả bài của một Result.
    /// Kèm trạng thái job chấm trên queue <c>grading</c> và tóm tắt từng answer.
    /// </summary>
    [HttpGet("results/{resultId:int}/grading-status")]
    [ProducesResponseType(typeof(ResultGradingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultStatus(
        int resultId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentRole();
        if (userId is null || role is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            return Ok(await _statusService.GetResultStatusAsync(
                resultId, userId.Value, role.Value, cancellationToken));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
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
