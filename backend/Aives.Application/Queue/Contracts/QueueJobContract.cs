namespace Aives.Application.Queue.Contracts;

/// <summary>
/// Task 1.4 — Queue Job Contract.
/// Chốt metadata chuẩn của 1 background job xử lý <c>Answer</c>: <c>jobId</c>,
/// <c>answerId</c>, <c>resultId</c>, <c>objectKey</c>, <c>attempt</c> và trạng thái job.
/// </summary>
/// <remarks>
/// <para><b>Các trường bắt buộc và nguồn sự thật</b></para>
/// <list type="bullet">
///   <item><c>jobId</c> — do Hangfire sinh, gán sau khi enqueue. Lưu thêm vào
///         <c>answers.job_id</c> để tra cứu job không chỉ từ Hangfire dashboard.</item>
///   <item><c>answerId</c> — PK của <c>Answer</c>. Bắt buộc > 0.</item>
///   <item><c>resultId</c> — PK của <c>Result</c>. Có thể null (chưa tạo row result).</item>
///   <item><c>objectKey</c> — key audio trong R2. Bắt buộc khi xử lý qua đường STT;
///         rỗng khi answer chỉ có text.</item>
///   <item><c>attempt</c> — <b>KHÔNG phải</b> nguồn sự thật. Hangfire serialize lại metadata
///         ở mỗi lần retry nên giá trị này không tự tăng. Nguồn sự thật là
///         <c>answers.attempt_count</c> (xem <c>IAnswerRepository.IncrementAttemptAsync</c>).</item>
///   <item><c>status</c> — trạng thái vòng đời job, map 1-1 với trạng thái Hangfire
///         (xem <see cref="MapHangfireState"/>) và với trạng thái của <c>Answer</c>.</item>
/// </list>
/// <para><b>Vì sao cần contract riêng?</b></para>
/// <list type="bullet">
///   <item>Job đang pending trong queue khi deploy version mới không được bị phá vỡ
///         → payload phải ổn định, không chứa entity EF.</item>
///   <item>Retry / dead-letter là quyết định hậu quả → phải suy ra từ 1 nguồn duy nhất
///         (<c>QueueLimits</c> + <c>answers.attempt_count</c>).</item>
///   <item>Dedup chống job trùng dựa vào <c>answerId</c> → phải validate metadata
///         trước khi enqueue để tránh job vô nghĩa.</item>
/// </list>
/// </remarks>
public static class QueueJobContract
{
    /// <summary>
    /// Độ dài tối đa của <c>objectKey</c> trong R2. Key quá dài thường là dấu hiệu
    /// dữ liệu bị nhập sai (vd: nhét cả base64 vào key).
    /// </summary>
    public const int MaxObjectKeyLength = 512;

    /// <summary>
    /// Độ dài tối đa của <c>jobId</c> (Hangfire GUID dài 22 ký tự — lấy biên an toàn).
    /// </summary>
    public const int MaxJobIdLength = 128;

    /// <summary>
    /// Độ dài tối đa của <c>lastError</c> lưu trong metadata (message ngắn, không phải stack trace).
    /// </summary>
    public const int MaxLastErrorLength = 500;

    /// <summary>
    /// Validate metadata trước khi enqueue.
    /// </summary>
    /// <param name="metadata">Metadata sắp được enqueue.</param>
    /// <param name="error">Thông điệp lỗi ngắn nếu vi phạm.</param>
    /// <returns><c>true</c> nếu hợp lệ.</returns>
    public static bool TryValidate(QueueJobMetadata? metadata, out string? error)
    {
        error = null;

        if (metadata == null)
        {
            error = "Queue job metadata is required.";
            return false;
        }

        if (metadata.AnswerId <= 0)
        {
            error = "Queue job metadata requires a positive AnswerId.";
            return false;
        }

        if (metadata.ResultId is <= 0)
        {
            error = "Queue job metadata ResultId must be positive when provided.";
            return false;
        }

        if (metadata.Attempt < 1)
        {
            error = "Queue job metadata Attempt must be 1-based (>= 1).";
            return false;
        }

        if (metadata.JobId.Length > MaxJobIdLength)
        {
            error = $"Queue job metadata JobId exceeds {MaxJobIdLength} characters.";
            return false;
        }

        if (metadata.ObjectKey.Length > MaxObjectKeyLength)
        {
            error = $"Queue job metadata ObjectKey exceeds {MaxObjectKeyLength} characters.";
            return false;
        }

        if (metadata.LastError is { Length: > MaxLastErrorLength })
        {
            error = $"Queue job metadata LastError exceeds {MaxLastErrorLength} characters.";
            return false;
        }

        if (!Enum.IsDefined(metadata.Status))
        {
            error = $"Queue job metadata Status '{metadata.Status}' is not a defined {nameof(QueueJobStatus)}.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validate và ném exception nếu vi phạm contract.
    /// </summary>
    /// <exception cref="ArgumentException">Metadata không hợp lệ.</exception>
    public static void Validate(QueueJobMetadata? metadata)
    {
        if (!TryValidate(metadata, out var error))
            throw new ArgumentException(error, nameof(metadata));
    }

    /// <summary>
    /// Map trạng thái Hangfire sang trạng thái job của hợp đồng.
    /// </summary>
    /// <remarks>
    /// Hangfire dùng state name (string) cho các state built-in. State không xác định
    /// (vd: state của một storage plugin khác) được map về <see cref="QueueJobStatus.Pending"/>
    /// để không làm hỏng dashboard.
    /// </remarks>
    public static QueueJobStatus MapHangfireState(string? stateName) =>
        stateName?.ToLowerInvariant() switch
        {
            "enqueued" or "scheduled" or "created" or "waiting" => QueueJobStatus.Pending,
            "processing" => QueueJobStatus.Processing,
            "succeeded" => QueueJobStatus.Completed,
            "failed" => QueueJobStatus.Failed,
            "deleted" => QueueJobStatus.DeadLetter,
            _ => QueueJobStatus.Pending
        };

    /// <summary>
    /// Trạng thái job cuối cùng — không còn retry được nữa, cần can thiệp thủ công.
    /// </summary>
    public static bool IsTerminal(QueueJobStatus status) =>
        status is QueueJobStatus.Completed or QueueJobStatus.DeadLetter;

    /// <summary>
    /// Job còn có thể được retry tự động không?
    /// </summary>
    /// <param name="status">Trạng thái hiện tại của job.</param>
    /// <param name="attemptCount">Giá trị hiện tại của <c>answers.attempt_count</c>.</param>
    public static bool CanRetry(QueueJobStatus status, int attemptCount) =>
        status == QueueJobStatus.Failed && !QueueLimits.IsExhausted(attemptCount);

    /// <summary>
    /// Tạo metadata chuẩn cho 1 job mới.
    /// </summary>
    /// <remarks>
    /// Factory tập trung để mọi nơi gọi enqueue (upload audio, retry thủ công) sinh ra
    /// metadata giống hệt nhau — tránh mỗi chỗ tự set field riêng và lệch contract.
    /// </remarks>
    /// <param name="answerId">PK của Answer.</param>
    /// <param name="resultId">PK của Result (có thể null).</param>
    /// <param name="objectKey">Key audio trong R2 (có thể rỗng nếu answer chỉ có text).</param>
    /// <param name="attempt">Lần thử tiếp theo (1-based).</param>
    public static QueueJobMetadata Create(int answerId, int? resultId, string? objectKey, int attempt)
    {
        var metadata = new QueueJobMetadata
        {
            AnswerId = answerId,
            ResultId = resultId,
            ObjectKey = objectKey ?? string.Empty,
            Attempt = attempt < 1 ? 1 : attempt,
            Status = QueueJobStatus.Pending,
            EnqueuedAt = DateTime.UtcNow
        };

        Validate(metadata);
        return metadata;
    }
}
