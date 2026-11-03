using Aives.Application.Queue.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Xử lý luồng upload audio độc lập cho một Answer:
/// 1. Validate ownership + trạng thái Answer.
/// 2. Upload file lên Cloudflare R2 (hoặc local fallback).
/// 3. Persist audio_url vào DB.
/// 4. Enqueue Hangfire job STT + AI eval.
/// </summary>
public interface IAnswerAudioService
{
    /// <summary>
    /// Upload audio cho một Answer.
    /// Nhận dữ liệu file đã được buffer sẵn (byte[]) để tránh closed stream từ IFormFile.
    /// </summary>
    /// <param name="answerId">PK của Answer cần gắn audio.</param>
    /// <param name="fileBytes">Nội dung file đã buffer.</param>
    /// <param name="fileName">Tên file gốc (dùng để xác định extension).</param>
    /// <param name="contentType">MIME type của file.</param>
    /// <param name="currentUserId">User ID từ JWT claim.</param>
    /// <param name="currentUserRole">Role từ JWT claim.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<UploadAnswerAudioResponse> UploadAudioAsync(
        int answerId,
        byte[] fileBytes,
        string fileName,
        string contentType,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default);
}
