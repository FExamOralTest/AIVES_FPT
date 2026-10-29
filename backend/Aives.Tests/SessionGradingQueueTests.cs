using System.Reflection;
using Aives.Application.Queue.Contracts;
using Aives.Application.Viva.DTOs;
using Aives.Application.Viva.Interfaces;
using Aives.Infrastructure.Queue;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// A burst of finished exams is stored as one Hangfire job per exam on the grading queue.
/// The worker processes that queue one exam at a time.
/// </summary>
public class SessionGradingQueueTests
{
    [Fact]
    public void BurstOfFiftyExams_EnqueuesEachExamOnceOnTheGradingQueue()
    {
        var storage = NewStorage();
        JobStorage.Current = storage;
        try
        {
            var queue = new HangfireQueueService(
                new BackgroundJobClient(storage),
                NullLogger<HangfireQueueService>.Instance);

            var jobIds = new List<string>();
            for (var resultId = 1; resultId <= 50; resultId++)
                jobIds.Add(queue.EnqueueSessionGrading(resultId));

            Assert.Equal(50, jobIds.Distinct().Count());
            Assert.All(jobIds, id => Assert.False(string.IsNullOrEmpty(id)));
            Assert.Equal(string.Empty, queue.EnqueueSessionGrading(50));

            var enqueued = storage.GetMonitoringApi().EnqueuedJobs(SessionGradingJob.QueueName, 0, 100);
            Assert.Equal(50, enqueued.Count);
            Assert.Equal(50, enqueued.Select(job => job.Value.Job.Args[0]).Distinct().Count());
        }
        finally
        {
            try { JobStorage.Current = null!; } catch { /* Hangfire rejects a null current storage. */ }
        }
    }

    [Fact]
    public void AnswerJobWithTheSameNumber_DoesNotBlockSessionGrading()
    {
        var storage = NewStorage();
        JobStorage.Current = storage;
        try
        {
            var queue = new HangfireQueueService(
                new BackgroundJobClient(storage),
                NullLogger<HangfireQueueService>.Instance);
            var metadata = new QueueJobMetadata { AnswerId = 42, ResultId = 7, ObjectKey = "audio/a.wav", Attempt = 1 };

            var answerJobId = queue.EnqueueAnswerProcessing(metadata);
            var gradingJobId = queue.EnqueueSessionGrading(42);

            Assert.False(string.IsNullOrEmpty(answerJobId));
            Assert.False(string.IsNullOrEmpty(gradingJobId));
            Assert.NotEqual(answerJobId, gradingJobId);
        }
        finally
        {
            try { JobStorage.Current = null!; } catch { /* Hangfire rejects a null current storage. */ }
        }
    }

    [Fact]
    public void GradingJob_IsConcreteAndLockedToTheGradingQueue()
    {
        var method = typeof(SessionGradingJob).GetMethod(nameof(SessionGradingJob.ExecuteAsync));
        Assert.NotNull(method);

        var queue = method!.GetCustomAttribute<QueueAttribute>();
        Assert.NotNull(queue);
        Assert.Equal(SessionGradingJob.QueueName, queue!.Queue);

        Assert.NotNull(method.GetCustomAttribute<SessionGradingLockAttribute>());

        var retry = typeof(SessionGradingJob).GetCustomAttribute<AutomaticRetryAttribute>();
        Assert.NotNull(retry);
        Assert.Equal(QueueLimits.MaxTotalAttempts - 1, retry!.Attempts);
        Assert.Contains(typeof(SessionGradingJob.RetryableJobException), retry.OnlyOn);
    }

    [Fact]
    public async Task Job_SucceedsWithoutRetry_WhenGradingSucceeds()
    {
        var grading = new Mock<ISessionGradingService>();
        grading.Setup(service => service.GradeAsync(15, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionGradingResult.Completed());
        var job = new SessionGradingJob(grading.Object, NullLogger<SessionGradingJob>.Instance);

        await job.ExecuteAsync(15, null, CancellationToken.None);

        grading.Verify(service => service.GradeAsync(15, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Job_ThrowsRetryable_WhenEvaluationCanBeRetried()
    {
        var grading = new Mock<ISessionGradingService>();
        grading.Setup(service => service.GradeAsync(15, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionGradingResult.Failed("timeout", "AI timed out.", retryable: true));
        var job = new SessionGradingJob(grading.Object, NullLogger<SessionGradingJob>.Instance);

        var error = await Assert.ThrowsAsync<SessionGradingJob.RetryableJobException>(
            () => job.ExecuteAsync(15, null, CancellationToken.None));

        Assert.Contains("AI timed out.", error.Message);
    }

    [Fact]
    public async Task Job_DoesNotRetry_WhenEvaluationCannotBeRetried()
    {
        var grading = new Mock<ISessionGradingService>();
        grading.Setup(service => service.GradeAsync(15, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionGradingResult.Failed("evaluation_failed", "The answer is invalid.", retryable: false));
        var job = new SessionGradingJob(grading.Object, NullLogger<SessionGradingJob>.Instance);

        await Assert.ThrowsAsync<SessionGradingJob.NonRetryableJobException>(
            () => job.ExecuteAsync(15, null, CancellationToken.None));
    }

    private static MemoryStorage NewStorage() => new(new MemoryStorageOptions
    {
        FetchNextJobTimeout = TimeSpan.FromMilliseconds(50)
    });
}
