using System;
using System.Collections.Generic;

using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = null!;

    public UserRole Role { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Course> CourseCreatedByNavigations { get; set; } = new List<Course>();

    public virtual ICollection<Course> CourseTeachers { get; set; } = new List<Course>();

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual ICollection<MarkdownDocument> MarkdownDocuments { get; set; } = new List<MarkdownDocument>();

    public virtual ICollection<Result> ResultReviewedByNavigations { get; set; } = new List<Result>();

    public virtual ICollection<Result> ResultStudents { get; set; } = new List<Result>();

    public virtual ICollection<SessionEnrollment> SessionEnrollments { get; set; } = new List<SessionEnrollment>();
}
