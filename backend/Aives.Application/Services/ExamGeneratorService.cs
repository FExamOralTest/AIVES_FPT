using Aives.Application.DTOs;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;

namespace Aives.Application.Services;

public class ExamGeneratorService : IExamGeneratorService
{
    private readonly IQuestionBankRepository _questionRepo;
    private readonly ICourseRepository       _courseRepo;

    public ExamGeneratorService(
        IQuestionBankRepository questionRepo,
        ICourseRepository courseRepo)
    {
        _questionRepo = questionRepo;
        _courseRepo   = courseRepo;
    }

    public async Task<GenerateExamResponse> GenerateAsync(GenerateExamRequest req)
    {
        // Validate counts
        if (req.EasyCount < 0 || req.MediumCount < 0 || req.HardCount < 0)
            throw new ArgumentException("Question counts must be non-negative.");

        var total = req.EasyCount + req.MediumCount + req.HardCount;
        if (total == 0)
            throw new ArgumentException("Total question count must be greater than 0.");

        // Kiểm tra course tồn tại
        var course = await _courseRepo.GetByIdAsync(req.CourseId)
            ?? throw new KeyNotFoundException($"Course {req.CourseId} not found.");

        // Query ngẫu nhiên từng level — tuần tự vì EF Core DbContext không thread-safe
        var easy   = (await _questionRepo.GetRandomByLevelAsync(req.CourseId, 1, req.EasyCount)).ToList();
        var medium = (await _questionRepo.GetRandomByLevelAsync(req.CourseId, 2, req.MediumCount)).ToList();
        var hard   = (await _questionRepo.GetRandomByLevelAsync(req.CourseId, 3, req.HardCount)).ToList();

        // Kiểm tra đủ câu hỏi theo từng level
        if (easy.Count < req.EasyCount)
            throw new InvalidOperationException(
                $"Not enough level-1 questions. Requested {req.EasyCount}, available {easy.Count}.");
        if (medium.Count < req.MediumCount)
            throw new InvalidOperationException(
                $"Not enough level-2 questions. Requested {req.MediumCount}, available {medium.Count}.");
        if (hard.Count < req.HardCount)
            throw new InvalidOperationException(
                $"Not enough level-3 questions. Requested {req.HardCount}, available {hard.Count}.");

        // Gộp và xáo trộn toàn bộ đề thi (Fisher-Yates via GUID)
        var questions = easy.Concat(medium).Concat(hard)
            .OrderBy(_ => Guid.NewGuid())
            .Select(ToDto)
            .ToList();

        return new GenerateExamResponse(
            CourseId:       req.CourseId,
            CourseName:     course.CourseName,
            TotalQuestions: questions.Count,
            EasyCount:      easy.Count,
            MediumCount:    medium.Count,
            HardCount:      hard.Count,
            Questions:      questions
        );
    }

    private static QuestionDto ToDto(QuestionBank q) => new(
        QuestionId:       q.QuestionId,
        CourseId:         q.CourseId,
        QuestionText:     q.QuestionText,
        Level:            q.Level,
        MaxScore:         q.MaxScore,
        IsFollowUpAllowed: q.IsFollowUpAllowed
    );
}
