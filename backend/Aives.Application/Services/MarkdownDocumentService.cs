using Aives.Application.DTOs;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Services;

public class MarkdownDocumentService : IMarkdownDocumentService
{
    private const int MaxContentLengthBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IMarkdownDocumentRepository _docRepo;
    private readonly ICourseRepository           _courseRepo;
    private readonly IUserRepository             _userRepo;

    public MarkdownDocumentService(
        IMarkdownDocumentRepository docRepo,
        ICourseRepository           courseRepo,
        IUserRepository             userRepo)
    {
        _docRepo    = docRepo;
        _courseRepo = courseRepo;
        _userRepo   = userRepo;
    }

    // ------------------------------------------------------------------ //
    //  Queries                                                             //
    // ------------------------------------------------------------------ //

    public async Task<IEnumerable<DocumentDto>> GetByCourseIdAsync(int courseId)
    {
        var docs = await _docRepo.GetByCourseIdAsync(courseId);
        return docs.Select(ToDto);
    }

    public async Task<DocumentDto> GetByIdAsync(int documentId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");
        return ToDto(doc);
    }

    // ------------------------------------------------------------------ //
    //  Upload â€” validate Ä‘áº§y Ä‘á»§ trÆ°á»›c khi lÆ°u                             //
    // ------------------------------------------------------------------ //

    public async Task<DocumentDto> UploadAsync(UploadDocumentRequest req)
    {
        // 1. Validate extension â€” báº¯t buá»™c pháº£i lÃ  .md
        var ext = Path.GetExtension(req.FileName);
        if (!ext.Equals(".md", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only .md files are allowed.");

        // 2. Validate tÃªn file khÃ´ng chá»©a kÃ½ tá»± nguy hiá»ƒm (path traversal)
        var safeFileName = Path.GetFileName(req.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName != req.FileName)
            throw new ArgumentException("Invalid file name.");

        // 3. Validate ná»™i dung khÃ´ng rá»—ng
        if (string.IsNullOrWhiteSpace(req.Content))
            throw new ArgumentException("File content cannot be empty.");

        // 4. Validate kÃ­ch thÆ°á»›c ná»™i dung (giá»›i háº¡n 5 MB)
        if (System.Text.Encoding.UTF8.GetByteCount(req.Content) > MaxContentLengthBytes)
            throw new ArgumentException("File content exceeds the 5 MB limit.");

        // 5. Kiá»ƒm tra Course tá»“n táº¡i
        var course = await _courseRepo.GetByIdAsync(req.CourseId)
            ?? throw new KeyNotFoundException($"Course {req.CourseId} not found.");

        // 6. Kiá»ƒm tra Teacher tá»“n táº¡i vÃ  Ä‘Æ°á»£c phÃ¢n cÃ´ng phá»¥ trÃ¡ch Course nÃ y
        var uploader = await _userRepo.GetByIdAsync(req.UploadedBy)
            ?? throw new KeyNotFoundException($"User {req.UploadedBy} not found.");

        if (uploader.Role != UserRole.TEACHER)
            throw new UnauthorizedAccessException("Only teachers can upload documents.");

        if (course.TeacherId != req.UploadedBy)
            throw new UnauthorizedAccessException(
                "Teacher is not assigned to this course.");

        // 7. Má»—i Course chá»‰ Ä‘Æ°á»£c cÃ³ 1 file má»—i DocType
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
        // Reload Ä‘á»ƒ láº¥y navigation Course
        return ToDto(await _docRepo.GetByIdAsync(created.DocumentId) ?? created);
    }

    // ------------------------------------------------------------------ //
    //  Delete â€” chá»‰ Teacher sá»Ÿ há»¯u hoáº·c Admin má»›i Ä‘Æ°á»£c xÃ³a               //
    // ------------------------------------------------------------------ //

    public async Task DeleteAsync(int documentId, int requestedByUserId)
    {
        var doc = await _docRepo.GetByIdAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");

        var requester = await _userRepo.GetByIdAsync(requestedByUserId)
            ?? throw new KeyNotFoundException($"User {requestedByUserId} not found.");

        var isOwner = doc.UploadedBy == requestedByUserId;
        var isAdmin = requester.Role == UserRole.ADMIN;

        if (!isOwner && !isAdmin)
            throw new UnauthorizedAccessException(
                "Only the uploading teacher or an admin can delete this document.");

        await _docRepo.DeleteAsync(documentId);
    }

    // ------------------------------------------------------------------ //
    //  Mapping                                                             //
    // ------------------------------------------------------------------ //

    private static DocumentDto ToDto(MarkdownDocument d) => new(
        d.DocumentId,
        d.CourseId,
        d.Course?.CourseName ?? string.Empty,
        d.UploadedBy,
        d.FileName,
        d.DocType.ToString(),
        d.UploadedAt
    );
}

