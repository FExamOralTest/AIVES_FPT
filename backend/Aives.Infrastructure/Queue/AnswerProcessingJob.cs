using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Interfaces;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Hangfire job thực thi <see cref="IAnswerOrchestrator"/> cho 1 Answer.
/// Class non-static + method public để Hangfire resolve qua DI scope.
/// </summary>
/// <remarks>
/// Retry policy:
/// <list type="bullet">
///   <item>Mặc định Hangfire retry 10 lần với exponential backoff. Ta override thành 3.</item>
///   <item>Retryable lỗi (timeout, internal): Hangfire sẽ re-throw → retry.</item>
///   <item>Non-retryable lỗi (audio corrupted, empty): throw <see cref="NonRetryableException"/>
///         để <c>AutomaticRetryAttribute</c> skip retry và đi vào dead-letter ngay.</item>
/// </list>
/// </remarks>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 120, 300 }, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
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
        _logger.LogInformation(
            "AnswerProcessingJob starting: AnswerId={AnswerId}, Attempt={Attempt}, JobId={JobId}",
            metadata.AnswerId, metadata.Attempt, metadata.JobId);

        var result = await _orchestrator.ProcessAnswerAsync(metadata.AnswerId, metadata.Attempt, cancellationToken);

        if (result.Success)
        {
            metadata.Status = QueueJobStatus.Completed;
            _logger.LogInformation(
                "AnswerProcessingJob completed: AnswerId={AnswerId}, Attempt={Attempt}",
                metadata.AnswerId, metadata.Attempt);
            return;
        }

        // Map kết quả → quyết định retry.
        if (result.SttError is { Retryable: false })
        {
            // Lỗi không retryable (audio corrupted, empty transcript) → dead-letter ngay.
            metadata.Status = QueueJobStatus.DeadLetter;
            metadata.LastError = result.SttError.Message;
            _logger.LogError(
                "AnswerProcessingJob dead-lettered (non-retryable): AnswerId={AnswerId}, Code={Code}, Message={Message}",
                metadata.AnswerId, result.SttError.Code, result.SttError.Message);
            // Throw non-retryable exception để Hangfire không retry.
            throw new NonRetryableJobException(
                $"STT error [{result.SttError.Code}]: {result.SttError.Message}");
        }

        // Evaluation failure (AI2 trả Success=false) → không retry vì retry sẽ cho cùng kết quả.
        // Transcript đã được persist OK, chỉ là AI2 chấm không ra điểm. → dead-letter.
        if (!string.IsNullOrEmpty(result.EvaluationErrorCode))
        {
            metadata.Status = QueueJobStatus.DeadLetter;
            metadata.LastError = result.Message;
            _logger.LogError(
                "AnswerProcessingJob dead-lettered (eval failure): AnswerId={AnswerId}, Code={Code}, Message={Message}",
                metadata.AnswerId, result.EvaluationErrorCode, result.Message);
            throw new NonRetryableJobException(
                $"Evaluation error [{result.EvaluationErrorCode}]: {result.Message}");
        }

        // Retryable error → throw để Hangfire retry (AutomaticRetry sẽ tăng Attempt).
        metadata.Status = QueueJobStatus.Failed;
        metadata.LastError = result.SttError?.Message ?? result.Message;
        _logger.LogWarning(
            "AnswerProcessingJob failed (will retry): AnswerId={AnswerId}, Attempt={Attempt}, Message={Message}",
            metadata.AnswerId, metadata.Attempt, metadata.LastError);
        throw new RetryableJobException(
            $"Retryable error for Answer {metadata.AnswerId}: {metadata.LastError}");
    }

    /// <summary>Exception đánh dấu lỗi không nên retry — Hangfire filter sẽ đi thẳng dead-letter.</summary>
    public class NonRetryableJobException : Exception
    {
        public NonRetryableJobException(string message) : base(message) { }
    }

    /// <summary>Exception đánh dấu lỗi có thể retry.</summary>
    public class RetryableJobException : Exception
    {
        public RetryableJobException(string message) : base(message) { }
    }
}
