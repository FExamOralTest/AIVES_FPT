using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/exams")]
[Authorize(Roles = "TEACHER")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public class ExamsController(IExamService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ExamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<IActionResult> CreateExam([FromBody] CreateExamRequest request)
        => ExecuteAsync(async teacherId =>
        {
            var exam = await service.CreateExamAsync(request, teacherId);
            return CreatedAtAction(nameof(GetExamById), new { examId = exam.ExamId }, exam);
        });

    [HttpGet("{examId:int}")]
    [ProducesResponseType(typeof(ExamDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetExamById(int examId)
        => ExecuteAsync(async teacherId => Ok(await service.GetExamByIdAsync(examId, teacherId)));

    [HttpGet("my")]
    [ProducesResponseType(typeof(IEnumerable<ExamDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetMyExams()
        => ExecuteAsync(async teacherId => Ok(await service.GetExamsByTeacherAsync(teacherId)));

    [HttpPost("suggest-questions")]
    [HttpPost("ai-suggest-questions")]
    [ProducesResponseType(typeof(SuggestedQuestionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetSuggestedQuestions([FromBody] SuggestQuestionsRequest request)
        => ExecuteAsync(async teacherId => Ok(await service.GetSuggestedQuestionsAsync(request, teacherId)));

    [HttpPut("{examId:int}/questions")]
    [ProducesResponseType(typeof(ExamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public Task<IActionResult> UpdateExamQuestions(int examId, [FromBody] UpdateExamQuestionsRequest request)
        => ExecuteAsync(async teacherId => Ok(await service.UpdateExamQuestionsAsync(examId, request.Questions, teacherId)));

    private async Task<IActionResult> ExecuteAsync(Func<int, Task<IActionResult>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var teacherId))
            return Unauthorized(new { message = "Invalid token." });
        try { return await action(teacherId); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unique Exam Code"))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = ex.Message });
        }
    }
}
