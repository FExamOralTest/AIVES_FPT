using System.Linq.Expressions;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Hangfire implementation của <see cref="IQueueService"/>.
/// Singleton vì Hangfire client (BackgroundJob / IMonitoringApi) là thread-safe.
/// </summary>
public class HangfireQueueService : IQueueService
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<HangfireQueueService> _logger;
    private readonly IMonitoringApi? _injectedMonitoringApi;

    /// <summary>
    /// Số stripe khoá dùng cho bước "check dedup + enqueue". 100 bài nộp đồng thời
    /// vẫn có thể rơi vào cùng stripe, nhưng mỗi stripe chỉ serialize các answer
    /// cùng modulo — không chặn toàn bộ queue và không giữ lock vô hạn.
    /// </summary>
    private const int EnqueueLockStripes = 64;

    private static readonly object[] EnqueueLocks =
        Enumerable.Range(0, EnqueueLockStripes).Select(_ => new object()).ToArray();

    // IMonitoringApi is not registered in DI by Hangfire — resolve it on demand
    // via JobStorage.Current so the singleton can be constructed before the storage
    // is fully initialised. Allows passing custom IMonitoringApi for unit tests.
    private IMonitoringApi MonitoringApi => _injectedMonitoringApi ?? JobStorage.Current.GetMonitoringApi();

    public HangfireQueueService(
        IBackgroundJobClient jobClient,
        ILogger<HangfireQueueService> logger)
    {
        _jobClient = jobClient;
        _logger = logger;
    }

    public HangfireQueueService(
        IBackgroundJobClient jobClient,
        IMonitoringApi monitoringApi,
        ILogger<HangfireQueueService> logger)
    {
        _jobClient = jobClient;
        _injectedMonitoringApi = monitoringApi;
        _logger = logger;
    }

    public string EnqueueAnswerProcessing(QueueJobMetadata metadata)
    {
        // Task 1.4 — validate metadata theo Queue Job Contract trước khi enqueue.
        // Metadata vi phạm → job vô nghĩa, chỉ làm đốt compute và gây nhiễu dashboard.
        if (!QueueJobContract.TryValidate(metadata, out var contractError))
        {
            _logger.LogError(
                "Refusing to enqueue invalid job metadata: AnswerId={AnswerId}, Error={Error}",
                metadata?.AnswerId, contractError);
            throw new ArgumentException(contractError, nameof(metadata));
        }

        // Trước khi enqueue: kiểm tra trùng — tránh 2 job cùng xử lý 1 answer gây race.
        // 100 request submit đồng thời có thể cùng thấy "chưa có job" rồi enqueue 2 lần.
        // Khoá theo answer (striped lock) bảo đảm check + enqueue là nguyên tử trong process.
        var gate = EnqueueLocks[(int)((uint)metadata.AnswerId % EnqueueLockStripes)];
        lock (gate)
        {
            if (HasPendingOrProcessingJob(metadata.AnswerId))
            {
                _logger.LogWarning(
                    "Skipping enqueue: AnswerId={AnswerId} already has a pending/processing job.",
                    metadata.AnswerId);
                return string.Empty;
            }

            metadata.Status = QueueJobStatus.Pending;
            metadata.EnqueuedAt = DateTime.UtcNow;

            // Hangfire sẽ serialize metadata vào storage. Dùng Expression thay vì method invoke
            // để Hangfire có thể hiển thị method signature trên dashboard.
            //
            // QUAN TRỌNG: phải enqueue qua CONCRETE CLASS (AnswerProcessingJob), KHÔNG qua interface
            // (IAnswerProcessingJob). Hangfire đọc job filter từ `Job.Method.DeclaringType`; khi
            // khai báo expression trên interface, DeclaringType là interface nên các attribute
            // job-level (ví dụ [AutomaticRetry]) trên class implementation sẽ KHÔNG được áp dụng —
            // Hangfire fallback về policy mặc định (10 lần retry, mọi exception đều retry).
            // Điều này làm hỏng BR-17: lỗi không retryable (audio hỏng, transcript rỗng) vẫn bị retry.
            // Dùng Expression<Func<T, Task>> (không phải Action<T>) vì ExecuteAsync trả về Task —
            // Action<T> sẽ bỏ qua Task và sinh cảnh báo CS4014 (fire-and-forget).
            Expression<Func<AnswerProcessingJob, Task>> method = j => j.ExecuteAsync(metadata, default);
            var jobId = _jobClient.Enqueue(method);

            metadata.JobId = jobId;
            _logger.LogInformation(
                "Enqueued AnswerProcessingJob: JobId={JobId}, AnswerId={AnswerId}, StudentId={StudentId}, " +
                "SessionId={SessionId}, Queue={Queue}, Attempt={Attempt}",
                jobId, metadata.AnswerId, metadata.StudentId, metadata.SessionId,
                AnswerProcessingJob.QueueName, metadata.Attempt);
            return jobId;
        }
    }

    public bool HasPendingOrProcessingJob(int answerId)
    {
        // Hangfire cung cấp MonitoringApi để query job states.
        // Dùng ProcessingJobs + ScheduledJobs + EnqueuedJobs (lọc theo Args).
        // Vì Args chứa AnswerId trong metadata, ta dựa vào chuỗi ToString() của metadata
        // (xem QueueJobMetadata.ToString) để match job theo AnswerId.
        try
        {
            if (HasJobForAnswer(MonitoringApi.ProcessingJobs(0, 1000), j => j.Job, answerId))
                return true;

            // ScheduledJobs chứa cả job đang chờ retry (ScheduledState) sau khi job fail.
            if (HasJobForAnswer(MonitoringApi.ScheduledJobs(0, 1000), j => j.Job, answerId))
                return true;

            if (HasJobForAnswer(MonitoringApi.EnqueuedJobs("default", 0, 1000), j => j.Job, answerId))
                return true;

            if (HasJobForAnswer(MonitoringApi.EnqueuedJobs("answers", 0, 1000), j => j.Job, answerId))
                return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Hangfire monitoring API for AnswerId={AnswerId}. Defaulting to allow enqueue.", answerId);
            // Fail-open: nếu Hangfire down, vẫn cho enqueue để không block submit flow.
        }
        return false;
    }

    public bool CancelByAnswerId(int answerId)
    {
        // Tìm tất cả job pending/processing cho answer, gọi Delete.
        // Lưu ý: Hangfire KHÔNG cho cancel job đang chạy — chỉ xoá khỏi queue.
        // Với processing job, cách thực sự cancel là dùng CancellationToken trong job.
        try
        {
            var cancelled = false;

            foreach (var (jobId, info) in MonitoringApi.ProcessingJobs(0, 1000))
            {
                if (MatchesAnswerId(info.Job, answerId))
                    cancelled = _jobClient.Delete(jobId) || cancelled;
            }

            foreach (var (jobId, info) in MonitoringApi.ScheduledJobs(0, 1000))
            {
                if (MatchesAnswerId(info.Job, answerId))
                    cancelled = _jobClient.Delete(jobId) || cancelled;
            }

            foreach (var (jobId, info) in MonitoringApi.EnqueuedJobs("default", 0, 1000))
            {
                if (MatchesAnswerId(info.Job, answerId))
                    cancelled = _jobClient.Delete(jobId) || cancelled;
            }

            foreach (var (jobId, info) in MonitoringApi.EnqueuedJobs("answers", 0, 1000))
            {
                if (MatchesAnswerId(info.Job, answerId))
                    cancelled = _jobClient.Delete(jobId) || cancelled;
            }

            return cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cancel jobs for AnswerId={AnswerId}.", answerId);
            return false;
        }
    }

    public string EnqueueSessionGrading(int resultId)
    {
        if (resultId <= 0)
            throw new ArgumentOutOfRangeException(nameof(resultId), "ResultId must be positive.");

        if (HasPendingSessionGrading(resultId))
        {
            _logger.LogWarning(
                "Skipping session grading enqueue: ResultId={ResultId} already has a pending job.",
                resultId);
            return string.Empty;
        }

        // Enqueue qua concrete class để [AutomaticRetry] và [Queue("grading")] được áp dụng.
        // PerformContext và CancellationToken do Hangfire thay lúc chạy; null ở expression là chủ đích.
        Expression<Func<SessionGradingJob, Task>> method = j => j.ExecuteAsync(resultId, null!, default);
        var jobId = _jobClient.Enqueue(method);
        _logger.LogInformation(
            "Enqueued SessionGradingJob: JobId={JobId}, ResultId={ResultId}, Queue={Queue}",
            jobId, resultId, SessionGradingJob.QueueName);
        return jobId;
    }

    public bool HasPendingSessionGrading(int resultId)
    {
        if (resultId <= 0)
            return false;

        try
        {
            return FindActiveSessionGradingJobId(resultId) != null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to query Hangfire for session grading ResultId={ResultId}. Allowing enqueue.",
                resultId);
        }

        return false;
    }

    public QueueJobStateDto? GetJobState(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return null;

        try
        {
            var details = MonitoringApi.JobDetails(jobId);
            if (details == null)
                return null;

            // History có thể chứa nhiều state; state mới nhất là state hiện tại.
            var latest = details.History?
                .Where(item => item != null)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault();

            var stateName = latest?.StateName ?? "Unknown";
            var reason = latest?.Reason;
            if (!string.IsNullOrWhiteSpace(reason) && reason.Length > QueueJobContract.MaxLastErrorLength)
                reason = reason[..QueueJobContract.MaxLastErrorLength];

            return new QueueJobStateDto
            {
                JobId = jobId,
                State = stateName,
                Status = QueueJobContract.MapHangfireState(stateName),
                JobType = details.Job?.Type?.Name,
                CreatedAt = details.CreatedAt,
                StateChangedAt = latest?.CreatedAt,
                Error = string.IsNullOrWhiteSpace(reason) ? null : reason
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Hangfire job state for JobId={JobId}.", jobId);
            return null;
        }
    }

    public QueueJobStateDto? GetSessionGradingState(int resultId)
    {
        if (resultId <= 0)
            return null;

        try
        {
            var jobId = FindActiveSessionGradingJobId(resultId);
            return jobId == null ? null : GetJobState(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to read session grading state for ResultId={ResultId}.", resultId);
            return null;
        }
    }

    /// <summary>
    /// Tìm JobId của job chấm bài đang chờ/đang chạy cho một Result.
    /// Chỉ quét các state còn hoạt động; job đã xong được suy ra từ DB
    /// (<c>results.ai_evaluation_status</c>) ở tầng status service.
    /// </summary>
    private string? FindActiveSessionGradingJobId(int resultId)
    {
        foreach (var (jobId, info) in MonitoringApi.ProcessingJobs(0, 1000))
        {
            if (MatchesSessionResult(info.Job, resultId))
                return jobId;
        }

        // ScheduledJobs chứa job đang chờ retry sau khi fail.
        foreach (var (jobId, info) in MonitoringApi.ScheduledJobs(0, 1000))
        {
            if (MatchesSessionResult(info.Job, resultId))
                return jobId;
        }

        foreach (var (jobId, info) in MonitoringApi.EnqueuedJobs(SessionGradingJob.QueueName, 0, 1000))
        {
            if (MatchesSessionResult(info.Job, resultId))
                return jobId;
        }

        return null;
    }

    /// <summary>
    /// Kiểm tra danh sách job có chứa job của answer hay không.
    /// </summary>
    private static bool HasJobForAnswer<TJob>(
        IEnumerable<KeyValuePair<string, TJob>> jobs,
        Func<TJob, Hangfire.Common.Job> jobSelector,
        int answerId) =>
        jobs.Any(j => MatchesAnswerId(jobSelector(j.Value), answerId));

    /// <summary>
    /// Match chính xác AnswerId trong chuỗi args của job.
    /// Phải so sánh theo ranh giới số để "AnswerId = 77" không khớp "AnswerId = 777".
    /// </summary>
    private static bool MatchesAnswerId(Hangfire.Common.Job job, int answerId)
    {
        var pattern = $"AnswerId = {answerId}";
        foreach (var arg in job.Args)
        {
            var text = arg?.ToString();
            if (text == null) continue;

            var index = text.IndexOf(pattern, StringComparison.Ordinal);
            while (index >= 0)
            {
                var end = index + pattern.Length;
                // Đảm bảo không có chữ số nào đứng ngay sau AnswerId (tránh match 77 với 777).
                var isExact = end >= text.Length || !char.IsDigit(text[end]);
                if (isExact) return true;

                index = text.IndexOf(pattern, index + 1, StringComparison.Ordinal);
            }
        }
        return false;
    }

    /// <summary>
    /// Chỉ khớp job chấm cả bài, và chỉ khớp đúng ResultId ở argument đầu.
    /// Job chấm từng câu trả lời không được tính, dù AnswerId trùng số.
    /// </summary>
    private static bool MatchesSessionResult(Hangfire.Common.Job? job, int resultId)
    {
        if (job?.Method?.DeclaringType != typeof(SessionGradingJob) || job.Args.Count == 0)
            return false;

        return job.Args[0] switch
        {
            int value => value == resultId,
            long value => value == resultId,
            short value => value == resultId,
            string value when int.TryParse(value, out var parsed) => parsed == resultId,
            _ => false
        };
    }
}
