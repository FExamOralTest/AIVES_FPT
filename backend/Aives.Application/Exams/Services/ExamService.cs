using System.ComponentModel.DataAnnotations;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

public class ExamService(
    IExamRepository exams, IQuestionBankRepository questions,
    ICourseRepository courses, IExamUnitOfWork unitOfWork) : IExamService
{
    public Task<ExamDto> CreateExamAsync(CreateExamRequest request, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            Validate(request);
            await EnsureAssignedAsync(request.CourseId, teacherId);
            var items = request.Questions ?? [];
            if (items.Any(q => q is null))
                throw new ArgumentException("Questions must not contain null items.");
            foreach (var item in items)
            {
                Validate(item);
                if (!float.IsFinite(item.QuestionScore))
                    throw new ArgumentException("Question scores must be finite.");
            }
            if (items.Count > request.MaxQuestions)
                throw new ArgumentException("Question count exceeds MaxQuestions.");
            var ids = items.Select(q => q.QuestionId).ToList();
            if (ids.Distinct().Count() != ids.Count)
                throw new ArgumentException("Duplicate question IDs are not allowed.");
            var selected = await questions.GetByIdsForCourseAsync(request.CourseId, ids);
            if (selected.Count != ids.Count)
                throw new ArgumentException("Every question must exist and belong to the exam course.");
            if (selected.Any(q => string.IsNullOrWhiteSpace(q.QuestionText) || q.Level is < 1 or > 3
                || !float.IsFinite(q.MaxScore ?? 1) || (q.MaxScore ?? 1) < 0.01f || (q.MaxScore ?? 1) > 100))
                throw new ArgumentException("The selected question bank contains invalid questions.");
            if (selected.Any(q => q.ApprovalStatus != QuestionApprovalStatus.APPROVED))
                throw new QuestionBankConflictException("Only approved questions can be added to an exam.");

            var exam = new Exam
            {
                Title = request.Title.Trim(),
                CourseId = request.CourseId,
                CreatedBy = teacherId,
                ExamCode = await GenerateUniqueExamCodeAsync(),
                DurationMinutes = request.DurationMinutes,
                MaxQuestions = request.MaxQuestions,
                ExamStatus = ExamStatus.DRAFT,
                CreatedAt = DateTime.UtcNow,
                ExamQuestions = items.Select(q => new ExamQuestion
                {
                    QuestionId = q.QuestionId,
                    QuestionScore = q.QuestionScore
                }).ToList()
            };
            var created = await exams.CreateAsync(exam);
            var fullExam = await exams.GetByIdWithQuestionsAsync(created.ExamId)
                ?? throw new InvalidOperationException("Exam was created but could not be retrieved.");
            return MapToExamDto(fullExam);
        });

    public async Task<ExamDto> GetExamByIdAsync(int examId, int teacherId)
    {
        var exam = await exams.GetByIdWithQuestionsAsync(examId)
            ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");
        if (exam.CreatedBy != teacherId)
            throw new UnauthorizedAccessException("You do not have permission to view this exam.");
        await EnsureAssignedAsync(exam.CourseId, teacherId);
        return MapToExamDto(exam);
    }

    public async Task<IEnumerable<ExamDto>> GetExamsByTeacherAsync(int teacherId)
        => (await exams.GetByTeacherIdAsync(teacherId)).Select(MapToExamDto);

    public async Task<SuggestedQuestionsDto> GetSuggestedQuestionsAsync(SuggestQuestionsRequest request, int teacherId)
    {
        Validate(request);
        await EnsureAssignedAsync(request.CourseId, teacherId);
        var selected = await questions.GetSuggestionsAsync(request.CourseId, request.FocusTopic, request.MaxSuggestions);
        return new SuggestedQuestionsDto
        {
            SuggestedQuestions = selected.Select(q => new ExamQuestionDto
            {
                QuestionId = q.QuestionId,
                QuestionText = q.QuestionText,
                QuestionScore = q.MaxScore ?? 1,
                MaxScore = q.MaxScore,
                IsFollowUpAllowed = q.IsFollowUpAllowed
            }).ToList(),
            SelectionMethod = "keyword",
            Rationale = string.IsNullOrWhiteSpace(request.FocusTopic)
                ? "Selected by maximum score, then newest question."
                : $"Ranked by keyword matches in question text for: {request.FocusTopic.Trim()}."
        };
    }

    public Task<ExamDto> UpdateExamAsync(int examId, UpdateExamRequest request, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            Validate(request);
            var exam = await GetEditableExamAsync(examId, teacherId);
            await ValidateQuestionsAsync(exam.CourseId, request.Questions, request.MaxQuestions);
            exam.Title = request.Title.Trim();
            exam.DurationMinutes = request.DurationMinutes;
            exam.MaxQuestions = request.MaxQuestions;
            await ReplaceQuestionsAsync(examId, request.Questions);
            return MapToExamDto(await exams.GetByIdWithQuestionsAsync(examId)
                ?? throw new KeyNotFoundException("Exam not found."));
        });

    public async Task DeleteExamAsync(int examId, int teacherId)
        => await unitOfWork.ExecuteAsync(async () =>
        {
            var exam = await GetEditableExamAsync(examId, teacherId);
            exams.Remove(exam);
            await exams.SaveChangesAsync();
            return true;
        });

    public Task<ExamDto> UpdateExamQuestionsAsync(int examId, List<ExamQuestionItem> newQuestions, int teacherId)
        => unitOfWork.ExecuteAsync(async () =>
        {
            var exam = await GetEditableExamAsync(examId, teacherId);
            await ValidateQuestionsAsync(exam.CourseId, newQuestions, exam.MaxQuestions);
            await ReplaceQuestionsAsync(examId, newQuestions);
            return MapToExamDto(await exams.GetByIdWithQuestionsAsync(examId)
                ?? throw new KeyNotFoundException("Exam not found."));
        });

    private async Task<Exam> GetEditableExamAsync(int examId, int teacherId)
    {
        var exam = await exams.GetByIdWithQuestionsAsync(examId)
            ?? throw new KeyNotFoundException("Exam not found.");
        if (exam.CreatedBy != teacherId)
            throw new UnauthorizedAccessException("You do not have permission to edit this exam.");
        await EnsureAssignedAsync(exam.CourseId, teacherId);
        if (exam.ExamStatus != ExamStatus.DRAFT || await exams.HasSessionsAsync(examId))
            throw new QuestionBankConflictException("Only draft exams without sessions can be changed or deleted.");
        return exam;
    }

    private async Task ValidateQuestionsAsync(int courseId, List<ExamQuestionItem>? items, int maxQuestions)
    {
        if (items is null || items.Any(q => q is null))
            throw new ArgumentException("Questions must not be null or contain null items.");
        foreach (var item in items)
        {
            Validate(item);
            if (!float.IsFinite(item.QuestionScore))
                throw new ArgumentException("Question scores must be finite.");
        }
        if (items.Count > maxQuestions)
            throw new ArgumentException("Question count exceeds MaxQuestions.");
        var ids = items.Select(q => q.QuestionId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("Duplicate question IDs are not allowed.");
        var selected = await questions.GetByIdsForCourseAsync(courseId, ids);
        if (selected.Count != ids.Count)
            throw new ArgumentException("Every question must exist and belong to the exam course.");
        if (selected.Any(q => string.IsNullOrWhiteSpace(q.QuestionText) || q.Level is < 1 or > 3
            || !QuestionRubricValidator.IsValidMaxScore(q.MaxScore ?? 1)))
            throw new ArgumentException("The selected question bank contains invalid questions.");
        if (selected.Any(q => q.ApprovalStatus != QuestionApprovalStatus.APPROVED))
            throw new QuestionBankConflictException("Only approved questions can be added to an exam.");
    }

    private Task ReplaceQuestionsAsync(int examId, List<ExamQuestionItem> items)
        => exams.ReplaceQuestionsAsync(examId, items.Select(q => new ExamQuestion
        {
            ExamId = examId,
            QuestionId = q.QuestionId,
            QuestionScore = q.QuestionScore
        }));

    private async Task EnsureAssignedAsync(int courseId, int teacherId)
    {
        var course = await courses.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");
        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("You are not assigned to this course.");
    }

    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new ArgumentException("Invalid exam or suggestion fields.");
    }

    private async Task<string> GenerateUniqueExamCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = "EX-" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
            if (!await exams.ExamCodeExistsAsync(code))
                return code;
        }
        throw new InvalidOperationException("Unable to generate a unique Exam Code. Please try again.");
    }

    private static ExamDto MapToExamDto(Exam exam) => new()
    {
        CanEdit = exam.ExamStatus == ExamStatus.DRAFT && !exam.ExamSessions.Any(),
        ExamId = exam.ExamId,
        Title = exam.Title,
        ExamCode = exam.ExamCode,
        CourseId = exam.CourseId,
        CourseName = exam.Course?.CourseName,
        CreatedBy = exam.CreatedBy,
        CreatedByName = exam.CreatedByNavigation?.FullName,
        DurationMinutes = exam.DurationMinutes,
        MaxQuestions = exam.MaxQuestions,
        ExamStatus = exam.ExamStatus?.ToString(),
        CreatedAt = exam.CreatedAt,
        Questions = exam.ExamQuestions.Select(eq => new ExamQuestionDto
        {
            QuestionId = eq.QuestionId,
            QuestionText = eq.Question?.QuestionText ?? string.Empty,
            QuestionScore = eq.QuestionScore,
            MaxScore = eq.Question?.MaxScore,
            IsFollowUpAllowed = eq.Question?.IsFollowUpAllowed
        }).ToList()
    };
}
