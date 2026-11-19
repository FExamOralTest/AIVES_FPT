using Aives.Application.Queue.Contracts;

namespace Aives.Application.Queue.DTOs;

/// <summary>
/// Trạng thái hiện tại của một job trên Hangfire — dùng cho endpoint tra cứu
/// <c>GET /api/answers/{answerId}/processing-status</c> và
/// <c>GET /api/results/{resultId}/grading-status</c>.
/// </summary>
public class QueueJobStateDto
{
    /// <summary>Hangfire JobId.</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>Tên state thô của Hangfire: Enqueued / Processing / Succeeded / Failed / Deleted…</summary>
    public string State { get; set; } = "Unknown";

    /// <summary>State đã map về hợp đồng job của AIVES.</summary>
    public QueueJobStatus Status { get; set; } = QueueJobStatus.Pending;

    /// <summary>Loại job (vd: <c>AnswerProcessingJob</c>).</summary>
    public string? JobType { get; set; }

    /// <summary>Thời điểm job được tạo (UTC).</summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>Thời điểm state hiện tại được ghi nhận (UTC).</summary>
    public DateTime? StateChangedAt { get; set; }

    /// <summary>Lý do fail gần nhất (đã cắt ngắn), null nếu job chưa fail.</summary>
    public string? Error { get; set; }
}
