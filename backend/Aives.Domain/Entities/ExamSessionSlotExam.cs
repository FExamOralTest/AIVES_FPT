using System;

namespace Aives.Domain.Entities;

public class ExamSessionSlotExam
{
    public int SlotId { get; set; }

    public int ExamId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ExamSessionSlot Slot { get; set; } = null!;

    public virtual Exam Exam { get; set; } = null!;
}

