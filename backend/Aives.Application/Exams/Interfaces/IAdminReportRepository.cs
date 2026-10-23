using Aives.Domain.Enums;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Truy vấn read-only cho các báo cáo tổng quan của ADMIN.
/// Tách riêng khỏi <see cref="IExamRepository"/> vì các truy vấn này không thuộc vòng đời CRUD của exam.
/// </summary>
public interface IAdminReportRepository
{
    /// <summary>
    /// Lấy toàn bộ exam khớp bộ lọc kèm số câu hỏi và số phiên thi đã tạo.
    /// Không phân trang: endpoint quản trị trả về mọi bản ghi khớp bộ lọc.
    /// </summary>
    /// <param name="includeInactive">
    /// <c>false</c> thì chỉ lấy exam đang hoạt động (<c>is_active = TRUE</c>); <c>true</c> thì lấy tất cả.
    /// </param>
    /// <param name="isDescending">Sắp xếp giảm dần theo <c>created_at</c> nếu <c>true</c>, tăng dần nếu <c>false</c>.</param>
    /// <param name="examStatus">
    /// Lọc theo trạng thái exam. Truyền <c>null</c> để không lọc.
    /// Phải là giá trị hợp lệ của <see cref="ExamStatus"/> — việc parse từ chuỗi do tầng service
    /// đảm nhiệm, vì <c>Enum.ToString()</c> không dịch được sang SQL khi ánh xạ sang
    /// native enum của PostgreSQL.
    /// </param>
    Task<(List<ExamReportRow> Items, int TotalCount)> GetExamOverviewAsync(
        int? courseId,
        ExamStatus? examStatus,
        string? search,
        bool includeInactive,
        bool isDescending);

    /// <summary>Tổng hợp số liệu hệ thống trong khoảng thời gian (session, enrollment, result, answer).</summary>
    /// <param name="sessionStatus">
    /// Lọc theo trạng thái session. Phải là giá trị hợp lệ của <see cref="SessionStatus"/>; <c>null</c> để không lọc.
    /// </param>
    Task<AdminReportSnapshot> GetSummarySnapshotAsync(
        DateTime? fromDate,
        DateTime? toDate,
        int? courseId,
        SessionStatus? sessionStatus);
}

/// <summary>Một dòng kết quả thô cho báo cáo exam (đã project sẵn ở tầng repository).</summary>
public class ExamReportRow
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

/// <summary>Kết quả aggregate thô, service sẽ chuyển sang DTO.</summary>
public class AdminReportSnapshot
{
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int CompletedSessions { get; set; }
    public int UpcomingSessions { get; set; }
    public int CancelledSessions { get; set; }
    public int TotalEnrollments { get; set; }
    public int TotalResults { get; set; }
    public int CompletedAttempts { get; set; }
    public double? AverageAiScore { get; set; }
    public double? AverageFinalScore { get; set; }
    public double? HighestFinalScore { get; set; }
    public double? LowestFinalScore { get; set; }
    public int PendingReviewCount { get; set; }
    public int ReviewedCount { get; set; }
    public int PublishedCount { get; set; }
    public int SubmittedCount { get; set; }
    public int CancelledResultCount { get; set; }
    public int ProcessingAnswerCount { get; set; }
    public int EvaluatedAnswerCount { get; set; }
    public int ErrorAnswerCount { get; set; }
}