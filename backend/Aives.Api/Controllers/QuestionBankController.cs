using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/questions")]
[Authorize(Roles = "TEACHER")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
public class QuestionBankController(IQuestionBankService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(QuestionBankPageDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetQuestions(int courseId, [FromQuery] QuestionBankQuery query)
        => ExecuteAsync(async teacherId => Ok(await service.GetQuestionsAsync(courseId, query, teacherId)));

    [HttpGet("{questionId:int}")]
    [ProducesResponseType(typeof(QuestionBankDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetQuestion(int courseId, int questionId)
        => ExecuteAsync(async teacherId => Ok(await service.GetQuestionAsync(courseId, questionId, teacherId)));

    [HttpPost]
    [ProducesResponseType(typeof(QuestionBankDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CreateQuestion(int courseId, [FromBody] WriteQuestionRequest request)
        => ExecuteAsync(async teacherId =>
        {
            var question = await service.CreateQuestionAsync(courseId, request, teacherId);
            return CreatedAtAction(nameof(GetQuestion), new { courseId, questionId = question.QuestionId }, question);
        });

    [HttpPut("{questionId:int}")]
    [ProducesResponseType(typeof(QuestionBankDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateQuestion(int courseId, int questionId, [FromBody] WriteQuestionRequest request)
        => ExecuteAsync(async teacherId => Ok(await service.UpdateQuestionAsync(courseId, questionId, request, teacherId)));

    [HttpDelete("{questionId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> DeleteQuestion(int courseId, int questionId)
        => ExecuteAsync(async teacherId =>
        {
            await service.DeleteQuestionAsync(courseId, questionId, teacherId);
            return NoContent();
        });

    private async Task<IActionResult> ExecuteAsync(Func<int, Task<IActionResult>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var teacherId))
            return Unauthorized(new { message = "Invalid token." });
        try { return await action(teacherId); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (QuestionBankConflictException ex) { return Conflict(new { message = ex.Message }); }
    }
}
