using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;

namespace Aives.Tests;

/// <summary>
/// Fake IQueueService — không gọi Hangfire, chỉ track job enqueue để test dedup.
/// </summary>
internal class FakeQueueService : IQueueService
{
    public List<QueueJobMetadata> EnqueuedJobs { get; } = new();

    public string EnqueueAnswerProcessing(QueueJobMetadata metadata)
    {
        if (HasPendingOrProcessingJob(metadata.AnswerId))
            return string.Empty;

        metadata.JobId = $"job-{Guid.NewGuid():N}";
        EnqueuedJobs.Add(metadata);
        return metadata.JobId;
    }

    public bool HasPendingOrProcessingJob(int answerId) =>
        EnqueuedJobs.Any(j => j.AnswerId == answerId);

    public bool CancelByAnswerId(int answerId)
    {
        var count = EnqueuedJobs.RemoveAll(j => j.AnswerId == answerId);
        return count > 0;
    }

    public List<int> EnqueuedSessionGradings { get; } = new();

    public string EnqueueSessionGrading(int resultId)
    {
        if (resultId <= 0)
            throw new ArgumentOutOfRangeException(nameof(resultId));
        if (HasPendingSessionGrading(resultId))
            return string.Empty;

        EnqueuedSessionGradings.Add(resultId);
        return $"grade-{resultId}";
    }

    public bool HasPendingSessionGrading(int resultId) =>
        EnqueuedSessionGradings.Contains(resultId);
}
