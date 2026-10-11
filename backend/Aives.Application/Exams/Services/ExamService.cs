using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Questions.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

public class ExamService : IExamService
{
    private readonly IExamRepository         _examRepository;
    private readonly IQuestionBankRepository _questionBankRepository;

    public ExamService(IExamRepository examRepository, IQuestionBankRepository questionBankRepository)
    {
        _examRepository         = examRepository;
        _questionBankRepository = questionBankRepository;
    }

    public async Task<ExamDto> CreateExamAsync(CreateExamRequest request, int teacherId)
    {
        var examCode = await GenerateUniqueExamCodeAsync();

        var exam = new Exam
        {
            Title           = request.Title,
            CourseId        = request.CourseId,
            CreatedBy       = teacherId,
            ExamCode        = examCode,
            DurationMinutes = request.DurationMinutes,
            MaxQuestions    = request.MaxQuestions,
            ExamStatus      = ExamStatus.DRAFT,
            CreatedAt       = DateTime.UtcNow
        };

        var created = await _examRepository.CreateAsync(exam);

        if (request.Questions is { Count: > 0 })
            await ValidateAndAddQuestionsAsync(created.ExamId, request.CourseId, request.Questions);

        var fullExam = await _examRepository.GetByIdWithQuestionsAsync(created.ExamId)
            ?? throw new InvalidOperationException("Exam was created but could not be retrieved.");

        return MapToExamDto(fullExam);
    }

    public async Task<ExamDto> GetExamByIdAsync(int examId, int teacherId)
    {
        var exam = await _examRepository.GetByIdWithQuestionsAsync(examId)
            ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

        if (exam.CreatedBy != teacherId)
            throw new UnauthorizedAccessException("You do not have permission to view this exam.");

        return MapToExamDto(exam);
    }

    public async Task<IEnumerable<ExamDto>> GetExamsByTeacherAsync(int teacherId)
        => (await _examRepository.GetByTeacherIdAsync(teacherId)).Select(MapToExamDto);

    public async Task<AiSuggestedQuestionsDto> GetAiSuggestedQuestionsAsync(
        AiSuggestQuestionsRequest request, int teacherId)
    {
        var allQuestions = await _questionBankRepository
            .GetByCourseIdAsync(request.CourseId, request.MaxSuggestions * 3);

        var filtered = FilterQuestionsWithAi(
            allQuestions.ToList(), request.FocusTopic, request.MaxSuggestions);

        return new AiSuggestedQuestionsDto
        {
            SuggestedQuestions = filtered.Select(q => new ExamQuestionDto
            {
                QuestionId        = q.QuestionId,
                QuestionText      = q.QuestionText,
                QuestionScore     = q.MaxScore ?? 1.0f,
                MaxScore          = q.MaxScore,
                IsFollowUpAllowed = q.IsFollowUpAllowed
            }).ToList(),
            AiRationale = string.IsNullOrWhiteSpace(request.FocusTopic)
                ? "Selected based on course coverage."
                : $"Selected based on topic relevance to: \"{request.FocusTopic}\"."
        };
    }

    private async Task<string> GenerateUniqueExamCodeAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = "EX-" + Convert.ToHexString(
                System.Security.Cryptography.RandomNumberGenerator.GetBytes(4));
            if (!await _examRepository.ExamCodeExistsAsync(code)) return code;
        }
        throw new InvalidOperationException("Unable to generate a unique Exam Code.");
    }

    private async Task ValidateAndAddQuestionsAsync(
        int examId, int courseId, List<ExamQuestionItem> items)
    {
        var questionIds = items.Select(q => q.QuestionId).Distinct().ToList();
        var validIds = await _questionBankRepository
            .GetValidQuestionIdsForCourseAsync(courseId, questionIds);

        var invalidIds = questionIds.Except(validIds).ToList();
        if (invalidIds.Count > 0)
            throw new InvalidOperationException(
                $"The following question IDs do not belong to course {courseId}: " +
                string.Join(", ", invalidIds));

        var examQuestions = items
            .Where(q => validIds.Contains(q.QuestionId))
            .Select(q => new ExamQuestion
            {
                ExamId        = examId,
                QuestionId    = q.QuestionId,
                QuestionScore = q.QuestionScore
            }).ToList();

        await _examRepository.AddQuestionsAsync(examQuestions);
        await _examRepository.SaveChangesAsync();
    }

    private static List<QuestionBank> FilterQuestionsWithAi(
        List<QuestionBank> questions, string? focusTopic, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(focusTopic))
            return questions.Take(maxCount).ToList();

        var keywords = focusTopic
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant())
            .ToHashSet();

        return questions
            .Select(q => new
            {
                Question = q,
                Score = keywords.Count(kw => q.QuestionText.Contains(kw, StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Question.MaxScore)
            .Take(maxCount)
            .Select(x => x.Question)
            .ToList();
    }

    private static ExamDto MapToExamDto(Exam exam) => new()
    {
        ExamId          = exam.ExamId,
        Title           = exam.Title,
        ExamCode        = exam.ExamCode,
        CourseId        = exam.CourseId,
        CourseName      = exam.Course?.CourseName,
        CreatedBy       = exam.CreatedBy,
        CreatedByName   = exam.CreatedByNavigation?.FullName,
        DurationMinutes = exam.DurationMinutes,
        MaxQuestions    = exam.MaxQuestions,
        ExamStatus      = exam.ExamStatus?.ToString(),
        CreatedAt       = exam.CreatedAt,
        Questions       = exam.ExamQuestions.Select(eq => new ExamQuestionDto
        {
            QuestionId        = eq.QuestionId,
            QuestionText      = eq.Question?.QuestionText ?? string.Empty,
            QuestionScore     = eq.QuestionScore,
            MaxScore          = eq.Question?.MaxScore,
            IsFollowUpAllowed = eq.Question?.IsFollowUpAllowed
        }).ToList()
    };
}
