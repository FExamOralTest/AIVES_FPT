using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.DTOs;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Queue;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Test logic phân quyền của <see cref="RetryAnswerService"/> — đây là phần quan trọng nhất
/// (RBAC + dedup + orphan detection). Storage và queue thực được mock bằng no-op.
/// </summary>
public class RetryAnswerServiceTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly FakeQueueService _queue;
    private readonly RetryAnswerService _service;

    public RetryAnswerServiceTests()
    {
        // InMemory provider đã có sẵn trong csproj tests.
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"RetryTest_{Guid.NewGuid()}")
            .Options;
        _db = new AivesDbContext(options);
        var answerRepo = new AnswerRepository(_db);
        _queue = new FakeQueueService();
        _service = new RetryAnswerService(
            answerRepo,
            _queue,
            NullLogger<RetryAnswerService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task Student_CanRetryOwnAnswer_AfterError()
    {
        // Arrange
        var (answer, student, examSession) = await SeedAsync(AnswerStatus.ERROR);
        var req = new RetryAnswerRequest { Reason = "test" };

        // Act
        var result = await _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req);

        // Assert
        Assert.Equal("QUEUED", result.Status);
        Assert.Equal("ERROR", result.PreviousStatus);
        Assert.False(string.IsNullOrEmpty(result.JobId));
        Assert.Single(_queue.EnqueuedJobs);
        Assert.Equal(answer.AnswerId, _queue.EnqueuedJobs[0].AnswerId);
        Assert.Equal(1, _queue.EnqueuedJobs[0].Attempt);

        // Verify DB state was reset
        var updatedAnswer = await _db.Answers.FindAsync(answer.AnswerId);
        Assert.Equal(AnswerStatus.PROCESSING, updatedAnswer!.Status);
        Assert.Null(updatedAnswer.Transcript);
    }

    [Fact]
    public async Task Student_CannotRetry_OtherStudentsAnswer()
    {
        var (answer, _, _) = await SeedAsync(AnswerStatus.ERROR, studentId: 100);
        var req = new RetryAnswerRequest();

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.RetryAsync(answer.AnswerId, currentUserId: 999, UserRole.STUDENT, req));
    }

    [Fact]
    public async Task Staff_CannotRetry_OtherStaffSessionAnswer()
    {
        // Answer thuộc session scheduled bởi user 200; staff caller là user 300.
        var (answer, _, examSession) = await SeedAsync(AnswerStatus.ERROR, scheduledBy: 200);
        var req = new RetryAnswerRequest();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _service.RetryAsync(answer.AnswerId, currentUserId: 300, UserRole.STAFF, req));
    }

    [Fact]
    public async Task Admin_CanRetry_AnyAnswer()
    {
        var (answer, _, _) = await SeedAsync(AnswerStatus.ERROR, scheduledBy: 200);
        var req = new RetryAnswerRequest();

        var result = await _service.RetryAsync(answer.AnswerId, currentUserId: 999, UserRole.ADMIN, req);
        Assert.Equal("QUEUED", result.Status);
    }

    [Fact]
    public async Task Retry_EvaluatedAnswer_Throws()
    {
        var (answer, student, _) = await SeedAsync(AnswerStatus.EVALUATED);
        var req = new RetryAnswerRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req));
    }

    [Fact]
    public async Task Retry_FreshProcessing_Throws_NotYetOrphan()
    {
        // Answer mới vào PROCESSING, answeredAt = now → chưa orphan.
        var (answer, student, _) = await SeedAsync(AnswerStatus.PROCESSING, withAnsweredAt: true);
        var req = new RetryAnswerRequest();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req));
    }

    [Fact]
    public async Task Retry_OrphanProcessing_Allowed()
    {
        // Answer PROCESSING nhưng answeredAt 2 tiếng trước → orphan, retry được.
        var (answer, student, _) = await SeedAsync(
            AnswerStatus.PROCESSING, withAnsweredAt: true, answeredAt: DateTime.UtcNow.AddHours(-2));
        var req = new RetryAnswerRequest();

        var result = await _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req);
        Assert.Equal("QUEUED", result.Status);
    }

    [Fact]
    public async Task Retry_Dedup_DoesNotEnqueueTwice()
    {
        // Setup: answer ở trạng thái ERROR, queue rỗng.
        var (answer, student, _) = await SeedAsync(AnswerStatus.ERROR);
        var req = new RetryAnswerRequest();

        // Lần 1: enqueue bình thường.
        var r1 = await _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req);
        Assert.Equal("QUEUED", r1.Status);

        // Lần 2: queue đã có job → dedup thắng → trả ALREADY_QUEUED, không enqueue thêm.
        var r2 = await _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req);
        Assert.Equal("ALREADY_QUEUED", r2.Status);
        Assert.Single(_queue.EnqueuedJobs);
    }

    [Fact]
    public async Task Retry_NotFound_Throws()
    {
        var req = new RetryAnswerRequest();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.RetryAsync(answerId: 9999, currentUserId: 1, UserRole.ADMIN, req));
    }

    [Fact]
    public async Task Retry_ExhaustedAttempts_Throws_MaxAttemptsReached()
    {
        // BR-17: "retry tối đa 3 lần thử tổng cộng". Khi attempt_count đã đạt giới hạn
        // thì không được retry thủ công nữa.
        var (answer, student, _) = await SeedAsync(AnswerStatus.ERROR);
        var dbAnswer = await _db.Answers.FindAsync(answer.AnswerId);
        dbAnswer!.AttemptCount = QueueLimits.MaxTotalAttempts;
        await _db.SaveChangesAsync();

        var req = new RetryAnswerRequest();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req));
        Assert.Empty(_queue.EnqueuedJobs);
    }

    [Fact]
    public async Task Retry_IncrementsAttemptInMetadata()
    {
        // Attempt trong metadata = lần thử tiếp theo (attempt_count hiện tại + 1).
        var (answer, student, _) = await SeedAsync(AnswerStatus.ERROR);
        var dbAnswer = await _db.Answers.FindAsync(answer.AnswerId);
        dbAnswer!.AttemptCount = 1;
        await _db.SaveChangesAsync();

        var req = new RetryAnswerRequest();
        var result = await _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req);

        Assert.Equal("QUEUED", result.Status);
        Assert.Equal(2, _queue.EnqueuedJobs[0].Attempt);
    }

    [Fact]
    public async Task Retry_PersistsJobIdToAnswer()
    {
        // JobId được lưu vào DB để tra cứu job không chỉ từ Hangfire dashboard.
        var (answer, student, _) = await SeedAsync(AnswerStatus.ERROR);
        var req = new RetryAnswerRequest();

        var result = await _service.RetryAsync(answer.AnswerId, student.UserId, UserRole.STUDENT, req);

        var dbAnswer = await _db.Answers.FindAsync(answer.AnswerId);
        Assert.Equal(result.JobId, dbAnswer!.JobId);
    }

    // ----- Helpers -----

    private async Task<(Answer answer, User student, ExamSession examSession)> SeedAsync(
        AnswerStatus status,
        int studentId = 100,
        int scheduledBy = 100,
        bool withAnsweredAt = false,
        DateTime? answeredAt = null)
    {
        var course = new Course { CourseCode = "TEST", CourseName = "Test Course" };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var teacher = new User
        {
            Username = "teacher1",
            Email = "t@a.com",
            FullName = "Teacher",
            Role = UserRole.EXAMINER,
            PasswordHash = "x"
        };
        _db.Users.Add(teacher);
        await _db.SaveChangesAsync();

        var student = new User
        {
            UserId = studentId,
            Username = $"s{studentId}",
            Email = $"s{studentId}@a.com",
            FullName = "Student",
            Role = UserRole.STUDENT,
            PasswordHash = "x"
        };
        _db.Users.Add(student);
        await _db.SaveChangesAsync();

        var exam = new Exam { ExamCode = "E1", Title = "Exam 1", CourseId = course.CourseId, DurationMinutes = 60 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var examSession = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "S1",
            SessionName = "Session 1",
            Status = SessionStatus.IN_PROGRESS,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(1),
            ScheduledBy = scheduledBy
        };
        _db.ExamSessions.Add(examSession);
        await _db.SaveChangesAsync();

        var result = new Result
        {
            SessionId = examSession.SessionId,
            StudentId = studentId,
            Status = SubmissionStatus.PENDING_REVIEW,
            SubmittedAt = DateTime.UtcNow
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        // QuestionBank cần tồn tại để Answer có thể resolve navigation Question
        // (InMemory provider không lazy-load, nhưng Include sẽ trả null nếu FK invalid).
        var question = new QuestionBank
        {
            QuestionId = 1,
            CourseId = course.CourseId,
            QuestionText = "Q1",
            MaxScore = 10.0f
        };
        _db.QuestionBanks.Add(question);
        await _db.SaveChangesAsync();

        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = 1,
            Status = status,
            StudentAnswer = "abc",
            Transcript = status == AnswerStatus.EVALUATED ? "transcript text" : null,
            AudioUrl = "exams/1/sessions/1/students/100/q_1.webm",
            AnsweredAt = withAnsweredAt ? (answeredAt ?? DateTime.UtcNow) : null
        };
        _db.Answers.Add(answer);
        await _db.SaveChangesAsync();

        return (answer, student, examSession);
    }
}
