using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

public class EnrollSessionRequest
{
    [Required]
    [MaxLength(50)]
    public string SessionCode { get; set; } = null!;
}

public class EnrollSessionResponse
{
    public int SessionEnrollmentId { get; set; }
    public int SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public string ExamTitle { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = null!;
}

public class SessionEnrollmentDto
{
    public int SessionEnrollmentId { get; set; }
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentEmail { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public int? CurrentQuestionIndex { get; set; }
    public int TotalQuestions { get; set; }
}