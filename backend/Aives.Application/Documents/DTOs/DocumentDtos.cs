using Aives.Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Documents.DTOs;

// ── Markdown document (Teacher admin upload) ────────────────────────────────

public record DocumentDto(
    int       DocumentId,
    int       CourseId,
    string    CourseName,
    int       UploadedBy,
    string    FileName,
    string    DocType,
    DateTime? UploadedAt
);

public record UploadDocumentRequest(
    int     CourseId,
    int     UploadedBy,
    string  FileName,
    DocType DocType,
    string  Content
);

// ── IFormFile-based upload (DocumentsController) ────────────────────────────

public class UploadMarkdownRequest
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    public DocType DocType { get; set; }
}

public class MarkdownDocumentDto
{
    public int      DocumentId  { get; set; }
    public int      CourseId    { get; set; }
    public string   FileName    { get; set; } = null!;
    public string   DocType     { get; set; } = null!;
    public string   Content     { get; set; } = null!;
    public DateTime? UploadedAt { get; set; }
}

// ── AI question generation ────────────────────────────────────────────────

public class GenerateQuestionsRequest
{
    [Required]
    public int CourseId { get; set; }

    [Range(5, 200)]
    public int QuestionCount { get; set; } = 20;

    public string? FocusTopics { get; set; }
}

public class GenerateQuestionsResponse
{
    public int GeneratedCount { get; set; }
    public List<GeneratedQuestionDto> Questions { get; set; } = new();
}

public class GeneratedQuestionDto
{
    public int    QuestionId        { get; set; }
    public string QuestionText      { get; set; } = null!;
    public float  MaxScore          { get; set; }
    public bool   IsFollowUpAllowed { get; set; }
}
