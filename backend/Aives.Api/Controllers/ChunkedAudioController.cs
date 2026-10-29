using System.Security.Claims;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Upload audio theo chunks (chunked multipart upload).
///
/// Flow:
///   1. POST /api/audio/chunked/init          → tạo phiên, nhận uploadId
///   2. POST /api/audio/chunked/chunk          → gửi từng chunk (N lần, có thể retry)
///   3. POST /api/audio/chunked/complete       → gộp + lên R2 + enqueue STT
///   4. DELETE /api/audio/chunked/{uploadId}   → hủy phiên (tuỳ chọn)
/// </summary>
[ApiController]
[Route("api/audio/chunked")]
[Authorize(Roles = "STUDENT,ADMIN")]
public class ChunkedAudioController : ControllerBase
{
    // Giới hạn mỗi chunk: 10 MB
    private const long MaxChunkSize = 10 * 1024 * 1024;

    private readonly IChunkedAudioUploadService _service;

    public ChunkedAudioController(IChunkedAudioUploadService service)
        => _service = service;

    // -----------------------------------------------------------------------
    // POST /api/audio/chunked/init
    // -----------------------------------------------------------------------

    /// <summary>
    /// [STUDENT/ADMIN] Khởi tạo phiên upload audio chunked.
    /// Nếu đã có phiên đang dở cho cùng (sessionId, questionId) thì trả về phiên cũ để resume.
    /// </summary>
    [HttpPost("init")]
    [ProducesResponseType(typeof(InitChunkedUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Init(
        [FromBody] InitChunkedUploadRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var role = GetRole();
        if (userId is null || role is null) return Unauthorized(new { message = "Token không hợp lệ." });

        if (string.IsNullOrWhiteSpace(request.UploadId))
            return BadRequest(new { message = "uploadId không được để trống." });
        if (request.QuestionId <= 0 || request.SessionId <= 0)
            return BadRequest(new { message = "questionId và sessionId phải > 0." });
        if (request.TotalChunks <= 0)
            return BadRequest(new { message = "totalChunks phải > 0." });

        try
        {
            var result = await _service.InitAsync(request, userId.Value, role.Value, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex){ return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)               { return StatusCode(500, new { message = ex.Message }); }
    }

    // -----------------------------------------------------------------------
    // POST /api/audio/chunked/chunk
    // -----------------------------------------------------------------------

    /// <summary>
    /// [STUDENT/ADMIN] Upload 1 chunk audio.
    /// Idempotent — có thể gửi lại cùng chunkIndex khi retry sau rớt mạng.
    /// </summary>
    [HttpPost("chunk")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxChunkSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxChunkSize)]
    [ProducesResponseType(typeof(UploadChunkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadChunk(
        [FromForm] UploadChunkRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Token không hợp lệ." });

        if (request.ChunkData == null || request.ChunkData.Length == 0)
            return BadRequest(new { message = "Dữ liệu chunk trống." });
        if (string.IsNullOrWhiteSpace(request.UploadId))
            return BadRequest(new { message = "uploadId không được để trống." });

        // Buffer chunk vào byte[] ngay lập tức — tránh closed stream
        byte[] chunkBytes;
        try
        {
            using var buf = new MemoryStream((int)request.ChunkData.Length);
            await request.ChunkData.CopyToAsync(buf, ct);
            chunkBytes = buf.ToArray();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Không thể đọc chunk: {ex.Message}" });
        }

        try
        {
            var result = await _service.UploadChunkAsync(request, chunkBytes, userId.Value, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)        { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex)   { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)                  { return StatusCode(500, new { message = ex.Message }); }
    }

    // -----------------------------------------------------------------------
    // POST /api/audio/chunked/complete
    // -----------------------------------------------------------------------

    /// <summary>
    /// [STUDENT/ADMIN] Hoàn thành upload: gộp tất cả chunks thành 1 file trên R2,
    /// cập nhật Answer, enqueue Hangfire STT job.
    /// </summary>
    [HttpPost("complete")]
    [ProducesResponseType(typeof(CompleteChunkedUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(
        [FromBody] CompleteChunkedUploadRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        var role = GetRole();
        if (userId is null || role is null) return Unauthorized(new { message = "Token không hợp lệ." });

        if (string.IsNullOrWhiteSpace(request.UploadId))
            return BadRequest(new { message = "uploadId không được để trống." });

        try
        {
            var result = await _service.CompleteAsync(request, userId.Value, role.Value, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)        { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex)   { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)                  { return StatusCode(500, new { message = ex.Message }); }
    }

    // -----------------------------------------------------------------------
    // DELETE /api/audio/chunked/{uploadId}
    // -----------------------------------------------------------------------

    /// <summary>
    /// [STUDENT/ADMIN] Hủy phiên upload — abort R2 multipart và xóa files local.
    /// </summary>
    [HttpDelete("{uploadId}")]
    [ProducesResponseType(typeof(AbortChunkedUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Abort(string uploadId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized(new { message = "Token không hợp lệ." });

        try
        {
            var result = await _service.AbortAsync(uploadId, userId.Value, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (Exception ex)                  { return StatusCode(500, new { message = ex.Message }); }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }

    private UserRole? GetRole()
    {
        var claim = User.FindFirstValue(ClaimTypes.Role);
        return Enum.TryParse<UserRole>(claim, out var role) ? role : null;
    }
}
