using Aives.Domain.Enums;

namespace Aives.Application.Exams.DTOs;

public class StudentSessionEnrollmentDto
{
    public int SessionEnrollmentId { get; set; }
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? Room { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public DateTime? EnrolledAt { get; set; }
    public SessionStatus SessionStatus { get; set; }
    public SessionStatus EnrollmentStatus { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? RetakeEndsAt { get; set; }
    public int? RetakeId { get; set; }
    public int? OriginalResultId { get; set; }
    public string? RetakeReason { get; set; }
    public int? RetakeDurationMinutes { get; set; }
    public int? ReplacementSessionId { get; set; }
}
