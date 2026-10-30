using System;
using System.Collections.Generic;
using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class ExamPeriod
{
    public int PeriodId { get; set; }

    public int CourseId { get; set; }

    public string PeriodName { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public ExamPeriodStatus Status { get; set; } = ExamPeriodStatus.DRAFT;

    public int CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();
}
