using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.DTOs;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Services;
using Aives.Application.Interfaces.Services;
using Aives.Application.Viva.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Aives.Tests;

public class ExamSessionSlotTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly StaffService _staffService;
    private readonly VivaSessionService _vivaService;

    public ExamSessionSlotTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"ExamSessionSlotTest_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);

        var sessionRepo = new ExamSessionRepository(_db);
        var examRepo = new ExamRepository(_db);
        var uow = new ExamUnitOfWork(_db);
        var courseSvc = new StubCourseService();
        var userRepo = new UserRepository(_db);
        var questionRepo = new QuestionBankRepository(_db);
        var resultRepo = new ResultRepository(_db);
        var docRepo = new MarkdownDocumentRepository(_db);
        var aiClient = new MockAiServiceClient();
        var storageSvc = new MockStorageService();
        var logger = NullLogger<VivaSessionService>.Instance;

        _staffService = new StaffService(sessionRepo, examRepo, uow, courseSvc, userRepo);
        _vivaService = new VivaSessionService(
            sessionRepo,
            examRepo,
            questionRepo,
            resultRepo,
            aiClient,
            docRepo,
            storageSvc,
            logger);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task DistributeSlots_50Students_5Slots_Balanced10Each_ExamPerSlot()
    {
        // Arrange
        var (session, slots, exams, students) = await SeedSessionWithSlotsAndExamsAsync(slotCount: 5, examCount: 5, studentCount: 50);

        // Act
        var result = await _staffService.DistributeSlotsAsync(
            session.SessionId,
            new DistributeSlotsRequest
            {
                Strategy = "SLOT_BALANCED",
                ExamAssignmentMode = "EXAM_PER_SLOT"
            },
            staffId: 1);

        // Assert response
        Assert.Equal(50, result.TotalStudents);
        Assert.Equal(5, result.TotalSlots);
        foreach (var alloc in result.Allocations)
        {
            Assert.Equal(10, alloc.StudentCount);
        }

        // Assert in DB
        var updatedEnrollments = await _db.SessionEnrollments
            .Where(e => e.SessionId == session.SessionId)
            .ToListAsync();

        var slotCounts = updatedEnrollments
            .GroupBy(e => e.AssignedSlotId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(5, slotCounts.Count);
        foreach (var count in slotCounts.Values)
        {
            Assert.Equal(10, count);
        }

        // With EXAM_PER_SLOT: all students in slot i must have assigned exam i % 5
        var orderedSlots = slots.OrderBy(s => s.SlotIndex).ToList();
        for (int i = 0; i < orderedSlots.Count; i++)
        {
            var expectedExam = exams[i % exams.Count];
            var slotId = orderedSlots[i].SlotId;
            var studentsInSlot = updatedEnrollments.Where(e => e.AssignedSlotId == slotId).ToList();
            Assert.Equal(10, studentsInSlot.Count);
            foreach (var student in studentsInSlot)
            {
                Assert.Equal(expectedExam.ExamId, student.AssignedExamId);
            }
        }
    }

    [Fact]
    public async Task DistributeSlots_50Students_5Slots_ShuffleInSlot_EvenlyDistributesExamsWithinSlot()
    {
        // Arrange
        var (session, slots, exams, _) = await SeedSessionWithSlotsAndExamsAsync(slotCount: 5, examCount: 5, studentCount: 50);

        // Act
        var result = await _staffService.DistributeSlotsAsync(
            session.SessionId,
            new DistributeSlotsRequest
            {
                Strategy = "SLOT_BALANCED",
                ExamAssignmentMode = "SHUFFLE_IN_SLOT"
            },
            staffId: 1);

        // Assert
        Assert.Equal(50, result.TotalStudents);
        Assert.Equal(5, result.TotalSlots);

        var updatedEnrollments = await _db.SessionEnrollments
            .Where(e => e.SessionId == session.SessionId)
            .ToListAsync();

        // Each slot (10 students) has exams distributed evenly (2 of each exam for 5 exams)
        foreach (var slot in slots)
        {
            var studentsInSlot = updatedEnrollments.Where(e => e.AssignedSlotId == slot.SlotId).ToList();
            Assert.Equal(10, studentsInSlot.Count);

            var examDistribution = studentsInSlot
                .GroupBy(e => e.AssignedExamId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            Assert.Equal(5, examDistribution.Count);
            foreach (var count in examDistribution.Values)
            {
                Assert.Equal(2, count);
            }
        }
    }

    [Fact]
    public async Task AssignStudentSlot_ReassignsSlotSuccessfully()
    {
        // Arrange
        var (session, slots, _, students) = await SeedSessionWithSlotsAndExamsAsync(slotCount: 2, examCount: 1, studentCount: 2);
        var targetStudent = students[0];
        var slot2 = slots[1];

        // Act
        await _staffService.AssignStudentSlotAsync(
            session.SessionId,
            targetStudent.UserId,
            new StaffAssignSlotRequest { SlotId = slot2.SlotId },
            staffId: 1);

        // Assert
        var enrollment = await _db.SessionEnrollments
            .FirstAsync(e => e.SessionId == session.SessionId && e.StudentId == targetStudent.UserId);
        Assert.Equal(slot2.SlotId, enrollment.AssignedSlotId);
    }

    [Fact]
    public async Task VivaSession_SlotGuard_BeforeSlotStartTime_ThrowsInvalidOperationException()
    {
        // Arrange: Slot starts 2 hours in the future
        var now = DateTime.UtcNow;
        var (session, slots, _, students) = await SeedSessionWithSlotsAndExamsAsync(
            slotCount: 1,
            examCount: 1,
            studentCount: 1,
            sessionStart: now.AddHours(-1),
            sessionEnd: now.AddHours(5),
            slotStart: now.AddHours(2), // Future slot
            slotEnd: now.AddHours(3));

        session.Status = SessionStatus.IN_PROGRESS;
        await _db.SaveChangesAsync();

        var student = students[0];
        var enrollment = await _db.SessionEnrollments.FirstAsync(e => e.SessionId == session.SessionId && e.StudentId == student.UserId);
        enrollment.AssignedSlotId = slots[0].SlotId;
        await _db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _vivaService.StartSessionAsync(session.SessionId, student.UserId));

        Assert.Contains("Chưa đến khung giờ thi của kíp bạn", ex.Message);
    }

    [Fact]
    public async Task VivaSession_SlotGuard_AfterSlotEndTime_ThrowsInvalidOperationException()
    {
        // Arrange: Slot ended 30 mins ago
        var now = DateTime.UtcNow;
        var (session, slots, _, students) = await SeedSessionWithSlotsAndExamsAsync(
            slotCount: 1,
            examCount: 1,
            studentCount: 1,
            sessionStart: now.AddHours(-2),
            sessionEnd: now.AddHours(5),
            slotStart: now.AddHours(-2),
            slotEnd: now.AddMinutes(-30)); // Ended slot

        session.Status = SessionStatus.IN_PROGRESS;
        await _db.SaveChangesAsync();

        var student = students[0];
        var enrollment = await _db.SessionEnrollments.FirstAsync(e => e.SessionId == session.SessionId && e.StudentId == student.UserId);
        enrollment.AssignedSlotId = slots[0].SlotId;
        await _db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _vivaService.StartSessionAsync(session.SessionId, student.UserId));

        Assert.Contains("đã kết thúc", ex.Message);
    }

    [Fact]
    public async Task VivaSession_SlotGuard_WithinSlotTime_SucceedsAndReturnsSlotInfo()
    {
        // Arrange: Slot is active right now
        var now = DateTime.UtcNow;
        var (session, slots, exams, students) = await SeedSessionWithSlotsAndExamsAsync(
            slotCount: 1,
            examCount: 1,
            studentCount: 1,
            sessionStart: now.AddMinutes(-30),
            sessionEnd: now.AddHours(2),
            slotStart: now.AddMinutes(-10),
            slotEnd: now.AddMinutes(20));

        session.Status = SessionStatus.IN_PROGRESS;
        await _db.SaveChangesAsync();

        var student = students[0];
        var enrollment = await _db.SessionEnrollments.FirstAsync(e => e.SessionId == session.SessionId && e.StudentId == student.UserId);
        enrollment.AssignedSlotId = slots[0].SlotId;
        enrollment.AssignedExamId = exams[0].ExamId;
        await _db.SaveChangesAsync();

        // Act
        var response = await _vivaService.StartSessionAsync(session.SessionId, student.UserId);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(session.SessionId, response.SessionId);
        Assert.Equal(slots[0].SlotId, response.AssignedSlotId);
        Assert.Equal(slots[0].SlotName, response.AssignedSlotName);
        Assert.Equal(slots[0].StartTime, response.SlotStartTime);
        Assert.Equal(slots[0].EndTime, response.SlotEndTime);
    }

    [Fact]
    public async Task StaffService_UpdateSlots_GeneratesSlotCodeWithExpectedPattern()
    {
        // Arrange
        var (session, _, _, _) = await SeedSessionWithSlotsAndExamsAsync(slotCount: 0, examCount: 1, studentCount: 0);
        var slotInput = new List<CreateSessionSlotInput>
        {
            new() { SlotIndex = 1, SlotName = "Kíp 1", StartTime = session.StartTime, EndTime = session.StartTime.AddMinutes(30), MaxCapacity = 20 },
            new() { SlotIndex = 2, SlotName = "Kíp 2", StartTime = session.StartTime.AddMinutes(30), EndTime = session.EndTime, MaxCapacity = 20 }
        };

        // Act
        var result = await _staffService.UpdateSessionAsync(session.SessionId, new StaffUpdateSessionRequest
        {
            SessionName = session.SessionName,
            Slots = slotInput
        }, 1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Slots.Count);
        foreach (var slot in result.Slots)
        {
            Assert.NotNull(slot.SlotCode);
            Assert.StartsWith($"{session.SessionCode}-S{slot.SlotIndex}-", slot.SlotCode);
            var parts = slot.SlotCode.Split('-');
            Assert.True(parts.Length >= 3, "Slot code should contain session code, slot index, and random digits");
            var lastPart = parts.Last();
            Assert.Equal(6, lastPart.Length);
            Assert.True(int.TryParse(lastPart, out _), "Last part of slot code must be 6 numeric digits");
        }
    }

    [Fact]
    public async Task ExamSessionService_EnrollWithSlotCode_AssignsStudentDirectlyToSlot()
    {
        // Arrange
        var (session, slots, _, _) = await SeedSessionWithSlotsAndExamsAsync(slotCount: 2, examCount: 1, studentCount: 0,
            sessionStart: DateTime.UtcNow.AddMinutes(-5), sessionEnd: DateTime.UtcNow.AddHours(2));

        var slotToEnroll = slots[1];
        Assert.NotNull(slotToEnroll.SlotCode);

        var newStudent = new User
        {
            Username = "new_student_slot",
            Email = "student_slot@fpt.edu.vn",
            PasswordHash = "hash",
            FullName = "New Student Slot",
            Role = UserRole.STUDENT
        };
        _db.Users.Add(newStudent);
        await _db.SaveChangesAsync();

        var examSessionService = new ExamSessionService(
            new ExamSessionRepository(_db),
            new ExamRepository(_db),
            new ExamUnitOfWork(_db));

        // Act: student enrolls using SlotCode instead of SessionCode
        var enrollResponse = await examSessionService.EnrollStudentAsync(
            new EnrollSessionRequest { SessionCode = slotToEnroll.SlotCode },
            newStudent.UserId);

        // Assert
        Assert.NotNull(enrollResponse);
        var enrollment = await _db.SessionEnrollments
            .FirstOrDefaultAsync(e => e.SessionId == session.SessionId && e.StudentId == newStudent.UserId);
        Assert.NotNull(enrollment);
        Assert.Equal(slotToEnroll.SlotId, enrollment.AssignedSlotId);
    }

    // ─── Helpers ────────────────────────────────────────────────
    private async Task<(ExamSession Session, List<ExamSessionSlot> Slots, List<Exam> Exams, List<User> Students)>
        SeedSessionWithSlotsAndExamsAsync(
            int slotCount,
            int examCount,
            int studentCount,
            DateTime? sessionStart = null,
            DateTime? sessionEnd = null,
            DateTime? slotStart = null,
            DateTime? slotEnd = null)
    {
        var teacher = new User
        {
            UserId = 1,
            Username = "teacher1",
            Email = $"teacher_{Guid.NewGuid()}@fpt.edu.vn",
            PasswordHash = "hash",
            FullName = "Teacher One",
            Role = UserRole.ADMIN
        };
        _db.Users.Add(teacher);
        await _db.SaveChangesAsync();

        var course = new Course
        {
            CourseCode = $"CS_{Guid.NewGuid():N}".Substring(0, 8),
            CourseName = "Advanced Computing",
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
                CreatedBy = teacher.UserId,
                ExamCode = $"EX_{i}_{Guid.NewGuid():N}".Substring(0, 10),
                Title = $"Đề thi số {i}",
                DurationMinutes = 30,
                MaxQuestions = 5,
                CreatedAt = DateTime.UtcNow
            };
            _db.Exams.Add(exam);
            await _db.SaveChangesAsync();

            var q = new QuestionBank
            {
                CourseId = course.CourseId,
                QuestionText = $"Question 1 for exam {i}",
                Level = 1,
                MaxScore = 10
            };
            _db.QuestionBanks.Add(q);
            await _db.SaveChangesAsync();

            _db.ExamQuestions.Add(new ExamQuestion
            {
                ExamId = exam.ExamId,
                QuestionId = q.QuestionId,
                QuestionScore = 10
            });
            await _db.SaveChangesAsync();

            exams.Add(exam);
        }

        var sStart = sessionStart ?? DateTime.UtcNow.AddMinutes(10);
        var sEnd = sessionEnd ?? sStart.AddHours(3);

        var session = new ExamSession
        {
            CourseId = course.CourseId,
            ExamId = exams[0].ExamId,
            ScheduledBy = teacher.UserId,
            SessionName = "Ca thi chia kíp",
            Room = "Room Alpha",
            StartTime = sStart,
            EndTime = sEnd,
            MaxCapacity = 100,
            Status = SessionStatus.NOT_STARTED,
            SessionCode = "SLOT-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow
        };
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        foreach (var exam in exams)
        {
            _db.ExamSessionExams.Add(new ExamSessionExam
            {
                SessionId = session.SessionId,
                ExamId = exam.ExamId,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var slots = new List<ExamSessionSlot>();
        if (slotCount > 0)
        {
            var slotDuration = (sEnd - sStart) / slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                var curSlotStart = slotStart ?? (sStart + (slotDuration * i));
                var curSlotEnd = slotEnd ?? (curSlotStart + slotDuration);

                var slot = new ExamSessionSlot
                {
                    SessionId = session.SessionId,
                    SlotIndex = i + 1,
                    SlotName = $"Kíp {i + 1}",
                    SlotCode = $"{session.SessionCode}-S{i + 1}-123456",
                    StartTime = curSlotStart,
                    EndTime = curSlotEnd,
                    MaxCapacity = 20,
                    CreatedAt = DateTime.UtcNow
                };
                _db.ExamSessionSlots.Add(slot);
                slots.Add(slot);
            }
        }
        await _db.SaveChangesAsync();

        var students = new List<User>();
        for (int i = 1; i <= studentCount; i++)
        {
            var st = new User
            {
                UserId = 2000 + i,
                Username = $"st_{i}_{Guid.NewGuid():N}".Substring(0, 10),
                Email = $"student_{i}_{Guid.NewGuid()}@fpt.edu.vn",
                PasswordHash = "hash",
                FullName = $"Sinh viên {i}",
                Role = UserRole.STUDENT
            };
            _db.Users.Add(st);
            students.Add(st);
        }
        await _db.SaveChangesAsync();

        foreach (var st in students)
        {
            _db.SessionEnrollments.Add(new SessionEnrollment
            {
                SessionId = session.SessionId,
                StudentId = st.UserId,
                Status = SessionStatus.NOT_STARTED,
                EnrolledAt = DateTime.UtcNow
            });
        }
        session.CurrentEnrollment = students.Count;
        await _db.SaveChangesAsync();

        return (session, slots, exams, students);
    }

    private class MockAiServiceClient : IAiServiceClient
    {
        public Task<SpeechToTextResponse> TranscribeAudioAsync(Stream audioStream, string fileName, string examSessionId, string language = "vi-VN", CancellationToken cancellationToken = default)
            => Task.FromResult(new SpeechToTextResponse { Success = true, Transcript = "Mock transcript" });
        public Task<SpeechToTextResponse> TranscribeStoredAudioAsync(int answerId, string objectKey, CancellationToken cancellationToken = default)
            => Task.FromResult(new SpeechToTextResponse { Success = true, Transcript = "Mock stored transcript" });
        public Task<TextToSpeechResponse> SynthesizeSpeechAsync(string text, string language = "vi-VN", string? voice = null)
            => Task.FromResult(new TextToSpeechResponse { Success = true, AudioBase64 = "" });
        public Task<FollowUpResponse> GenerateFollowUpAsync(FollowUpRequest request)
            => Task.FromResult(new FollowUpResponse { ShouldFollowUp = false });
        public Task<EvaluateSessionResponse> EvaluateSessionAsync(EvaluateSessionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new EvaluateSessionResponse { Success = true, TotalScore = 10 });
    }

    private class MockStorageService : IStorageService
    {
        public Task<string> UploadAudioAsync(string objectKey, Stream content, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult($"https://r2.storage/{objectKey}");
        public string GeneratePresignedGetUrl(string objectKey, TimeSpan duration)
            => $"https://r2.storage/{objectKey}?signed";
        public string? ResolvePlaybackUrl(string? audioKeyOrUrl, TimeSpan? duration = null)
            => audioKeyOrUrl;
        public Task<bool> DeleteAudioAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
