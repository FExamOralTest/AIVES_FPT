using Aives.Application.AI.DTOs;

namespace Aives.Application.Exams.DTOs;

public class StudentResultDto
{
    public int ResultId { get; set; }
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPublished { get; set; }
    public string? Message { get; set; }
    public float? FinalScore { get; set; }
    public string? FinalFeedback { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<StudentAnswerItemDto> Answers { get; set; } = new();
}

public class StudentAnswerItemDto
{
    public int AnswerId { get; set; }
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? StudentAnswer { get; set; }
    public string? Transcript { get; set; }
    public string? AudioUrl { get; set; }
    public float? FinalScore { get; set; }
    public string? TeacherComment { get; set; }
    public bool IsFollowUp { get; set; }
    public StoredAnswerAnalysisDto? AiAnalysis { get; set; }
}
