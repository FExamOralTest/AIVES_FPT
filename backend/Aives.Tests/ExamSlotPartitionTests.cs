using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

public class ExamSlotPartitionTests : IDisposable
{
    private readonly AivesDbContext _db;
    private readonly ExamService _examService;
    private readonly int _courseId = 101;
    private readonly int _teacherId = 99;

    public ExamSlotPartitionTests()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"ExamSlotPartitionTest_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AivesDbContext(options);

        var examRepo = new ExamRepository(_db);
        var questionRepo = new QuestionBankRepository(_db);
        var courseRepo = new CourseRepository(_db);
        var uow = new ExamUnitOfWork(_db);

        _examService = new ExamService(examRepo, questionRepo, courseRepo, uow);

        // Seed teacher and course
        var teacher = new User
        {
            UserId = _teacherId,
            Username = "teacher_test",
            Email = "teacher@fpt.edu.vn",
            PasswordHash = "hash",
            FullName = "Test Teacher",
            Role = UserRole.STAFF,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(teacher);

        var course = new Course
        {
            CourseId = _courseId,
            CourseCode = "PRJ301",
            CourseName = "Java Web Development",
            TeacherId = _teacherId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Courses.Add(course);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    private List<QuestionBank> SeedQuestions(int count, int easyCount, int mediumCount, int hardCount)
    {
        var list = new List<QuestionBank>();
        int id = 1000;

        for (int i = 0; i < easyCount; i++)
        {
            list.Add(new QuestionBank
            {
                QuestionId = ++id,
                CourseId = _courseId,
                QuestionText = $"Easy Question {i + 1}",
                Level = 1,
                MaxScore = 1.0f,
                ApprovalStatus = QuestionApprovalStatus.APPROVED,
                CreatedAt = DateTime.UtcNow
            });
        }
        for (int i = 0; i < mediumCount; i++)
        {
            list.Add(new QuestionBank
            {
                QuestionId = ++id,
                CourseId = _courseId,
                QuestionText = $"Medium Question {i + 1}",
                Level = 2,
                MaxScore = 2.0f,
                ApprovalStatus = QuestionApprovalStatus.APPROVED,
                CreatedAt = DateTime.UtcNow
            });
        }
        for (int i = 0; i < hardCount; i++)
        {
            list.Add(new QuestionBank
            {
                QuestionId = ++id,
                CourseId = _courseId,
                QuestionText = $"Hard Question {i + 1}",
                Level = 3,
                MaxScore = 3.0f,
                ApprovalStatus = QuestionApprovalStatus.APPROVED,
                CreatedAt = DateTime.UtcNow
            });
        }

        _db.QuestionBanks.AddRange(list);
        _db.SaveChanges();
        return list;
    }

    [Fact]
    public async Task PreviewPartition_Throws_When_Pool_Size_Insufficient()
    {
        // 3 slots, 5 questions per slot = 15 required. Seed only 10 questions.
        var questions = SeedQuestions(10, 4, 4, 2);

        var request = new AutoPartitionSlotPapersRequest
        {
            CourseId = _courseId,
            SlotCount = 3,
            QuestionsPerSlot = 5,
            QuestionIds = questions.Select(q => q.QuestionId).ToList()
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _examService.PreviewPartitionSlotPapersAsync(request, _teacherId));

        Assert.Contains("insufficient for 3 slots requiring 5 questions each (minimum 15 required)", ex.Message);
    }

    [Fact]
    public async Task PreviewPartition_Stratified_Partitions_Disjoint_With_Surplus_In_Reserve()
    {
        // 3 slots, 5 questions per slot = 15 needed. Seed 20 questions (6 Easy, 9 Medium, 5 Hard).
        var questions = SeedQuestions(20, 6, 9, 5);

        var request = new AutoPartitionSlotPapersRequest
        {
            CourseId = _courseId,
            SlotCount = 3,
            QuestionsPerSlot = 5,
            ParentExamTitle = "PRJ301 Viva Exam",
            ParentExamCode = "PRJ-VIVA",
            QuestionIds = questions.Select(q => q.QuestionId).ToList(),
            SlotTemplates = new List<CreateExamSlotTemplateInput>
            {
                new() { SlotIndex = 1, SlotName = "Morning Slot 1", DurationMinutes = 30, MaxCapacity = 20 },
                new() { SlotIndex = 2, SlotName = "Morning Slot 2", DurationMinutes = 30, MaxCapacity = 20 },
                new() { SlotIndex = 3, SlotName = "Morning Slot 3", DurationMinutes = 30, MaxCapacity = 20 }
            }
        };

        var response = await _examService.PreviewPartitionSlotPapersAsync(request, _teacherId);

        Assert.Equal(3, response.Slots.Count);
        Assert.Equal(20, response.TotalSelected);
        Assert.Equal(15, response.TotalAllocated);
        Assert.Equal(5, response.TotalReserve);
        Assert.Equal(5, response.ReserveQuestions.Count);

        // Verify each slot has exactly 5 questions
        foreach (var slot in response.Slots)
        {
            Assert.Equal(5, slot.Questions.Count);
            // Difficulty balance check: each slot should have received at least 2 Easy and 3 Medium/Hard
            Assert.True(slot.EasyCount >= 1);
            Assert.True(slot.MediumCount >= 1);
        }

        // Verify ZERO overlap (disjoint sets) across all slots and reserve
        var allAllocatedIds = response.Slots.SelectMany(s => s.Questions.Select(q => q.QuestionId)).ToList();
        var reserveIds = response.ReserveQuestions.Select(q => q.QuestionId).ToList();

        Assert.Equal(15, allAllocatedIds.Distinct().Count());
        Assert.Empty(allAllocatedIds.Intersect(reserveIds));
    }

    [Fact]
    public async Task CreateExam_With_GeneratedSubPapers_Creates_Child_Exams_And_Links_SlotTemplates()
    {
        var questions = SeedQuestions(15, 5, 5, 5);

        var request = new CreateExamRequest
        {
            Title = "SE1601 Final Exam",
            CourseId = _courseId,
            DurationMinutes = 30,
            MaxQuestions = 5,
            AutoGenerateSlotPapers = true,
            Questions = questions.Take(10).Select(q => new ExamQuestionItem
            {
                QuestionId = q.QuestionId,
                QuestionScore = 1.0f
            }).ToList(),
            SlotTemplates = new List<CreateExamSlotTemplateInput>
            {
                new() { SlotIndex = 1, SlotName = "Kíp 1", DurationMinutes = 30, MaxCapacity = 25 },
                new() { SlotIndex = 2, SlotName = "Kíp 2", DurationMinutes = 30, MaxCapacity = 25 }
            },
            GeneratedSubPapers = new List<CreateSubPaperInput>
            {
                new()
                {
                    SlotIndex = 1,
                    Title = "SE1601 Final Exam - Kíp 1",
                    Questions = questions.Take(5).Select(q => new ExamQuestionItem
                    {
                        QuestionId = q.QuestionId,
                        QuestionScore = 1.0f
                    }).ToList()
                },
                new()
                {
                    SlotIndex = 2,
                    Title = "SE1601 Final Exam - Kíp 2",
                    Questions = questions.Skip(5).Take(5).Select(q => new ExamQuestionItem
                    {
                        QuestionId = q.QuestionId,
                        QuestionScore = 1.0f
                    }).ToList()
                }
            }
        };

        var created = await _examService.CreateExamAsync(request, _teacherId);

        Assert.NotNull(created);
        Assert.Equal(2, created.SlotTemplates.Count);

        // Verify slot templates have sub-papers linked
        var slot1 = created.SlotTemplates.First(s => s.SlotIndex == 1);
        Assert.Single(slot1.ExamPaperIds);
        Assert.Single(slot1.ExamPapers);
        Assert.Equal("SE1601 Final Exam - Kíp 1", slot1.ExamPapers[0].Title);

        var slot2 = created.SlotTemplates.First(s => s.SlotIndex == 2);
        Assert.Single(slot2.ExamPaperIds);
        Assert.Single(slot2.ExamPapers);
        Assert.Equal("SE1601 Final Exam - Kíp 2", slot2.ExamPapers[0].Title);

        // Sub-papers are distinct
        Assert.NotEqual(slot1.ExamPaperIds[0], slot2.ExamPaperIds[0]);

        // Verify sub-papers exist in database with their 5 questions
        var subExam1 = await _db.Exams.Include(e => e.ExamQuestions).FirstOrDefaultAsync(e => e.ExamId == slot1.ExamPaperIds[0]);
        Assert.NotNull(subExam1);
        Assert.Equal(5, subExam1.ExamQuestions.Count);

        var subExam2 = await _db.Exams.Include(e => e.ExamQuestions).FirstOrDefaultAsync(e => e.ExamId == slot2.ExamPaperIds[0]);
        Assert.NotNull(subExam2);
        Assert.Equal(5, subExam2.ExamQuestions.Count);
    }
}
