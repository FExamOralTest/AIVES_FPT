using Aives.Application.Queue.Contracts;
using Aives.Application.Viva.DTOs;
using Aives.Application.Viva.Interfaces;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Hangfire job that grades one submitted exam. Every job shares one distributed lock and
/// the <c>grading</c> queue runs a single worker, so a burst of submissions is graded
/// one exam at a time.
/// </summary>
[AutomaticRetry(
    Attempts = QueueLimits.MaxTotalAttempts - 1,
    DelaysInSeconds = new[]
    {
        QueueLimits.RetryDelay1Seconds,
        QueueLimits.RetryDelay2Seconds,
        QueueLimits.RetryDelay3Seconds
    },
    OnAttemptsExceeded = AttemptsExceededAction.Delete,
    OnlyOn = new[] { typeof(SessionGradingJob.RetryableJobException) })]
public class SessionGradingJob
{
    public const string QueueName = "grading";

    /// <summary>
    /// How long a worker waits for the previous exam to finish before giving up the lock.
    /// Long enough for a room of about 50 exams, including retries.
    /// </summary>
    public const int LockTimeoutSeconds = 12 * 60 * 60;

    private readonly ISessionGradingService _grading;
    private readonly ILogger<SessionGradingJob> _logger;

    public SessionGradingJob(ISessionGradingService grading, ILogger<SessionGradingJob> logger)
    {
        _grading = grading;
        _logger = logger;
    }

    [Queue(QueueName)]
    [SessionGradingLock(LockTimeoutSeconds)]
    public async Task ExecuteAsync(int resultId, PerformContext? context, CancellationToken cancellationToken)
    {
        if (resultId <= 0)
            throw new NonRetryableJobException($"Invalid result id {resultId}.");

        var attempt = ResolveAttempt(context);
        var finalAttempt = attempt >= QueueLimits.MaxTotalAttempts;
        _logger.LogInformation(
            "SessionGradingJob starting: ResultId={ResultId}, Attempt={Attempt}/{MaxAttempts}",
            resultId, attempt, QueueLimits.MaxTotalAttempts);

        SessionGradingResult grading;
        try
        {
            grading = await _grading.GradeAsync(resultId, finalAttempt, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is not RetryableJobException and not NonRetryableJobException)
        {
            _logger.LogError(ex, "SessionGradingJob crashed: ResultId={ResultId}, Attempt={Attempt}", resultId, attempt);
            if (finalAttempt)
                await _grading.MarkFailedAsync(resultId, ex.Message, CancellationToken.None);
            throw new RetryableJobException(ex.Message);
        }

        if (grading.Success)
        {
            _logger.LogInformation("SessionGradingJob completed: ResultId={ResultId}, Attempt={Attempt}", resultId, attempt);
            return;
        }

        if (!grading.Retryable || finalAttempt)
        {
            _logger.LogError(
                "SessionGradingJob stopped: ResultId={ResultId}, Code={Code}, Message={Message}",
                resultId, grading.ErrorCode, grading.Message);
            throw new NonRetryableJobException($"Grading error [{grading.ErrorCode}]: {grading.Message}");
        }

        _logger.LogWarning(
            "SessionGradingJob will retry: ResultId={ResultId}, Attempt={Attempt}, Message={Message}",
            resultId, attempt, grading.Message);
        throw new RetryableJobException($"Retryable grading error for result {resultId}: {grading.Message}");
    }

    internal static int ResolveAttempt(PerformContext? context)
    {
        if (context == null)
            return 1;

        var retryCount = context.GetJobParameter<int>("RetryCount");
        return Math.Max(1, retryCount + 1);
    }

    public class NonRetryableJobException : Exception
    {
        public NonRetryableJobException(string message) : base(message) { }
    }

    public class RetryableJobException : Exception
    {
        public RetryableJobException(string message) : base(message) { }
    }
}

/// <summary>
/// One shared lock for every exam-grading job. The built-in Hangfire concurrency filter
/// keys the lock by arguments, which would still let different exams run together.
/// </summary>
public sealed class SessionGradingLockAttribute : JobFilterAttribute, IServerFilter
{
    private const string LockItem = "SessionGradingLock";
    private readonly TimeSpan _timeout;

    public SessionGradingLockAttribute(int timeoutInSeconds)
    {
        _timeout = TimeSpan.FromSeconds(timeoutInSeconds);
    }

    public void OnPerforming(PerformingContext context)
    {
        var distributedLock = context.Connection.AcquireDistributedLock("aives:session-grading", _timeout);
        context.Items[LockItem] = distributedLock;
    }

    public void OnPerformed(PerformedContext context)
    {
        if (context.Items.TryGetValue(LockItem, out var handle) && handle is IDisposable distributedLock)
            distributedLock.Dispose();
    }
}
