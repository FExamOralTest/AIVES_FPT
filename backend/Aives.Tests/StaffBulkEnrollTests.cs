using Aives.Application.DTOs;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Application.Interfaces.Services;
using Aives.Application.Auth.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Aives.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Test bulk enroll — endpoint Staff thêm nhiều sinh viên vào ca thi trong 1 transaction.
/// Sử dụng InMemory provider + real EF repos, mock CourseService (không cần trong test này).
/// </summary>
public class StaffBulkEnrollTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly StaffService _service;

    public StaffBulkEnrollTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"StaffBulk_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);

        var sessionRepo = new ExamSessionRepository(_db);
        var examRepo = new ExamRepository(_db);
        var uow = new ExamUnitOfWork(_db);
        var courseSvc = new NoopCourseService();
        var userRepo = new Aives.Infrastructure.Repositories.UserRepository(_db);

        _service = new StaffService(sessionRepo, examRepo, uow, courseSvc, userRepo);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task AddStudentsToSession_AllValid_AddsAllAndIncrementsCounter()
    {
        var (session, _, _, _, _, s1, s2, s3) = await SeedAsync(maxCapacity: 50);

        var req = new StaffAddEnrollmentsRequest { StudentIds = new[] { s1.UserId, s2.UserId, s3.UserId } };

        var result = await _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1);

        Assert.Equal(3, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(3, result.CurrentEnrollment);
        Assert.All(result.Results, r => Assert.Equal("added", r.Status));
    }

    [Fact]
    public async Task AddStudentsToSession_DuplicateIds_AreDeduplicated_AndAddedOnce()
    {
        var (session, _, _, _, _, s1, s2, _) = await SeedAsync();

        // Trùng s1 3 lần + s2 1 lần → distinct count = 2
        var req = new StaffAddEnrollmentsRequest
        {
            StudentIds = new[] { s1.UserId, s1.UserId, s1.UserId, s2.UserId }
        };

        var result = await _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1);

        Assert.Equal(2, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(2, result.CurrentEnrollment);

        // Database cũng chỉ có 2 row
        var dbCount = await _db.SessionEnrollments
            .CountAsync(se => se.SessionId == session.SessionId);
        Assert.Equal(2, dbCount);
    }

    [Fact]
    public async Task AddStudentsToSession_AlreadyEnrolled_Skipped_NotReadded()
    {
        var (session, _, _, _, _, s1, s2, s3) = await SeedAsync();

        // Pre-enroll s1
        _db.SessionEnrollments.Add(new SessionEnrollment
        {
            SessionId = session.SessionId,
            StudentId = s1.UserId,
            Status = SessionStatus.NOT_STARTED,
            EnrolledAt = DateTime.UtcNow
        });
        session.CurrentEnrollment = 1;
        await _db.SaveChangesAsync();

        var req = new StaffAddEnrollmentsRequest { StudentIds = new[] { s1.UserId, s2.UserId, s3.UserId } };

        var result = await _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1);

        Assert.Equal(2, result.AddedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(3, result.CurrentEnrollment);
        var s1Result = result.Results.First(r => r.StudentId == s1.UserId);
        Assert.Equal("already_enrolled", s1Result.Status);
    }

    [Fact]
    public async Task AddStudentsToSession_NonExistentUser_MarkedNotFound()
    {
        var (session, _, _, _, _, s1, _, _) = await SeedAsync();

        var req = new StaffAddEnrollmentsRequest
        {
            StudentIds = new[] { s1.UserId, 99999 }   // 99999 không tồn tại
        };

        var result = await _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.SkippedCount);
        var missing = result.Results.First(r => r.StudentId == 99999);
        Assert.Equal("not_found", missing.Status);
    }

    [Fact]
    public async Task AddStudentsToSession_NonStudentRole_MarkedNotStudent()
    {
        var (session, teacher, _, _, _, s1, _, _) = await SeedAsync();

        // teacher có Role = EXAMINER (không phải STUDENT)
        var req = new StaffAddEnrollmentsRequest
        {
            StudentIds = new[] { s1.UserId, teacher.UserId }
        };

        var result = await _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.SkippedCount);
        var teacherResult = result.Results.First(r => r.StudentId == teacher.UserId);
        Assert.Equal("not_student", teacherResult.Status);
    }

    [Fact]
    public async Task AddStudentsToSession_ExceedsCapacity_Throws()
    {
        var (session, _, _, _, _, s1, s2, s3) = await SeedAsync(maxCapacity: 2);

        var req = new StaffAddEnrollmentsRequest
        {
            StudentIds = new[] { s1.UserId, s2.UserId, s3.UserId }   // 3 > capacity 2
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1));

        // Verify nothing was added (transaction rolled back)
        var dbCount = await _db.SessionEnrollments
            .CountAsync(se => se.SessionId == session.SessionId);
        Assert.Equal(0, dbCount);
        Assert.Equal(0, session.CurrentEnrollment);
    }

    [Fact]
    public async Task AddStudentsToSession_CompletedSession_Throws()
    {
        var (session, _, _, _, _, s1, _, _) = await SeedAsync();
        session.Status = SessionStatus.COMPLETED;
        await _db.SaveChangesAsync();

        var req = new StaffAddEnrollmentsRequest { StudentIds = new[] { s1.UserId } };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1));
    }

    [Fact]
    public async Task AddStudentsToSession_EmptyArray_Throws()
    {
        var (session, _, _, _, _, _, _, _) = await SeedAsync();

        var req = new StaffAddEnrollmentsRequest { StudentIds = Array.Empty<int>() };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1));
    }

    [Fact]
    public async Task AddStudentsToSession_InvalidIds_AreFiltered()
    {
        var (session, _, _, _, _, s1, _, _) = await SeedAsync();

        // id = 0, -1 bị filter; s1 hợp lệ
        var req = new StaffAddEnrollmentsRequest
        {
            StudentIds = new[] { 0, -1, s1.UserId }
        };

        var result = await _service.AddStudentsToSessionAsync(session.SessionId, req, staffId: 1);

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
    }

    // ─── Seed helpers ──────────────────────────────────────────────

    private async Task<(ExamSession session, User teacher, Course course, Exam exam, QuestionBank qb,
        User s1, User s2, User s3)> SeedAsync(int maxCapacity = 50)
    {
        var teacher = new User
        {
            Username = "t1",
            Email = "t1@x.com",
            FullName = "Teacher 1",
            Role = UserRole.EXAMINER,
            PasswordHash = "x"
        };
        _db.Users.Add(teacher);
        await _db.SaveChangesAsync();

        var course = new Course { CourseCode = "C1", CourseName = "Course 1", TeacherId = teacher.UserId };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var exam = new Exam
        {
            CourseId = course.CourseId,
            ExamCode = "E1",
            Title = "Exam 1",
            CreatedBy = teacher.UserId,
            DurationMinutes = 60
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var qb = new QuestionBank
        {
            CourseId = course.CourseId,
            QuestionText = "Q1",
            MaxScore = 10.0f
        };
        _db.QuestionBanks.Add(qb);
        await _db.SaveChangesAsync();

        _db.ExamQuestions.Add(new ExamQuestion { ExamId = exam.ExamId, QuestionId = qb.QuestionId, QuestionScore = 10.0f });
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            ScheduledBy = teacher.UserId,
            SessionName = "Session 1",
            SessionCode = "ESS-AAAA1111",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(1),
            Status = SessionStatus.NOT_STARTED,
            MaxCapacity = maxCapacity,
            CurrentEnrollment = 0,
            CreatedAt = DateTime.UtcNow
        };
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var s1 = new User { UserId = 1001, Username = "s1", Email = "s1@x.com", FullName = "Student 1", Role = UserRole.STUDENT, PasswordHash = "x" };
        var s2 = new User { UserId = 1002, Username = "s2", Email = "s2@x.com", FullName = "Student 2", Role = UserRole.STUDENT, PasswordHash = "x" };
        var s3 = new User { UserId = 1003, Username = "s3", Email = "s3@x.com", FullName = "Student 3", Role = UserRole.STUDENT, PasswordHash = "x" };
        _db.Users.AddRange(s1, s2, s3);
        await _db.SaveChangesAsync();

        return (session, teacher, course, exam, qb, s1, s2, s3);
    }
}

/// <summary>Stub ICourseService — bulk enroll test không cần, nhưng DI yêu cầu inject.</summary>
internal class NoopCourseService : ICourseService
{
    public Task<IEnumerable<CourseDto>> GetAllAsync() => Task.FromResult<IEnumerable<CourseDto>>(Array.Empty<CourseDto>());
    public Task<IEnumerable<CourseDto>> GetByTeacherIdAsync(int teacherId) => Task.FromResult<IEnumerable<CourseDto>>(Array.Empty<CourseDto>());
    public Task<CourseDto> GetByIdAsync(int courseId) => throw new NotImplementedException();
    public Task<CourseDto> CreateAsync(CreateCourseRequest request) => throw new NotImplementedException();
    public Task<CourseDto> UpdateAsync(int courseId, UpdateCourseRequest request) => throw new NotImplementedException();
    public Task<CourseDto> AssignTeacherAsync(int courseId, AssignTeacherRequest request) => throw new NotImplementedException();
    public Task DeleteAsync(int courseId) => throw new NotImplementedException();
}
