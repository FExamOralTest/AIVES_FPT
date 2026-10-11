using Aives.Application.Courses.Interfaces;
using Aives.Application.Questions.DTOs;
using Aives.Application.Questions.Interfaces;
using Aives.Domain.Entities;

namespace Aives.Application.Questions.Services;

public class ExamGeneratorService : IExamGeneratorService
{
    private readonly IQuestionBankRepository _questionRepo;
    private readonly ICourseRepository       _courseRepo;

    public ExamGeneratorService(IQuestionBankRepository questionRepo, ICourseRepository courseRepo)
    {
        _questionRepo = questionRepo;
        _courseRepo   = courseRepo;
    }

    public async Task<GenerateExamResponse> GenerateAsync(GenerateExamRequest req)
    {
        if (req.EasyCount < 0 || req.MediumCount < 0 || req.HardCount < 0)
            throw new ArgumentException("Question counts must be non-negative.");
        if (req.EasyCount + req.MediumCount + req.HardCount == 0)
            throw new ArgumentException("Total question count must be greater than 0.");

        var course = await _courseRepo.GetByIdAsync(req.CourseId)
            ?? throw new KeyNotFoundException($"Course {req.CourseId} not found.");

        var easy   = (await _questionRepo.GetRandomByLevelAsync(req.CourseId, 1, req.EasyCount)).ToList();
        var medium = (await _questionRepo.GetRandomByLevelAsync(req.CourseId, 2, req.MediumCount)).ToList();
        var hard   = (await _questionRepo.GetRandomByLevelAsync(req.CourseId, 3, req.HardCount)).ToList();

        if (easy.Count < req.EasyCount)
            throw new InvalidOperationException(
                $"Not enough level-1 questions. Requested {req.EasyCount}, available {easy.Count}.");
        if (medium.Count < req.MediumCount)
            throw new InvalidOperationException(
                $"Not enough level-2 questions. Requested {req.MediumCount}, available {medium.Count}.");
        if (hard.Count < req.HardCount)
            throw new InvalidOperationException(
                $"Not enough level-3 questions. Requested {req.HardCount}, available {hard.Count}.");

        var questions = easy.Concat(medium).Concat(hard)
            .OrderBy(_ => Guid.NewGuid())
            .Select(ToDto)
            .ToList();

        return new GenerateExamResponse(
            req.CourseId, course.CourseName,
            questions.Count,
            easy.Count, medium.Count, hard.Count,
            questions
        );
    }

    private static QuestionDto ToDto(QuestionBank q) => new(
        q.QuestionId, q.CourseId, q.QuestionText,
        q.Level, q.MaxScore, q.IsFollowUpAllowed
    );
}
