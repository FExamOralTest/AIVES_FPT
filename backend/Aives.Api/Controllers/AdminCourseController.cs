using Aives.Application.Courses.DTOs;
using Aives.Application.Courses.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = "ADMIN")]
public class AdminCourseController : ControllerBase
{
    private readonly ICourseService _courseService;

    public AdminCourseController(ICourseService courseService) => _courseService = courseService;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _courseService.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try { return Ok(await _courseService.GetByIdAsync(id)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("teacher/{teacherId:int}")]
    public async Task<IActionResult> GetByTeacher(int teacherId)
        => Ok(await _courseService.GetByTeacherIdAsync(teacherId));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCourseRequest request)
    {
        try
        {
            var created = await _courseService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.CourseId }, created);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCourseRequest request)
    {
        try { return Ok(await _courseService.UpdateAsync(id, request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}/assign-teacher")]
    public async Task<IActionResult> AssignTeacher(int id, [FromBody] AssignTeacherRequest request)
    {
        try { return Ok(await _courseService.AssignTeacherAsync(id, request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try { await _courseService.DeleteAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
