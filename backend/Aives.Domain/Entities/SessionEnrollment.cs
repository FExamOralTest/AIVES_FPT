using System;
using System.Collections.Generic;

using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class SessionEnrollment
{
    public int SessionEnrollmentId { get; set; }

    public int SessionId { get; set; }

    public int StudentId { get; set; }

    public SessionStatus? Status { get; set; }

    public DateTime? EnrolledAt { get; set; }

    // === Monitoring fields ===
    public DateTime? StartedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public int? CurrentQuestionIndex { get; set; }

    public virtual ExamSession Session { get; set; } = null!;

    public virtual User Student { get; set; } = null!;
}
