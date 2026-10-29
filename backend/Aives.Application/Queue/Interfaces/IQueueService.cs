using Aives.Application.Queue.Contracts;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Dịch vụ queue nội bộ — abstraction cho Hangfire. Tách lớp abstraction để:
/// <list type="bullet">
///   <item>Unit test dễ (mock <c>IQueueService</c> thay vì mock Hangfire static).</item>
///   <item>Job được gọi đồng bộ trong test mà không cần Hangfire server.</item>
///   <item>Chuyển đổi backend queue (RabbitMQ/ServiceBus) sau này không sửa service.</item>
/// </list>
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Đăng ký job xử lý 1 Answer. Trả về Hangfire JobId.
    /// </summary>
    /// <param name="metadata">Metadata chuẩn — KHÔNG truyền entity EF trực tiếp.</param>
    /// <returns>JobId (dùng để track/dedup/cancel).</returns>
    string EnqueueAnswerProcessing(QueueJobMetadata metadata);

    /// <summary>
    /// Kiểm tra job đã tồn tại cho AnswerId chưa (tránh enqueue trùng khi user spam retry).
    /// </summary>
    bool HasPendingOrProcessingJob(int answerId);

    /// <summary>
    /// Huỷ job đang pending/processing (nếu user xoá answer trước khi xử lý xong).
    /// </summary>
    bool CancelByAnswerId(int answerId);

    /// <summary>
    /// Xếp một bài thi đã nộp vào hàng đợi chấm tuần tự.
    /// Trả về JobId, hoặc chuỗi rỗng khi bài này đã có job đang chờ hoặc đang chạy.
    /// </summary>
    string EnqueueSessionGrading(int resultId);

    /// <summary>
    /// Kiểm tra bài thi đã có job chấm đang chờ, đang retry hoặc đang chạy hay chưa.
    /// </summary>
    bool HasPendingSessionGrading(int resultId);
}
