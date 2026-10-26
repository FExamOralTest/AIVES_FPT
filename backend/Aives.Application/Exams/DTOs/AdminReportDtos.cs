using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

/// <summary>
/// Query cho <c>GET /api/admin/reports/summary</c>.
/// Khoảng thời gian áp dụng cho session (start_time) và kết quả (submitted_at / published_at).
/// </summary>
public class AdminReportSummaryQuery
{
    /// <summary>Ngày bắt đầu (UTC). Bỏ trống = không giới hạn dưới.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Ngày kết thúc (UTC). Bỏ trống = không giới hạn trên.</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Lọc theo course của exam.</summary>
    [Range(1, int.MaxValue)]
    public int? CourseId { get; set; }

    /// <summary>Lọc theo trạng thái session.</summary>
    [MaxLength(20)]
    public string? SessionStatus { get; set; }
}

/// <summary>
/// Tổng quan hệ thống cho dashboard ADMIN: số phiên thi, số lượt nộp bài, điểm trung bình và hàng đợi review.
/// </summary>
public class AdminReportSummaryDto
{
    // --- Sessions ---
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int UpcomingSessions { get; set; }
    public int CancelledSessions { get; set; }

    // --- Attempts / enrollments ---
    public int TotalEnrollments { get; set; }
    public int TotalResults { get; set; }
    public int CompletedAttempts { get; set; }

    // --- Scores ---
    public double? AverageAiScore { get; set; }
    public double? AverageFinalScore { get; set; }
    public double? HighestFinalScore { get; set; }
    public double? LowestFinalScore { get; set; }

    // --- Review queue (theo SubmissionStatus) ---
    public int PendingReviewCount { get; set; }
    public int ReviewedCount { get; set; }
    public int PublishedCount { get; set; }
    public int SubmittedCount { get; set; }
    public int CancelledResultCount { get; set; }

    // --- Answer processing health (AnswerStatus) ---
    public int ProcessingAnswerCount { get; set; }
    public int EvaluatedAnswerCount { get; set; }
    public int ErrorAnswerCount { get; set; }

    /// <summary>Khoảng thời gian đã áp dụng, để client hiển thị lại cho người dùng.</summary>
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}