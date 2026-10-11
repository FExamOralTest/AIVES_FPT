using System.Security.Claims;
using Aives.Application.Courses.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/teacher/courses")]
[Authorize(Roles = "TEACHER,ADMIN")]
public class TeacherCourseController : ControllerBase
{
    private readonly ICourseService _courseService;

    public TeacherCourseController(ICourseService courseService) => _courseService = courseService;

    /// <summary>Lấy tất cả course được phân công cho teacher đang đăng nhập.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyCourses()
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        return Ok(await _courseService.GetByTeacherIdAsync(teacherId.Value));
    }

    /// <summary>Chi tiết một course (403 nếu không phải của mình).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try
        {
            var course = await _courseService.GetByIdAsync(id);
            if (course.TeacherId != teacherId.Value) return Forbid();
            return Ok(course);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
