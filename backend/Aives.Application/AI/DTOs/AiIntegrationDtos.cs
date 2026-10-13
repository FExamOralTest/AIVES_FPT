using System.Text.Json.Serialization;

namespace Aives.Application.AI.DTOs;

public class DocumentContextDto
{
    [JsonPropertyName("document_id")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = "course_content";
}

public class SpeechToTextResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public float? Confidence { get; set; }

    [JsonPropertyName("language_detected")]
    public string? LanguageDetected { get; set; }
}

public class TextToSpeechRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = "vi-VN";

    [JsonPropertyName("voice")]
    public string? Voice { get; set; }
}

public class TextToSpeechResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("audio_base64")]
    public string AudioBase64 { get; set; } = string.Empty;

    [JsonPropertyName("audio_format")]
    public string AudioFormat { get; set; } = "mp3";

    [JsonPropertyName("duration_seconds")]
    public float? DurationSeconds { get; set; }
}

public class FollowUpRequest
{
    [JsonPropertyName("exam_session_id")]
    public string ExamSessionId { get; set; } = string.Empty;

    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("documents")]
    public List<DocumentContextDto> Documents { get; set; } = new();

    [JsonPropertyName("original_question")]
    public string OriginalQuestion { get; set; } = string.Empty;

    [JsonPropertyName("student_answer")]
    public string StudentAnswer { get; set; } = string.Empty;

    [JsonPropertyName("question_index")]
    public int QuestionIndex { get; set; } = 1;
}

public class FollowUpResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("follow_up_question")]
    public string FollowUpQuestion { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("should_follow_up")]
    public bool ShouldFollowUp { get; set; }
}

public class QuestionAnswerPairDto
{
    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("student_answer")]
    public string StudentAnswer { get; set; } = string.Empty;

    [JsonPropertyName("is_follow_up")]
    public bool IsFollowUp { get; set; }
}

public class EvaluateSessionRequest
{
    [JsonPropertyName("exam_session_id")]
    public string ExamSessionId { get; set; } = string.Empty;

    [JsonPropertyName("student_id")]
    public string StudentId { get; set; } = string.Empty;

    [JsonPropertyName("course_name")]
    public string CourseName { get; set; } = string.Empty;

    [JsonPropertyName("documents")]
    public List<DocumentContextDto> Documents { get; set; } = new();

    [JsonPropertyName("question_answers")]
    public List<QuestionAnswerPairDto> QuestionAnswers { get; set; } = new();

    [JsonPropertyName("max_score_per_question")]
    public float MaxScorePerQuestion { get; set; } = 10.0f;
}

public class QuestionScoreDetailDto
{
    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("student_answer")]
    public string StudentAnswer { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonPropertyName("max_score")]
    public float MaxScore { get; set; }

    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = string.Empty;

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonPropertyName("weaknesses")]
    public List<string> Weaknesses { get; set; } = new();

    [JsonPropertyName("missing_content")]
    public List<string> MissingContent { get; set; } = new();
}

public class EvaluateSessionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("exam_session_id")]
    public string ExamSessionId { get; set; } = string.Empty;

    [JsonPropertyName("total_score")]
    public float TotalScore { get; set; }

    [JsonPropertyName("max_total_score")]
    public float MaxTotalScore { get; set; }

    [JsonPropertyName("percentage")]
    public float Percentage { get; set; }

    [JsonPropertyName("overall_feedback")]
    public string OverallFeedback { get; set; } = string.Empty;

    [JsonPropertyName("question_scores")]
    public List<QuestionScoreDetailDto> QuestionScores { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "PendingReview";
}
