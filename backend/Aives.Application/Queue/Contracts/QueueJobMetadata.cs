namespace Aives.Application.Queue.Contracts;

/// <summary>
/// Metadata chuẩn cho mỗi background job xử lý <c>Answer</c>.
/// Là DTO truyền vào Hangfire job — phải [Serializable] và ổn định giữa các version.
/// </summary>
/// <remarks>
/// Tại sao cần DTO riêng (không truyền entity trực tiếp)?
/// <list type="bullet">
///   <item>Entity EF thay đổi theo migration → phá vỡ job đang pending trong queue.</item>
///   <item>Hangfire serialize toàn bộ graph → payload phình, hash job trùng sai.</item>
///   <item>Cần metadata filter/log (attempt, objectKey) mà entity không expose.</item>
/// </list>
/// </remarks>
[Serializable]
public class QueueJobMetadata
{
    /// <summary>Hangfire JobId — dùng để dedup, query dashboard, cancel.</summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>PK của record <c>Answer</c> đang xử lý.</summary>
    public int AnswerId { get; set; }

    /// <summary>
    /// PK của sinh viên sở hữu answer. Được ghi vào metadata ngay tại call site
    /// (upload audio / upload chunked / retry) để worker đối chiếu lại với DB
    /// trước khi xử lý. Bảo vệ chống "nhầm sinh viên" khi 100 bài nộp đồng thời:
    /// job chỉ được chạy nếu <c>Answer.Result.StudentId</c> khớp giá trị này.
    /// </summary>
    /// <remarks>
    /// Nullable để job cũ (enqueue trước khi nâng cấp) vẫn deserialize được.
    /// Job không có <see cref="StudentId"/> và <see cref="SessionId"/> sẽ bỏ qua
    /// bước kiểm tra ownership (tương thích ngược).
    /// </remarks>
    public int? StudentId { get; set; }

    /// <summary>
    /// PK của ca thi sở hữu answer. Worker đối chiếu với <c>Answer.Result.SessionId</c>
    /// để chắc chắn audio/job không bị gán chéo sang ca thi khác.
    /// </summary>
    public int? SessionId { get; set; }

    /// <summary>
    /// PK của record <c>Result</c> liên kết. Có thể null ở attempt đầu (chưa tạo row result)
    /// hoặc khi result trước đó đã bị xoá.
    /// </summary>
    public int? ResultId { get; set; }

    /// <summary>
    /// Object Key trong R2, vd: <c>sessions/12/answers/45.webm</c>.
    /// Worker dùng key này để download audio trước khi gửi sang AI1.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// Lần thử hiện tại (1-based).
    /// </summary>
    /// <remarks>
    /// Hangfire serialize lại metadata ở mỗi lần retry nên giá trị này KHÔNG tự tăng.
    /// Nguồn sự thật của "lần thử thứ mấy" là <c>answers.attempt_count</c> trong DB —
    /// xem <c>QueueLimits.MaxTotalAttempts</c> và <c>IAnswerRepository.IncrementAttemptAsync</c>.
    /// Giá trị ở đây chỉ để log/audit và hiển thị trên dashboard.
    /// </remarks>
    public int Attempt { get; set; } = 1;

    /// <summary>Trạng thái hiện tại của job trong vòng đời.</summary>
    public QueueJobStatus Status { get; set; } = QueueJobStatus.Pending;

    /// <summary>Thời điểm job được enqueue (UTC).</summary>
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Lý do thất bại gần nhất (message ngắn). Null khi job chưa từng fail.
    /// Không log raw exception ở đây — exception đầy đủ đi vào logger.
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Format ngắn dùng cho log và Hangfire args filtering.
    /// <c>HangfireQueueService.HasPendingOrProcessingJob</c> dựa vào chuỗi này
    /// để match job theo AnswerId — nếu đổi format, cần cập nhật cả 2 nơi.
    /// </summary>
    public override string ToString() =>
        $"QueueJobMetadata {{ JobId = {JobId}, AnswerId = {AnswerId}, ResultId = {ResultId}, " +
        $"StudentId = {StudentId}, SessionId = {SessionId}, Attempt = {Attempt}, Status = {Status} }}";
}
