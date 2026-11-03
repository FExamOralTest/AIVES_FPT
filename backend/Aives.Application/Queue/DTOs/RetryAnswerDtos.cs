namespace Aives.Application.Queue.DTOs;

/// <summary>
/// Request để retry 1 answer đã failed (STT/Eval lỗi).
/// </summary>
public class RetryAnswerRequest
{
    /// <summary>
    /// Lý do retry (audit log, optional). Ví dụ: "audio_corrupted_please_retry"
    /// — staff dùng để phân loại lỗi hàng loạt.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Response sau khi enqueue retry job.
/// </summary>
public class RetryAnswerResponse
{
    public int AnswerId { get; set; }
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "QUEUED";

    /// <summary>Status của answer TRƯỚC khi retry (PROCESSING/EVALUATED/ERROR).</summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>Thời điểm retry được enqueue.</summary>
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
}
