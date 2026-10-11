using Aives.Application.Questions.DTOs;
using Aives.Application.Questions.Interfaces;
using Aives.Application.Results.DTOs;
using Aives.Application.Results.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Lịch sử thi (History) + sinh đề ngẫu nhiên (Generator).
/// Route: /api/exams
/// </summary>
[ApiController]
[Route("api/exams")]
[Authorize(Roles = "TEACHER,ADMIN")]
public class ExamController : ControllerBase
{
    private readonly IExamHistoryService   _historyService;
    private readonly IExamGeneratorService _generatorService;

    public ExamController(IExamHistoryService historyService, IExamGeneratorService generatorService)
    {
        _historyService   = historyService;
        _generatorService = generatorService;
    }

    [HttpGet("course/{courseId:int}")]
    public async Task<IActionResult> GetExamsByCourse(int courseId)
        => Ok(await _historyService.GetExamsByCourseAsync(courseId));

    [HttpGet("{examId:int}/sessions")]
    public async Task<IActionResult> GetSessionsByExam(int examId)
    {
        try { return Ok(await _historyService.GetSessionsByExamAsync(examId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("sessions/{sessionId:int}")]
    public async Task<IActionResult> GetSessionDetail(int sessionId)
    {
        try { return Ok(await _historyService.GetSessionDetailAsync(sessionId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("results/{resultId:int}")]
    public async Task<IActionResult> GetResultDetail(int resultId)
    {
        try { return Ok(await _historyService.GetResultDetailAsync(resultId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPost("generate")]
    public async Task<IActionResult> Generate([FromBody] GenerateExamRequest request)
    {
        try { return Ok(await _generatorService.GenerateAsync(request)); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
