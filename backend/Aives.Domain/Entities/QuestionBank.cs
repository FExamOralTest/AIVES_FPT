using System;
using System.Collections.Generic;

namespace Aives.Domain.Entities;

public partial class QuestionBank
{
    public int QuestionId { get; set; }

    public int CourseId { get; set; }

    public int? DocumentId { get; set; }

    public string QuestionText { get; set; } = null!;

    public float? MaxScore { get; set; }

    public bool? IsFollowUpAllowed { get; set; }

    public int Level { get; set; } // 1: Easy, 2: Medium, 3: Hard

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual Course Course { get; set; } = null!;

    public virtual MarkdownDocument? Document { get; set; }

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}
