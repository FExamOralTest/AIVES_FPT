using System.Security.Claims;
using Aives.Application.Documents.DTOs;
using Aives.Application.Documents.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Teacher upload .md và sinh câu hỏi bằng AI.
/// Route: /api/courses/{courseId}/documents
/// </summary>
[ApiController]
[Route("api/courses/{courseId:int}/documents")]
[Authorize(Roles = "TEACHER")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _docService;

    public DocumentsController(IDocumentService docService) => _docService = docService;

    [HttpPost]
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

    [HttpGet]
    public async Task<IActionResult> GetDocuments(int courseId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try { return Ok(await _docService.GetDocumentsByCourseAsync(courseId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("generate-questions")]
    public async Task<IActionResult> GenerateQuestions(int courseId, [FromBody] GenerateQuestionsRequest request)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        request.CourseId = courseId;
        try { return Ok(await _docService.GenerateQuestionsAsync(request, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
