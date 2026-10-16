using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;
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
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
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
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
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
    [HttpGet("{documentId:int}")]
    [ProducesResponseType(typeof(MarkdownDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDocumentById(int courseId, int documentId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try { return Ok(await _docService.GetDocumentByIdAsync(courseId, documentId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpPut("{documentId:int}")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(MarkdownDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDocument(int courseId, int documentId, [FromForm] UpdateMarkdownRequest request)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try
        {
            var updated = await _docService.UpdateDocumentAsync(courseId, documentId, request, teacherId.Value);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpDelete("{documentId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(int courseId, int documentId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        try
        {
            await _docService.DeleteDocumentAsync(courseId, documentId, teacherId.Value);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpPost("generate-questions")]
    [ProducesResponseType(typeof(GenerateQuestionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> GenerateQuestions(int courseId, [FromBody] GenerateQuestionsRequest request, CancellationToken cancellationToken)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized();

        request.CourseId = courseId;
        try { return Ok(await _docService.GenerateQuestionsAsync(request, teacherId.Value, cancellationToken)); }
        catch (AIQuestionException ex)
        {
            var status = ex.Failure == AIQuestionFailure.Timeout
                ? StatusCodes.Status504GatewayTimeout : StatusCodes.Status502BadGateway;
            return StatusCode(status, new { message = ex.Message, code = ex.Failure.ToString() });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
