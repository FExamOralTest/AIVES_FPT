namespace Aives.Domain.Entities;

/// <summary>A single, auditable permission to replace an interrupted attempt in a private session.</summary>
public class ExamRetake
{
    public int RetakeId { get; set; }
    public int OriginalResultId { get; set; }
    public int RetakeSessionId { get; set; }
    public int StudentId { get; set; }
    public int GrantedBy { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public int DurationMinutes { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime? ConsumedAt { get; set; }
    public Result OriginalResult { get; set; } = null!;
    public ExamSession RetakeSession { get; set; } = null!;
}
