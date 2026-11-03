using System.ComponentModel.DataAnnotations;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

/// <summary>
/// Implementation của <see cref="IAdminReportService"/>.
/// Validate input ở đây để controller chỉ cần map exception sang HTTP status.
/// </summary>
public class AdminReportService(IAdminReportRepository repository) : IAdminReportService
{
    public async Task<AdminExamListResponse> GetExamListAsync(AdminExamQuery query)
    {
        Validate(query);

        var examStatus = ParseEnum<ExamStatus>(query.ExamStatus, nameof(query.ExamStatus));

        var (rows, total) = await repository.GetExamOverviewAsync(
            query.CourseId,
            examStatus,
            query.Search,
            query.IncludeInactive,
            query.IsDescending);

        return new AdminExamListResponse
        {
            Items = rows.Select(r => new AdminExamSummaryDto
            {
                ExamId = r.ExamId,
                Title = r.Title,
                ExamCode = r.ExamCode,
                CourseId = r.CourseId,
                CourseName = r.CourseName,
                CreatedBy = r.CreatedBy,
                CreatedByName = r.CreatedByName,
                DurationMinutes = r.DurationMinutes,
                MaxQuestions = r.MaxQuestions,
                QuestionCount = r.QuestionCount,
                SessionCount = r.SessionCount,
                ExamStatus = r.ExamStatus,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt
            }).ToList(),
            TotalCount = total
        };
    }

    public async Task<AdminReportSummaryDto> GetSummaryAsync(AdminReportSummaryQuery query)
    {
        Validate(query);

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate > query.ToDate)
            throw new ArgumentException("fromDate must be earlier than or equal to toDate.");

        var sessionStatus = ParseEnum<SessionStatus>(query.SessionStatus, nameof(query.SessionStatus));

        var snapshot = await repository.GetSummarySnapshotAsync(
            query.FromDate,
            query.ToDate,
            query.CourseId,
            sessionStatus);

        return new AdminReportSummaryDto
        {
            TotalSessions = snapshot.TotalSessions,
            ActiveSessions = snapshot.ActiveSessions,
            CompletedSessions = snapshot.CompletedSessions,
            UpcomingSessions = snapshot.UpcomingSessions,
            CancelledSessions = snapshot.CancelledSessions,
            TotalEnrollments = snapshot.TotalEnrollments,
            TotalResults = snapshot.TotalResults,
            CompletedAttempts = snapshot.CompletedAttempts,
            AverageAiScore = snapshot.AverageAiScore,
            AverageFinalScore = snapshot.AverageFinalScore,
            HighestFinalScore = snapshot.HighestFinalScore,
            LowestFinalScore = snapshot.LowestFinalScore,
            PendingReviewCount = snapshot.PendingReviewCount,
            ReviewedCount = snapshot.ReviewedCount,
            PublishedCount = snapshot.PublishedCount,
            SubmittedCount = snapshot.SubmittedCount,
            CancelledResultCount = snapshot.CancelledResultCount,
            ProcessingAnswerCount = snapshot.ProcessingAnswerCount,
            EvaluatedAnswerCount = snapshot.EvaluatedAnswerCount,
            ErrorAnswerCount = snapshot.ErrorAnswerCount,
            FromDate = query.FromDate,
            ToDate = query.ToDate,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static void Validate(AdminExamQuery query)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            throw new ArgumentException(string.Join(" ", errors.Select(e => e.ErrorMessage ?? "Invalid value.")));
    }

    private static void Validate(AdminReportSummaryQuery query)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(query, new ValidationContext(query), errors, true))
            throw new ArgumentException(string.Join(" ", errors.Select(e => e.ErrorMessage ?? "Invalid value.")));
    }

    /// <summary>
    /// Chuyển chuỗi query thành enum, trả về <c>null</c> khi không lọc.
    /// </summary>
    /// <remarks>
    /// Bắt buộc phải parse ở tầng này thay vì đẩy chuỗi xuống repository: cột enum được ánh xạ
    /// sang native enum của PostgreSQL, nên <c>Enum.ToString()</c> trong LINQ sẽ ném
    /// "could not be translated" lúc chạy thật.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Chuỗi không rỗng nhưng không khớp giá trị nào của <typeparamref name="TEnum"/>.
    /// Controller ánh xạ exception này thành HTTP 400.
    /// </exception>
    private static TEnum? ParseEnum<TEnum>(string? value, string paramName) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
        {
            var allowed = string.Join(", ", Enum.GetNames<TEnum>());
            throw new ArgumentException(
                $"Invalid {paramName} '{value}'. Allowed values: [{allowed}].");
        }

        return parsed;
    }
}