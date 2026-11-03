using System.ComponentModel.DataAnnotations;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.DTOs;

public class ResultSearchQuery
{
    [Range(1, int.MaxValue)] public int? PeriodId { get; set; }
    public bool Unassigned { get; set; }
    [Range(1, int.MaxValue)] public int? SessionId { get; set; }
    public SubmissionStatus? Status { get; set; }
    public AiEvaluationStatus? AiStatus { get; set; }
    [MaxLength(150)] public string? Search { get; set; }
    [Range(1, 1000000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public class ResultListItemDto
{
    public int ResultId { get; set; }
    public int SessionId { get; set; }
    public int? PeriodId { get; set; }
    public string? PeriodName { get; set; }
    public string StudentName { get; set; } = "";
    public string StudentEmail { get; set; } = "";
    public string StudentCode { get; set; } = "";
    public string SessionName { get; set; } = "";
    public string ExamTitle { get; set; } = "";
    public string Status { get; set; } = "";
    public string? AiEvaluationStatus { get; set; }
    public float? AiScore { get; set; }
    public float? FinalScore { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public class ResultSessionOptionDto
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = "";
}

public class ResultSearchResponse
{
    public List<ResultListItemDto> Items { get; set; } = new();
    public List<ResultSessionOptionDto> Sessions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
