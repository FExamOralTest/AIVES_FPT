using Aives.Application.Exams.Interfaces;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Repository cho entity <c>Answer</c> — tách riêng vì Answer flow không dùng
/// <c>IResultRepository.AddOrUpdateAnswerAsync</c> (method đó dùng cho Submit API,
/// orchestration cần update linh hoạt hơn: status, transcript, ai_score, audio key...).
/// </summary>
public interface IAnswerRepository
{
    /// <summary>Lấy answer theo PK — kèm Question + Rubric để build eval request.</summary>
    Task<Domain.Entities.Answer?> GetByIdWithDetailsAsync(int answerId, CancellationToken cancellationToken = default);

    /// <summary>Cập nhật transcript + status (sau STT).</summary>
    Task UpdateTranscriptAsync(int answerId, string transcript, Domain.Enums.AnswerStatus status,
        CancellationToken cancellationToken = default, string? detectedLanguage = null, float? durationSeconds = null);

    /// <summary>Cập nhật kết quả AI (score + feedback + analysis JSON + status).</summary>
    Task UpdateAiEvaluationAsync(
        int answerId,
        float? aiScore,
        string? aiFeedback,
        string? aiAnalysisJson,
        Domain.Enums.AnswerStatus status,
        CancellationToken cancellationToken);

    /// <summary>Đánh dấu answer lỗi (status = ERROR) + message ngắn để debug.</summary>
    Task MarkFailedAsync(int answerId, string errorMessage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-enqueue cho retry: reset transcript/ai về null nếu user yêu cầu làm lại từ đầu.
    /// </summary>
    /// <remarks>
    /// KHÔNG reset <c>AttemptCount</c> — bộ đếm attempt là trạng thái bền vững của BR-17
    /// ("retry tối đa 3 lần thử tổng cộng") và phải sống sót qua các lần retry của Hangfire.
    /// </remarks>
    Task ResetForRetryAsync(int answerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật audio_url sau khi upload file lên R2 (dùng cho standalone audio upload API).
    /// Đồng thời reset transcript/ai_score về null và set status = PROCESSING để Hangfire xử lý lại.
    /// </summary>
    Task UpdateAudioUrlAsync(int answerId, string audioUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tăng bộ đếm attempt của Answer và ghi lại Hangfire JobId hiện tại.
    /// </summary>
    /// <remarks>
    /// Đây là nguồn sự thật duy nhất cho "lần thử thứ mấy" (BR-17). Metadata của Hangfire bị
    /// serialize lại mỗi lần retry nên <c>metadata.Attempt</c> không tự tăng — chỉ DB mới đáng tin.
    /// Hàm trả về giá trị attempt MỚI (1-based). Trả về 0 nếu answer không tồn tại.
    /// </remarks>
    Task<int> IncrementAttemptAsync(int answerId, string? jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi Hangfire JobId vào Answer (dùng để tra cứu job từ DB, không chỉ từ Hangfire dashboard).
    /// </summary>
    Task SetJobIdAsync(int answerId, string? jobId, CancellationToken cancellationToken = default);
}
