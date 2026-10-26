using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

/// <summary>
/// Service cho các API quản trị: danh sách exam toàn hệ thống và báo cáo tổng quan.
/// Chỉ ADMIN được gọi — quyền được enforce ở controller.
/// </summary>
public interface IAdminReportService
{
    /// <summary>Lấy danh sách exam toàn hệ thống có phân trang và bộ lọc.</summary>
    Task<AdminExamListResponse> GetExamListAsync(AdminExamQuery query);

    /// <summary>Lấy tổng quan hệ thống cho dashboard ADMIN.</summary>
    Task<AdminReportSummaryDto> GetSummaryAsync(AdminReportSummaryQuery query);
}