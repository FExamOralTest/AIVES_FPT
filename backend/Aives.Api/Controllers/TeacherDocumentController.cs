using Aives.Application.Documents.DTOs;
using Aives.Application.Documents.Interfaces;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "TEACHER,ADMIN")]
public class TeacherDocumentController : ControllerBase
{
    private readonly IMarkdownDocumentService _docService;

    public TeacherDocumentController(IMarkdownDocumentService docService) => _docService = docService;

    [HttpGet("course/{courseId:int}")]
    public async Task<IActionResult> GetByCourse(int courseId)
        => Ok(await _docService.GetByCourseIdAsync(courseId));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try { return Ok(await _docService.GetByIdAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] int courseId,
        [FromForm] int uploadedBy,
        [FromForm] string docType,
        IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file was uploaded." });

        if (!Enum.TryParse<DocType>(docType.ToUpper(), out var parsedDocType))
            return BadRequest(new { message = $"Invalid docType '{docType}'. Valid: CONTENT, LEARNING_OUTCOMES." });

        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
            content = await reader.ReadToEndAsync();

        var request = new UploadDocumentRequest(courseId, uploadedBy, file.FileName, parsedDocType, content);

        try
        {
            var created = await _docService.UploadAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.DocumentId }, created);
        }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int requestedBy)
    {
        try { await _docService.DeleteAsync(id, requestedBy); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
