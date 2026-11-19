namespace Aives.Application.Queue.DTOs;

/// <summary>
/// Trạng thái xử lý STT + evaluation của một Answer, kèm trạng thái Hangfire job.
/// Dùng cho <c>GET /api/answers/{answerId}/processing-status</c>.
/// </summary>
public class AnswerProcessingStatusDto
{
    public int AnswerId { get; set; }
    public int ResultId { get; set; }
    public int QuestionId { get; set; }
    public int StudentId { get; set; }
    public int SessionId { get; set; }

    /// <summary>Trạng thái lưu trong DB: PROCESSING / EVALUATED / ERROR / …</summary>
    public string AnswerStatus { get; set; } = string.Empty;

    /// <summary>
    /// Trạng thái job đã map về hợp đồng: Pending / Processing / Completed / Failed / DeadLetter.
    /// <c>NotQueued</c> khi answer không có JobId và chưa từng được xếp lịch.
    /// </summary>
    public string JobStatus { get; set; } = "NotQueued";

    /// <summary>State thô của Hangfire (Enqueued / Processing / Succeeded / Failed / Deleted…).</summary>
    public string? HangfireState { get; set; }

    public string? JobId { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; }

    /// <summary>True khi có thể retry thủ công (answer lỗi và còn ngân sách attempt).</summary>
    public bool CanRetry { get; set; }

    /// <summary>Lỗi gần nhất từ DB hoặc từ Hangfire.</summary>
    public string? Error { get; set; }

    public DateTime? AnsweredAt { get; set; }
    public DateTime? JobCreatedAt { get; set; }
    public DateTime? JobStateChangedAt { get; set; }
}

/// <summary>Tóm tắt trạng thái một answer trong một bài thi đã nộp.</summary>
public class AnswerStatusSummaryDto
{
    public int AnswerId { get; set; }
    public int QuestionId { get; set; }
    public string AnswerStatus { get; set; } = string.Empty;
    public string JobStatus { get; set; } = "NotQueued";
    public int AttemptCount { get; set; }
    public bool CanRetry { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Trạng thái chấm cả bài (session grading) của một Result.
/// Dùng cho <c>GET /api/results/{resultId}/grading-status</c>.
/// </summary>
public class ResultGradingStatusDto
{
    public int ResultId { get; set; }
    public int SessionId { get; set; }
    public int StudentId { get; set; }

    /// <summary>Trạng thái bài nộp: PENDING_REVIEW / REVIEWED / PUBLISHED / CANCELLED.</summary>
    public string SubmissionStatus { get; set; } = string.Empty;

    /// <summary>Trạng thái AI chấm: PENDING / SUCCEEDED / FAILED.</summary>
    public string AiEvaluationStatus { get; set; } = string.Empty;

    public string? AiEvaluationError { get; set; }
    public float? AiScore { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public string? GradingJobId { get; set; }

    /// <summary>Pending / Processing / Completed / Failed / DeadLetter / NotQueued.</summary>
    public string GradingJobStatus { get; set; } = "NotQueued";

    public string? HangfireState { get; set; }
    public DateTime? JobCreatedAt { get; set; }
    public DateTime? JobStateChangedAt { get; set; }

    public int AnswerCount { get; set; }
    public int EvaluatedAnswerCount { get; set; }
    public int FailedAnswerCount { get; set; }
    public List<AnswerStatusSummaryDto> Answers { get; set; } = new();
}

/// <summary>
/// Trạng thái tổng hợp của bài nộp trong một ca thi — dành cho sinh viên theo dõi
/// sau khi bấm "Kết thúc". Dùng cho
/// <c>GET /api/student/viva-sessions/{sessionId}/submission-status</c>.
/// </summary>
public class SubmissionStatusDto
{
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public int ResultId { get; set; }
    public string SubmissionStatus { get; set; } = string.Empty;
    public string AiEvaluationStatus { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ResultGradingStatusDto Grading { get; set; } = new();
}
