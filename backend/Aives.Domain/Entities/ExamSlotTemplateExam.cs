using System;

namespace Aives.Domain.Entities;

public class ExamSlotTemplateExam
{
    public int SlotTemplateId { get; set; }

    public int ExamPaperId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ExamSlotTemplate SlotTemplate { get; set; } = null!;

    public virtual Exam ExamPaper { get; set; } = null!;
}

