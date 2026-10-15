using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/student/enrollments")]
[Authorize(Roles = "STUDENT")]
public class StudentEnrollmentsController : ControllerBase
{
    private readonly IExamSessionService _sessionService;

    public StudentEnrollmentsController(IExamSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<StudentSessionEnrollmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyEnrollments(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var studentId) || studentId <= 0)
            return Unauthorized(new { message = "Invalid token." });

        var enrollments = await _sessionService.GetStudentEnrollmentsAsync(studentId, cancellationToken);
        return Ok(enrollments);
    }
}
