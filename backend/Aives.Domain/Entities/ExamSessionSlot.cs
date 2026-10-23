namespace Aives.Domain.Entities;

public class ExamSessionSlot
{
    public int SlotId { get; set; }

    public int SessionId { get; set; }

    public int SlotIndex { get; set; }

    public string SlotName { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int MaxCapacity { get; set; } = 10;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual ExamSession Session { get; set; } = null!;

    public virtual ICollection<SessionEnrollment> SessionEnrollments { get; set; } = new List<SessionEnrollment>();
}
