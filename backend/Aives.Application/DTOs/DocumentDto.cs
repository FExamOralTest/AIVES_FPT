using Aives.Domain.Enums;

namespace Aives.Application.DTOs;

public record DocumentDto(
    int       DocumentId,
    int       CourseId,
    string    CourseName,
    int       UploadedBy,
    string    FileName,
    string    DocType,
    DateTime? UploadedAt
);

/// <summary>
/// Upload file .md moi. DocType: 0 = CONTENT, 1 = LEARNING_OUTCOMES.
/// </summary>
public record UploadDocumentRequest(
    int     CourseId,
    int     UploadedBy,
    string  FileName,
    DocType DocType,
    string  Content
);

/// <summary>
/// De file .md cu bang file moi. DocType: 0 = CONTENT, 1 = LEARNING_OUTCOMES.
/// </summary>
public record ReplaceDocumentRequest(
    int     DocumentId,
    int     RequestedByUserId,
    string  FileName,
    DocType DocType,
    string  Content
);