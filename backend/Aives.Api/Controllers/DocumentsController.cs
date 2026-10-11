using System.Security.Claims;
using Aives.Application.Documents.DTOs;
using Aives.Application.Documents.Interfaces;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Quản lý tài liệu Markdown (.md) của Course.
/// Admin và Teacher đều upload được.
/// Route: /api/documents
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize(Roles = "TEACHER,ADMIN")]
public class DocumentsController : ControllerBase
{
    private readonly IMarkdownDocumentService _docService;

    public DocumentsController(IMarkdownDocumentService docService) => _docService = docService;

    /// <summary>Lấy tất cả document của một course.</summary>
    [HttpGet("course/{courseId:int}")]
    public async Task<IActionResult> GetByCourse(int courseId)
        => Ok(await _docService.GetByCourseIdAsync(courseId));

    /// <summary>Lấy chi tiết một document theo ID.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try { return Ok(await _docService.GetByIdAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Upload file .md — Admin và Teacher đều dùng được.
    /// uploadedBy lấy từ JWT token, không cần truyền tay.
    /// Form fields: courseId, docType (CONTENT | LEARNING_OUTCOMES), file (.md, tối đa 5MB).
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] int    courseId,
        [FromForm] string docType,
        IFormFile         file)
    {
        var uploadedBy = GetCurrentUserId();
        if (uploadedBy is null) return Unauthorized(new { message = "Invalid token." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file was uploaded." });

        if (!Enum.TryParse<DocType>(docType.ToUpper(), out var parsedDocType))
            return BadRequest(new { message = $"Invalid docType '{docType}'. Valid: CONTENT, LEARNING_OUTCOMES." });

        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
            content = await reader.ReadToEndAsync();

        var request = new UploadDocumentRequest(
            courseId, uploadedBy.Value, file.FileName, parsedDocType, content);

        try
        {
            var created = await _docService.UploadAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.DocumentId }, created);
        }
        catch (ArgumentException ex)          { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)       { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex)  { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException)   { return Forbid(); }
    }

    /// <summary>
    /// Xóa document.
    /// Teacher chỉ xóa được document của mình; Admin xóa được tất cả.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var requestedBy = GetCurrentUserId();
        if (requestedBy is null) return Unauthorized(new { message = "Invalid token." });

        try { await _docService.DeleteAsync(id, requestedBy.Value); return NoContent(); }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
