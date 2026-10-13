using System;
using System.Collections.Generic;

namespace Aives.Domain.Entities;

public partial class ExamQuestion
{
    public int ExamId { get; set; }

    public int QuestionId { get; set; }

    public float QuestionScore { get; set; }

    public virtual Exam Exam { get; set; } = null!;

    public virtual QuestionBank Question { get; set; } = null!;
}
