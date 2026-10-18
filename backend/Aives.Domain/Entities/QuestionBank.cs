using System;
using System.Collections.Generic;
using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class QuestionBank
{
    public int QuestionId { get; set; }

    public int CourseId { get; set; }

    public int? DocumentId { get; set; }

    public string QuestionText { get; set; } = null!;
    public string? Topic { get; set; }
    public string? SampleAnswer { get; set; }
    public string? LearningOutcome { get; set; }
    public virtual ICollection<QuestionRubricCriterion> Rubric { get; set; } = new List<QuestionRubricCriterion>();

    
    public float? MaxScore { get; set; }
    public int Level { get; set; }

    
    public bool? IsFollowUpAllowed { get; set; }

    public List<string> ExpectedKeywords { get; set; } = new();
    public QuestionApprovalStatus ApprovalStatus { get; set; } = QuestionApprovalStatus.PENDING_REVIEW;
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual Course Course { get; set; } = null!;

    public virtual MarkdownDocument? Document { get; set; }

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();
}


