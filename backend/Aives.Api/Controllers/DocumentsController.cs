using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Teacher quản lý tài liệu .md và sinh ngân hàng câu hỏi bằng AI.
/// </summary>
[ApiController]
[Route("api/courses/{courseId:int}/documents")]
[Authorize(Roles = "TEACHER")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _docService;

    public DocumentsController(IDocumentService docService)
    {
        _docService = docService;
    }

    /// <summary>
    /// [TEACHER] Upload file .md (Content hoặc Learning Outcomes).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MarkdownDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadMarkdown(int courseId, [FromForm] UploadMarkdownRequest request)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try
        {
            var doc = await _docService.UploadMarkdownAsync(courseId, request, teacherId.Value);
            return CreatedAtAction(nameof(GetDocuments), new { courseId }, doc);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// [TEACHER] Lấy danh sách file .md đã upload của môn học.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MarkdownDocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocuments(int courseId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try { return Ok(await _docService.GetDocumentsByCourseAsync(courseId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// [TEACHER] Gọi AI sinh câu hỏi từ tài liệu .md đã upload.
    /// </summary>
    [HttpPost("generate-questions")]
    [ProducesResponseType(typeof(GenerateQuestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateQuestions(int courseId, [FromBody] GenerateQuestionsRequest request)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        request.CourseId = courseId; // Ensure consistency
        try { return Ok(await _docService.GenerateQuestionsAsync(request, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}