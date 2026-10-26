using System;
using System.Collections.Generic;

using Aives.Domain.Enums;

namespace Aives.Domain.Entities;

public partial class Exam
{
    public int ExamId { get; set; }

    public int CourseId { get; set; }

    public int CreatedBy { get; set; }

    public string Title { get; set; } = null!;

    public string ExamCode { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public int MaxQuestions { get; set; }

    public ExamStatus? ExamStatus { get; set; }

    /// <summary>
    /// Soft-delete flag. Khi <c>false</c>, exam bị ẩn khỏi các API đọc mặc định
    /// (ví dụ <c>GET /api/admin/exams</c> với <c>includeInactive=false</c>) nhưng vẫn còn trong DB.
    /// </summary>
    /// <remarks>
    /// Mặc định <c>true</c> để exam mới luôn hiện diện. Soft-delete là thao tác UPDATE
    /// (không phải INSERT) nên không bị ảnh hưởng bởi sentinel value của EF Core.
    /// </remarks>
    public bool IsActive { get; set; } = true;

    public DateTime? CreatedAt { get; set; }

    public virtual Course Course { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<ExamQuestion> ExamQuestions { get; set; } = new List<ExamQuestion>();

    public virtual ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();

    public virtual ICollection<ExamSessionExam> SessionExams { get; set; } = new List<ExamSessionExam>();

    public virtual ICollection<SessionEnrollment> AssignedEnrollments { get; set; } = new List<SessionEnrollment>();

    public virtual ICollection<Result> Results { get; set; } = new List<Result>();
}
