using Aives.Application.DTOs;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Services;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

public class MultiExamDistributionTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly StaffService _staffService;
    private readonly ExamSessionService _sessionService;

    public MultiExamDistributionTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"MultiExamDist_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);

        var sessionRepo = new ExamSessionRepository(_db);
        var examRepo = new ExamRepository(_db);
        var uow = new ExamUnitOfWork(_db);
        var courseSvc = new StubCourseService();
        var userRepo = new UserRepository(_db);

        _staffService = new StaffService(sessionRepo, examRepo, uow, courseSvc, userRepo);
        _sessionService = new ExamSessionService(sessionRepo, examRepo, uow);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task DistributeExams_50Students_5Exams_Balanced10Each()
    {
        // Arrange: 1 course, 5 exams, 1 session with 5 exams configured, 50 enrolled students
        var (session, exams, students) = await SeedSessionWithExamsAndStudentsAsync(examCount: 5, studentCount: 50);

        // Act: Distribute exams
        var result = await _staffService.DistributeExamsAsync(
            session.SessionId,
            new DistributeExamsRequest { Strategy = "BALANCED_RANDOM" },
            staffId: 1);

        // Assert
        Assert.Equal(50, result.TotalStudents);
        Assert.Equal(5, result.Allocations.Count);
        foreach (var alloc in result.Allocations)
        {
            Assert.Equal(10, alloc.StudentCount);
        }

        // Verify in DB that all 50 enrollments have an AssignedExamId
        var updatedSession = await _db.ExamSessions
            .Include(s => s.SessionEnrollments)
            .FirstAsync(s => s.SessionId == session.SessionId);

        var examIds = exams.Select(e => e.ExamId).ToHashSet();
        foreach (var en in updatedSession.SessionEnrollments)
        {
            Assert.NotNull(en.AssignedExamId);
            Assert.Contains(en.AssignedExamId.Value, examIds);
        }

        var counts = updatedSession.SessionEnrollments
            .GroupBy(e => e.AssignedExamId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(5, counts.Count);
        foreach (var count in counts.Values)
        {
            Assert.Equal(10, count);
        }
    }

    [Fact]
    public async Task DistributeExams_52Students_5Exams_MaxDeltaLe1()
    {
        // Arrange: 52 students, 5 exams -> 2 exams get 11, 3 exams get 10
        var (session, _, _) = await SeedSessionWithExamsAndStudentsAsync(examCount: 5, studentCount: 52);

        // Act
        var result = await _staffService.DistributeExamsAsync(
            session.SessionId,
            new DistributeExamsRequest { Strategy = "BALANCED_RANDOM" },
            staffId: 1);

        // Assert
        Assert.Equal(52, result.TotalStudents);
        var minCount = result.Allocations.Min(a => a.StudentCount);
        var maxCount = result.Allocations.Max(a => a.StudentCount);

        Assert.Equal(10, minCount);
        Assert.Equal(11, maxCount);
        Assert.True(maxCount - minCount <= 1);
        Assert.Equal(2, result.Allocations.Count(a => a.StudentCount == 11));
        Assert.Equal(3, result.Allocations.Count(a => a.StudentCount == 10));
    }

    [Fact]
    public async Task SelfEnroll_AssignsToLeastLoadedExam()
    {
        // Arrange: Session with 2 exams (Exam A and Exam B).
        var (session, exams, _) = await SeedSessionWithExamsAndStudentsAsync(examCount: 2, studentCount: 0);
        session.StartTime = DateTime.UtcNow.AddMinutes(-10);
        session.EndTime = DateTime.UtcNow.AddHours(1);
        await _db.SaveChangesAsync();

        var examA = exams[0].ExamId;
        var examB = exams[1].ExamId;

        // Manually add 2 students to Exam A
        var student1 = new User { UserId = 201, Username = "st1", Email = "st1@fpt.edu.vn", FullName = "S1", Role = UserRole.STUDENT, PasswordHash = "x" };
        var student2 = new User { UserId = 202, Username = "st2", Email = "st2@fpt.edu.vn", FullName = "S2", Role = UserRole.STUDENT, PasswordHash = "x" };
        var student3 = new User { UserId = 203, Username = "st3", Email = "st3@fpt.edu.vn", FullName = "S3", Role = UserRole.STUDENT, PasswordHash = "x" };
        _db.Users.AddRange(student1, student2, student3);
        await _db.SaveChangesAsync();

        _db.SessionEnrollments.Add(new SessionEnrollment
        {
            SessionId = session.SessionId,
            StudentId = student1.UserId,
            AssignedExamId = examA,
            Status = SessionStatus.NOT_STARTED,
            EnrolledAt = DateTime.UtcNow
        });
        _db.SessionEnrollments.Add(new SessionEnrollment
        {
            SessionId = session.SessionId,
            StudentId = student2.UserId,
            AssignedExamId = examA,
            Status = SessionStatus.NOT_STARTED,
            EnrolledAt = DateTime.UtcNow
        });
        session.CurrentEnrollment = 2;
        await _db.SaveChangesAsync();

        // Act: Student 3 self-enrolls with SessionCode
        var response = await _sessionService.EnrollStudentAsync(
            new EnrollSessionRequest { SessionCode = session.SessionCode },
            student3.UserId);

        // Assert: Since Exam B had 0 students and Exam A had 2 students, student 3 must be assigned Exam B!
        var enrollment3 = await _db.SessionEnrollments
            .FirstOrDefaultAsync(e => e.SessionId == session.SessionId && e.StudentId == student3.UserId);

        Assert.NotNull(enrollment3);
        Assert.Equal(examB, enrollment3.AssignedExamId);
    }

    [Fact]
    public async Task DistributeExams_FailsIfSessionInProgress()
    {
        // Arrange
        var (session, _, _) = await SeedSessionWithExamsAndStudentsAsync(examCount: 3, studentCount: 10);
        session.Status = SessionStatus.IN_PROGRESS;
        await _db.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _staffService.DistributeExamsAsync(
                session.SessionId,
                new DistributeExamsRequest(),
                staffId: 1));
    }

    [Fact]
    public async Task AssignStudentExam_SuccessfullyReassignsExam()
    {
        // Arrange
        var (session, exams, students) = await SeedSessionWithExamsAndStudentsAsync(examCount: 2, studentCount: 2);
        var examA = exams[0].ExamId;
        var examB = exams[1].ExamId;

        // Set student 0 to Exam A
        var studentId = students[0].UserId;
        var enrollment = await _db.SessionEnrollments.FirstAsync(e => e.SessionId == session.SessionId && e.StudentId == studentId);
        enrollment.AssignedExamId = examA;
        await _db.SaveChangesAsync();

        // Act: Reassign to Exam B
        await _staffService.AssignStudentExamAsync(
            session.SessionId,
            studentId,
            new StaffAssignExamRequest { ExamId = examB },
            staffId: 1);

        // Assert
        var updatedEnrollment = await _db.SessionEnrollments.FirstAsync(e => e.SessionId == session.SessionId && e.StudentId == studentId);
        Assert.Equal(examB, updatedEnrollment.AssignedExamId);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<(ExamSession session, List<Exam> exams, List<User> students)> SeedSessionWithExamsAndStudentsAsync(
        int examCount, int studentCount)
    {
        var teacher = new User
        {
            UserId = 1,
            Username = "staff1",
            Email = "staff1@fpt.edu.vn",
            FullName = "Staff Member",
            Role = UserRole.STAFF,
            PasswordHash = "x"
        };
        _db.Users.Add(teacher);
        await _db.SaveChangesAsync();

        var course = new Course
        {
            CourseCode = "SWP391",
            CourseName = "Software Development Project",
            TeacherId = teacher.UserId
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var exams = new List<Exam>();
        for (int i = 1; i <= examCount; i++)
        {
            var exam = new Exam
            {
                CourseId = course.CourseId,
                ExamCode = $"SWP-DE{i:D2}",
                Title = $"Đề thi số {i}",
                CreatedBy = teacher.UserId,
                DurationMinutes = 30,
                MaxQuestions = 5
            };
            _db.Exams.Add(exam);
            exams.Add(exam);
        }
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            CourseId = course.CourseId,
            ScheduledBy = teacher.UserId,
            SessionName = "Ca thi sáng 01",
            SessionCode = $"ESS-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            StartTime = DateTime.UtcNow.AddHours(1),
            EndTime = DateTime.UtcNow.AddHours(2),
            Status = SessionStatus.NOT_STARTED,
            MaxCapacity = Math.Max(50, studentCount + 10),
            CurrentEnrollment = studentCount,
            CreatedAt = DateTime.UtcNow
        };
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        // Link exams to session
        foreach (var exam in exams)
        {
            _db.Set<ExamSessionExam>().Add(new ExamSessionExam
            {
                SessionId = session.SessionId,
                ExamId = exam.ExamId
            });
        }
        await _db.SaveChangesAsync();

        var students = new List<User>();
        for (int i = 1; i <= studentCount; i++)
        {
            var student = new User
            {
                UserId = 1000 + i,
                Username = $"se{i:D4}",
                Email = $"se{i:D4}@fpt.edu.vn",
                FullName = $"Sinh Viên {i}",
                Role = UserRole.STUDENT,
                PasswordHash = "x"
            };
            _db.Users.Add(student);
            students.Add(student);

            _db.SessionEnrollments.Add(new SessionEnrollment
            {
                SessionId = session.SessionId,
                StudentId = student.UserId,
                Status = SessionStatus.NOT_STARTED,
                EnrolledAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        return (session, exams, students);
    }
}

internal class StubCourseService : ICourseService
{
    public Task<IEnumerable<CourseDto>> GetAllAsync() => Task.FromResult(Enumerable.Empty<CourseDto>());
    public Task<IEnumerable<CourseDto>> GetByTeacherIdAsync(int teacherId) => Task.FromResult(Enumerable.Empty<CourseDto>());
    public Task<CourseDto> GetByIdAsync(int courseId) => Task.FromResult(new CourseDto(courseId, "SWP391", "Software Development Project", 1, "Staff Member", 1, DateTime.UtcNow));
    public Task<CourseDto> CreateAsync(CreateCourseRequest request) => throw new NotImplementedException();
    public Task<CourseDto> UpdateAsync(int courseId, UpdateCourseRequest request) => throw new NotImplementedException();
    public Task<CourseDto> AssignTeacherAsync(int courseId, AssignTeacherRequest request) => throw new NotImplementedException();
    public Task DeleteAsync(int courseId) => throw new NotImplementedException();
}
