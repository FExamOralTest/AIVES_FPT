namespace Aives.Application.Exams.DTOs;

public class SessionMonitorDto
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public string ExamTitle { get; set; } = null!;
    public string ExamCode { get; set; } = null!;
    public string SessionCode { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = null!;
    public int MaxCapacity { get; set; }
    public int CurrentEnrollment { get; set; }
    public List<StudentProgressDto> Students { get; set; } = new();
}

public class StudentProgressDto
{
    public int SessionEnrollmentId { get; set; }
    public int StudentId { get; set; }
    public string StudentName { get; set; } = null!;
    public string StudentEmail { get; set; } = null!;
    public string EnrollmentStatus { get; set; } = null!;
    public DateTime? EnrolledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public int TotalQuestions { get; set; }
    public int AnsweredQuestions { get; set; }
    public double ProgressPercent => TotalQuestions > 0 ? (double)AnsweredQuestions / TotalQuestions * 100 : 0;
}