namespace Aives.Application.Queue.DTOs;

/// <summary>
/// Response trả về URL audio có thể phát trên browser (presigned R2 hoặc local path).
/// URL có thời hạn — client không nên cache.
/// </summary>
public class AudioPlaybackResponse
{
    public int AnswerId { get; set; }
    public int ResultId { get; set; }
    public string PlaybackUrl { get; set; } = string.Empty;

    /// <summary>Thời điểm URL hết hạn (UTC). Client check để refresh.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>MIME type (audio/webm, audio/wav, ...).</summary>
    public string? ContentType { get; set; }

    /// <summary>Kích thước file (bytes) — optional, hiện R2 không trả về nhanh.</summary>
    public long? SizeBytes { get; set; }
}
