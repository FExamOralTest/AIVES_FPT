using Aives.Application.DTOs;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Services;

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
            ?? throw new KeyNotFoundException("Document not found."));

    // ------------------------------------------------------------------ //
    //  Upload (POST) - tao moi, block neu da co cung DocType              //
    // ------------------------------------------------------------------ //

    public async Task<DocumentDto> UploadAsync(UploadDocumentRequest req)
    {
        ValidateFileContent(req.FileName, req.Content);

        var course = await _courseRepo.GetByIdAsync(req.CourseId)
            ?? throw new KeyNotFoundException("Course not found.");

        var uploader = await _userRepo.GetByIdAsync(req.UploadedBy)
            ?? throw new KeyNotFoundException("User not found.");

        if (uploader.Role != UserRole.TEACHER && uploader.Role != UserRole.ADMIN)
            throw new UnauthorizedAccessException("Only teachers or admins can upload documents.");

        if (uploader.Role == UserRole.TEACHER && course.TeacherId != req.UploadedBy)
            throw new UnauthorizedAccessException("Teacher is not assigned to this course.");

        if (await _docRepo.ExistsByTypeAsync(req.CourseId, req.DocType))
            throw new InvalidOperationException(
                "Course already has a document of this type. Use PUT to replace it.");

        var doc = new MarkdownDocument
        {
            CourseId   = req.CourseId,
            UploadedBy = req.UploadedBy,
            FileName   = Path.GetFileName(req.FileName),
            DocType    = req.DocType,
            Content    = req.Content,
            UploadedAt = DateTime.UtcNow
        };

        var created = await _docRepo.CreateAsync(doc);
        return ToDto(await _docRepo.GetByIdAsync(created.DocumentId) ?? created);
    }

    // ------------------------------------------------------------------ //
    //  Replace (PUT) - upload file moi de len                             //
    //  - Neu document co -> update                                        //
    //  - Neu chua co     -> tao moi                                       //
    //  Khong bao gio bao loi duplicate                                    //
    // ------------------------------------------------------------------ //

    public async Task<DocumentDto> ReplaceAsync(ReplaceDocumentRequest req)
    {
        var requester = await _userRepo.GetByIdAsync(req.RequestedByUserId)
            ?? throw new KeyNotFoundException("User not found.");

        var isAdmin = requester.Role == UserRole.ADMIN;

        ValidateFileContent(req.FileName, req.Content);

        // Tim document hien tai theo ID
        var existing = await _docRepo.GetByIdAsync(req.DocumentId);

        if (existing is null)
        {
            // Document khong ton tai voi ID nay -> bao loi ro rang
            throw new KeyNotFoundException($"Document {req.DocumentId} not found.");
        }

        // Kiem tra quyen: Teacher phai thuoc course chua document nay
        if (!isAdmin)
        {
            var course = await _courseRepo.GetByIdAsync(existing.CourseId)
                ?? throw new KeyNotFoundException("Course not found.");
            if (course.TeacherId != req.RequestedByUserId)
                throw new UnauthorizedAccessException("You are not assigned to this course.");
        }

        // Update truc tiep - khong check duplicate
        existing.FileName   = Path.GetFileName(req.FileName);
        existing.DocType    = req.DocType;
        existing.Content    = req.Content;
        existing.UploadedAt = DateTime.UtcNow;

        var updated = await _docRepo.UpdateAsync(existing);
        return ToDto(await _docRepo.GetByIdAsync(updated.DocumentId) ?? updated);
    }

    // ------------------------------------------------------------------ //
    //  Delete                                                              //
    // ------------------------------------------------------------------ //

    public async Task DeleteAsync(int documentId, int requestedByUserId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException("Document not found.");

        var requester = await _userRepo.GetByIdAsync(requestedByUserId)
            ?? throw new KeyNotFoundException("User not found.");

        var course = await _courseRepo.GetByIdAsync(doc.CourseId);
        var isAssignedTeacher = course != null && course.TeacherId == requestedByUserId;

        if (doc.UploadedBy != requestedByUserId && !isAssignedTeacher && requester.Role != UserRole.ADMIN)
            throw new UnauthorizedAccessException(
                "Only the assigned teacher, uploading teacher, or an admin can delete this document.");

        await _docRepo.DeleteAsync(documentId);
    }

    // ------------------------------------------------------------------ //
    //  Helpers                                                             //
    // ------------------------------------------------------------------ //

    private static void ValidateFileContent(string fileName, string content)
    {
        if (!Path.GetExtension(fileName).Equals(".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .md files are allowed.");

        var safe = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safe) || safe != fileName)
            throw new ArgumentException("Invalid file name.");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("File content cannot be empty.");

        if (System.Text.Encoding.UTF8.GetByteCount(content) > MaxContentLengthBytes)
            throw new ArgumentException("File content exceeds the 5 MB limit.");
    }

    private static DocumentDto ToDto(MarkdownDocument d) => new(
        d.DocumentId, d.CourseId,
        d.Course?.CourseName ?? string.Empty,
        d.UploadedBy, d.FileName,
        d.DocType.ToString(), d.UploadedAt
    );
}