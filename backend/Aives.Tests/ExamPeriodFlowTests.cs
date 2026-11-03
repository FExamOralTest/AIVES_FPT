using System.Security.Claims;
using System.Net;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using Aives.Api.Controllers;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Exams;
using Aives.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aives.Tests;

public class ExamPeriodFlowTests : IDisposable
{
    private readonly AivesDbContext db = new(new DbContextOptionsBuilder<AivesDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly DateTime start = new(2026, 11, 3, 1, 0, 0, DateTimeKind.Utc);

    public ExamPeriodFlowTests()
    {
        var staff = new User { UserId = 1, FullName = "Staff", Username = "staff", Email = "staff@test", PasswordHash = "hash", Role = UserRole.STAFF };
        var examiner = new User { UserId = 2, FullName = "Examiner", Username = "examiner", Email = "examiner@test", PasswordHash = "hash", Role = UserRole.EXAMINER };
        var student = new User { UserId = 3, FullName = "Demo Student", Username = "SE001", Email = "student@test", PasswordHash = "hash", Role = UserRole.STUDENT };
        db.Users.AddRange(staff, examiner, student);
        db.Courses.AddRange(new Course { CourseId = 1, CourseName = "AI", CourseCode = "AI101", TeacherId = 1 },
            new Course { CourseId = 2, CourseName = "Java", CourseCode = "PRJ", TeacherId = 9 });
        db.ExamPeriods.AddRange(new ExamPeriod { PeriodId = 1, CourseId = 1, PeriodName = "Final", StartDate = start.Date, EndDate = start.Date, CreatedBy = 1, Status = ExamPeriodStatus.ACTIVE },
            new ExamPeriod { PeriodId = 2, CourseId = 2, PeriodName = "Other", StartDate = start.Date, EndDate = start.Date, CreatedBy = 1, Status = ExamPeriodStatus.ACTIVE });
        db.Exams.Add(new Exam { ExamId = 1, CourseId = 1, CreatedBy = 1, Title = "Paper", ExamCode = "EX1" });
        db.ExamSessions.AddRange(Session(1, 1), Session(2, 1), Session(3, 2), Session(4, null));
        db.Results.AddRange(Result(10, 1, SubmissionStatus.PENDING_REVIEW, AiEvaluationStatus.FAILED),
            Result(11, 2, SubmissionStatus.PUBLISHED, AiEvaluationStatus.SUCCEEDED),
            Result(12, 3, SubmissionStatus.PENDING_REVIEW, AiEvaluationStatus.PENDING),
            Result(13, 4, SubmissionStatus.REVIEWED, AiEvaluationStatus.SUCCEEDED));
        db.SaveChanges();
    }

    private ExamSession Session(int id, int? periodId) => new() {
        SessionId = id, CourseId = periodId == 2 ? 2 : 1, ExamId = 1, PeriodId = periodId, ScheduledBy = 1,
        SessionName = $"Session {id}", SessionCode = $"CODE{id}", StartTime = start, EndTime = start.AddHours(1), Status = SessionStatus.NOT_STARTED };
    private Result Result(int id, int session, SubmissionStatus status, AiEvaluationStatus ai) => new() {
        ResultId = id, SessionId = session, StudentId = 3, Status = status, AiEvaluationStatus = ai,
        AiScore = ai == AiEvaluationStatus.SUCCEEDED ? 0 : null, SubmittedAt = start, ExamId = 1 };

    private ResultService Service(UserRole role)
    {
        var users = new Mock<IUserRepository>();
        users.Setup(u => u.GetByIdAsync(2)).ReturnsAsync(new User { Role = role });
        return new ResultService(new ResultRepository(db), new ExamSessionRepository(db), users.Object,
            Mock.Of<IStorageService>(), Mock.Of<IAuditLogRepository>());
    }

    [Fact]
    public async Task ExaminerFindsAllSessionsOfPeriod_AndZeroRemainsDifferentFromNotGraded()
    {
        var page = await Service(UserRole.EXAMINER).SearchAsync(new ResultSearchQuery { PeriodId = 1 }, 2);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(new[] { 11, 10 }, page.Items.Select(r => r.ResultId));
        Assert.Equal(0f, page.Items[0].AiScore);
        Assert.Null(page.Items[1].AiScore);
        Assert.All(page.Items, r => Assert.Equal(1, r.PeriodId));
    }

    [Fact]
    public async Task SearchSupportsStatusAiSessionStudentAndPagination()
    {
        var repo = new ResultRepository(db);
        var filtered = await repo.SearchAsync(new ResultSearchQuery { PeriodId = 1, SessionId = 1,
            Status = SubmissionStatus.PENDING_REVIEW, AiStatus = AiEvaluationStatus.FAILED, Search = " se001 " });
        Assert.Equal(10, Assert.Single(filtered.Items).ResultId);
        var page = await repo.SearchAsync(new ResultSearchQuery { PeriodId = 1, Page = 2, PageSize = 1 });
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(10, Assert.Single(page.Items).ResultId);
        Assert.Equal(2, page.Sessions.Count);
        Assert.Empty((await repo.SearchAsync(new ResultSearchQuery { PeriodId = 1, SessionId = 3 })).Items);
    }

    [Fact]
    public async Task UnassignedKeepsLegacyResultsAccessible()
    {
        var page = await Service(UserRole.EXAMINER).SearchAsync(new ResultSearchQuery { Unassigned = true }, 2);
        Assert.Equal(13, Assert.Single(page.Items).ResultId);
    }

    [Theory]
    [InlineData(UserRole.STAFF)]
    [InlineData(UserRole.STUDENT)]
    public async Task NonReviewersCannotSearchStudentAttempts(UserRole role) =>
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(role).SearchAsync(new ResultSearchQuery { PeriodId = 1 }, 2));

    [Fact]
    public async Task UnknownPeriodAndConflictingFiltersAreRejected()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(UserRole.EXAMINER).SearchAsync(new ResultSearchQuery { PeriodId = 999 }, 2));
        await Assert.ThrowsAsync<ArgumentException>(() => Service(UserRole.EXAMINER).SearchAsync(new ResultSearchQuery { PeriodId = 1, Unassigned = true }, 2));
    }

    [Fact]
    public async Task StaffCreatesPeriodForAssignedCourse_AndCannotCreateForOthers()
    {
        var controller = PeriodController(UserRole.STAFF, 1);
        var request = new CreateExamPeriodRequest { CourseId = 1, PeriodName = " New period ", StartDate = start.Date, EndDate = start.Date };
        var created = Assert.IsType<CreatedAtActionResult>(await controller.CreatePeriod(request));
        Assert.Equal("New period", Assert.IsType<ExamPeriodDto>(created.Value).PeriodName);
        request.CourseId = 2;
        Assert.IsType<ForbidResult>(await controller.CreatePeriod(request));
    }

    [Fact]
    public async Task PeriodReadScopeAllowsExaminersAllPeriodsAndStaffAssignedCourses()
    {
        var staff = Assert.IsType<OkObjectResult>(await PeriodController(UserRole.STAFF, 1).GetPeriods(null));
        Assert.Single(Assert.IsType<List<ExamPeriodDto>>(staff.Value));
        var examiner = Assert.IsType<OkObjectResult>(await PeriodController(UserRole.EXAMINER, 2).GetPeriods(null));
        Assert.Equal(2, Assert.IsType<List<ExamPeriodDto>>(examiner.Value).Count);
    }

    private ExamPeriodsController PeriodController(UserRole role, int id) => new(db) {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(
            new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()), new Claim(ClaimTypes.Role, role.ToString()) }, "test")) } } };

    [Fact]
    public async Task RepositoryRejectsWrongCourseInactiveAndOutsidePeriod()
    {
        var repo = new ExamSessionRepository(db);
        var session = Session(20, 2); session.CourseId = 1;
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CreateAsync(session));
        session.PeriodId = 1; session.EndTime = start.AddDays(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CreateAsync(session));
        session.EndTime = start.AddHours(1);
        var period = await db.ExamPeriods.FindAsync(1); period!.Status = ExamPeriodStatus.COMPLETED; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CreateAsync(session));
    }

    [Fact]
    public async Task ExistingAttemptCannotBeMovedToAnotherPeriod()
    {
        var session = await db.ExamSessions.FindAsync(1);
        session!.PeriodId = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ExamSessionRepository(db).UpdateAsync(session));
    }

    [Fact]
    public async Task StatusOnlyUpdateStillWorksForHistoricalPeriod()
    {
        var period = await db.ExamPeriods.FindAsync(1); period!.Status = ExamPeriodStatus.COMPLETED;
        await db.SaveChangesAsync();
        var session = await db.ExamSessions.FindAsync(1); session!.Status = SessionStatus.COMPLETED;
        var repo = new ExamSessionRepository(db);
        await repo.UpdateAsync(session); await repo.SaveChangesAsync();
        Assert.Equal(1, session.PeriodId);
    }

    [Fact]
    public async Task RecoveryRetakeInheritsPeriodEvenOutsideNormalWindow()
    {
        db.QuestionBanks.Add(new QuestionBank { QuestionId = 1, CourseId = 1, QuestionText = "Question", Level = 1, MaxScore = 10 });
        db.ExamQuestions.Add(new ExamQuestion { ExamId = 1, QuestionId = 1, QuestionScore = 10 });
        db.SessionEnrollments.Add(new SessionEnrollment { SessionId = 1, StudentId = 3, StartedAt = start, Status = SessionStatus.COMPLETED });
        await db.SaveChangesAsync();
        var nextStart = DateTime.UtcNow.AddHours(1);
        var grant = await new RetakeService(db).GrantAsync(10, new GrantRetakeRequest {
            Reason = "Network failure", StartTime = nextStart, EndTime = nextStart.AddHours(1), DurationMinutes = 30
        }, 2);
        var session = await db.ExamSessions.FindAsync(grant.RetakeSessionId);
        Assert.Equal(1, session!.PeriodId);
        Assert.Equal(1, session.CourseId);
        var results = await Service(UserRole.EXAMINER).SearchAsync(new ResultSearchQuery { PeriodId = 1 }, 2);
        Assert.Contains(results.Sessions, s => s.SessionId == grant.RetakeSessionId);
    }

    [Fact]
    public async Task InclusiveLastDayUsesVietnamCalendarAndBothCreatePathsAreGuarded()
    {
        var session = Session(20, 1);
        session.StartTime = new DateTime(2026, 11, 2, 17, 0, 0, DateTimeKind.Utc); // Nov 3, 00:00 VN
        session.EndTime = new DateTime(2026, 11, 3, 17, 0, 0, DateTimeKind.Utc); // Nov 4, 00:00 VN
        var repo = new ExamSessionRepository(db);
        await repo.CreateAsync(session);
        session.EndTime = session.EndTime.AddTicks(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateAsync(session));
    }

    [Fact]
    public async Task HttpExaminerCanReadPeriodsAndResults_ButOnlyStaffCanCreatePeriods()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton<IResultService>(Service(UserRole.EXAMINER));
        builder.Services.AddControllers().AddApplicationPart(typeof(ExamPeriodsController).Assembly);
        builder.Services.AddAuthentication("period-test").AddScheme<AuthenticationSchemeOptions, RoleHeaderHandler>("period-test", _ => { });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/exam-periods")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Role", "EXAMINER");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/exam-periods")).StatusCode);
        var found = await client.GetFromJsonAsync<ResultSearchResponse>("/api/results?periodId=1");
        Assert.Equal(2, found!.TotalCount);
        var request = new CreateExamPeriodRequest { CourseId = 1, PeriodName = "HTTP period", StartDate = start.Date, EndDate = start.Date };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/exam-periods", request)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "STAFF");
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/exam-periods", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/results?periodId=1")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "STUDENT");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/exam-periods")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/results?periodId=1")).StatusCode);
        await app.StopAsync();
    }

    // Test host only. Production JWT authentication is not changed.
    private sealed class RoleHeaderHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new[] { new Claim(ClaimTypes.Role, role), new Claim(ClaimTypes.NameIdentifier, role == "STAFF" ? "1" : "2") };
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }

    public void Dispose() => db.Dispose();
}
