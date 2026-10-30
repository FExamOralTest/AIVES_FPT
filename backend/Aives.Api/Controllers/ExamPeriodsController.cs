using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Aives.Application.Exams.DTOs;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Aives.Api.Controllers;

[ApiController]
[Route("api/exam-periods")]
[Authorize(Roles = "STAFF,ADMIN")]
public class ExamPeriodsController : ControllerBase
{
    private readonly AivesDbContext _db;

    public ExamPeriodsController(AivesDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ExamPeriodDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriods([FromQuery] int? courseId)
    {
        var query = _db.ExamPeriods
            .Include(p => p.Course)
            .Include(p => p.ExamSessions)
            .AsNoTracking();

        if (courseId.HasValue && courseId.Value > 0)
        {
            query = query.Where(p => p.CourseId == courseId.Value);
        }

        var list = await query
            .OrderByDescending(p => p.StartDate)
            .Select(p => new ExamPeriodDto
            {
                PeriodId = p.PeriodId,
                CourseId = p.CourseId,
                CourseName = p.Course.CourseName,
                PeriodName = p.PeriodName,
                Description = p.Description,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Status = p.Status.ToString(),
                SessionsCount = p.ExamSessions.Count,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        return Ok(list);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ExamPeriodDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreatePeriod([FromBody] CreateExamPeriodRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdStr, out var userId))
            return Unauthorized(new { message = "Invalid token." });

        if (request.StartDate > request.EndDate)
            return BadRequest(new { message = "StartDate must be earlier than EndDate." });

        var course = await _db.Courses.FindAsync(request.CourseId);
        if (course == null)
            return BadRequest(new { message = "Course not found." });

        var period = new ExamPeriod
        {
            CourseId = request.CourseId,
            PeriodName = request.PeriodName.Trim(),
            Description = request.Description?.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = ExamPeriodStatus.ACTIVE,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.ExamPeriods.Add(period);
        await _db.SaveChangesAsync();

        var dto = new ExamPeriodDto
        {
            PeriodId = period.PeriodId,
            CourseId = period.CourseId,
            CourseName = course.CourseName,
            PeriodName = period.PeriodName,
            Description = period.Description,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            Status = period.Status.ToString(),
            SessionsCount = 0,
            CreatedAt = period.CreatedAt
        };

        return CreatedAtAction(nameof(GetPeriods), new { courseId = period.CourseId }, dto);
    }
}

