using Microsoft.AspNetCore.Http;

namespace Aives.Application.Queue.DTOs;

/// <summary>
/// Request multipart/form-data để upload audio cho một Answer đã tồn tại.
/// </summary>
public class UploadAnswerAudioRequest
{
    /// <summary>
    /// File âm thanh (webm, wav, mp3, ogg, …).
    /// Giới hạn kích thước được enforce ở controller qua [RequestSizeLimit].
    /// </summary>
    public IFormFile AudioFile { get; set; } = null!;
}

/// <summary>
/// Response trả về sau khi upload thành công.
/// </summary>
public class UploadAnswerAudioResponse
{
    /// <summary>PK của Answer vừa được gắn audio.</summary>
    public int AnswerId { get; set; }

    /// <summary>
    /// Object key R2 (hoặc đường dẫn local fallback) đã lưu vào cột answers.audio_url.
    /// Client không cần dùng trực tiếp — dùng GET /api/answers/{answerId}/audio để lấy playback URL.
    /// </summary>
    public string AudioKey { get; set; } = string.Empty;

    /// <summary>Hangfire Job ID vừa được enqueue để xử lý STT + AI eval.</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>Trạng thái answer sau khi upload (luôn là "PROCESSING").</summary>
    public string Status { get; set; } = "PROCESSING";

    /// <summary>Thời điểm upload (UTC).</summary>
    public DateTime UploadedAt { get; set; }
}
