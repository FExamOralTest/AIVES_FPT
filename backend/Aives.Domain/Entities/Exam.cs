using System;
using System.Collections.Generic;

using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class Exam
{
    public int ExamId { get; set; }

    public int CourseId { get; set; }

    public int CreatedBy { get; set; }

    public string Title { get; set; } = null!;

    public string ExamCode { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public int MaxQuestions { get; set; }

    public ExamStatus? ExamStatus { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();
}
