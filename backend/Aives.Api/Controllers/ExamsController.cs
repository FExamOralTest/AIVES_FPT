using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Teacher quản lý Đề thi (CRUD Exam).
/// Route: /api/teacher/exams
/// </summary>
[ApiController]
[Route("api/teacher/exams")]
[Authorize(Roles = "TEACHER")]
public class ExamsController : ControllerBase
{
    private readonly IExamService _examService;

    public ExamsController(IExamService examService) => _examService = examService;

    [HttpPost]
    public async Task<IActionResult> CreateExam([FromBody] CreateExamRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try
        {
            var exam = await _examService.CreateExamAsync(request, teacherId.Value);
            return CreatedAtAction(nameof(GetExamById), new { examId = exam.ExamId }, exam);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("{examId:int}")]
    public async Task<IActionResult> GetExamById(int examId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try { return Ok(await _examService.GetExamByIdAsync(examId, teacherId.Value)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyExams()
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });
        return Ok(await _examService.GetExamsByTeacherAsync(teacherId.Value));
    }

    [HttpPost("ai-suggest-questions")]
    public async Task<IActionResult> GetAiSuggestedQuestions([FromBody] AiSuggestQuestionsRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });
        return Ok(await _examService.GetAiSuggestedQuestionsAsync(request, teacherId.Value));
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
