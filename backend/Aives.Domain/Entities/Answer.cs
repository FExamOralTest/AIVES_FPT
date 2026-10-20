using System;
using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class Answer
{
    public int AnswerId { get; set; }

    public AnswerStatus Status { get; set; } = AnswerStatus.PROCESSING;

    public int ResultId { get; set; }

    public int QuestionId { get; set; }

    public string? StudentAnswer { get; set; }

    public string? Transcript { get; set; }

    /// <summary>
    /// Legacy/full URL if needed.
    /// </summary>
    public string? AudioUrl { get; set; }

    /// <summary>
    /// S3/R2 Object Key for secure storage & generating presigned URLs (BR-15).
    /// Example: "exams/1/sessions/5/students/10/q_3_1729000000.webm"
    /// </summary>
    public string? ObjectKey { get; set; }

    /// <summary>
    /// Hangfire background job identifier for tracking processing (BR-17).
    /// </summary>
    public string? JobId { get; set; }

    /// <summary>
    /// Number of processing/retry attempts (max 3 according to BR-17).
    /// </summary>
    public int AttemptCount { get; set; } = 0;

    /// <summary>
    /// Error message if STT or AI evaluation failed (Status = ERROR).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Audio duration in seconds returned from Faster-Whisper.
    /// </summary>
    public float? DurationSeconds { get; set; }

    /// <summary>
    /// Detected language from speech recognition (e.g. "vi", "en").
    /// </summary>
    public string? DetectedLanguage { get; set; }

    public bool? IsFollowUp { get; set; }

    public float? AiScore { get; set; }

    public string? AiFeedback { get; set; }

    public string? AiAnalysis { get; set; }

    public float? FinalScore { get; set; }

    public string? TeacherComment { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public virtual QuestionBank Question { get; set; } = null!;

    public virtual Result Result { get; set; } = null!;
}
