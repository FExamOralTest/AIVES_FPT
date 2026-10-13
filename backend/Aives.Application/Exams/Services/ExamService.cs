using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Services;

/// <summary>
/// Triển khai nghiệp vụ quản lý Exam cho Teacher.
/// Bao gồm tạo đề thi, sinh ExamCode bảo mật, và tích hợp AI gợi ý câu hỏi.
/// </summary>
public class ExamService : IExamService
{
    private readonly IExamRepository _examRepository;
    private readonly IQuestionBankRepository _questionBankRepository;

    public ExamService(
        IExamRepository examRepository,
        IQuestionBankRepository questionBankRepository)
    {
        _examRepository = examRepository;
        _questionBankRepository = questionBankRepository;
    }

    /// <inheritdoc/>
    public async Task<ExamDto> CreateExamAsync(CreateExamRequest request, int teacherId)
    {
        // Sinh ExamCode duy nhất, chống collision bằng vòng lặp kiểm tra
        var examCode = await GenerateUniqueExamCodeAsync();

        var exam = new Exam
        {
            Title = request.Title,
            CourseId = request.CourseId,
            CreatedBy = teacherId,
            ExamCode = examCode,
            DurationMinutes = request.DurationMinutes,
            MaxQuestions = request.MaxQuestions,
            ExamStatus = ExamStatus.DRAFT,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _examRepository.CreateAsync(exam);

        // Gán câu hỏi vào đề thi nếu Teacher đã cung cấp (thủ công hoặc từ AI)
        if (request.Questions is { Count: > 0 })
        {
            await ValidateAndAddQuestionsAsync(created.ExamId, request.CourseId, request.Questions);
        }

        // Tải lại entity đầy đủ để trả về DTO
        var fullExam = await _examRepository.GetByIdWithQuestionsAsync(created.ExamId)
            ?? throw new InvalidOperationException("Exam was created but could not be retrieved.");

        return MapToExamDto(fullExam);
    }

    /// <inheritdoc/>
    public async Task<ExamDto> GetExamByIdAsync(int examId, int teacherId)
    {
        var exam = await _examRepository.GetByIdWithQuestionsAsync(examId)
            ?? throw new KeyNotFoundException($"Exam with ID {examId} not found.");

        // Chỉ Teacher sở hữu mới được xem chi tiết
        if (exam.CreatedBy != teacherId)
            throw new UnauthorizedAccessException("You do not have permission to view this exam.");

        return MapToExamDto(exam);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ExamDto>> GetExamsByTeacherAsync(int teacherId)
    {
        var exams = await _examRepository.GetByTeacherIdAsync(teacherId);
        return exams.Select(MapToExamDto);
    }

    /// <inheritdoc/>
    public async Task<AiSuggestedQuestionsDto> GetAiSuggestedQuestionsAsync(
        AiSuggestQuestionsRequest request, int teacherId)
    {
        // Lấy toàn bộ câu hỏi của môn học từ Question Bank
        var allQuestions = await _questionBankRepository
            .GetByCourseIdAsync(request.CourseId, request.MaxSuggestions * 3);

        // ── AI Filtering Logic ──────────────────────────────────────────────────
        // Hiện tại: Simple keyword-based ranking.
        // TODO: Tích hợp gọi FastAPI AI service để dùng embedding/semantic search.
        // ────────────────────────────────────────────────────────────────────────
        var filtered = FilterQuestionsWithAi(allQuestions.ToList(), request.FocusTopic, request.MaxSuggestions);

        return new AiSuggestedQuestionsDto
        {
            SuggestedQuestions = filtered.Select(q => new ExamQuestionDto
            {
                QuestionId = q.QuestionId,
                QuestionText = q.QuestionText,
                QuestionScore = q.MaxScore ?? 1.0f,
                MaxScore = q.MaxScore,
                IsFollowUpAllowed = q.IsFollowUpAllowed
            }).ToList(),
            AiRationale = string.IsNullOrWhiteSpace(request.FocusTopic)
                ? "Selected based on course coverage."
                : $"Selected based on topic relevance to: \"{request.FocusTopic}\"."
        };
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Sinh ExamCode ngẫu nhiên bảo mật dạng "EX-XXXXXXXX" (8 ký tự hex).
    /// Kiểm tra DB để đảm bảo không trùng.
    /// </summary>
    private async Task<string> GenerateUniqueExamCodeAsync()
    {
        const int maxAttempts = 10;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = GenerateExamCode();
            if (!await _examRepository.ExamCodeExistsAsync(code))
                return code;
        }
        throw new InvalidOperationException(
            "Unable to generate a unique Exam Code after multiple attempts. Please try again.");
    }

    /// <summary>Sinh ExamCode: "EX-" + 8 ký tự hex ngẫu nhiên, uppercase.</summary>
    private static string GenerateExamCode()
    {
        var randomBytes = new byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        return "EX-" + Convert.ToHexString(randomBytes);
    }

    /// <summary>
    /// Validate câu hỏi thuộc đúng CourseId, sau đó bulk-insert vào exam_questions.
    /// </summary>
    private async Task ValidateAndAddQuestionsAsync(
        int examId, int courseId, List<ExamQuestionItem> items)
    {
        var questionIds = items.Select(q => q.QuestionId).Distinct().ToList();
        var validIds = await _questionBankRepository.GetValidQuestionIdsForCourseAsync(courseId, questionIds);

        var invalidIds = questionIds.Except(validIds).ToList();
        if (invalidIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"The following question IDs do not belong to course {courseId}: " +
                string.Join(", ", invalidIds));
        }

        var examQuestions = items
            .Where(q => validIds.Contains(q.QuestionId))
            .Select(q => new ExamQuestion
            {
                ExamId = examId,
                QuestionId = q.QuestionId,
                QuestionScore = q.QuestionScore
            }).ToList();

        await _examRepository.AddQuestionsAsync(examQuestions);
        await _examRepository.SaveChangesAsync();
    }

    /// <summary>
    /// Simple AI-like filtering: keyword matching trên QuestionText.
    /// Sẽ được thay thế bằng gọi FastAPI embedding service trong sprint sau.
    /// </summary>
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

    // ─── Mapper ──────────────────────────────────────────────────────────────

    private static ExamDto MapToExamDto(Exam exam) => new()
    {
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
