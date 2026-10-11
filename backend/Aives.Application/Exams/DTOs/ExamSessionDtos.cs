using System.ComponentModel.DataAnnotations;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.DTOs;

// ─── Request DTOs ───────────────────────────────────────────────

/// <summary>Payload để Teacher tạo ca thi mới.</summary>
public class CreateExamSessionRequest
{
    [Required]
    public int ExamId { get; set; }

    [Required]
    [MaxLength(150)]
    public string SessionName { get; set; } = null!;

    [MaxLength(150)]
    public string? Room { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>Số chỗ ngồi tối đa (mặc định 50).</summary>
    [Range(1, 500)]
    public int? MaxCapacity { get; set; } = 50;
}

/// <summary>Payload để cập nhật trạng thái ca thi.</summary>
public class UpdateSessionStatusRequest
{
    [Required]
    public SessionStatus Status { get; set; }
}

// ─── Response DTOs ──────────────────────────────────────────────

/// <summary>Thông tin ca thi trả về cho client.</summary>
public class ExamSessionDto
{
    public int SessionId { get; set; }
    public int ExamId { get; set; }
    public string ExamTitle { get; set; } = null!;
    public string ExamCode { get; set; } = null!;
    public string SessionName { get; set; } = null!;
    public string? Room { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Status { get; set; } = null!;
    public int ScheduledBy { get; set; }
    public string? ScheduledByName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int EnrolledStudents { get; set; }
    
    // === P0 NEW ===
    public string SessionCode { get; set; } = null!;
    public int MaxCapacity { get; set; }
}

/// <summary>Danh sách ca thi tóm tắt cho Teacher dashboard.</summary>
public class ExamSessionSummaryDto
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = null!;
    public string ExamTitle { get; set; } = null!;
    public string ExamCode { get; set; } = null!;
    public string? Room { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = null!;
    public int EnrolledStudents { get; set; }
}
