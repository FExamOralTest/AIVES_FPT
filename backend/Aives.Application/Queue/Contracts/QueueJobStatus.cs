namespace Aives.Application.Queue.Contracts;

/// <summary>
/// Trạng thái vòng đời của 1 job trong queue (Hangfire).
/// Dùng để map giữa trạng thái nội bộ (DB) và Hangfire state.
/// </summary>
public enum QueueJobStatus
{
    /// <summary>Đã đăng ký, chờ worker nhận.</summary>
    Pending = 0,

    /// <summary>Worker đang thực thi (gọi R2 → STT → Eval).</summary>
    Processing = 1,

    /// <summary>Hoàn tất thành công, result đã được lưu.</summary>
    Completed = 2,

    /// <summary>Lần retry sẽ thực hiện lại (do exception throwable hoặc AI trả retryable=true).</summary>
    Failed = 3,

    /// <summary>Đã vượt quá max attempt (3) hoặc lỗi không retryable → vào dead-letter, cần can thiệp thủ công.</summary>
    DeadLetter = 4
}
