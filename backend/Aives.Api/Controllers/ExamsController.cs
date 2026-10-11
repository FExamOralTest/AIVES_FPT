using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Quản lý Đề thi (Exam). Chỉ dành cho Teacher.
/// </summary>
[ApiController]
[Route("api/exams")]
[Authorize(Roles = "TEACHER")]
public class ExamsController : ControllerBase
{
    private readonly IExamService _examService;

    public ExamsController(IExamService examService)
    {
        _examService = examService;
    }

    /// <summary>
    /// [TEACHER] Tạo đề thi mới.
    /// Hệ thống tự động sinh ExamCode bảo mật duy nhất.
    /// Có thể kèm danh sách câu hỏi (thủ công hoặc từ gợi ý AI).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateExam([FromBody] CreateExamRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var exam = await _examService.CreateExamAsync(request, teacherId.Value);
            return CreatedAtAction(nameof(GetExamById), new { examId = exam.ExamId }, exam);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("unique Exam Code"))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("do not belong"))
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// [TEACHER] Lấy chi tiết đề thi kèm danh sách câu hỏi.
    /// </summary>
    [HttpGet("{examId:int}")]
    [ProducesResponseType(typeof(ExamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExamById(int examId)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        try
        {
            var exam = await _examService.GetExamByIdAsync(examId, teacherId.Value);
            return Ok(exam);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// [TEACHER] Lấy tất cả đề thi của Teacher hiện tại.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IEnumerable<ExamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyExams()
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        var exams = await _examService.GetExamsByTeacherAsync(teacherId.Value);
        return Ok(exams);
    }

    /// <summary>
    /// [TEACHER] Gọi AI để gợi ý danh sách câu hỏi phù hợp cho đề thi.
    /// Teacher xem xét và duyệt câu hỏi trước khi tạo đề chính thức.
    /// </summary>
    [HttpPost("ai-suggest-questions")]
    [ProducesResponseType(typeof(AiSuggestedQuestionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAiSuggestedQuestions(
        [FromBody] AiSuggestQuestionsRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var teacherId = GetCurrentUserId();
        if (teacherId is null)
            return Unauthorized(new { message = "Invalid token." });

        var result = await _examService.GetAiSuggestedQuestionsAsync(request, teacherId.Value);
        return Ok(result);
    }

    // ─── Helper ───────────────────────────────────────────────────────────────

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }
}
