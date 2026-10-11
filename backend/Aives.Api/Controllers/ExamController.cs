using Aives.Application.DTOs;
using Aives.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Teacher và Admin: xem lịch sử thi + tạo đề thi ngẫu nhiên.
/// Base route: /api/exams
/// </summary>
[ApiController]
[Route("api/exams")]
[Authorize(Roles = "TEACHER,ADMIN")]
public class ExamController : ControllerBase
{
    private readonly IExamHistoryService   _historyService;
    private readonly IExamGeneratorService _generatorService;

    public ExamController(
        IExamHistoryService   historyService,
        IExamGeneratorService generatorService)
    {
        _historyService   = historyService;
        _generatorService = generatorService;
    }

    // ── Lịch sử thi ─────────────────────────────────────────────────────────

    /// <summary>
    /// Danh sách tất cả bài thi của một course.
    /// GET /api/exams/course/{courseId}
    /// </summary>
    [HttpGet("course/{courseId:int}")]
    [ProducesResponseType(typeof(IEnumerable<ExamSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExamsByCourse(int courseId)
    {
        var exams = await _historyService.GetExamsByCourseAsync(courseId);
        return Ok(exams);
    }

    /// <summary>
    /// Tất cả phiên thi và danh sách student đã thi của một bài thi.
    /// GET /api/exams/{examId}/sessions
    /// </summary>
    [HttpGet("{examId:int}/sessions")]
    [ProducesResponseType(typeof(IEnumerable<ExamSessionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionsByExam(int examId)
    {
        try
        {
            var sessions = await _historyService.GetSessionsByExamAsync(examId);
            return Ok(sessions);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Chi tiết một phiên thi — danh sách student và điểm.
    /// GET /api/exams/sessions/{sessionId}
    /// </summary>
    [HttpGet("sessions/{sessionId:int}")]
    [ProducesResponseType(typeof(ExamSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSessionDetail(int sessionId)
    {
        try
        {
            var session = await _historyService.GetSessionDetailAsync(sessionId);
            return Ok(session);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Kết quả chi tiết của một student — từng câu hỏi và câu trả lời.
    /// GET /api/exams/results/{resultId}
    /// </summary>
    [HttpGet("results/{resultId:int}")]
    [ProducesResponseType(typeof(StudentResultDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultDetail(int resultId)
    {
        try
        {
            var result = await _historyService.GetResultDetailAsync(resultId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // ── Tạo đề thi ngẫu nhiên ────────────────────────────────────────────────

    /// <summary>
    /// Tạo đề thi ngẫu nhiên từ QuestionBank theo courseId và số câu mỗi level.
    /// POST /api/exams/generate
    /// </summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateExamResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate([FromBody] GenerateExamRequest request)
    {
        try
        {
            var result = await _generatorService.GenerateAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
