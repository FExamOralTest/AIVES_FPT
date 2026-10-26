using System;
using System.Linq;
using System.Threading.Tasks;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Test cho <c>GET /api/admin/exams</c> và <c>GET /api/admin/reports/summary</c>.
/// Dùng InMemory provider cho phần filter/phân trang; các aggregate dùng
/// Moq để verify mapping DTO và validation.
/// </summary>
public class AdminReportTests : IDisposable
{
    private readonly AivesDbContext _db;

    public AdminReportTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdminReportTest_{Guid.NewGuid()}")
            .Options;
        _db = new AivesDbContext(options);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    // =====================================================================
    // GET /api/admin/exams
    // =====================================================================

    [Fact]
    public async Task GetExamOverviewAsync_ReturnsAllExams_WithQuestionAndSessionCounts()
    {
        var repo = new AdminReportRepository(_db);
        await SeedAsync();

        var (rows, total) = await repo.GetExamOverviewAsync(null, null, null, true, true);

        Assert.Equal(2, total);
        Assert.Equal(2, rows.Count);
        // Newest first.
        Assert.Equal("Midterm", rows[0].Title);
        Assert.Equal(2, rows[0].QuestionCount);
        Assert.Equal(1, rows[0].SessionCount);
        Assert.Equal(0, rows[1].SessionCount);
        Assert.Equal("Software Engineering", rows[0].CourseName);
        Assert.Equal("Staff One", rows[0].CreatedByName);
    }

    [Fact]
    public async Task GetExamOverviewAsync_FiltersByCourse()
    {
        var repo = new AdminReportRepository(_db);
        var (courseAId, _, _) = await SeedAsync();

        var (rows, total) = await repo.GetExamOverviewAsync(courseAId, null, null, true, true);

        Assert.Equal(1, total);
        Assert.Equal("Midterm", rows[0].Title);
    }

    [Fact]
    public async Task GetExamOverviewAsync_FiltersByStatus()
    {
        var repo = new AdminReportRepository(_db);
        await SeedAsync();

        var (rows, total) = await repo.GetExamOverviewAsync(null, ExamStatus.CLOSED, null, true, true);

        Assert.Equal(1, total);
        Assert.Equal("Final", rows[0].Title);
    }

    [Fact]
    public async Task GetExamOverviewAsync_ReturnsEveryMatchingRow()
    {
        var repo = new AdminReportRepository(_db);
        await SeedAsync();

        var (rows, total) = await repo.GetExamOverviewAsync(null, null, null, true, true);

        // No pagination: rows count always matches total.
        Assert.Equal(total, rows.Count);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public async Task GetExamOverviewAsync_ExcludeInactive_ReturnsOnlyActiveExams()
    {
        var repo = new AdminReportRepository(_db);
        var (_, _, staffId) = await SeedAsync();

        // Soft-delete the "Final" exam.
        var final = _db.Exams.Single(e => e.ExamCode == "EX-FIN");
        final.IsActive = false;
        await _db.SaveChangesAsync();

        var (all, totalAll) = await repo.GetExamOverviewAsync(null, null, null, true, true);
        var (active, totalActive) = await repo.GetExamOverviewAsync(null, null, null, false, true);

        Assert.Equal(2, totalAll);        // includeInactive = true → everything
        Assert.Equal(2, all.Count);
        Assert.Equal(1, totalActive);     // includeInactive = false → active only
        Assert.Equal("Midterm", active[0].Title);
        Assert.True(active[0].IsActive);
    }

    [Fact]
    public async Task GetExamOverviewAsync_AscendingSort_ReturnsOldestFirst()
    {
        var repo = new AdminReportRepository(_db);
        await SeedAsync();

        var (desc, _) = await repo.GetExamOverviewAsync(null, null, null, true, true);
        var (asc, _) = await repo.GetExamOverviewAsync(null, null, null, true, false);

        Assert.Equal("Midterm", desc[0].Title);   // newest first
        Assert.Equal("Final", asc[0].Title);      // oldest first
    }

    // =====================================================================
    // GET /api/admin/reports/summary
    // =====================================================================

    [Fact]
    public async Task GetSummaryAsync_MapsSnapshotToDto()
    {
        var snapshot = new AdminReportSnapshot
        {
            TotalSessions = 5,
            ActiveSessions = 1,
            CompletedSessions = 3,
            UpcomingSessions = 1,
            CancelledSessions = 0,
            TotalEnrollments = 40,
            TotalResults = 12,
            CompletedAttempts = 8,
            AverageAiScore = 7.25,
            AverageFinalScore = 7.8,
            HighestFinalScore = 9.5,
            LowestFinalScore = 5.0,
            PendingReviewCount = 4,
            ReviewedCount = 2,
            PublishedCount = 6,
            SubmittedCount = 0,
            CancelledResultCount = 0,
            ProcessingAnswerCount = 3,
            EvaluatedAnswerCount = 50,
            ErrorAnswerCount = 1
        };
        var repo = new StubAdminReportRepository(snapshot);
        var service = new AdminReportService(repo);

        var dto = await service.GetSummaryAsync(new AdminReportSummaryQuery());

        Assert.Equal(5, dto.TotalSessions);
        Assert.Equal(40, dto.TotalEnrollments);
        Assert.Equal(7.25, dto.AverageAiScore);
        Assert.Equal(4, dto.PendingReviewCount);
        Assert.Equal(6, dto.PublishedCount);
        Assert.Equal(1, dto.ErrorAnswerCount);
        Assert.NotEqual(default, dto.GeneratedAt);
    }

    [Fact]
    public async Task GetSummaryAsync_WhenFromDateAfterToDate_Throws()
    {
        var service = new AdminReportService(new StubAdminReportRepository(new AdminReportSnapshot()));

        var query = new AdminReportSummaryQuery
        {
            FromDate = DateTime.UtcNow,
            ToDate = DateTime.UtcNow.AddDays(-1)
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetSummaryAsync(query));
    }

    [Fact]
    public async Task GetExamListAsync_ReturnsAllRowsWithTotalCount()
    {
        var repo = new StubAdminReportRepository(new AdminReportSnapshot())
        {
            ExamRows = Enumerable.Range(0, 5).Select(i => new ExamReportRow
            {
                ExamId = i,
                Title = $"Exam {i}",
                ExamCode = $"EX-{i}"
            }).ToList(),
            ExamTotal = 25
        };
        var service = new AdminReportService(repo);

        var response = await service.GetExamListAsync(new AdminExamQuery());

        // No pagination: every matching row comes back, total still reflects the full match count.
        Assert.Equal(25, response.TotalCount);
        Assert.Equal(5, response.Items.Count);
    }

    [Fact]
    public async Task GetExamListAsync_WithInvalidExamStatus_Throws()
    {
        var service = new AdminReportService(new StubAdminReportRepository(new AdminReportSnapshot()));
        var query = new AdminExamQuery { ExamStatus = "NOT_A_STATUS" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GetExamListAsync(query));

        // The message must list the allowed values so the API client can self-correct.
        Assert.Contains("DRAFT", ex.Message);
        Assert.Contains("CANCELLED", ex.Message);
    }

    [Fact]
    public async Task GetExamListAsync_WithLowercaseExamStatus_IsAccepted()
    {
        var stub = new StubAdminReportRepository(new AdminReportSnapshot());
        var service = new AdminReportService(stub);

        // Enum names are DRAFT/SCHEDULED/...; clients often send lowercase.
        var response = await service.GetExamListAsync(new AdminExamQuery { ExamStatus = "closed" });

        Assert.Equal(ExamStatus.CLOSED, stub.LastExamStatusFilter);
    }

    [Fact]
    public async Task GetSummaryAsync_WithInvalidSessionStatus_Throws()
    {
        var service = new AdminReportService(new StubAdminReportRepository(new AdminReportSnapshot()));
        var query = new AdminReportSummaryQuery { SessionStatus = "NOPE" };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.GetSummaryAsync(query));

        Assert.Contains("NOT_STARTED", ex.Message);
    }

    [Fact]
    public async Task GetExamListAsync_WithBlankStatus_DoesNotFilter()
    {
        var stub = new StubAdminReportRepository(new AdminReportSnapshot());
        var service = new AdminReportService(stub);

        await service.GetExamListAsync(new AdminExamQuery { ExamStatus = "   " });

        Assert.Null(stub.LastExamStatusFilter);
    }

    // ----- Helpers -----

    private async Task<(int courseA, int courseB, int staffId)> SeedAsync()
    {
        var staff = new User
        {
            Username = "staff1",
            Email = "staff1@a.com",
            FullName = "Staff One",
            Role = UserRole.STAFF,
            PasswordHash = "x"
        };
        _db.Users.Add(staff);
        await _db.SaveChangesAsync();

        var courseA = new Course { CourseCode = "SE101", CourseName = "Software Engineering" };
        var courseB = new Course { CourseCode = "SE102", CourseName = "Databases" };
        _db.Courses.AddRange(courseA, courseB);
        await _db.SaveChangesAsync();

        var midterm = new Exam
        {
            ExamCode = "EX-MID",
            Title = "Midterm",
            CourseId = courseA.CourseId,
            CreatedBy = staff.UserId,
            DurationMinutes = 60,
            MaxQuestions = 10,
            ExamStatus = ExamStatus.OPEN,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        var final = new Exam
        {
            ExamCode = "EX-FIN",
            Title = "Final",
            CourseId = courseB.CourseId,
            CreatedBy = staff.UserId,
            DurationMinutes = 90,
            MaxQuestions = 15,
            ExamStatus = ExamStatus.CLOSED,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        _db.Exams.AddRange(midterm, final);
        await _db.SaveChangesAsync();

        _db.QuestionBanks.AddRange(
            new QuestionBank { CourseId = courseA.CourseId, QuestionText = "Q1", MaxScore = 10f },
            new QuestionBank { CourseId = courseA.CourseId, QuestionText = "Q2", MaxScore = 10f });
        await _db.SaveChangesAsync();

        var questions = _db.QuestionBanks.Where(q => q.CourseId == courseA.CourseId).ToList();
        _db.ExamQuestions.AddRange(questions.Select(q => new ExamQuestion
        {
            ExamId = midterm.ExamId,
            QuestionId = q.QuestionId,
            QuestionScore = 5f
        }));
        await _db.SaveChangesAsync();

        _db.ExamSessions.Add(new ExamSession
        {
            ExamId = midterm.ExamId,
            SessionCode = "S-MID",
            SessionName = "Midterm Session",
            Status = SessionStatus.IN_PROGRESS,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(1),
            ScheduledBy = staff.UserId
        });
        await _db.SaveChangesAsync();

        return (courseA.CourseId, courseB.CourseId, staff.UserId);
    }

    private sealed class StubAdminReportRepository(AdminReportSnapshot snapshot) : IAdminReportRepository
    {
        public List<ExamReportRow> ExamRows { get; set; } = new();
        public int ExamTotal { get; set; }

        /// <summary>Captures the parsed enum filter so tests can assert parsing happened correctly.</summary>
        public ExamStatus? LastExamStatusFilter { get; private set; }

        public Task<(List<ExamReportRow> Items, int TotalCount)> GetExamOverviewAsync(
            int? courseId, ExamStatus? examStatus, string? search,
            bool includeInactive, bool isDescending)
        {
            LastExamStatusFilter = examStatus;
            return Task.FromResult((ExamRows, ExamTotal));
        }

        public Task<AdminReportSnapshot> GetSummarySnapshotAsync(
            DateTime? fromDate, DateTime? toDate, int? courseId, SessionStatus? sessionStatus)
            => Task.FromResult(snapshot);
    }
}