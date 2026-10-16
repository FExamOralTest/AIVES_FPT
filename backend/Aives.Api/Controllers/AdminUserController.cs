using Aives.Application.DTOs;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Enums;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "ADMIN")]
public class AdminUserController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUserController(IUserService userService) => _userService = userService;
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }
    [HttpGet("role/{role}")]
    public async Task<IActionResult> GetByRole(string role)
    {
        if (!Enum.TryParse<UserRole>(role.ToUpper(), out var parsed))
            return BadRequest($"Invalid role '{role}'. Valid values: ADMIN, TEACHER, STUDENT.");

        var users = await _userService.GetByRoleAsync(parsed);
        return Ok(users);
    }
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
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var callerId))
            return Unauthorized();
        if (id == callerId)
            return Conflict(new { message = "You cannot delete your own account." });
        try
        {
            await _userService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23503" or "40001" })
        {
            return Conflict(new { message = "This user has related data or changed concurrently. Reload and try again." });
        }
        catch (PostgresException ex) when (ex.SqlState is "40001" or "40P01")
        {
            return Conflict(new { message = "The data changed concurrently. Reload and try again." });
        }
    }
}
