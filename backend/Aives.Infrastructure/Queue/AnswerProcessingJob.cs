using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Hangfire job that runs <see cref="IAnswerOrchestrator"/> for a single answer.
/// Non-static class with a public method so Hangfire can resolve it from the DI scope.
/// </summary>
/// <remarks>
/// Retry policy (BR-17 — "retry at most 3 total attempts"):
/// <list type="bullet">
///   <item>The attempt number is counted in the database (<c>answers.attempt_count</c>), not in the
///         Hangfire metadata, because Hangfire re-serializes the metadata on every retry so
///         <c>metadata.Attempt</c> never increases on its own.</item>
///   <item>Retryable failures (STT timeout/internal, transient evaluation errors) rethrow so that
///         Hangfire retries them with the configured backoff.</item>
///   <item>Non-retryable failures (corrupted audio, empty transcript, business evaluation errors)
///         throw <see cref="NonRetryableJobException"/>. The <c>AutomaticRetry</c> filter is limited
///         to <see cref="RetryableJobException"/>, so those failures are never retried and the job
///         goes straight to the dead-letter state.</item>
///   <item>When the attempt budget is exhausted the job is aborted and the answer is marked as
///         failed, so no further automatic or manual retry is possible.</item>
/// </list>
/// </remarks>
[AutomaticRetry(
    Attempts = QueueLimits.MaxTotalAttempts - 1,
    DelaysInSeconds = new[]
    {
        QueueLimits.RetryDelay1Seconds,
        QueueLimits.RetryDelay2Seconds,
        QueueLimits.RetryDelay3Seconds
    },
    OnAttemptsExceeded = AttemptsExceededAction.Delete,
    OnlyOn = new[] { typeof(AnswerProcessingJob.RetryableJobException) })]
public class AnswerProcessingJob : IAnswerProcessingJob
{
    private readonly IAnswerOrchestrator _orchestrator;
    private readonly IAnswerRepository _answerRepository;
    private readonly ILogger<AnswerProcessingJob> _logger;

    public AnswerProcessingJob(
        IAnswerOrchestrator orchestrator,
        IAnswerRepository answerRepository,
        ILogger<AnswerProcessingJob> logger)
    {
        _orchestrator = orchestrator;
        _answerRepository = answerRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(QueueJobMetadata metadata, CancellationToken cancellationToken)
    {
        // Task 1.4 — validate metadata theo Queue Job Contract.
        // Job được enqueue từ version cũ có thể thiếu field → abort ngay thay vì
        // chạy pipeline với dữ liệu rác.
        if (!QueueJobContract.TryValidate(metadata, out var contractError))
        {
            metadata.Status = QueueJobStatus.DeadLetter;
            metadata.LastError = contractError;
            _logger.LogError(
                "AnswerProcessingJob aborted: invalid job metadata for AnswerId={AnswerId}. Error={Error}",
                metadata?.AnswerId, contractError);
            throw new NonRetryableJobException($"Invalid job metadata: {contractError}");
        }

        // 1. Increase the attempt counter in the database — the single source of truth (BR-17).
        var attempt = await _answerRepository.IncrementAttemptAsync(
            metadata.AnswerId, metadata.JobId, cancellationToken);

        if (attempt == 0)
        {
            // The answer was deleted between enqueue and execution.
            metadata.Status = QueueJobStatus.DeadLetter;
            metadata.LastError = "Answer not found.";
            _logger.LogError(
                "AnswerProcessingJob aborted: AnswerId={AnswerId} not found.", metadata.AnswerId);
            throw new NonRetryableJobException($"Answer {metadata.AnswerId} not found.");
        }

        // Keep the metadata in sync with the database for logs and the dashboard.
        metadata.Attempt = attempt;

        _logger.LogInformation(
            "AnswerProcessingJob starting: AnswerId={AnswerId}, Attempt={Attempt}/{MaxAttempts}, JobId={JobId}",
            metadata.AnswerId, attempt, QueueLimits.MaxTotalAttempts, metadata.JobId);

        // 2. The attempt budget is exhausted — abort without running the pipeline again.
        if (attempt > QueueLimits.MaxTotalAttempts)
        {
            metadata.Status = QueueJobStatus.DeadLetter;
            metadata.LastError = $"Maximum attempts ({QueueLimits.MaxTotalAttempts}) exceeded.";
            await _answerRepository.MarkFailedAsync(
                metadata.AnswerId, metadata.LastError, cancellationToken);
            _logger.LogError(
                "AnswerProcessingJob aborted: AnswerId={AnswerId} exceeded max attempts ({MaxAttempts}).",
                metadata.AnswerId, QueueLimits.MaxTotalAttempts);
            throw new NonRetryableJobException(
                $"Answer {metadata.AnswerId} exceeded maximum attempts ({QueueLimits.MaxTotalAttempts}).");
        }

        var result = await _orchestrator.ProcessAnswerAsync(metadata.AnswerId, attempt, cancellationToken);

        if (result.Success)
        {
            metadata.Status = QueueJobStatus.Completed;
            _logger.LogInformation(
                "AnswerProcessingJob completed: AnswerId={AnswerId}, Attempt={Attempt}",
                metadata.AnswerId, attempt);
            return;
        }

        // 3. Non-retryable STT failures (corrupted audio, empty transcript) — abort immediately.
        if (result.SttError is { Retryable: false })
        {
            metadata.Status = QueueJobStatus.DeadLetter;
            metadata.LastError = result.SttError.Message;
            _logger.LogError(
                "AnswerProcessingJob dead-lettered (non-retryable): AnswerId={AnswerId}, Code={Code}, Message={Message}",
                metadata.AnswerId, result.SttError.Code, result.SttError.Message);
            throw new NonRetryableJobException(
                $"STT error [{result.SttError.Code}]: {result.SttError.Message}");
        }

        // 4. Evaluation failures.
        //    - Transient errors (timeout / connection_error / internal_error) are retryable.
        //    - Business errors (invalid response, score out of range…) always produce the same
        //      outcome, so they are dead-lettered immediately.
        if (!string.IsNullOrEmpty(result.EvaluationErrorCode))
        {
            if (!QueueLimits.IsRetryableEvaluationError(result.EvaluationErrorCode))
            {
                metadata.Status = QueueJobStatus.DeadLetter;
                metadata.LastError = result.Message;
                _logger.LogError(
                    "AnswerProcessingJob dead-lettered (eval failure): AnswerId={AnswerId}, Code={Code}, Message={Message}",
                    metadata.AnswerId, result.EvaluationErrorCode, result.Message);
                throw new NonRetryableJobException(
                    $"Evaluation error [{result.EvaluationErrorCode}]: {result.Message}");
            }

            metadata.Status = QueueJobStatus.Failed;
            metadata.LastError = result.Message;
            await _answerRepository.MarkFailedAsync(
                metadata.AnswerId, metadata.LastError ?? "Evaluation failed.", cancellationToken);

            if (QueueLimits.IsExhausted(attempt))
            {
                metadata.Status = QueueJobStatus.DeadLetter;
                _logger.LogError(
                    "AnswerProcessingJob dead-lettered (no attempts left): AnswerId={AnswerId}, Attempt={Attempt}",
                    metadata.AnswerId, attempt);
                throw new NonRetryableJobException(
                    $"Evaluation error [{result.EvaluationErrorCode}] and no attempts left: {result.Message}");
            }

            _logger.LogWarning(
                "AnswerProcessingJob failed (will retry): AnswerId={AnswerId}, Attempt={Attempt}, Message={Message}",
                metadata.AnswerId, attempt, metadata.LastError);
            throw new RetryableJobException(
                $"Retryable error for Answer {metadata.AnswerId}: {metadata.LastError}");
        }

        // 5. Retryable STT failures (timeout, internal error).
        metadata.Status = QueueJobStatus.Failed;
        metadata.LastError = result.SttError?.Message ?? result.Message;
        await _answerRepository.MarkFailedAsync(
            metadata.AnswerId, metadata.LastError ?? "STT failed.", cancellationToken);

        if (QueueLimits.IsExhausted(attempt))
        {
            metadata.Status = QueueJobStatus.DeadLetter;
            _logger.LogError(
                "AnswerProcessingJob dead-lettered (no attempts left): AnswerId={AnswerId}, Attempt={Attempt}",
                metadata.AnswerId, attempt);
            throw new NonRetryableJobException(
                $"Retryable error but no attempts left for Answer {metadata.AnswerId}: {metadata.LastError}");
        }

        _logger.LogWarning(
            "AnswerProcessingJob failed (will retry): AnswerId={AnswerId}, Attempt={Attempt}, Message={Message}",
            metadata.AnswerId, attempt, metadata.LastError);
        throw new RetryableJobException(
            $"Retryable error for Answer {metadata.AnswerId}: {metadata.LastError}");
    }

    /// <summary>
    /// Marks a failure that must not be retried. The <c>AutomaticRetry</c> filter is restricted to
    /// <see cref="RetryableJobException"/>, so any other exception type is never retried.
    /// </summary>
    public class NonRetryableJobException : Exception
    {
        public NonRetryableJobException(string message) : base(message) { }
    }

    /// <summary>Marks a failure that can be retried by Hangfire.</summary>
    public class RetryableJobException : Exception
    {
        public RetryableJobException(string message) : base(message) { }
    }
}
