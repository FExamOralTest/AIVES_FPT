using System.Linq.Expressions;
using Aives.Application.Queue.Contracts;
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
        // Trước khi enqueue: kiểm tra trùng — tránh 2 job cùng xử lý 1 answer gây race.
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
        Expression<Action<IAnswerProcessingJob>> method = j => j.ExecuteAsync(metadata, default);
        var jobId = _jobClient.Enqueue(method);

        metadata.JobId = jobId;
        _logger.LogInformation(
            "Enqueued AnswerProcessingJob: JobId={JobId}, AnswerId={AnswerId}, Attempt={Attempt}",
            jobId, metadata.AnswerId, metadata.Attempt);
        return jobId;
    }

    public bool HasPendingOrProcessingJob(int answerId)
    {
        // Hangfire cung cấp MonitoringApi để query job states.
        // Dùng ProcessingJobs + ScheduledJobs + EnqueuedJobs (lọc theo Args).
        // Vì Args chứa AnswerId trong metadata, ta dựa vào tag/filter — đơn giản nhất là
        // thêm tag = "answer:{id}" qua JobFilter (xem HangfireJobTagsAttribute).
        // Ở đây dùng tổng quát: query list, filter theo args.Contains.
        try
        {
            var processing = MonitoringApi.ProcessingJobs(0, 1000);
            if (processing.Any(j => j.Value.Job.Args.Any(a => a?.ToString()?.Contains($"AnswerId = {answerId}") == true)))
                return true;

            var scheduled = MonitoringApi.ScheduledJobs(0, 1000);
            if (scheduled.Any(j => j.Value.Job.Args.Any(a => a?.ToString()?.Contains($"AnswerId = {answerId}") == true)))
                return true;

            var enqueued = MonitoringApi.EnqueuedJobs("default", 0, 1000);
            if (enqueued.Any(j => j.Value.Job.Args.Any(a => a?.ToString()?.Contains($"AnswerId = {answerId}") == true)))
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
            var processing = MonitoringApi.ProcessingJobs(0, 1000);
            foreach (var (jobId, info) in processing)
            {
                if (info.Job.Args.Any(a => a?.ToString()?.Contains($"AnswerId = {answerId}") == true))
                {
                    cancelled = _jobClient.Delete(jobId) || cancelled;
                }
            }
            var scheduled = MonitoringApi.ScheduledJobs(0, 1000);
            foreach (var (jobId, info) in scheduled)
            {
                if (info.Job.Args.Any(a => a?.ToString()?.Contains($"AnswerId = {answerId}") == true))
                {
                    cancelled = _jobClient.Delete(jobId) || cancelled;
                }
            }
            var enqueued = MonitoringApi.EnqueuedJobs("default", 0, 1000);
            foreach (var (jobId, info) in enqueued)
            {
                if (info.Job.Args.Any(a => a?.ToString()?.Contains($"AnswerId = {answerId}") == true))
                {
                    cancelled = _jobClient.Delete(jobId) || cancelled;
                }
            }
            return cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cancel jobs for AnswerId={AnswerId}.", answerId);
            return false;
        }
    }
}
