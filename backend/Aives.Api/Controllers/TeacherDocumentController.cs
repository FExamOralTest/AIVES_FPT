using System.Security.Claims;
using Aives.Application.DTOs;
using Aives.Application.Exams.DTOs;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Teacher/Admin quan ly file .md lien ket voi Course.
/// Route: /api/documents
/// DocType: 0 = CONTENT, 1 = LEARNING_OUTCOMES
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize(Roles = "STAFF,ADMIN")]
public class TeacherDocumentController : ControllerBase
{
    private readonly IMarkdownDocumentService _docService;
    private readonly IExamGeneratorService    _generatorService;

    public TeacherDocumentController(
        IMarkdownDocumentService docService,
        IExamGeneratorService    generatorService)
    {
        _docService       = docService;
        _generatorService = generatorService;
    }

    // GET /api/documents/course/{courseId}
    [HttpGet("course/{courseId:int}")]
    public async Task<IActionResult> GetByCourse(int courseId)
        => Ok(await _docService.GetByCourseIdAsync(courseId));

    // GET /api/documents/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try { return Ok(await _docService.GetByIdAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>
    /// Upload file .md moi.
    /// Form fields: courseId, docType (0=CONTENT, 1=LEARNING_OUTCOMES), file (.md toi da 5MB).
    /// uploadedBy lay tu JWT token.
    /// </summary>
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromForm] int    courseId,
        [FromForm] int    docType,
        IFormFile         file)
    {
        var uploadedBy = GetCurrentUserId();
        if (uploadedBy is null) return Unauthorized(new { message = "Invalid token." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file was uploaded." });

        if (!Enum.IsDefined(typeof(DocType), docType))
            return BadRequest(new { message = $"Invalid docType '{docType}'. Valid: 0 (CONTENT), 1 (LEARNING_OUTCOMES)." });

        var parsedDocType = (DocType)docType;

        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
            content = await reader.ReadToEndAsync();

        var request = new UploadDocumentRequest(
            CourseId:   courseId,
            UploadedBy: uploadedBy.Value,
            FileName:   file.FileName,
            DocType:    parsedDocType,
            Content:    content);

        try
        {
            var created = await _docService.UploadAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.DocumentId }, created);
        }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException)  { return Forbid(); }
    }

    /// <summary>
    /// De file .md moi len file cu (replace).
    /// Form fields: docType (0=CONTENT, 1=LEARNING_OUTCOMES), file (.md toi da 5MB).
    /// Teacher chi duoc sua document thuoc course cua minh; Admin bypass.
    /// PUT /api/documents/{id}
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Replace(
        int       id,
        [FromForm] int docType,
        IFormFile      file)
    {
        var requestedBy = GetCurrentUserId();
        if (requestedBy is null) return Unauthorized(new { message = "Invalid token." });

        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file was uploaded." });

        if (!Enum.IsDefined(typeof(DocType), docType))
            return BadRequest(new { message = $"Invalid docType '{docType}'. Valid: 0 (CONTENT), 1 (LEARNING_OUTCOMES)." });

        var parsedDocType = (DocType)docType;

        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
            content = await reader.ReadToEndAsync();

        var request = new ReplaceDocumentRequest(
            DocumentId:        id,
            RequestedByUserId: requestedBy.Value,
            FileName:          file.FileName,
            DocType:           parsedDocType,
            Content:           content);

        try { return Ok(await _docService.ReplaceAsync(request)); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException)  { return Forbid(); }
    }

    // DELETE /api/documents/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var requestedBy = GetCurrentUserId();
        if (requestedBy is null) return Unauthorized(new { message = "Invalid token." });

        try { await _docService.DeleteAsync(id, requestedBy.Value); return NoContent(); }
        catch (KeyNotFoundException ex)     { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Sinh de thi ngau nhien tu QuestionBank theo level.
    /// Teacher chi duoc generate cho course cua minh; Admin bypass.
    /// POST /api/documents/generate-exam
    /// </summary>
    [HttpPost("generate-exam")]
    public async Task<IActionResult> GenerateExam([FromBody] GenerateExamRequest request)
    {
        var callerId = GetCurrentUserId();
        if (callerId is null) return Unauthorized(new { message = "Invalid token." });

        var isAdmin = User.IsInRole("ADMIN");

        try { return Ok(await _generatorService.GenerateAsync(request, callerId.Value, isAdmin)); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException)  { return Forbid(); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}