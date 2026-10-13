using Aives.Application.DTOs;
using Aives.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Admin tạo, quản lý Course và phân công Teacher.
/// Base route: /api/admin/courses
/// </summary>
[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = "ADMIN")]
public class AdminCourseController : ControllerBase
{
    private readonly ICourseService _courseService;

    public AdminCourseController(ICourseService courseService) => _courseService = courseService;

    // GET /api/admin/courses
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var courses = await _courseService.GetAllAsync();
        return Ok(courses);
    }

    // GET /api/admin/courses/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var course = await _courseService.GetByIdAsync(id);
            return Ok(course);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // GET /api/admin/courses/teacher/{teacherId}
    [HttpGet("teacher/{teacherId:int}")]
    public async Task<IActionResult> GetByTeacher(int teacherId)
    {
        var courses = await _courseService.GetByTeacherIdAsync(teacherId);
        return Ok(courses);
    }

    // POST /api/admin/courses
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request)
    {
        try
        {
            var created = await _courseService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.CourseId }, created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    // PUT /api/admin/courses/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCourseRequest request)
    {
        try
        {
            var updated = await _courseService.UpdateAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // PUT /api/admin/courses/{id}/assign-teacher
    [HttpPut("{id:int}/assign-teacher")]
    public async Task<IActionResult> AssignTeacher(int id, [FromBody] AssignTeacherRequest request)
    {
        try
        {
            var updated = await _courseService.AssignTeacherAsync(id, request);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // DELETE /api/admin/courses/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _courseService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
