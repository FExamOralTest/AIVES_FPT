using System.ComponentModel.DataAnnotations;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.DTOs;

// ── Request ───────────────────────────────────────────────────────────────────

public class CreateExamSessionRequest
{
    [Required]
    public int ExamId { get; set; }

    [Required]
    [MaxLength(150)]
    public string SessionName { get; set; } = null!;

    [MaxLength(150)]
    public string? Room { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Range(1, 500)]
    public int? MaxCapacity { get; set; } = 50;
}

public class UpdateSessionStatusRequest
{
    [Required]
    public SessionStatus Status { get; set; }
}

// ── Response ──────────────────────────────────────────────────────────────────

public class ExamSessionDto
{
    public int     SessionId       { get; set; }
    public int     ExamId          { get; set; }
    public string  ExamTitle       { get; set; } = null!;
    public string  ExamCode        { get; set; } = null!;
    public string  SessionName     { get; set; } = null!;
    public string? Room            { get; set; }
    public DateTime StartTime      { get; set; }
    public DateTime EndTime        { get; set; }
    public int     DurationMinutes { get; set; }
    public string  Status          { get; set; } = null!;
    public int     ScheduledBy     { get; set; }
    public string? ScheduledByName { get; set; }
    public DateTime? CreatedAt     { get; set; }
    public int     EnrolledStudents { get; set; }
    public string  SessionCode     { get; set; } = null!;
    public int     MaxCapacity     { get; set; }
}

public class ExamSessionSummaryDto
{
    public int     SessionId       { get; set; }
    public string  SessionName     { get; set; } = null!;
    public string  ExamTitle       { get; set; } = null!;
    public string  ExamCode        { get; set; } = null!;
    public string? Room            { get; set; }
    public DateTime StartTime      { get; set; }
    public DateTime EndTime        { get; set; }
    public string  Status          { get; set; } = null!;
    public int     EnrolledStudents { get; set; }
}
