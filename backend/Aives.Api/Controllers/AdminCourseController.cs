using System.Security.Claims;
using Aives.Application.DTOs;
using Aives.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/admin/courses")]
[Authorize(Roles = "ADMIN")]
public class AdminCourseController(ICourseService courses) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> GetAll() => ExecuteAsync(async () => Ok(await courses.GetAllAsync()));

    [HttpGet("{id:int}")]
    public Task<IActionResult> GetById(int id) => ExecuteAsync(async () => Ok(await courses.GetByIdAsync(id)));

    [HttpGet("teacher/{teacherId:int}")]
    public Task<IActionResult> GetByTeacher(int teacherId)
        => ExecuteAsync(async () => Ok(await courses.GetByTeacherIdAsync(teacherId)));

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateCourseRequest request) => ExecuteAsync(async () =>
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId))
            return Unauthorized();
        var created = await courses.CreateAsync(request with { CreatedBy = adminId });
        return CreatedAtAction(nameof(GetById), new { id = created.CourseId }, created);
    });

    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, [FromBody] UpdateCourseRequest request)
        => ExecuteAsync(async () => Ok(await courses.UpdateAsync(id, request)));

    [HttpPut("{id:int}/assign-teacher")]
    public Task<IActionResult> AssignTeacher(int id, [FromBody] AssignTeacherRequest request)
        => ExecuteAsync(async () => Ok(await courses.AssignTeacherAsync(id, request)));

    [HttpDelete("{id:int}")]
    public Task<IActionResult> Delete(int id) => ExecuteAsync(async () =>
    {
        await courses.DeleteAsync(id);
        return NoContent();
    });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" or "23503" or "40001" })
        {
            return Conflict(new { message = "The course code already exists or related data changed. Reload and try again." });
        }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "40P01")
        {
            return Conflict(new { message = "The data changed concurrently. Reload and try again." });
        }
    }
}
