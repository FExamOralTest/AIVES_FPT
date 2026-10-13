using Aives.Application.DTOs;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

/// <summary>
/// Admin quản lý User (Teacher, Student, Admin).
/// Base route: /api/admin/users
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "ADMIN")]
public class AdminUserController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUserController(IUserService userService) => _userService = userService;

    // GET /api/admin/users
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }

    // GET /api/admin/users/role/{role}
    [HttpGet("role/{role}")]
    public async Task<IActionResult> GetByRole(string role)
    {
        if (!Enum.TryParse<UserRole>(role.ToUpper(), out var parsed))
            return BadRequest($"Invalid role '{role}'. Valid values: ADMIN, TEACHER, STUDENT.");

        var users = await _userService.GetByRoleAsync(parsed);
        return Ok(users);
    }

    // GET /api/admin/users/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var user = await _userService.GetByIdAsync(id);
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    // POST /api/admin/users
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        try
        {
            var created = await _userService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = created.UserId }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // PUT /api/admin/users/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var updated = await _userService.UpdateAsync(id, request);
            return Ok(updated);
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

    // DELETE /api/admin/users/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
