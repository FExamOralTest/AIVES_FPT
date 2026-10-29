using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

public class GrantRetakeRequest
{
    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    [Range(1, 240)] public int DurationMinutes { get; set; }
    public int? ExamId { get; set; }
}

public class RetakeDto
{
    public int RetakeId { get; set; }
    public int OriginalResultId { get; set; }
    public string OriginalStatus { get; set; } = "";
    public int RetakeSessionId { get; set; }
    public int? RetakeResultId { get; set; }
    public string? ResultStatus { get; set; }
    public float? FinalScore { get; set; }
    public int StudentId { get; set; }
    public int GrantedBy { get; set; }
    public string Reason { get; set; } = "";
    public DateTime GrantedAt { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public string Status { get; set; } = "GRANTED";
}

public record RetakeStart(int DurationMinutes, DateTime EndsAt);
