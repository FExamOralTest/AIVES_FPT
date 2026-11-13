using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
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
    private readonly IProcessingStatusService _statusService;

    public VivaSessionController(
        IVivaSessionService vivaService,
        IProcessingStatusService statusService)
    {
        _vivaService = vivaService;
        _statusService = statusService;
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
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The attempt changed. Refresh its retake or result status." });
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
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The attempt changed. Refresh its retake or result status." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [STUDENT] Đọc câu hỏi hiện tại bằng giọng nói (TTS). <paramref name="questionId"/> phải là câu hỏi hiện tại.
    /// Trả 503 khi TTS không khả dụng để client tự dùng giọng đọc của trình duyệt.
    /// </summary>
    [HttpGet("{sessionId:int}/current-question/audio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetCurrentQuestionAudio(int sessionId, [FromQuery] int questionId)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var audio = await _vivaService.GetCurrentQuestionAudioAsync(sessionId, studentId.Value, questionId);
            if (audio is null)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Text-to-speech is not available." });
            Response.Headers.CacheControl = "private, no-store";
            return File(audio.Audio, audio.ContentType);
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
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The attempt changed. Refresh its retake or result status." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [STUDENT] Kết thúc ca thi. Bài được lưu ngay và xếp vào hàng đợi chấm lần lượt.
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
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The attempt changed. Refresh its retake or result status." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [STUDENT] Theo dõi trạng thái bài nộp của chính mình trong ca thi:
    /// job chấm cả bài, từng answer và lỗi (nếu có).
    /// </summary>
    [HttpGet("{sessionId:int}/submission-status")]
    [ProducesResponseType(typeof(SubmissionStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubmissionStatus(
        int sessionId,
        CancellationToken cancellationToken)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            return Ok(await _statusService.GetSubmissionStatusAsync(
                sessionId, studentId.Value, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
