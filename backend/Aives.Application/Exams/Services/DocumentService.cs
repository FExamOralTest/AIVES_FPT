using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Aives.Application.Exams.Services;

public class DocumentService : IDocumentService
{
    private readonly IMarkdownDocumentRepository _docRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IQuestionBankRepository _questionRepository;
    private readonly IAIQuestionService _aiQuestionService;

    public DocumentService(
        IMarkdownDocumentRepository docRepository,
        ICourseRepository courseRepository,
        IQuestionBankRepository questionRepository,
        IAIQuestionService aiQuestionService)
    {
        _docRepository = docRepository;
        _courseRepository = courseRepository;
        _questionRepository = questionRepository;
        _aiQuestionService = aiQuestionService;
    }

    public async Task<MarkdownDocumentDto> UploadMarkdownAsync(int courseId, UploadMarkdownRequest request, int teacherId)
    {
        var course = await _courseRepository.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");

        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("Not your course.");

        if (!request.File.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .md files are allowed.");

        using var reader = new StreamReader(request.File.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        var doc = new MarkdownDocument
        {
            CourseId = courseId,
            UploadedBy = teacherId,
            FileName = request.File.FileName,
            DocType = request.DocType,
            Content = content,
            UploadedAt = DateTime.UtcNow
        };

        var created = await _docRepository.CreateAsync(doc);
        await _docRepository.SaveChangesAsync();

        return new MarkdownDocumentDto
        {
            DocumentId = created.DocumentId,
            CourseId = created.CourseId,
            FileName = created.FileName,
            DocType = created.DocType.ToString(),
            Content = created.Content,
            UploadedAt = created.UploadedAt
        };
    }

    public async Task<IEnumerable<MarkdownDocumentDto>> GetDocumentsByCourseAsync(int courseId, int teacherId)
    {
        var course = await _courseRepository.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");
        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("Not your course.");

        var docs = await _docRepository.GetByCourseIdAsync(courseId);
        return docs.Select(d => new MarkdownDocumentDto
        {
            DocumentId = d.DocumentId,
            CourseId = d.CourseId,
            FileName = d.FileName,
            DocType = d.DocType.ToString(),
            Content = d.Content,
            UploadedAt = d.UploadedAt
        });
    }

    public async Task<GenerateQuestionsResponse> GenerateQuestionsAsync(
        GenerateQuestionsRequest request, int teacherId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.QuestionCount is < 5 or > 200 || !float.IsFinite(request.MaxScore)
            || request.MaxScore < 0.01f || request.MaxScore > 100)
            throw new InvalidOperationException("Question count must be 5-200 and maximum score must be 0.01-100.");

        var course = await _courseRepository.GetByIdAsync(request.CourseId)
            ?? throw new KeyNotFoundException("Course not found.");
        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("Not your course.");

        var docs = (await _docRepository.GetByCourseIdAsync(request.CourseId)).ToList();
        if (docs.Count != 2 || docs.Count(d => d.DocType == DocType.CONTENT) != 1
            || docs.Count(d => d.DocType == DocType.LEARNING_OUTCOMES) != 1)
            throw new InvalidOperationException("Exactly one CONTENT and one LEARNING_OUTCOMES document are required.");
        if (docs.Any(d => string.IsNullOrWhiteSpace(d.Content)))
            throw new InvalidOperationException("CONTENT and LEARNING_OUTCOMES document contents must not be empty.");

        cancellationToken.ThrowIfCancellationRequested();
        var aiRequest = new AIQuestionRequest
        {
            Documents = docs.Select(d => new AIQuestionDocumentDto
            {
                DocumentId = d.DocumentId,
                DocumentType = d.DocType.ToString(),
                Content = d.Content
            }).ToList(),
            QuestionCount = request.QuestionCount,
            FocusTopics = request.FocusTopics?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>(),
            MaxScore = request.MaxScore
        };
        var aiQuestions = await _aiQuestionService.GenerateQuestionsAsync(aiRequest, cancellationToken);
        AIQuestionValidator.Validate(aiQuestions, request.QuestionCount, request.MaxScore);
        cancellationToken.ThrowIfCancellationRequested();

        var questions = aiQuestions.Select(q => new QuestionBank
        {
            CourseId = request.CourseId,
            DocumentId = docs.Single(d => d.DocType == DocType.CONTENT).DocumentId,
            QuestionText = q.QuestionText.Trim(),
            Topic = q.Topic.Trim(),
            SampleAnswer = q.SampleAnswer.Trim(),
            MaxScore = q.MaxScore,
            Level = q.Level,
            IsFollowUpAllowed = q.IsFollowUpAllowed,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        await _questionRepository.SaveGeneratedAsync(questions, cancellationToken);

        return new GenerateQuestionsResponse
        {
            GeneratedCount = questions.Count,
            Questions = questions.Select(q => QuestionBankService.ToDto(q, false)).ToList()
        };
    }
}
