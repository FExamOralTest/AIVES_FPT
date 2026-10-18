using Aives.Application.Queue.Contracts;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Orchestrator AI — gom logic STT + Eval cho 1 Answer. Được gọi từ cả 2 context:
/// <list type="number">
///   <item>Sync: <c>VivaSessionService.SubmitAnswerAsync</c> (giữ latency thấp, có timeout 5 phút).</item>
///   <item>Async: <c>AnswerProcessingJob</c> (Hangfire background worker).</item>
/// </list>
/// Tách thành interface để test với mock AI client.
/// </summary>
public interface IAnswerOrchestrator
{
    /// <summary>
    /// Pipeline: download audio từ R2 → STT → AI Eval → save Answer/Result.
    /// </summary>
    /// <param name="answerId">PK Answer cần xử lý.</param>
    /// <param name="attempt">Lần thử (1, 2, 3) — ghi vào log để debug retry.</param>
    /// <param name="cancellationToken">Bị hủy khi Hangfire retry filter kích hoạt.</param>
    /// <returns>Kết quả xử lý — caller dùng để quyết định retry/dead-letter.</returns>
    Task<OrchestrationResult> ProcessAnswerAsync(
        int answerId,
        int attempt,
        CancellationToken cancellationToken);
}

/// <summary>Kết quả pipeline — thay vì throw, orchestrator trả object để caller dễ quyết định.</summary>
public class OrchestrationResult
{
    /// <summary>Job thành công hoàn toàn (eval xong, score đã lưu).</summary>
    public bool Success { get; set; }

    /// <summary>STT error — null nếu không phải lỗi STT.</summary>
    public Contracts.Stt.SttError? SttError { get; set; }

    /// <summary>Eval error — null nếu không phải lỗi Eval (vd: timeout, validation).</summary>
    public string? EvaluationErrorCode { get; set; }

    /// <summary>Message ngắn cho log.</summary>
    public string? Message { get; set; }

    /// <summary>Transcript sinh ra từ STT — caller dùng để lưu vào DB.</summary>
    public string? Transcript { get; set; }

    /// <summary>AI score (sau khi eval thành công).</summary>
    public float? AiScore { get; set; }

    /// <summary>AI feedback (sau khi eval thành công).</summary>
    public string? AiFeedback { get; set; }

    /// <summary>AI analysis JSON (sau khi eval thành công).</summary>
    public string? AiAnalysisJson { get; set; }
}
