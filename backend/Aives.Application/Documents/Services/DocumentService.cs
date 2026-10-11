using Aives.Application.Courses.Interfaces;
using Aives.Application.Documents.DTOs;
using Aives.Application.Documents.Interfaces;
using Aives.Application.Questions.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Documents.Services;

public class DocumentService : IDocumentService
{
    private readonly IMarkdownDocumentRepository _docRepository;
    private readonly ICourseRepository           _courseRepository;
    private readonly IQuestionBankRepository     _questionRepository;
    private readonly IAIQuestionService          _aiQuestionService;

    public DocumentService(
        IMarkdownDocumentRepository docRepository,
        ICourseRepository courseRepository,
        IQuestionBankRepository questionRepository,
        IAIQuestionService aiQuestionService)
    {
        _docRepository      = docRepository;
        _courseRepository   = courseRepository;
        _questionRepository = questionRepository;
        _aiQuestionService  = aiQuestionService;
    }

    public async Task<MarkdownDocumentDto> UploadMarkdownAsync(
        int courseId, UploadMarkdownRequest request, int teacherId)
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
            CourseId   = courseId,
            UploadedBy = teacherId,
            FileName   = request.File.FileName,
            DocType    = request.DocType,
            Content    = content,
            UploadedAt = DateTime.UtcNow
        };

        var created = await _docRepository.CreateAsync(doc);
        await _docRepository.SaveChangesAsync();

        return Map(created);
    }

    public async Task<IEnumerable<MarkdownDocumentDto>> GetDocumentsByCourseAsync(
        int courseId, int teacherId)
    {
        var course = await _courseRepository.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException("Course not found.");
        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("Not your course.");

        var docs = await _docRepository.GetByCourseIdAsync(courseId);
        return docs.Select(Map);
    }

    public async Task<GenerateQuestionsResponse> GenerateQuestionsAsync(
        GenerateQuestionsRequest request, int teacherId)
    {
        var course = await _courseRepository.GetByIdAsync(request.CourseId)
            ?? throw new KeyNotFoundException("Course not found.");
        if (course.TeacherId != teacherId)
            throw new UnauthorizedAccessException("Not your course.");

        var docs = await _docRepository.GetByCourseIdAsync(request.CourseId);
        if (!docs.Any())
            throw new InvalidOperationException("No markdown documents uploaded for this course.");

        var aiQuestions = await _aiQuestionService.GenerateQuestionsAsync(
            docs.Select(d => d.Content).ToList(),
            request.QuestionCount,
            request.FocusTopics);

        var questions = aiQuestions.Select(q => new QuestionBank
        {
            CourseId          = request.CourseId,
            QuestionText      = q.QuestionText,
            MaxScore          = q.MaxScore,
            IsFollowUpAllowed = q.IsFollowUpAllowed,
            CreatedAt         = DateTime.UtcNow
        }).ToList();

        await _questionRepository.AddRangeAsync(questions);
        await _questionRepository.SaveChangesAsync();

        return new GenerateQuestionsResponse
        {
            GeneratedCount = questions.Count,
            Questions = questions.Select(q => new GeneratedQuestionDto
            {
                QuestionId        = q.QuestionId,
                QuestionText      = q.QuestionText,
                MaxScore          = q.MaxScore ?? 1.0f,
                IsFollowUpAllowed = q.IsFollowUpAllowed ?? true
            }).ToList()
        };
    }

    private static MarkdownDocumentDto Map(MarkdownDocument d) => new()
    {
        DocumentId  = d.DocumentId,
        CourseId    = d.CourseId,
        FileName    = d.FileName,
        DocType     = d.DocType.ToString(),
        Content     = d.Content,
        UploadedAt  = d.UploadedAt
    };
}
