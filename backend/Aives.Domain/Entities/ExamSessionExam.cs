using System;

namespace Aives.Domain.Entities;

public partial class ExamSessionExam
{
    public int SessionId { get; set; }

    public int ExamId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ExamSession Session { get; set; } = null!;

    public virtual Exam Exam { get; set; } = null!;
}
