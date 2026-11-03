using System.Security.Claims;
using Aives.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/teacher/courses")]
[Authorize(Roles = "STAFF,ADMIN")]
public class TeacherCourseController : ControllerBase
{
    private readonly ICourseService _courseService;

    public TeacherCourseController(ICourseService courseService) => _courseService = courseService;

    /// <summary>Lay tat ca course duoc phan cong cho teacher dang dang nhap.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMyCourses()
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        return Ok(await _courseService.GetByTeacherIdAsync(teacherId.Value));
    }

    /// <summary>Chi tiet mot course (403 neu khong phai cua minh).</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var teacherId = GetCurrentUserId();
        if (teacherId is null) return Unauthorized(new { message = "Invalid token." });

        try
        {
            var course = await _courseService.GetByIdAsync(id);
            // Admin bypass ownership check
            var isAdmin = User.IsInRole("ADMIN");
            if (!isAdmin && course.TeacherId != teacherId.Value) return Forbid();
            return Ok(course);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    private int? GetCurrentUserId()
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

