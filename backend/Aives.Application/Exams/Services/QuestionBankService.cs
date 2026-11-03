using System.ComponentModel.DataAnnotations;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

public class QuestionBankService(
    IQuestionBankRepository questions, ICourseRepository courses, IExamUnitOfWork unitOfWork)
    : IQuestionBankService
{
    public async Task<QuestionBankPageDto> GetQuestionsAsync(int courseId, QuestionBankQuery query, int teacherId)
    {
        Validate(query);
        await EnsureAssignedAsync(courseId, teacherId);
        return await questions.GetPageAsync(courseId, query);
    }

    public async Task<QuestionBankDto> GetQuestionAsync(int courseId, int questionId, int teacherId)
    {
        await EnsureAssignedAsync(courseId, teacherId);
        var question = await GetForCourseAsync(courseId, questionId);
        return ToDto(question, await questions.IsInUseAsync(questionId));
    }

    public Task<QuestionBankDto> CreateQuestionAsync(int courseId, WriteQuestionRequest request, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            Validate(request);
            await EnsureAssignedAsync(courseId, teacherId);
            var question = new QuestionBank { CourseId = courseId, CreatedAt = DateTime.UtcNow };
            Apply(question, request);
            await questions.AddAsync(question);
            await questions.SaveChangesAsync();
            return ToDto(question, false);
        });

    public Task<QuestionBankDto> UpdateQuestionAsync(int courseId, int questionId, WriteQuestionRequest request, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            Validate(request);
            await EnsureAssignedAsync(courseId, teacherId);
            var question = await GetForCourseAsync(courseId, questionId);
            await EnsureUnusedAsync(questionId);
            Apply(question, request);
            await questions.SaveChangesAsync();
            return ToDto(question, false);
        });

    public async Task DeleteQuestionAsync(int courseId, int questionId, int teacherId)
        => await unitOfWork.ExecuteAsync(async () =>
        {
            await EnsureAssignedAsync(courseId, teacherId);
            var question = await GetForCourseAsync(courseId, questionId);
            await EnsureUnusedAsync(questionId);
            questions.Remove(question);
            await questions.SaveChangesAsync();
            return true;
        });

    public Task<QuestionBankDto> ApproveQuestionAsync(int courseId, int questionId, int staffId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            await EnsureAssignedAsync(courseId, staffId);
            var question = await GetForCourseAsync(courseId, questionId);
            if (question.ApprovalStatus != QuestionApprovalStatus.PENDING_REVIEW)
                throw new QuestionBankConflictException("Only pending questions can be approved.");
            question.ApprovalStatus = QuestionApprovalStatus.APPROVED;
            question.ReviewedBy = staffId;
            question.ReviewedAt = DateTime.UtcNow;
            question.RejectionReason = null;
            await questions.SaveChangesAsync();
            return ToDto(question, await questions.IsInUseAsync(questionId));
        });

    public Task<QuestionBankDto> RejectQuestionAsync(int courseId, int questionId, string reason, int staffId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            await EnsureAssignedAsync(courseId, staffId);
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A rejection reason is required.");
            var question = await GetForCourseAsync(courseId, questionId);
            if (question.ApprovalStatus != QuestionApprovalStatus.PENDING_REVIEW)
                throw new QuestionBankConflictException("Only pending questions can be rejected.");
            question.ApprovalStatus = QuestionApprovalStatus.REJECTED;
            question.ReviewedBy = staffId;
            question.ReviewedAt = DateTime.UtcNow;
            question.RejectionReason = reason.Trim();
            await questions.SaveChangesAsync();
            return ToDto(question, await questions.IsInUseAsync(questionId));
        });

    private async Task EnsureAssignedAsync(int courseId, int teacherId)
    {
        var course = await courses.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");
        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("You are not assigned to this course.");
    }

    private async Task<QuestionBank> GetForCourseAsync(int courseId, int questionId)
    {
        var question = await questions.GetByIdAsync(questionId);
        if (question is null || question.CourseId != courseId)
            throw new KeyNotFoundException("Question not found in this course.");
        return question;
    }

    private async Task EnsureUnusedAsync(int questionId)
    {
        if (await questions.IsInUseAsync(questionId))
            throw new QuestionBankConflictException("This question is already in use. Create a new question instead.");
    }

    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true)
            || request is WriteQuestionRequest write && !float.IsFinite(write.MaxScore))
            throw new ArgumentException(errors.Count > 0
                ? string.Join(" ", errors.Select(e => e.ErrorMessage))
                : "Invalid question fields or pagination parameters.");
    }

    private static void Apply(QuestionBank question, WriteQuestionRequest request)
    {
        question.QuestionText = request.QuestionText.Trim();
        question.Topic = string.IsNullOrWhiteSpace(request.Topic) ? null : request.Topic.Trim();
        question.SampleAnswer = string.IsNullOrWhiteSpace(request.SampleAnswer) ? null : request.SampleAnswer.Trim();
        question.LearningOutcome = request.LearningOutcome!.Trim();
        question.Rubric.Clear();
        foreach (var (criterion, position) in request.Rubric!.Select((c, i) => (c, i)))
            question.Rubric.Add(new QuestionRubricCriterion
            {
                Description = criterion.Description.Trim(),
                MaxScore = criterion.MaxScore,
                Position = position
            });
        question.Level = request.Level;
        question.MaxScore = request.MaxScore;
        question.IsFollowUpAllowed = request.IsFollowUpAllowed;
        question.ExpectedKeywords = (request.ExpectedKeywords ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList();
        question.ApprovalStatus = QuestionApprovalStatus.PENDING_REVIEW;
        question.ReviewedBy = null;
        question.ReviewedAt = null;
        question.RejectionReason = null;
    }

    public static QuestionBankDto ToDto(QuestionBank question, bool isInUse) => new()
    {
        QuestionId = question.QuestionId,
        CourseId = question.CourseId,
        DocumentId = question.DocumentId,
        QuestionText = question.QuestionText,
        Topic = question.Topic,
        SampleAnswer = question.SampleAnswer,
        LearningOutcome = question.LearningOutcome,
        Rubric = question.Rubric.OrderBy(c => c.Position).Select(c => new RubricCriterionDto
        {
            Description = c.Description,
            MaxScore = c.MaxScore
        }).ToList(),
        Level = question.Level,
        MaxScore = question.MaxScore ?? 1,
        IsFollowUpAllowed = question.IsFollowUpAllowed ?? true,
        ExpectedKeywords = question.ExpectedKeywords,
        ApprovalStatus = question.ApprovalStatus,
        ReviewedBy = question.ReviewedBy,
        ReviewedAt = question.ReviewedAt,
        RejectionReason = question.RejectionReason,
        CreatedAt = question.CreatedAt,
        IsInUse = isInUse
    };
}
