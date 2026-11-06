using Aives.Application.Exams;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

public class ExamStatusTests : IDisposable
{
    private const int CourseId = 201;
    private const int TeacherId = 77;
    private const int OtherTeacherId = 78;

    private readonly AivesDbContext _db;
    private readonly ExamService _service;

    public ExamStatusTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase($"ExamStatusTests_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);
        _service = new ExamService(
            new ExamRepository(_db), new QuestionBankRepository(_db),
            new CourseRepository(_db), new ExamUnitOfWork(_db));

        _db.Users.AddRange(
            new User { UserId = TeacherId, Username = "owner", Email = "owner@fpt.edu.vn", PasswordHash = "x", FullName = "Owner", Role = UserRole.STAFF, CreatedAt = DateTime.UtcNow },
            new User { UserId = OtherTeacherId, Username = "other", Email = "other@fpt.edu.vn", PasswordHash = "x", FullName = "Other", Role = UserRole.STAFF, CreatedAt = DateTime.UtcNow });
        _db.Courses.Add(new Course { CourseId = CourseId, CourseCode = "SWD392", CourseName = "Software Design", TeacherId = TeacherId, CreatedAt = DateTime.UtcNow });
        _db.QuestionBanks.Add(new QuestionBank
        {
            QuestionId = 5001, CourseId = CourseId, QuestionText = "Explain layered architecture.",
            Level = 1, MaxScore = 1f, ApprovalStatus = QuestionApprovalStatus.APPROVED, CreatedAt = DateTime.UtcNow
        });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private int SeedExam(ExamStatus status, bool withQuestion = true)
    {
        var exam = new Exam
        {
            CourseId = CourseId, CreatedBy = TeacherId, Title = "Midterm viva",
            ExamCode = "EX-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            DurationMinutes = 30, MaxQuestions = 5, ExamStatus = status, CreatedAt = DateTime.UtcNow
        };
        if (withQuestion)
            exam.ExamQuestions.Add(new ExamQuestion { QuestionId = 5001, QuestionScore = 100 });
        _db.Exams.Add(exam);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return exam.ExamId;
    }

    [Theory]
    [InlineData(ExamStatus.DRAFT, "SCHEDULED")]
    [InlineData(ExamStatus.DRAFT, "open")]
    [InlineData(ExamStatus.SCHEDULED, "OPEN")]
    [InlineData(ExamStatus.SCHEDULED, "DRAFT")]
    [InlineData(ExamStatus.OPEN, "CLOSED")]
    [InlineData(ExamStatus.DRAFT, "CANCELLED")]
    public async Task Allowed_transition_is_saved(ExamStatus from, string to)
    {
        var examId = SeedExam(from);

        var dto = await _service.UpdateExamStatusAsync(examId, to, TeacherId);

        Assert.Equal(to.ToUpperInvariant(), dto.ExamStatus);
        _db.ChangeTracker.Clear();
        Assert.Equal(Enum.Parse<ExamStatus>(to, true), (await _db.Exams.SingleAsync(e => e.ExamId == examId)).ExamStatus);
    }

    [Theory]
    [InlineData(ExamStatus.OPEN, "DRAFT")]
    [InlineData(ExamStatus.OPEN, "CANCELLED")]
    [InlineData(ExamStatus.CLOSED, "OPEN")]
    [InlineData(ExamStatus.CANCELLED, "DRAFT")]
    [InlineData(ExamStatus.DRAFT, "CLOSED")]
    public async Task Disallowed_transition_is_rejected(ExamStatus from, string to)
    {
        var examId = SeedExam(from);

        await Assert.ThrowsAsync<QuestionBankConflictException>(
            () => _service.UpdateExamStatusAsync(examId, to, TeacherId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("PUBLISHED")]
    [InlineData("7")]
    public async Task Unknown_status_is_a_bad_request(string to)
    {
        var examId = SeedExam(ExamStatus.DRAFT);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateExamStatusAsync(examId, to, TeacherId));
    }

    [Fact]
    public async Task Exam_without_questions_cannot_be_opened()
    {
        var examId = SeedExam(ExamStatus.DRAFT, withQuestion: false);

        var ex = await Assert.ThrowsAsync<QuestionBankConflictException>(
            () => _service.UpdateExamStatusAsync(examId, "OPEN", TeacherId));
        Assert.Contains("Add questions", ex.Message);
    }

    [Fact]
    public async Task Exam_without_questions_can_still_be_cancelled()
    {
        var examId = SeedExam(ExamStatus.DRAFT, withQuestion: false);

        var dto = await _service.UpdateExamStatusAsync(examId, "CANCELLED", TeacherId);

        Assert.Equal("CANCELLED", dto.ExamStatus);
    }

    [Fact]
    public async Task Only_the_exam_creator_can_change_status()
    {
        var examId = SeedExam(ExamStatus.DRAFT);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateExamStatusAsync(examId, "OPEN", OtherTeacherId));
    }

    [Fact]
    public async Task Missing_exam_is_not_found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateExamStatusAsync(999_999, "OPEN", TeacherId));
    }

    [Fact]
    public async Task Leaving_draft_locks_editing_and_reports_next_statuses()
    {
        var examId = SeedExam(ExamStatus.DRAFT);

        var dto = await _service.UpdateExamStatusAsync(examId, "OPEN", TeacherId);

        Assert.False(dto.CanEdit);
        Assert.Equal(["CLOSED"], dto.AllowedNextStatuses);
    }

    [Fact]
    public void Null_status_is_treated_as_draft()
    {
        Assert.Equal(ExamStatusTransitions.NextFrom(ExamStatus.DRAFT), ExamStatusTransitions.NextFrom(null));
    }
}
