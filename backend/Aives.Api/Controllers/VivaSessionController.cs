using System.Security.Claims;
using Aives.Application.Viva.DTOs;
using Aives.Application.Viva.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Quá trình thi vấn đáp AI Viva của Sinh viên (Bắt đầu, Nhận câu hỏi, Nộp câu trả lời âm thanh, Kết thúc).
/// </summary>
[ApiController]
[Route("api/student/viva-sessions")]
[Authorize(Roles = "STUDENT")]
public class VivaSessionController : ControllerBase
{
    private readonly IVivaSessionService _vivaService;

    public VivaSessionController(IVivaSessionService vivaService)
    {
        _vivaService = vivaService;
    }

    /// <summary>
    /// [STUDENT] Bắt đầu ca thi vấn đáp trực tuyến.
    /// </summary>
    [HttpPost("{sessionId:int}/start")]
    [ProducesResponseType(typeof(StartVivaSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartSession(int sessionId)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _vivaService.StartSessionAsync(sessionId, studentId.Value);
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

    /// <summary>
    /// [STUDENT] Lấy câu hỏi hiện tại của ca thi.
    /// </summary>
    [HttpGet("{sessionId:int}/current-question")]
    [ProducesResponseType(typeof(VivaQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentQuestion(int sessionId)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _vivaService.GetCurrentQuestionAsync(sessionId, studentId.Value);
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

    /// <summary>
    /// [STUDENT] Nộp câu trả lời (Audio wav/webm hoặc text) cho một câu hỏi.
    /// Backend tự động gửi file sang AI Service để STT và kiểm tra câu hỏi phụ (follow-up).
    /// </summary>
    [HttpPost("{sessionId:int}/submit-answer")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024)]
    [ProducesResponseType(typeof(SubmitVivaAnswerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitAnswer(
        int sessionId,
        [FromForm] SubmitVivaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _vivaService.SubmitAnswerAsync(sessionId, studentId.Value, request, cancellationToken);
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

    /// <summary>
    /// [STUDENT] Kết thúc ca thi vấn đáp. AI sẽ đánh giá toàn bộ ca thi và tạo Result PENDING_REVIEW.
    /// </summary>
    [HttpPost("{sessionId:int}/finish")]
    [ProducesResponseType(typeof(FinishVivaSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> FinishSession(int sessionId, CancellationToken cancellationToken)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _vivaService.FinishSessionAsync(sessionId, studentId.Value, cancellationToken);
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
}
