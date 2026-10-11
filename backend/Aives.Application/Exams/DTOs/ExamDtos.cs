using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

// ── Request ───────────────────────────────────────────────────────────────────

public class CreateExamRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [Required]
    public int CourseId { get; set; }

    [Range(1, 480)]
    public int DurationMinutes { get; set; } = 30;

    [Range(1, 200)]
    public int MaxQuestions { get; set; } = 10;

    public List<ExamQuestionItem>? Questions { get; set; }
}

public class ExamQuestionItem
{
    [Required]
    public int QuestionId { get; set; }

    [Range(0.0, 100.0)]
    public float QuestionScore { get; set; } = 1.0f;
}

public class AiSuggestQuestionsRequest
{
    [Required]
    public int CourseId { get; set; }

    [MaxLength(1000)]
    public string? FocusTopic { get; set; }

    [Range(1, 100)]
    public int MaxSuggestions { get; set; } = 10;
}

// ── Response ──────────────────────────────────────────────────────────────────

public class ExamDto
{
    public int     ExamId          { get; set; }
    public string  Title           { get; set; } = null!;
    public string  ExamCode        { get; set; } = null!;
    public int     CourseId        { get; set; }
    public string? CourseName      { get; set; }
    public int     CreatedBy       { get; set; }
    public string? CreatedByName   { get; set; }
    public int     DurationMinutes { get; set; }
    public int     MaxQuestions    { get; set; }
    public string? ExamStatus      { get; set; }
    public DateTime? CreatedAt     { get; set; }
    public List<ExamQuestionDto> Questions { get; set; } = new();
}

public class ExamQuestionDto
{
    public int    QuestionId        { get; set; }
    public string QuestionText      { get; set; } = null!;
    public float  QuestionScore     { get; set; }
    public float? MaxScore          { get; set; }
    public bool?  IsFollowUpAllowed { get; set; }
}

public class AiSuggestedQuestionsDto
{
    public List<ExamQuestionDto> SuggestedQuestions { get; set; } = new();
    public string? AiRationale { get; set; }
}
