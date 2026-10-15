using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
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
        ICourseRepository       courseRepo)
    {
        _questionRepo = questionRepo;
        _courseRepo   = courseRepo;
    }

    public async Task<GenerateExamResponse> GenerateAsync(
        GenerateExamRequest request, int callerId, bool isAdmin)
    {
        if (request.EasyCount < 0 || request.MediumCount < 0 || request.HardCount < 0)
            throw new ArgumentException("Question counts must be non-negative.");

        if (request.EasyCount + request.MediumCount + request.HardCount == 0)
            throw new ArgumentException("Total question count must be greater than 0.");

        var course = await _courseRepo.GetByIdAsync(request.CourseId)
            ?? throw new KeyNotFoundException($"Course {request.CourseId} not found.");

        // Teacher chi duoc generate cho course cua minh; Admin bypass
        if (!isAdmin && course.TeacherId != callerId)
            throw new UnauthorizedAccessException("You are not assigned to this course.");

        var easy   = (await _questionRepo.GetRandomByLevelAsync(request.CourseId, 1, request.EasyCount)).ToList();
        var medium = (await _questionRepo.GetRandomByLevelAsync(request.CourseId, 2, request.MediumCount)).ToList();
        var hard   = (await _questionRepo.GetRandomByLevelAsync(request.CourseId, 3, request.HardCount)).ToList();

        if (easy.Count < request.EasyCount)
            throw new InvalidOperationException(
                $"Not enough level-1 (easy) questions. Requested {request.EasyCount}, available {easy.Count}.");
        if (medium.Count < request.MediumCount)
            throw new InvalidOperationException(
                $"Not enough level-2 (medium) questions. Requested {request.MediumCount}, available {medium.Count}.");
        if (hard.Count < request.HardCount)
            throw new InvalidOperationException(
                $"Not enough level-3 (hard) questions. Requested {request.HardCount}, available {hard.Count}.");

        var questions = easy.Concat(medium).Concat(hard)
            .OrderBy(_ => Guid.NewGuid())
            .Select(ToDto)
            .ToList();

        return new GenerateExamResponse(
            request.CourseId,
            course.CourseName,
            questions.Count,
            easy.Count, medium.Count, hard.Count,
            questions
        );
    }

    private static GenerateQuestionDto ToDto(QuestionBank q) => new(
        q.QuestionId, q.CourseId, q.QuestionText,
        q.Level, q.MaxScore, q.IsFollowUpAllowed
    );
}
