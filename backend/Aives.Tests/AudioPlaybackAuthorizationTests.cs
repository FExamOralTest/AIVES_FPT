using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Queue;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 2.12 — Audio Playback Authorization.
/// Kiểm tra quyền và ownership trước khi cấp presigned audio URL:
/// bucket phải private và URL phải có thời hạn (TTL ≤ 3600s — BR-15).
/// </summary>
public class AudioPlaybackAuthorizationTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly AnswerRepository _answerRepository;
    private readonly FakeStorageService _storage = new();
    private readonly AudioPlaybackService _service;

    public AudioPlaybackAuthorizationTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"AudioPlayback_{Guid.NewGuid()}")
            .Options;
        _db = new AivesDbContext(options);
        _answerRepository = new AnswerRepository(_db);
        _service = new AudioPlaybackService(
            _answerRepository, _storage, Microsoft.Extensions.Logging.Abstractions.NullLogger<AudioPlaybackService>.Instance);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task Student_CanListenOwnRecording()
    {
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        var response = await _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 100, currentUserRole: UserRole.STUDENT);

        Assert.Equal(answerId, response.AnswerId);
        Assert.False(string.IsNullOrWhiteSpace(response.PlaybackUrl));
    }

    [Fact]
    public async Task Student_CannotListenOtherStudentsRecording()
    {
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 999, currentUserRole: UserRole.STUDENT));

        // Không được cấp URL khi không có quyền.
        Assert.Null(_storage.LastGeneratedPresignedUrl);
    }

    [Fact]
    public async Task Staff_CannotListenOtherStaffSessionRecording()
    {
        // Session do user 200 phụ trách; staff caller là user 300.
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 300, currentUserRole: UserRole.STAFF));

        Assert.Null(_storage.LastGeneratedPresignedUrl);
    }

    [Fact]
    public async Task Staff_CanListenRecording_OfSessionTheyScheduled()
    {
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        var response = await _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 200, currentUserRole: UserRole.STAFF);

        Assert.False(string.IsNullOrWhiteSpace(response.PlaybackUrl));
    }

    [Fact]
    public async Task Examiner_CanListenRecording_OfSessionTheyScheduled()
    {
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        var response = await _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 200, currentUserRole: UserRole.EXAMINER);

        Assert.False(string.IsNullOrWhiteSpace(response.PlaybackUrl));
    }

    [Fact]
    public async Task Admin_CanListenAnyRecording()
    {
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        var response = await _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 777, currentUserRole: UserRole.ADMIN);

        Assert.False(string.IsNullOrWhiteSpace(response.PlaybackUrl));
    }

    [Fact]
    public async Task UnauthenticatedRequest_IsRejected()
    {
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.GetPlaybackUrlAsync(
            answerId, currentUserId: null, currentUserRole: null));
    }

    [Fact]
    public async Task Answer_WithoutAudio_ReturnsNotFound()
    {
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200, withAudio: false);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 100, currentUserRole: UserRole.STUDENT));
    }

    [Fact]
    public async Task UnknownAnswer_ReturnsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetPlaybackUrlAsync(
            999_999, currentUserId: 1, currentUserRole: UserRole.ADMIN));
    }

    [Fact]
    public async Task PresignedUrl_IsTimeBound_AndWithinMaxTtl()
    {
        // BR-15: URL có thời hạn, TTL tối đa 3600 giây.
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        var response = await _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 100, currentUserRole: UserRole.STUDENT);

        var ttl = response.ExpiresAt - DateTime.UtcNow;
        Assert.True(ttl > TimeSpan.Zero, "Presigned URL must expire in the future.");
        Assert.True(ttl <= TimeSpan.FromHours(1) + TimeSpan.FromSeconds(30),
            $"Presigned URL TTL must not exceed 3600s (BR-15), but was {ttl}.");
    }

    [Fact]
    public async Task PresignedUrl_IsBoundToTheRequestedObjectKey()
    {
        // URL phải trỏ đúng object key của answer — không cấp URL chung cho mọi audio.
        var answerId = await SeedAsync(studentId: 100, scheduledBy: 200);

        var response = await _service.GetPlaybackUrlAsync(
            answerId, currentUserId: 100, currentUserRole: UserRole.STUDENT);

        Assert.Contains(Uri.EscapeDataString("exams/1/sessions"), response.PlaybackUrl,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> SeedAsync(int studentId, int scheduledBy, bool withAudio = true)
    {
        var course = new Course { CourseCode = "AUD", CourseName = "Audio Test" };
        _db.Courses.Add(course);
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

        var exam = new Exam { ExamCode = "AUD-E", Title = "Audio Exam", CourseId = course.CourseId, DurationMinutes = 60 };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "AUD-S",
            SessionName = "Audio Session",
            Status = SessionStatus.IN_PROGRESS,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(1),
            ScheduledBy = scheduledBy
        };
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var result = new Result
        {
            SessionId = session.SessionId,
            StudentId = studentId,
            Status = SubmissionStatus.PENDING_REVIEW,
            SubmittedAt = DateTime.UtcNow
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var question = new QuestionBank
        {
            CourseId = course.CourseId,
            QuestionText = "Q1",
            MaxScore = 10.0f
        };
        _db.QuestionBanks.Add(question);
        await _db.SaveChangesAsync();

        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = question.QuestionId,
            Status = AnswerStatus.EVALUATED,
            StudentAnswer = "abc",
            AudioUrl = withAudio ? "exams/1/sessions/1/students/100/q_1.webm" : null,
            ObjectKey = withAudio ? "exams/1/sessions/1/students/100/q_1.webm" : null
        };
        _db.Answers.Add(answer);
        await _db.SaveChangesAsync();

        return answer.AnswerId;
    }
}
