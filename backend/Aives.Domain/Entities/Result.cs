using System;
using System.Collections.Generic;

using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class Result
{
    public int ResultId { get; set; }

    public int SessionId { get; set; }

    public int StudentId { get; set; }

    public int? ReviewedBy { get; set; }

    public float? AiScore { get; set; }

    public string? AiFeedback { get; set; }

    public float? FinalScore { get; set; }

    public string? FinalFeedback { get; set; }

    public SubmissionStatus? Status { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual ExamSession Session { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
