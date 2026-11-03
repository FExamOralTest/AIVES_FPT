using System.ComponentModel.DataAnnotations;
using Aives.Application.Exams.Services;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.DTOs;

public class QuestionBankQuery
{
    [MaxLength(1000)]
    public string? Search { get; set; }
    [Range(1, int.MaxValue / 100)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
    [Range(1, 3)]
    public int? Level { get; set; }
    [RegularExpression("^(createdAt|questionText|level|maxScore)$")]
    public string SortBy { get; set; } = "createdAt";
    [RegularExpression("^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";
    public QuestionApprovalStatus? Status { get; set; }
}

public class WriteQuestionRequest : IValidatableObject
{
    [Required, MaxLength(10000)]
    public string QuestionText { get; set; } = null!;
    [MaxLength(1000)]
    public string? Topic { get; set; }
    [Required, MaxLength(10000)]
    public string? SampleAnswer { get; set; }
    [Required, MaxLength(2000)]
    public string? LearningOutcome { get; set; }
    [Required]
    public List<RubricCriterionDto>? Rubric { get; set; }
    [Range(1, 3)]
    public int Level { get; set; } = 1;
    [Range(typeof(float), "0.01", "100")]
    public float MaxScore { get; set; } = 1;
    public bool IsFollowUpAllowed { get; set; } = true;
    public List<string>? ExpectedKeywords { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        => QuestionRubricValidator.GetErrors(SampleAnswer, LearningOutcome, Rubric, MaxScore);
}

public record QuestionBankPageDto(List<QuestionBankDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public class RejectQuestionRequest
{
    [Required, MaxLength(2000)]
    public string Reason { get; set; } = null!;
}
