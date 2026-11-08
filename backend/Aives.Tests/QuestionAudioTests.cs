using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Viva;
using Aives.Application.Viva.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aives.Tests;

public class QuestionAudioTests : IDisposable
{
    private const int StudentId = 501;
    private const int OtherStudentId = 502;
    private const string FirstQuestion = "Giải thích kiến trúc phân lớp.";
    private const string SecondQuestion = "What is dependency injection?";

    private readonly AivesDbContext _db;
    private readonly RecordingAiClient _ai = new();
    private readonly VivaSessionService _service;
    private int _sessionId;
    private int _firstQuestionId;
    private int _secondQuestionId;

    public QuestionAudioTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase($"QuestionAudioTests_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);
        _service = new VivaSessionService(
            new ExamSessionRepository(_db), new ExamRepository(_db), new QuestionBankRepository(_db),
            new ResultRepository(_db), _ai, new FakeStorageService(), new FakeQueueService(),
            new MediaUploadRepository(_db), NullLogger<VivaSessionService>.Instance);
        Seed();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private void Seed()
    {
        var teacher = new User { UserId = 1, Username = "t", Email = "t@fpt.edu.vn", PasswordHash = "x", FullName = "Teacher", Role = UserRole.STAFF };
        _db.Users.AddRange(teacher,
            new User { UserId = StudentId, Username = "s1", Email = "s1@fpt.edu.vn", PasswordHash = "x", FullName = "Student One", Role = UserRole.STUDENT },
            new User { UserId = OtherStudentId, Username = "s2", Email = "s2@fpt.edu.vn", PasswordHash = "x", FullName = "Student Two", Role = UserRole.STUDENT });
        var course = new Course { CourseCode = "SWD392", CourseName = "Software Design", TeacherId = teacher.UserId };
        _db.Courses.Add(course);
        _db.SaveChanges();

        var first = new QuestionBank { CourseId = course.CourseId, QuestionText = FirstQuestion, Level = 1, MaxScore = 1 };
        var second = new QuestionBank { CourseId = course.CourseId, QuestionText = SecondQuestion, Level = 1, MaxScore = 1 };
        _db.QuestionBanks.AddRange(first, second);
        var exam = new Exam { CourseId = course.CourseId, CreatedBy = teacher.UserId, ExamCode = "EX-AUDIO1", Title = "Viva", DurationMinutes = 30, MaxQuestions = 5 };
        _db.Exams.Add(exam);
        _db.SaveChanges();
        _db.ExamQuestions.AddRange(
            new ExamQuestion { ExamId = exam.ExamId, QuestionId = first.QuestionId, QuestionScore = 50 },
            new ExamQuestion { ExamId = exam.ExamId, QuestionId = second.QuestionId, QuestionScore = 50 });

        var session = new ExamSession
        {
            CourseId = course.CourseId, ExamId = exam.ExamId, ScheduledBy = teacher.UserId, SessionName = "Audio session",
            Room = "A1", StartTime = DateTime.UtcNow.AddMinutes(-5), EndTime = DateTime.UtcNow.AddHours(1),
            MaxCapacity = 10, Status = SessionStatus.IN_PROGRESS, SessionCode = "AUDIO1", CreatedAt = DateTime.UtcNow
        };
        _db.ExamSessions.Add(session);
        _db.SaveChanges();
        _db.SessionEnrollments.Add(new SessionEnrollment
        {
            SessionId = session.SessionId, StudentId = StudentId, Status = SessionStatus.IN_PROGRESS,
            CurrentQuestionIndex = 0, EnrolledAt = DateTime.UtcNow
        });
        _db.SaveChanges();

        _sessionId = session.SessionId;
        // The service orders exam questions by QuestionId, so the lower id is asked first.
        _firstQuestionId = Math.Min(first.QuestionId, second.QuestionId);
        _secondQuestionId = Math.Max(first.QuestionId, second.QuestionId);
    }

    [Fact]
    public async Task Returns_audio_for_the_current_question()
    {
        _ai.Next = new TextToSpeechResponse { Success = true, AudioBase64 = Convert.ToBase64String([1, 2, 3]), AudioFormat = "mp3" };

        var audio = await _service.GetCurrentQuestionAudioAsync(_sessionId, StudentId, _firstQuestionId);

        Assert.NotNull(audio);
        Assert.Equal([1, 2, 3], audio!.Audio);
        Assert.Equal("audio/mpeg", audio.ContentType);
        Assert.Equal(FirstQuestion, _ai.LastText);
        Assert.Equal(SpeechLanguage.Vietnamese, _ai.LastLanguage);
    }

    [Fact]
    public async Task Wav_audio_gets_the_wav_content_type()
    {
        _ai.Next = new TextToSpeechResponse { Success = true, AudioBase64 = Convert.ToBase64String([9]), AudioFormat = "WAV" };

        var audio = await _service.GetCurrentQuestionAudioAsync(_sessionId, StudentId, _firstQuestionId);

        Assert.Equal("audio/wav", audio!.ContentType);
    }

    [Fact]
    public async Task A_later_question_cannot_be_heard_early()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.GetCurrentQuestionAudioAsync(_sessionId, StudentId, _secondQuestionId));
        Assert.Null(_ai.LastText);
    }

    [Fact]
    public async Task A_student_who_is_not_enrolled_is_refused()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.GetCurrentQuestionAudioAsync(_sessionId, OtherStudentId, _firstQuestionId));
    }

    [Fact]
    public async Task A_missing_session_is_not_found()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetCurrentQuestionAudioAsync(999_999, StudentId, _firstQuestionId));
    }

    [Theory]
    [InlineData(false, "AAAA")]
    [InlineData(true, "")]
    [InlineData(true, "not base64!")]
    public async Task Failed_or_empty_tts_returns_null(bool success, string audioBase64)
    {
        _ai.Next = new TextToSpeechResponse { Success = success, AudioBase64 = audioBase64 };

        Assert.Null(await _service.GetCurrentQuestionAudioAsync(_sessionId, StudentId, _firstQuestionId));
    }

    [Theory]
    [InlineData("Giải thích kiến trúc phân lớp.", SpeechLanguage.Vietnamese)]
    [InlineData("ĐỊNH NGHĨA", SpeechLanguage.Vietnamese)]
    [InlineData("What is dependency injection?", SpeechLanguage.English)]
    [InlineData("Phan tich SOLID", SpeechLanguage.English)]
    [InlineData("", SpeechLanguage.English)]
    [InlineData(null, SpeechLanguage.English)]
    public void Language_follows_the_question_text(string? text, string expected)
    {
        Assert.Equal(expected, SpeechLanguage.Detect(text));
    }

    private sealed class RecordingAiClient : IAiServiceClient
    {
        public TextToSpeechResponse Next { get; set; } = new() { Success = false };
        public string? LastText { get; private set; }
        public string? LastLanguage { get; private set; }

        public Task<TextToSpeechResponse> SynthesizeSpeechAsync(string text, string language = "vi-VN", string? voice = null)
        {
            LastText = text;
            LastLanguage = language;
            return Task.FromResult(Next);
        }

        public Task<SpeechToTextResponse> TranscribeAudioAsync(Stream audioStream, string fileName, string examSessionId, string language = "vi-VN", CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<SpeechToTextResponse> TranscribeStoredAudioAsync(int answerId, string objectKey, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<FollowUpResponse> GenerateFollowUpAsync(FollowUpRequest request)
            => throw new NotSupportedException();
        public Task<EvaluateSessionResponse> EvaluateSessionAsync(EvaluateSessionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
