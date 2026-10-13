using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

public class QuestionBankQuery
{
    [MaxLength(1000)]
    public string? Search { get; set; }
    [Range(1, int.MaxValue / 100)]
    public int Page { get; set; } = 1;
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public class WriteQuestionRequest
{
    [Required, MaxLength(10000)]
    public string QuestionText { get; set; } = null!;
    [MaxLength(1000)]
    public string? Topic { get; set; }
    [MaxLength(10000)]
    public string? SampleAnswer { get; set; }
    [Range(1, 3)]
    public int Level { get; set; } = 1;
    [Range(typeof(float), "0.01", "100")]
    public float MaxScore { get; set; } = 1;
    public bool IsFollowUpAllowed { get; set; } = true;
}

public record QuestionBankPageDto(List<QuestionBankDto> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)(((long)TotalCount + PageSize - 1) / PageSize);
}
