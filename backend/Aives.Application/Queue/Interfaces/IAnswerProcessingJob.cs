using Aives.Application.Queue.Contracts;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Job Hangfire xử lý pipeline cho 1 Answer: download audio từ R2 → STT → Eval → save DB.
/// Method phải là <c>public</c> và class non-static để Hangfire có thể resolve DI scope.
/// </summary>
/// <remarks>
/// Worker thread chạy ở background — KHÔNG có HttpContext, KHÔNG có User claim.
/// Toàn bộ quyền truy cập phải dựa vào metadata truyền vào, không lấy từ HttpContext.
/// </remarks>
public interface IAnswerProcessingJob
{
    /// <summary>
    /// Thực thi pipeline. Hangfire sẽ retry tối đa theo config (mặc định 3) khi throw exception.
    /// </summary>
    /// <param name="metadata">Metadata job — đã được dedup-check trước khi enqueue.</param>
    /// <param name="cancellationToken">Hangfire tự truyền (CancellationToken từ Shutdown/Retry filter).</param>
    Task ExecuteAsync(QueueJobMetadata metadata, CancellationToken cancellationToken);
}
