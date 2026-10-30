using System.Text.Json.Serialization;
using System.Text.Json;

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

/// <summary>
/// Response chuẩn từ AI1 (Faster-Whisper) — dùng làm HĐ giữa AI service và Backend.
/// Backend KHÔNG phụ thuộc schema gốc của AI1: mọi mapping được thực hiện ở tầng
/// <c>AiServiceClient</c> (cast kiểu + fallback mặc định).
/// </summary>
/// <remarks>
/// Schema bắt buộc (cho AI1 team):
/// <code>
/// {
///   "success": true,
///   "message": null,             // optional
///   "transcript": "string",      // rỗng nếu audio im lặng — KHÔNG coi là lỗi
///   "language_detected": "vi",   // ISO-639-1 (vd: "vi", "en") — giữ tên cũ để tương thích AI service
///   "duration": 12.34,           // giây, > 0 nếu audio hợp lệ
///   "confidence": 0.92,          // optional, 0..1
///   "error": {                    // optional, chỉ set khi success=false
///     "code": "AUDIO_CORRUPTED", // chuẩn hoá theo SttErrorCode
///     "message": "...",
///     "retryable": false
///   }
/// }
/// </code>
/// Backend tự bổ sung <c>ProcessedAt</c> (UTC) sau khi parse — không cần AI1 trả.
/// </remarks>
public class SpeechToTextResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;

    /// <summary>
    /// Mã ngôn ngữ ISO-639-1 (vd: "vi", "en"). Backend fallback "vi" nếu thiếu.
    /// JSON name giữ <c>language_detected</c> để tương thích ngược với AI service hiện tại;
    /// tên C# là <c>LanguageDetected</c> vì property đã được dùng trong callers.
    /// </summary>
    [JsonPropertyName("language_detected")]
    public string LanguageDetected { get; set; } = "vi";

    /// <summary>Độ dài audio (giây). Dùng để validate audio hợp lệ (vd: &gt; 0).</summary>
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    /// <summary>Độ tin cậy của Whisper (0..1). Optional.</summary>
    [JsonPropertyName("confidence")]
    public float? Confidence { get; set; }

    /// <summary>Error format chuẩn — chỉ set khi <c>success = false</c>.</summary>
    [JsonPropertyName("error")]
    public SttErrorPayload? Error { get; set; }

    /// <summary>Thời điểm STT hoàn tất (UTC) — Backend tự gán sau parse, không cần AI1 trả.</summary>
    [JsonIgnore]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Payload lỗi chuẩn từ AI1 (JSON contract).</summary>
public class SttErrorPayload
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("retryable")]
    public bool Retryable { get; set; } = true;
}

public class StoredSpeechToTextResponse
{
    [JsonRequired]
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonRequired]
    [JsonPropertyName("answerId")]
    public int AnswerId { get; set; }

    [JsonRequired]
    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    [JsonPropertyName("error")]
    public SttErrorPayload? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
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

    [JsonPropertyName("sample_answer")]
    public string? SampleAnswer { get; set; }

    [JsonPropertyName("learning_outcome")]
    public string? LearningOutcome { get; set; }

    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    [JsonPropertyName("max_score")]
    public float? MaxScore { get; set; }

    [JsonPropertyName("rubric")]
    public List<AnswerRubricCriterionDto> Rubric { get; set; } = new();

    [JsonPropertyName("expected_keywords")]
    public List<string> ExpectedKeywords { get; set; } = new();
}

public class AnswerRubricCriterionDto
{
    [JsonPropertyName("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("max_score")]
    public float MaxScore { get; set; }
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
    [JsonRequired]
    [JsonPropertyName("question_id")]
    public string QuestionId { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("student_answer")]
    public string StudentAnswer { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("score")]
    public float Score { get; set; }

    [JsonRequired]
    [JsonPropertyName("max_score")]
    public float MaxScore { get; set; }

    [JsonRequired]
    [JsonPropertyName("feedback")]
    public string Feedback { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("weaknesses")]
    public List<string> Weaknesses { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("missing_content")]
    public List<string> MissingContent { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("sentence_feedback")]
    public List<SentenceFeedbackDto> SentenceFeedback { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("criterion_analyses")]
    public List<CriterionAnalysisDto> CriterionAnalyses { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("improvements")]
    public List<string> Improvements { get; set; } = new();
}

public class SentenceFeedbackDto
{
    [JsonRequired]
    [JsonPropertyName("sentence")]
    public string Sentence { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("judgment")]
    public string Judgment { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("comment")]
    public string Comment { get; set; } = string.Empty;
}

public class CriterionAnalysisDto
{
    [JsonRequired]
    [JsonPropertyName("criterion_id")]
    public string CriterionId { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("judgment")]
    public string Judgment { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("awarded_score")]
    public float AwardedScore { get; set; }

    [JsonRequired]
    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("omissions")]
    public List<string> Omissions { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("evidence_quotes")]
    public List<string> EvidenceQuotes { get; set; } = new();
}

public class StoredAnswerAnalysisDto
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web);

    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("gradedAt")]
    public DateTime GradedAt { get; set; }

    [JsonPropertyName("transcript")]
    public string Transcript { get; set; } = string.Empty;

    [JsonPropertyName("rubric")]
    public List<StoredRubricCriterionDto> Rubric { get; set; } = new();

    [JsonPropertyName("evaluation")]
    public StoredEvaluationDto Evaluation { get; set; } = new();

    public static StoredAnswerAnalysisDto Create(
        string provider,
        string model,
        string transcript,
        IEnumerable<AnswerRubricCriterionDto> rubric,
        QuestionScoreDetailDto evaluation) => new()
    {
        SchemaVersion = "2.0",
        Provider = provider,
        Model = model,
        GradedAt = DateTime.UtcNow,
        Transcript = transcript,
        Rubric = rubric.Select(criterion => new StoredRubricCriterionDto
        {
            CriterionId = criterion.CriterionId,
            Description = criterion.Description,
            MaxScore = criterion.MaxScore
        }).ToList(),
        Evaluation = new StoredEvaluationDto
        {
            QuestionId = evaluation.QuestionId,
            QuestionText = evaluation.QuestionText,
            StudentAnswer = evaluation.StudentAnswer,
            Score = evaluation.Score,
            MaxScore = evaluation.MaxScore,
            Feedback = evaluation.Feedback,
            Strengths = evaluation.Strengths,
            Weaknesses = evaluation.Weaknesses,
            MissingContent = evaluation.MissingContent,
            SentenceFeedback = evaluation.SentenceFeedback.Select(sentence => new StoredSentenceFeedbackDto
            {
                Sentence = sentence.Sentence,
                Judgment = sentence.Judgment,
                Comment = sentence.Comment
            }).ToList(),
            CriterionAnalyses = evaluation.CriterionAnalyses.Select(criterion => new StoredCriterionAnalysisDto
            {
                CriterionId = criterion.CriterionId,
                AwardedScore = criterion.AwardedScore,
                Judgment = criterion.Judgment,
                Explanation = criterion.Explanation,
                Errors = criterion.Errors,
                Omissions = criterion.Omissions,
                EvidenceQuotes = criterion.EvidenceQuotes
            }).ToList(),
            Improvements = evaluation.Improvements
        }
    };
}

public class StoredRubricCriterionDto
{
    public string CriterionId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float MaxScore { get; set; }
}

public class StoredEvaluationDto
{
    public string QuestionId { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string StudentAnswer { get; set; } = string.Empty;
    public float Score { get; set; }
    public float MaxScore { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<string> Strengths { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
    public List<string> MissingContent { get; set; } = new();
    public List<StoredSentenceFeedbackDto> SentenceFeedback { get; set; } = new();
    public List<StoredCriterionAnalysisDto> CriterionAnalyses { get; set; } = new();
    public List<string> Improvements { get; set; } = new();
}

public class StoredSentenceFeedbackDto
{
    public string Sentence { get; set; } = string.Empty;
    public string Judgment { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public class StoredCriterionAnalysisDto
{
    public string CriterionId { get; set; } = string.Empty;
    public float AwardedScore { get; set; }
    public string Judgment { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = new();
    public List<string> Omissions { get; set; } = new();
    public List<string> EvidenceQuotes { get; set; } = new();
}

public class SessionAnalysisDto
{
    public string SchemaVersion { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime GradedAt { get; set; }
    public float MaxTotalScore { get; set; }
    public float Percentage { get; set; }
    public List<string> StudySuggestions { get; set; } = new();
}

public class EvaluateSessionResponse
{
    [JsonRequired]
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonRequired]
    [JsonPropertyName("exam_session_id")]
    public string ExamSessionId { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("total_score")]
    public float TotalScore { get; set; }

    [JsonRequired]
    [JsonPropertyName("max_total_score")]
    public float MaxTotalScore { get; set; }

    [JsonRequired]
    [JsonPropertyName("percentage")]
    public float Percentage { get; set; }

    [JsonRequired]
    [JsonPropertyName("overall_feedback")]
    public string OverallFeedback { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("question_scores")]
    public List<QuestionScoreDetailDto> QuestionScores { get; set; } = new();

    [JsonRequired]
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonRequired]
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "PendingReview";

    [JsonRequired]
    [JsonPropertyName("study_suggestions")]
    public List<string> StudySuggestions { get; set; } = new();

    [JsonIgnore]
    public string? ErrorCode { get; set; }
}
