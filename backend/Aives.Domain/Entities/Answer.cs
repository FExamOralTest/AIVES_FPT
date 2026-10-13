using System;
using System.Collections.Generic;

namespace Aives.Domain.Entities;

public partial class Answer
{
    public int AnswerId { get; set; }

    public int ResultId { get; set; }

    public int QuestionId { get; set; }

    public string? StudentAnswer { get; set; }

    public string? Transcript { get; set; }

    public string? AudioUrl { get; set; }

    public bool? IsFollowUp { get; set; }

    public float? AiScore { get; set; }

    public string? AiFeedback { get; set; }

    public float? FinalScore { get; set; }

    public string? TeacherComment { get; set; }

    public DateTime? AnsweredAt { get; set; }

    public virtual QuestionBank Question { get; set; } = null!;

    public virtual Result Result { get; set; } = null!;
}
