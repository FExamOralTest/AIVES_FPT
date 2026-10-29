using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Aives.Api.Controllers;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 2.25 — Result End-to-End Testing:
/// Kiểm thử Answer hoàn tất → Result chờ review → Examiner review → publish → Student xem kết quả.
/// BR-08: Sinh viên hoàn tất thi, AI chấm, Result chuyển sang PENDING_REVIEW.
/// BR-09: Result được gửi cho Examiner (Khảo thí) review và kiểm tra đối soát.
/// BR-10: Examiner phê duyệt (Approve) và công bố (Publish) kết quả thi chính thức.
/// BR-11: Sinh viên chỉ xem được kết quả và feedback khi Result ở trạng thái PUBLISHED.
/// BR-15: Không ghi đè AiScore, lưu trữ audit log đối soát đầy đủ.
/// </summary>
public class ResultEndToEndTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly ResultRepository _resultRepo;
    private readonly ExamSessionRepository _sessionRepo;
    private readonly UserRepository _userRepo;
    private readonly AuditLogRepository _auditLogRepo;
    private readonly FakeStorageService _storageService;
    private readonly IResultService _resultService;

    public ResultEndToEndTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"ResultE2E_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AivesDbContext(options);
        _resultRepo = new ResultRepository(_db);
        _sessionRepo = new ExamSessionRepository(_db);
        _userRepo = new UserRepository(_db);
        _auditLogRepo = new AuditLogRepository(_db);
        _storageService = new FakeStorageService();

        _resultService = new ResultService(
            _resultRepo,
            _sessionRepo,
            _userRepo,
            _storageService,
            _auditLogRepo);
    }

    public void Dispose()
    {
        _storageService.Clear();
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private class SeedData
    {
        public User Examiner { get; set; } = null!;
        public User Staff { get; set; } = null!;
        public User StudentA { get; set; } = null!;
        public User StudentB { get; set; } = null!;
        public Course Course { get; set; } = null!;
        public Exam Exam { get; set; } = null!;
        public ExamSession Session { get; set; } = null!;
        public QuestionBank Question { get; set; } = null!;
        public Result Result { get; set; } = null!;
        public Answer Answer { get; set; } = null!;
    }

    private async Task<SeedData> SeedAsync()
    {
        var examiner = new User
        {
            Username = "examiner_bac",
            FullName = "Examiner Bac",
            Email = "examiner@fpt.edu.vn",
            PasswordHash = "hash",
            Role = UserRole.EXAMINER,
            CreatedAt = DateTime.UtcNow
        };
        var staff = new User
        {
            Username = "staff_hung",
            FullName = "Staff Hung",
            Email = "staff@fpt.edu.vn",
            PasswordHash = "hash",
            Role = UserRole.STAFF,
            CreatedAt = DateTime.UtcNow
        };
        var studentA = new User
        {
            Username = "student_phuong",
            FullName = "Student Phuong",
            Email = "phuong@fpt.edu.vn",
            PasswordHash = "hash",
            Role = UserRole.STUDENT,
            CreatedAt = DateTime.UtcNow
        };
        var studentB = new User
        {
            Username = "student_nam",
            FullName = "Student Nam",
            Email = "nam@fpt.edu.vn",
            PasswordHash = "hash",
            Role = UserRole.STUDENT,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.AddRange(examiner, staff, studentA, studentB);
        await _db.SaveChangesAsync();

        var course = new Course
        {
            CourseCode = "SWD392",
            CourseName = "Software Architecture & Design",
            TeacherId = staff.UserId,
            CreatedBy = staff.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();

        var exam = new Exam
        {
            CourseId = course.CourseId,
            Title = "SWD392 Viva Examination",
            ExamCode = "EXAM-SWD392",
            DurationMinutes = 30,
            ExamStatus = ExamStatus.OPEN,
            CreatedBy = staff.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionName = "SWD392 Viva Session 1",
            SessionCode = "ESS-12345678",
            ScheduledBy = examiner.UserId,
            Status = SessionStatus.IN_PROGRESS,
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow.AddHours(2),
            MaxCapacity = 30,
            CreatedAt = DateTime.UtcNow
        };
        _db.ExamSessions.Add(session);
        await _db.SaveChangesAsync();

        var enrollmentA = new SessionEnrollment
        {
            SessionId = session.SessionId,
            StudentId = studentA.UserId,
            EnrolledAt = DateTime.UtcNow
        };
        _db.SessionEnrollments.Add(enrollmentA);

        var question = new QuestionBank
        {
            CourseId = course.CourseId,
            QuestionText = "Explain the C4 model and differences between C0 and C1.",
            Topic = "Architecture",
            Level = 2,
            MaxScore = 10f,
            ApprovalStatus = QuestionApprovalStatus.APPROVED,
            CreatedAt = DateTime.UtcNow
        };
        _db.QuestionBanks.Add(question);
        await _db.SaveChangesAsync();

        // Giai đoạn ban đầu: Answer hoàn tất xử lý qua AI, Result chuyển sang PENDING_REVIEW (BR-08)
        var result = new Result
        {
            SessionId = session.SessionId,
            StudentId = studentA.UserId,
            Status = SubmissionStatus.PENDING_REVIEW,
            AiScore = 7.5f,
            AiFeedback = "Good grasp of C4 model, but needs more depth on Container interactions.",
            SubmittedAt = DateTime.UtcNow
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = question.QuestionId,
            Status = AnswerStatus.EVALUATED,
            StudentAnswer = "C0 is System Context, C1 is Container Diagram.",
            Transcript = "c0 is system context c1 is container diagram",
            AudioUrl = "sessions/1/students/1/q1_audio.webm",
            ObjectKey = "sessions/1/students/1/q1_audio.webm",
            AiScore = 7.5f,
            AiFeedback = "Clear explanation of C0 and C1.",
            AnsweredAt = DateTime.UtcNow,
            DurationSeconds = 12.5f,
            DetectedLanguage = "en"
        };
        _db.Answers.Add(answer);
        await _db.SaveChangesAsync();

        return new SeedData
        {
            Examiner = examiner,
            Staff = staff,
            StudentA = studentA,
            StudentB = studentB,
            Course = course,
            Exam = exam,
            Session = session,
            Question = question,
            Result = result,
            Answer = answer
        };
    }

    [Fact]
    public async Task FullResultLifecycle_AnswerDone_PendingReview_ExaminerReview_Publish_StudentViewsResult()
    {
        // -------------------------------------------------------------
        // Arrange: Seed initial state (Answer Evaluated → Result PENDING_REVIEW)
        // -------------------------------------------------------------
        var seed = await SeedAsync();

        // -------------------------------------------------------------
        // Step 1: Student checks result while still in PENDING_REVIEW
        // Security rule (BR-10, BR-11): Result is NOT published, scores must be hidden!
        // -------------------------------------------------------------
        var pendingStudentResult = await _resultService.GetStudentResultAsync(seed.Session.SessionId, seed.StudentA.UserId);
        Assert.NotNull(pendingStudentResult);
        Assert.False(pendingStudentResult.IsPublished, "Result must not be marked as published while PENDING_REVIEW.");
        Assert.Null(pendingStudentResult.FinalScore);
        Assert.Null(pendingStudentResult.FinalFeedback);
        Assert.Contains("under evaluation and review", pendingStudentResult.Message);
        Assert.Empty(pendingStudentResult.Answers);

        // -------------------------------------------------------------
        // Step 2: Examiner inspects pending review list & result details
        // (BR-09: Result sends to Examiner for review)
        // -------------------------------------------------------------
        var pendingList = (await _resultService.GetAllPendingReviewResultsAsync(seed.Examiner.UserId)).ToList();
        Assert.Single(pendingList);
        Assert.Equal(seed.Result.ResultId, pendingList[0].ResultId);
        Assert.Equal(seed.StudentA.FullName, pendingList[0].StudentName);

        var reviewDetail = await _resultService.GetResultForReviewAsync(seed.Result.ResultId, seed.Examiner.UserId);
        Assert.NotNull(reviewDetail);
        Assert.Equal(SubmissionStatus.PENDING_REVIEW.ToString(), reviewDetail.Status);
        Assert.Equal(7.5f, reviewDetail.AiScore);
        Assert.Single(reviewDetail.Answers);
        Assert.Equal("c0 is system context c1 is container diagram", reviewDetail.Answers[0].Transcript);

        // -------------------------------------------------------------
        // Step 3: Examiner reviews, adjusts FinalScore & comments
        // (BR-10 & BR-15: Status → REVIEWED, AiScore NOT overwritten, AuditLog recorded)
        // -------------------------------------------------------------
        var reviewRequest = new ReviewResultRequest
        {
            FinalScore = 8.5f,
            FinalFeedback = "Clear explanation and confident vocal delivery.",
            AnswerReviews = new List<AnswerReviewItem>
            {
                new()
                {
                    AnswerId = seed.Answer.AnswerId,
                    FinalScore = 8.5f,
                    TeacherComment = "Good understanding of system boundaries."
                }
            }
        };

        var reviewedResult = await _resultService.ReviewResultAsync(seed.Result.ResultId, reviewRequest, seed.Examiner.UserId);
        Assert.Equal(SubmissionStatus.REVIEWED.ToString(), reviewedResult.Status);
        Assert.Equal(8.5f, reviewedResult.FinalScore);
        Assert.Equal(7.5f, reviewedResult.AiScore); // Ensure AI score is preserved (BR-15)

        // Verify in Database
        var dbResultAfterReview = await _db.Results.Include(r => r.Answers).FirstAsync(r => r.ResultId == seed.Result.ResultId);
        Assert.Equal(SubmissionStatus.REVIEWED, dbResultAfterReview.Status);
        Assert.Equal(8.5f, dbResultAfterReview.FinalScore);
        Assert.Equal(7.5f, dbResultAfterReview.AiScore);
        Assert.Equal(seed.Examiner.UserId, dbResultAfterReview.ReviewedBy);
        Assert.NotNull(dbResultAfterReview.ReviewedAt);

        // Verify Audit Trail recorded REVIEW_SCORE_OVERRIDE
        var auditLogs = await _db.AuditLogs.Where(a => a.EntityId == seed.Result.ResultId).ToListAsync();
        Assert.Contains(auditLogs, a => a.Action == "REVIEW_SCORE_OVERRIDE" && a.PerformedBy == seed.Examiner.UserId);

        // -------------------------------------------------------------
        // Step 4: Student checks result while in REVIEWED state
        // Result is reviewed by Examiner, but NOT published yet! Must still be hidden.
        // -------------------------------------------------------------
        var reviewedStudentResult = await _resultService.GetStudentResultAsync(seed.Session.SessionId, seed.StudentA.UserId);
        Assert.False(reviewedStudentResult.IsPublished, "Student must not see scores while Result is in REVIEWED state.");
        Assert.Null(reviewedStudentResult.FinalScore);

        // -------------------------------------------------------------
        // Step 5: Examiner approves and publishes final result
        // (BR-10: Status → PUBLISHED, PublishedAt, PublishedBy, AuditLog recorded)
        // -------------------------------------------------------------
        var publishedResult = await _resultService.PublishResultAsync(seed.Result.ResultId, seed.Examiner.UserId);
        Assert.Equal(SubmissionStatus.PUBLISHED.ToString(), publishedResult.Status);

        // Verify in Database
        var dbResultAfterPublish = await _db.Results.FirstAsync(r => r.ResultId == seed.Result.ResultId);
        Assert.Equal(SubmissionStatus.PUBLISHED, dbResultAfterPublish.Status);
        Assert.Equal(seed.Examiner.UserId, dbResultAfterPublish.PublishedBy);
        Assert.NotNull(dbResultAfterPublish.PublishedAt);

        // Verify Audit Trail recorded PUBLISH_RESULT
        var publishAudit = await _db.AuditLogs
            .FirstOrDefaultAsync(a => a.EntityId == seed.Result.ResultId && a.Action == "PUBLISH_RESULT");
        Assert.NotNull(publishAudit);
        Assert.Equal(seed.Examiner.UserId, publishAudit.PerformedBy);

        // -------------------------------------------------------------
        // Step 6: Student views published final result & feedback (BR-11)
        // -------------------------------------------------------------
        var finalStudentResult = await _resultService.GetStudentResultAsync(seed.Session.SessionId, seed.StudentA.UserId);
        Assert.NotNull(finalStudentResult);
        Assert.True(finalStudentResult.IsPublished);
        Assert.Equal(8.5f, finalStudentResult.FinalScore);
        Assert.Equal("Clear explanation and confident vocal delivery.", finalStudentResult.FinalFeedback);
        Assert.NotNull(finalStudentResult.PublishedAt);
        Assert.NotNull(finalStudentResult.Answers);
        Assert.Single(finalStudentResult.Answers);

        var studentAnswerItem = finalStudentResult.Answers[0];
        Assert.Equal(8.5f, studentAnswerItem.FinalScore);
        Assert.Equal("Good understanding of system boundaries.", studentAnswerItem.TeacherComment);
        Assert.Equal("c0 is system context c1 is container diagram", studentAnswerItem.Transcript);
        Assert.NotNull(studentAnswerItem.AudioUrl);
    }

    [Fact]
    public async Task StudentIsolation_StudentCannotViewAnotherStudentsResult()
    {
        var seed = await SeedAsync();

        // Publish Student A's result
        await _resultService.PublishResultAsync(seed.Result.ResultId, seed.Examiner.UserId);

        // Student B attempts to query result for this session (Student B did not take the exam)
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _resultService.GetStudentResultAsync(seed.Session.SessionId, seed.StudentB.UserId));
    }

    [Fact]
    public async Task PublishResult_WhenAlreadyPublished_ThrowsInvalidOperationException()
    {
        var seed = await SeedAsync();

        // First publish succeeds
        await _resultService.PublishResultAsync(seed.Result.ResultId, seed.Examiner.UserId);

        // Second publish on already published result must throw
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _resultService.PublishResultAsync(seed.Result.ResultId, seed.Examiner.UserId));

        Assert.Contains("already published", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExaminerDirectPublish_WithoutManualScoreOverride_AcceptsAiScore()
    {
        var seed = await SeedAsync();

        // Examiner directly publishes from PENDING_REVIEW (accepting AI recommendation)
        var publishedResult = await _resultService.PublishResultAsync(seed.Result.ResultId, seed.Examiner.UserId);

        Assert.Equal(SubmissionStatus.PUBLISHED.ToString(), publishedResult.Status);
        Assert.Equal(seed.Result.AiScore, publishedResult.FinalScore); // FinalScore defaults to AiScore (7.5f)

        var studentView = await _resultService.GetStudentResultAsync(seed.Session.SessionId, seed.StudentA.UserId);
        Assert.True(studentView.IsPublished);
        Assert.Equal(7.5f, studentView.FinalScore);
    }

    [Fact]
    public async Task RoleSeparation_StaffOrStudent_CannotReviewOrPublishResult()
    {
        var seed = await SeedAsync();

        var reviewRequest = new ReviewResultRequest
        {
            FinalScore = 9.0f,
            FinalFeedback = "Staff trying to review"
        };

        // STAFF cannot review results (Separation of duties: only EXAMINER or ADMIN)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _resultService.ReviewResultAsync(seed.Result.ResultId, reviewRequest, seed.Staff.UserId));

        // STAFF cannot publish results
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _resultService.PublishResultAsync(seed.Result.ResultId, seed.Staff.UserId));

        // STUDENT cannot review results
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _resultService.ReviewResultAsync(seed.Result.ResultId, reviewRequest, seed.StudentA.UserId));

        // STUDENT cannot publish results
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _resultService.PublishResultAsync(seed.Result.ResultId, seed.StudentA.UserId));
    }

    [Fact]
    public async Task HttpControllers_EndToEndFlow_WithClaimsIdentity()
    {
        var seed = await SeedAsync();

        // 1. Setup Examiner Controller
        var resultsController = new ResultsController(_resultService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, seed.Examiner.UserId.ToString()),
                        new Claim(ClaimTypes.Role, "EXAMINER")
                    }, "TestAuth"))
                }
            }
        };

        // Examiner reviews
        var reviewAction = await resultsController.ReviewResult(seed.Result.ResultId, new ReviewResultRequest
        {
            FinalScore = 9.0f,
            FinalFeedback = "Excellent performance."
        });
        var okReview = Assert.IsType<OkObjectResult>(reviewAction);
        var reviewDto = Assert.IsType<ResultForReviewDto>(okReview.Value);
        Assert.Equal(SubmissionStatus.REVIEWED.ToString(), reviewDto.Status);

        // Examiner publishes
        var publishAction = await resultsController.PublishResult(seed.Result.ResultId);
        var okPublish = Assert.IsType<OkObjectResult>(publishAction);
        var publishDto = Assert.IsType<ResultForReviewDto>(okPublish.Value);
        Assert.Equal(SubmissionStatus.PUBLISHED.ToString(), publishDto.Status);

        // 2. Setup Student Controller
        var studentController = new StudentResultController(_resultService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, seed.StudentA.UserId.ToString()),
                        new Claim(ClaimTypes.Role, "STUDENT")
                    }, "TestAuth"))
                }
            }
        };

        // Student views published result
        var studentAction = await studentController.GetStudentResult(seed.Session.SessionId);
        var okStudent = Assert.IsType<OkObjectResult>(studentAction);
        var studentDto = Assert.IsType<StudentResultDto>(okStudent.Value);
        Assert.True(studentDto.IsPublished);
        Assert.Equal(9.0f, studentDto.FinalScore);
    }
}
