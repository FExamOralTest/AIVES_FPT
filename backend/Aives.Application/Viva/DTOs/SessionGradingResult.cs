namespace Aives.Application.Viva.DTOs;

/// <summary>
/// Kết quả một lần worker chấm cả bài thi. Không chứa điểm chi tiết;
/// điểm được ghi thẳng vào <c>Result</c> và <c>Answer</c>.
/// </summary>
public sealed class SessionGradingResult
{
    public bool Success { get; init; }

    /// <summary>Chỉ có ý nghĩa khi <see cref="Success"/> là false.</summary>
    public bool Retryable { get; init; }

    public string? ErrorCode { get; init; }

    public string? Message { get; init; }

    public static SessionGradingResult Completed() => new() { Success = true };

    public static SessionGradingResult Failed(string code, string message, bool retryable) => new()
    {
        Success = false,
        Retryable = retryable,
        ErrorCode = code,
        Message = message
    };
}
