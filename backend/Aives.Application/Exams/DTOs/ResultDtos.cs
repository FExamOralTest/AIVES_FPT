using Aives.Application.AI.DTOs;

namespace Aives.Application.Exams.DTOs;

public class ResultForReviewDto
{
    public int ResultId { get; set; }
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentEmail { get; set; } = null!;
    public string SessionName { get; set; } = null!;
    public string ExamTitle { get; set; } = null!;
    public float? AiScore { get; set; }
    public string? AiFeedback { get; set; }
    public SessionAnalysisDto? AiAnalysis { get; set; }
    public string? AiEvaluationStatus { get; set; }
    public string? AiEvaluationError { get; set; }
    public float? FinalScore { get; set; }
    public string? FinalFeedback { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int? PublishedBy { get; set; }
    public List<AnswerForReviewDto> Answers { get; set; } = new();
}

public class AnswerForReviewDto
{
    public int AnswerId { get; set; }
    public string Status { get; set; } = "PROCESSING";
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = null!;
    public float? MaxScore { get; set; }
    public string? StudentAnswer { get; set; }
    public string? Transcript { get; set; }
    public string? AudioUrl { get; set; }
    public float? AiScore { get; set; }
    public string? AiFeedback { get; set; }
    public StoredAnswerAnalysisDto? AiAnalysis { get; set; }
    public float? FinalScore { get; set; }
    public string? TeacherComment { get; set; }
    public bool IsFollowUp { get; set; }
}


public class ReviewResultRequest
{
    public float? FinalScore { get; set; }
    public string? FinalFeedback { get; set; }
    public List<AnswerReviewItem> AnswerReviews { get; set; } = new();
}

public class AnswerReviewItem
{
    public int AnswerId { get; set; }
    public float? FinalScore { get; set; }
    public string? TeacherComment { get; set; }
}

public class PublishResultRequest
{
    // Empty body - just trigger publish
}
