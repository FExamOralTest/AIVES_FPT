using System;
using System.Collections.Generic;

namespace Aives.Domain.Entities;

public partial class Course
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = null!;

    public string CourseName { get; set; } = null!;

    public int? TeacherId { get; set; }

    public int? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? CreatedByNavigation { get; set; }

    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    public virtual ICollection<Exam> Exams { get; set; } = new List<Exam>();

    public virtual ICollection<MarkdownDocument> MarkdownDocuments { get; set; } = new List<MarkdownDocument>();

    public virtual ICollection<QuestionBank> QuestionBanks { get; set; } = new List<QuestionBank>();

    public virtual User? Teacher { get; set; }
}
