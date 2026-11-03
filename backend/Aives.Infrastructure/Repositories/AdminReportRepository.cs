using Aives.Application.Exams.Interfaces;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

/// <summary>
/// Truy vấn read-only phục vụ dashboard ADMIN: danh sách exam toàn hệ thống và báo cáo tổng quan.
/// Mọi truy vấn đều project cột tối thiểu, không load entity đầy đủ, để tránh N+1.
/// </summary>
public class AdminReportRepository : IAdminReportRepository
{
    private readonly AivesDbContext _db;

    public AdminReportRepository(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<(List<ExamReportRow> Items, int TotalCount)> GetExamOverviewAsync(
        int? courseId,
        ExamStatus? examStatus,
        string? search,
        bool includeInactive,
        bool isDescending)
    {
        var query = _db.Exams.AsNoTracking();

        if (!includeInactive)
            query = query.Where(e => e.IsActive);

        if (courseId.HasValue)
            query = query.Where(e => e.CourseId == courseId.Value);

        // So sánh thẳng với giá trị enum: EF ánh xạ sang native enum của PostgreSQL.
        // Không dùng .ToString() — hàm đó không dịch được sang SQL (xem IAdminReportRepository).
        if (examStatus.HasValue)
            query = query.Where(e => e.ExamStatus == examStatus.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(e =>
                EF.Functions.ILike(e.Title, pattern) ||
                EF.Functions.ILike(e.ExamCode, pattern));
        }

        var total = await query.CountAsync();

        var items = await (isDescending
                ? query.OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.ExamId)
                : query.OrderBy(e => e.CreatedAt).ThenBy(e => e.ExamId))
            .Select(e => new ExamReportRow
            {
                ExamId = e.ExamId,
                Title = e.Title,
                ExamCode = e.ExamCode,
                CourseId = e.CourseId,
                CourseName = e.Course != null ? e.Course.CourseName : null,
                CreatedBy = e.CreatedBy,
                CreatedByName = e.CreatedByNavigation != null ? e.CreatedByNavigation.FullName : null,
                DurationMinutes = e.DurationMinutes,
                MaxQuestions = e.MaxQuestions,
                QuestionCount = e.ExamQuestions.Count,
                SessionCount = e.ExamSessions.Count,
                ExamStatus = e.ExamStatus != null ? e.ExamStatus.ToString() : null,
                IsActive = e.IsActive,
                CreatedAt = e.CreatedAt
            })
            .ToListAsync();

        return (items, total);
    }

    public async Task<AdminReportSnapshot> GetSummarySnapshotAsync(
        DateTime? fromDate,
        DateTime? toDate,
        int? courseId,
        SessionStatus? sessionStatus)
    {
        // ----- Sessions -----
        var sessions = _db.ExamSessions.AsNoTracking().AsQueryable();
        if (courseId.HasValue)
            sessions = sessions.Where(s => s.Exam.CourseId == courseId.Value);
        if (sessionStatus.HasValue)
            sessions = sessions.Where(s => s.Status == sessionStatus.Value);
        if (fromDate.HasValue)
            sessions = sessions.Where(s => s.StartTime >= fromDate.Value);
        if (toDate.HasValue)
            sessions = sessions.Where(s => s.StartTime <= toDate.Value);

        var now = DateTime.UtcNow;
        var sessionStats = await sessions
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Active = g.Count(s => s.Status == SessionStatus.IN_PROGRESS),
                Completed = g.Count(s => s.Status == SessionStatus.COMPLETED),
                Upcoming = g.Count(s => s.Status == SessionStatus.NOT_STARTED && s.StartTime > now),
                Cancelled = g.Count(s => s.Status == SessionStatus.CANCELLED)
            })
            .FirstOrDefaultAsync();

        // ----- Enrollments (participants registered for the filtered sessions) -----
        var enrollments = _db.SessionEnrollments.AsNoTracking().AsQueryable();
        if (courseId.HasValue)
            enrollments = enrollments.Where(e => e.Session.Exam.CourseId == courseId.Value);
        if (sessionStatus.HasValue)
            enrollments = enrollments.Where(e => e.Session.Status == sessionStatus.Value);
        if (fromDate.HasValue)
            enrollments = enrollments.Where(e => e.Session.StartTime >= fromDate.Value);
        if (toDate.HasValue)
            enrollments = enrollments.Where(e => e.Session.StartTime <= toDate.Value);

        var totalEnrollments = await enrollments.CountAsync();

        // ----- Results -----
        var results = _db.Results.AsNoTracking().AsQueryable();
        if (courseId.HasValue)
            results = results.Where(r => r.Session.Exam.CourseId == courseId.Value);
        if (sessionStatus.HasValue)
            results = results.Where(r => r.Session.Status == sessionStatus.Value);
        if (fromDate.HasValue)
            results = results.Where(r => r.Session.StartTime >= fromDate.Value);
        if (toDate.HasValue)
            results = results.Where(r => r.Session.StartTime <= toDate.Value);

        var resultStats = await results
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Completed = g.Count(r => r.Status == SubmissionStatus.REVIEWED || r.Status == SubmissionStatus.PUBLISHED),
                PendingReview = g.Count(r => r.Status == SubmissionStatus.PENDING_REVIEW),
                Reviewed = g.Count(r => r.Status == SubmissionStatus.REVIEWED),
                Published = g.Count(r => r.Status == SubmissionStatus.PUBLISHED),
                Submitted = g.Count(r => r.Status == SubmissionStatus.SUBMITTED),
                Cancelled = g.Count(r => r.Status == SubmissionStatus.CANCELLED),
                AvgAi = g.Where(r => r.AiScore != null).Average(r => (double?)r.AiScore),
                AvgFinal = g.Where(r => r.FinalScore != null).Average(r => (double?)r.FinalScore),
                MaxFinal = g.Where(r => r.FinalScore != null).Max(r => (double?)r.FinalScore),
                MinFinal = g.Where(r => r.FinalScore != null).Min(r => (double?)r.FinalScore)
            })
            .FirstOrDefaultAsync();

        // ----- Answers processing health -----
        var answers = _db.Answers.AsNoTracking().AsQueryable();
        if (courseId.HasValue)
            answers = answers.Where(a => a.Result.Session.Exam.CourseId == courseId.Value);
        if (sessionStatus.HasValue)
            answers = answers.Where(a => a.Result.Session.Status == sessionStatus.Value);
        if (fromDate.HasValue)
            answers = answers.Where(a => a.Result.Session.StartTime >= fromDate.Value);
        if (toDate.HasValue)
            answers = answers.Where(a => a.Result.Session.StartTime <= toDate.Value);

        var answerStats = await answers
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Processing = g.Count(a => a.Status == AnswerStatus.PROCESSING),
                Evaluated = g.Count(a => a.Status == AnswerStatus.EVALUATED),
                Error = g.Count(a => a.Status == AnswerStatus.ERROR)
            })
            .FirstOrDefaultAsync();

        return new AdminReportSnapshot
        {
            TotalSessions = sessionStats?.Total ?? 0,
            ActiveSessions = sessionStats?.Active ?? 0,
            CompletedSessions = sessionStats?.Completed ?? 0,
            UpcomingSessions = sessionStats?.Upcoming ?? 0,
            CancelledSessions = sessionStats?.Cancelled ?? 0,
            TotalEnrollments = totalEnrollments,
            TotalResults = resultStats?.Total ?? 0,
            CompletedAttempts = resultStats?.Completed ?? 0,
            AverageAiScore = resultStats?.AvgAi,
            AverageFinalScore = resultStats?.AvgFinal,
            HighestFinalScore = resultStats?.MaxFinal,
            LowestFinalScore = resultStats?.MinFinal,
            PendingReviewCount = resultStats?.PendingReview ?? 0,
            ReviewedCount = resultStats?.Reviewed ?? 0,
            PublishedCount = resultStats?.Published ?? 0,
            SubmittedCount = resultStats?.Submitted ?? 0,
            CancelledResultCount = resultStats?.Cancelled ?? 0,
            ProcessingAnswerCount = answerStats?.Processing ?? 0,
            EvaluatedAnswerCount = answerStats?.Evaluated ?? 0,
            ErrorAnswerCount = answerStats?.Error ?? 0
        };
    }
}