using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

/// <summary>
/// Query cho <c>GET /api/admin/exams</c> — liệt kê toàn bộ exam trong hệ thống (không giới hạn theo owner).
/// </summary>
public class AdminExamQuery
{
    /// <summary>Lọc theo course. Bỏ trống = tất cả course.</summary>
    [Range(1, int.MaxValue)]
    public int? CourseId { get; set; }

    /// <summary>Lọc theo trạng thái exam (DRAFT, SCHEDULED, OPEN, CLOSED, CANCELLED).</summary>
    [MaxLength(20)]
    public string? ExamStatus { get; set; }

    /// <summary>Từ khoá tìm theo title hoặc exam code.</summary>
    [MaxLength(255)]
    public string? Search { get; set; }

    /// <summary>
    /// Có trả về exam đã soft-delete không? Mặc định <c>false</c> — chỉ trả về exam đang hoạt động.
    /// </summary>
    public bool IncludeInactive { get; set; } = true;

    /// <summary>
    /// Sắp xếp giảm dần theo <c>created_at</c> (mặc định <c>true</c>).
    /// Đặt <c>false</c> để sắp xếp tăng dần.
    /// </summary>
    public bool IsDescending { get; set; } = true;
}

/// <summary>
/// Một dòng exam trong danh sách quản trị.
/// </summary>
public class AdminExamSummaryDto
{
    public int ExamId { get; set; }
    public string Title { get; set; } = null!;
    public string ExamCode { get; set; } = null!;
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public int CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public int DurationMinutes { get; set; }
    public int MaxQuestions { get; set; }
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public string? ExamStatus { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
    /// Response cho <c>GET /api/admin/exams</c> — trả về toàn bộ exam khớp bộ lọc.
    /// </summary>
public class AdminExamListResponse
{
    public List<AdminExamSummaryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
}