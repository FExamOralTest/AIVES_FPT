using System;
using System.Collections.Generic;

namespace Aives.Domain.Entities;

public class ExamSlotTemplate
{
    public int SlotTemplateId { get; set; }

    public int ExamId { get; set; }

    public int SlotIndex { get; set; }

    public string SlotName { get; set; } = null!;

    public int DurationMinutes { get; set; } = 30;

    public int MaxCapacity { get; set; } = 20;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Exam Exam { get; set; } = null!;

    public virtual ICollection<ExamSlotTemplateExam> TemplateExams { get; set; } = new List<ExamSlotTemplateExam>();

    public virtual ICollection<ExamSessionSlot> SessionSlots { get; set; } = new List<ExamSessionSlot>();
}

