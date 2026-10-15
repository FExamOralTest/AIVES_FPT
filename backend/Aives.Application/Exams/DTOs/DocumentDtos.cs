using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Aives.Domain.Enums;
using Aives.Application.Exams.Services;

namespace Aives.Application.Exams.DTOs;

public class UploadMarkdownRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public DocType DocType { get; set; }
}

public class UpdateMarkdownRequest
{
    public IFormFile? File { get; set; }
    public DocType DocType { get; set; }
    public string? FileName { get; set; }
    public string? Content { get; set; }
}

public class MarkdownDocumentDto
{
    public int DocumentId { get; set; }
    public int CourseId { get; set; }
    public string FileName { get; set; } = null!;
    public string DocType { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime? UploadedAt { get; set; }
}

public class GenerateQuestionsRequest : IValidatableObject
{
    [Required]
    public int CourseId { get; set; }

    [Range(5, 200)]
    public int QuestionCount { get; set; } = 20;

    public string? FocusTopics { get; set; }

    [Range(typeof(float), "0.01", "100")]
    public float MaxScore { get; set; } = 1.0f;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!QuestionRubricValidator.IsValidMaxScore(MaxScore))
            yield return new ValidationResult("Maximum score must be 0.01-100 with at most four decimal places.", [nameof(MaxScore)]);
    }
}

public class GenerateQuestionsResponse
{
    public int GeneratedCount { get; set; }
    public List<QuestionBankDto> Questions { get; set; } = new();
}

public class QuestionBankDto
{
    public int QuestionId { get; set; }
    public int CourseId { get; set; }
    public int? DocumentId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool IsInUse { get; set; }
    public string QuestionText { get; set; } = null!;
    public string? Topic { get; set; }
    public string? SampleAnswer { get; set; }
    public string? LearningOutcome { get; set; }
    public List<RubricCriterionDto> Rubric { get; set; } = new();
    public float MaxScore { get; set; }
    public int Level { get; set; }
    public bool IsFollowUpAllowed { get; set; }
}
