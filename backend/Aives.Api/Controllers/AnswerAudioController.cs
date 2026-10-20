using System.Security.Claims;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Quản lý audio của Answer: upload, phát lại, xóa, kiểm tra trạng thái xử lý.
/// Route gốc: /api/answers/{answerId}/audio
/// </summary>
[ApiController]
[Route("api/answers")]
[Authorize]
public class AnswerAudioController : ControllerBase
{
    private const long MaxRequestSize = 50 * 1024 * 1024;
    private static readonly TimeSpan PlaybackUrlDuration = TimeSpan.FromHours(1);

    private readonly IAnswerAudioService _audioService;
    private readonly IAudioPlaybackService _playbackService;

    public AnswerAudioController(
        IAnswerAudioService audioService,
        IAudioPlaybackService playbackService)
    {
        _audioService = audioService;
        _playbackService = playbackService;
    }

    // -----------------------------------------------------------------------
    // POST /api/answers/{answerId}/audio
    // -----------------------------------------------------------------------

    /// <summary>
    /// [STUDENT/ADMIN] Upload file âm thanh nộp bài cho một Answer.
    /// </summary>
    /// <remarks>
    /// Gửi <c>multipart/form-data</c> với field <c>audioFile</c>.
    ///
    /// Sau khi upload:
    /// - File được lưu trên Cloudflare R2 (hoặc local fallback ở dev).
    /// - <c>answers.audio_url</c> được cập nhật, status → PROCESSING.
    /// - Hangfire job STT + AI evaluation được enqueue bất đồng bộ.
    ///
    /// Giới hạn: 50 MB. Định dạng: webm, wav, mp3, ogg, aac, flac.
    /// </remarks>
    [HttpPost("{answerId:int}/audio")]
    [Authorize(Roles = "STUDENT,ADMIN")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxRequestSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSize)]
    [ProducesResponseType(typeof(UploadAnswerAudioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAudio(
        int answerId,
        [FromForm] UploadAnswerAudioRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentRole();
        if (userId is null || role is null)
            return Unauthorized(new { message = "Token không hợp lệ." });

        if (request.AudioFile == null || request.AudioFile.Length == 0)
            return BadRequest(new { message = "Vui lòng chọn file âm thanh để upload." });

        // Buffer IFormFile stream ngay tại đây — stream bị ASP.NET dispose sau request,
        // phải đọc xong trước bất kỳ await nào phía sau.
        byte[] fileBytes;
        try
        {
            using var buffer = new MemoryStream((int)request.AudioFile.Length);
            await request.AudioFile.CopyToAsync(buffer, cancellationToken);
            fileBytes = buffer.ToArray();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Không thể đọc file: {ex.Message}" });
        }

        if (fileBytes.Length == 0)
            return BadRequest(new { message = "File audio rỗng." });

        try
        {
            var response = await _audioService.UploadAudioAsync(
                answerId,
                fileBytes,
                request.AudioFile.FileName,
                request.AudioFile.ContentType,
                userId.Value,
                role.Value,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException ex)        { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex){ return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)               { return StatusCode(500, new { message = ex.Message }); }
    }

    // -----------------------------------------------------------------------
    // GET /api/answers/{answerId}/audio
    // -----------------------------------------------------------------------

    /// <summary>
    /// [STUDENT/STAFF/EXAMINER/ADMIN] Lấy URL phát audio có thời hạn (1 giờ).
    /// </summary>
    /// <remarks>
    /// Trả về presigned URL hoặc CDN URL để stream/download audio trực tiếp từ Cloudflare R2.
    ///
    /// Phân quyền:
    /// - STUDENT: chỉ nghe được audio của chính mình.
    /// - STAFF/EXAMINER: nghe được audio trong session do mình tạo.
    /// - ADMIN: không giới hạn.
    /// </remarks>
    [HttpGet("{answerId:int}/audio")]
    [ProducesResponseType(typeof(AudioPlaybackResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlaybackUrl(
        int answerId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentRole();
        if (userId is null || role is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var response = await _playbackService.GetPlaybackUrlAsync(
                answerId, userId, role, PlaybackUrlDuration, cancellationToken);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex){ return StatusCode(500, new { message = ex.Message }); }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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
