namespace Aives.Application.Queue.Contracts.Stt;

/// <summary>
/// Lỗi chuẩn hoá từ AI1 (Faster-Whisper). Worker sẽ kiểm tra <see cref="Retryable"/>
/// để quyết định requeue hay dead-letter.
/// </summary>
public class SttError
{
    /// <summary>Mã lỗi — dùng để phân loại và map sang <c>Answer.ErrorCode</c> trong DB.</summary>
    public SttErrorCode Code { get; set; } = SttErrorCode.Unknown;

    /// <summary>Thông điệp mô tả ngắn (≤ 500 ký tự), hiển thị cho staff xem.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Có nên retry không. Worker đặt giá trị này dựa trên <see cref="Code"/>:
    /// <c>AudioCorrupted</c> / <c>EmptyTranscript</c> / <c>UnsupportedFormat</c> = false,
    /// <c>Timeout</c> / <c>InternalError</c> / <c>Unknown</c> = true.
    /// </summary>
    public bool Retryable { get; set; } = true;

    /// <summary>Thời điểm lỗi xảy ra (UTC), dùng để log timeline.</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
