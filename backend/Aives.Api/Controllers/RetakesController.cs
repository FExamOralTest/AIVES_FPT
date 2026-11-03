using System.Security.Claims;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aives.Api.Controllers;

[ApiController]
[Authorize(Roles = "EXAMINER,ADMIN")]
public class RetakesController(IRetakeService retakes) : ControllerBase
{
    [HttpPost("api/results/{resultId:int}/retake")]
    public Task<IActionResult> Grant(int resultId, GrantRetakeRequest request, CancellationToken ct) =>
        Execute(async id => await retakes.GrantAsync(resultId, request, id, ct));

    [HttpGet("api/results/{resultId:int}/retake-history")]
    public Task<IActionResult> History(int resultId, CancellationToken ct) =>
        Execute(async id => await retakes.HistoryAsync(resultId, id, ct));

    [HttpPut("api/retakes/{retakeId:int}/schedule")]
    public Task<IActionResult> Schedule(int retakeId, GrantRetakeRequest request, CancellationToken ct) =>
        Execute(async id => await retakes.RescheduleAsync(retakeId, request, id, ct));

    private async Task<IActionResult> Execute<T>(Func<int, Task<T>> action)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        try { return Ok(await action(id)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
