using Aives.Application.Queue.Contracts;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Tests for the BR-17 attempt counter stored on the <c>answers</c> table.
/// The database is the single source of truth for "which attempt are we on", because the
/// Hangfire job metadata is re-serialized on every retry and therefore never increments.
/// </summary>
public class AnswerAttemptCounterTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly AnswerRepository _repository;

    public AnswerAttemptCounterTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"AttemptCounter_{Guid.NewGuid()}")
            .Options;
        _db = new AivesDbContext(options);
        _repository = new AnswerRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task IncrementAttemptAsync_ReturnsIncrementedValue_AndStoresJobId()
    {
        var answerId = await SeedAnswerAsync();

        var first = await _repository.IncrementAttemptAsync(answerId, "job-1");
        var second = await _repository.IncrementAttemptAsync(answerId, "job-2");

        Assert.Equal(1, first);
        Assert.Equal(2, second);

        var dbAnswer = await _db.Answers.FindAsync(answerId);
        Assert.Equal(2, dbAnswer!.AttemptCount);
        Assert.Equal("job-2", dbAnswer.JobId);
    }

    [Fact]
    public async Task IncrementAttemptAsync_MissingAnswer_ReturnsZero()
    {
        var result = await _repository.IncrementAttemptAsync(999_999, "job-x");
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ResetForRetryAsync_KeepsAttemptCount()
    {
        // BR-17 counts the total attempts across the whole lifetime of the answer,
        // so a manual retry must not reset the counter.
        var answerId = await SeedAnswerAsync();
        await _repository.IncrementAttemptAsync(answerId, "job-1");

        await _repository.ResetForRetryAsync(answerId);

        var dbAnswer = await _db.Answers.FindAsync(answerId);
        Assert.Equal(1, dbAnswer!.AttemptCount);
        Assert.Equal(AnswerStatus.PROCESSING, dbAnswer.Status);
        Assert.Null(dbAnswer.Transcript);
    }

    [Fact]
    public async Task UpdateAudioUrlAsync_ResetsAttemptCount()
    {
        // A brand new upload is a brand new processing cycle, so the budget starts over.
        var answerId = await SeedAnswerAsync();
        await _repository.IncrementAttemptAsync(answerId, "job-1");

        await _repository.UpdateAudioUrlAsync(answerId, "exams/1/sessions/1/q_1.webm");

        var dbAnswer = await _db.Answers.FindAsync(answerId);
        Assert.Equal(0, dbAnswer!.AttemptCount);
        Assert.Null(dbAnswer.JobId);
    }

    [Fact]
    public async Task SetJobIdAsync_StoresJobId()
    {
        var answerId = await SeedAnswerAsync();

        await _repository.SetJobIdAsync(answerId, "job-abc");

        var dbAnswer = await _db.Answers.FindAsync(answerId);
        Assert.Equal("job-abc", dbAnswer!.JobId);
    }

    [Fact]
    public void QueueLimits_Exhaustion_RespectsMaxTotalAttempts()
    {
        Assert.False(QueueLimits.IsExhausted(0));
        Assert.False(QueueLimits.IsExhausted(1));
        Assert.False(QueueLimits.IsExhausted(2));
        Assert.True(QueueLimits.IsExhausted(3));
        Assert.True(QueueLimits.IsExhausted(4));
    }

    private async Task<int> SeedAnswerAsync()
    {
        var course = new Course { CourseCode = "ATT", CourseName = "Attempt Test" };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var student = new User
        {
            UserId = 6001, Username = "a1", Email = "a1@a.com", FullName = "A",
            Role = UserRole.STUDENT, PasswordHash = "x"
        };
        _db.Users.Add(student);

        var exam = new Exam { ExamCode = "ATT-E", Title = "Attempt", CourseId = course.CourseId, DurationMinutes = 30 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "ATT-S",
            SessionName = "Attempt Session",
            Status = SessionStatus.IN_PROGRESS,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(1),
            ScheduledBy = 1
        };
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = new Result
        {
            SessionId = session.SessionId,
            StudentId = student.UserId,
            Status = SubmissionStatus.PENDING_REVIEW,
            SubmittedAt = DateTime.UtcNow
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var qb = new QuestionBank { CourseId = course.CourseId, QuestionText = "Q", MaxScore = 10 };
        _db.QuestionBanks.Add(qb);
        await _db.SaveChangesAsync();

        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = qb.QuestionId,
            Status = AnswerStatus.PROCESSING,
            StudentAnswer = "abc"
        };
        _db.Answers.Add(answer);
        await _db.SaveChangesAsync();

        return answer.AnswerId;
    }
}
