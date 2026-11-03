namespace Aives.Application.Queue.Contracts;

/// <summary>
/// Limits and constants for the answer processing queue (BR-17: "retry at most 3 total attempts").
/// </summary>
/// <remarks>
/// The single source of truth for the attempt number is <c>answers.attempt_count</c> in the
/// database — NOT the Hangfire job metadata, because Hangfire re-serializes the metadata on
/// every retry so <c>metadata.Attempt</c> never increases on its own.
/// See <c>IAnswerRepository.IncrementAttemptAsync</c>.
/// </remarks>
public static class QueueLimits
{
    /// <summary>
    /// Maximum number of processing attempts for a single answer (BR-17).
    /// The first attempt counts as 1, so at most 2 automatic retries are allowed.
    /// </summary>
    public const int MaxTotalAttempts = 3;

    /// <summary>
    /// Delay (seconds) before the first retry.
    /// </summary>
    public const int RetryDelay1Seconds = 30;

    /// <summary>
    /// Delay (seconds) before the second retry.
    /// </summary>
    public const int RetryDelay2Seconds = 120;

    /// <summary>
    /// Delay (seconds) before the third retry.
    /// </summary>
    public const int RetryDelay3Seconds = 300;

    /// <summary>
    /// Backoff (seconds) between retries: 30s -> 120s -> 300s.
    /// Index 0 is used after the first failed attempt, index 1 after the second, and so on.
    /// </summary>
    public static readonly int[] RetryDelaysInSeconds =
        { RetryDelay1Seconds, RetryDelay2Seconds, RetryDelay3Seconds };

    /// <summary>
    /// Returns the backoff delay (seconds) for the given attempt number (1-based).
    /// Falls back to the last configured delay when the attempt exceeds the configured range.
    /// </summary>
    /// <param name="attempt">The attempt number that just failed (1-based).</param>
    public static int GetRetryDelaySeconds(int attempt)
    {
        if (attempt < 1) return RetryDelaysInSeconds[0];
        var index = Math.Min(attempt - 1, RetryDelaysInSeconds.Length - 1);
        return RetryDelaysInSeconds[index];
    }

    /// <summary>
    /// Distinguishes transient AI evaluation failures (safe to retry) from business failures
    /// (retrying produces the exact same outcome, so they must be dead-lettered immediately).
    /// </summary>
    /// <remarks>
    /// Retryable codes come from <c>AiServiceClient</c> / <c>AnswerOrchestrator</c>:
    /// <list type="bullet">
    ///   <item><c>timeout</c> — AI không phản hồi trong 2 phút.</item>
    ///   <item><c>connection_error</c> — không kết nối được tới AI service.</item>
    ///   <item><c>internal_error</c> — lỗi nội bộ phía AI / hợp đồng response.</item>
    ///   <item><c>http_error</c> — AI trả lỗi server (5xx) hoặc 429 (tạm thời).</item>
    /// </list>
    /// Non-retryable codes (dead-letter ngay): <c>evaluation_failed</c>,
    /// <c>invalid_response</c>, <c>question_not_found</c>, <c>internal_auth_failed</c>,
    /// <c>http_client_error</c> (AI trả 4xx — request sai, retry cho kết quả y hệt).
    /// </remarks>
    /// <param name="errorCode">Error code returned by the AI service.</param>
    public static bool IsRetryableEvaluationError(string? errorCode) =>
        errorCode is "timeout" or "connection_error" or "internal_error" or "http_error";

    /// <summary>
    /// Checks whether the answer has already consumed its whole attempt budget.
    /// </summary>
    /// <param name="attemptCount">Current value of <c>answers.attempt_count</c>.</param>
    public static bool IsExhausted(int attemptCount) => attemptCount >= MaxTotalAttempts;
}
