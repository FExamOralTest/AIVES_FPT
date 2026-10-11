using Aives.Application.Courses.Interfaces;
using Aives.Application.Documents.DTOs;
using Aives.Application.Documents.Interfaces;
using Aives.Application.Users.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Documents.Services;

public class MarkdownDocumentService : IMarkdownDocumentService
{
    private const int MaxContentLengthBytes = 5 * 1024 * 1024;

    private readonly IMarkdownDocumentRepository _docRepo;
    private readonly ICourseRepository           _courseRepo;
    private readonly IUserRepository             _userRepo;

    public MarkdownDocumentService(
        IMarkdownDocumentRepository docRepo,
        ICourseRepository courseRepo,
        IUserRepository userRepo)
    {
        _docRepo    = docRepo;
        _courseRepo = courseRepo;
        _userRepo   = userRepo;
    }

    public async Task<IEnumerable<DocumentDto>> GetByCourseIdAsync(int courseId)
        => (await _docRepo.GetByCourseIdAsync(courseId)).Select(ToDto);

    public async Task<DocumentDto> GetByIdAsync(int documentId)
        => ToDto(await _docRepo.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found."));

    public async Task<DocumentDto> UploadAsync(UploadDocumentRequest req)
    {
        var ext = Path.GetExtension(req.FileName);
        if (!ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .md files are allowed.");

        var safeFileName = Path.GetFileName(req.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName != req.FileName)
            throw new ArgumentException("Invalid file name.");

        if (string.IsNullOrWhiteSpace(req.Content))
            throw new ArgumentException("File content cannot be empty.");

        if (System.Text.Encoding.UTF8.GetByteCount(req.Content) > MaxContentLengthBytes)
            throw new ArgumentException("File content exceeds the 5 MB limit.");

        var course = await _courseRepo.GetByIdAsync(req.CourseId)
            ?? throw new KeyNotFoundException($"Course {req.CourseId} not found.");

        var uploader = await _userRepo.GetByIdAsync(req.UploadedBy)
            ?? throw new KeyNotFoundException($"User {req.UploadedBy} not found.");

        if (uploader.Role != UserRole.TEACHER)
            throw new UnauthorizedAccessException("Only teachers can upload documents.");

        if (course.TeacherId != req.UploadedBy)
            throw new UnauthorizedAccessException("Teacher is not assigned to this course.");

        if (await _docRepo.ExistsByTypeAsync(req.CourseId, req.DocType))
            throw new InvalidOperationException(
                $"Course already has a '{req.DocType}' document. Delete the existing one first.");

        var doc = new MarkdownDocument
        {
            CourseId   = req.CourseId,
            UploadedBy = req.UploadedBy,
            FileName   = safeFileName,
            DocType    = req.DocType,
            Content    = req.Content,
            UploadedAt = DateTime.UtcNow
        };

        var created = await _docRepo.CreateAsync(doc);
        return ToDto(await _docRepo.GetByIdAsync(created.DocumentId) ?? created);
    }

    public async Task DeleteAsync(int documentId, int requestedByUserId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        var requester = await _userRepo.GetByIdAsync(requestedByUserId)
            ?? throw new KeyNotFoundException($"User {requestedByUserId} not found.");

        if (doc.UploadedBy != requestedByUserId && requester.Role != UserRole.ADMIN)
            throw new UnauthorizedAccessException(
                "Only the uploading teacher or an admin can delete this document.");

        await _docRepo.DeleteAsync(documentId);
    }

    private static DocumentDto ToDto(MarkdownDocument d) => new(
        d.DocumentId, d.CourseId,
        d.Course?.CourseName ?? string.Empty,
        d.UploadedBy, d.FileName,
        d.DocType.ToString(), d.UploadedAt
    );
}
