using Aives.Application.DTOs;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Services;

public class CourseService : ICourseService
{
    private readonly ICourseRepository _courseRepo;
    private readonly IUserRepository   _userRepo;

    public CourseService(ICourseRepository courseRepo, IUserRepository userRepo)
    {
        _courseRepo = courseRepo;
        _userRepo   = userRepo;
    }

    public async Task<IEnumerable<CourseDto>> GetAllAsync()
    {
        var courses = await _courseRepo.GetAllAsync();
        return courses.Select(ToDto);
    }

    public async Task<IEnumerable<CourseDto>> GetByTeacherIdAsync(int teacherId)
    {
        var courses = await _courseRepo.GetByTeacherIdAsync(teacherId);
        return courses.Select(ToDto);
    }

    public async Task<CourseDto> GetByIdAsync(int courseId)
    {
        var course = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");
        return ToDto(course);
    }

    public async Task<CourseDto> CreateAsync(CreateCourseRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CourseCode) || string.IsNullOrWhiteSpace(req.CourseName))
            throw new ArgumentException("Course code and name are required.");
        var courseCode = req.CourseCode.Trim().ToUpperInvariant();
        if (await _courseRepo.ExistsCodeAsync(courseCode))
            throw new InvalidOperationException($"Course code '{req.CourseCode}' already exists.");

        var createdBy = req.CreatedBy ?? throw new InvalidOperationException("CreatedBy is required.");
        var creator = await _userRepo.GetByIdAsync(createdBy)
            ?? throw new KeyNotFoundException($"User {createdBy} not found.");

        if (creator.Role != UserRole.ADMIN)
            throw new UnauthorizedAccessException("Only admin can create courses.");

        if (req.TeacherId.HasValue && req.TeacherId.Value > 0)
        {
            var teacher = await _userRepo.GetByIdAsync(req.TeacherId.Value)
                ?? throw new KeyNotFoundException($"Teacher {req.TeacherId.Value} not found.");
            if (teacher.Role != UserRole.STAFF)
                throw new InvalidOperationException($"User {req.TeacherId.Value} is not academic staff.");
        }

        var course = new Course
        {
            CourseCode = courseCode,
            CourseName = req.CourseName.Trim(),
            CreatedBy  = createdBy,
            TeacherId  = req.TeacherId.HasValue && req.TeacherId.Value > 0 ? req.TeacherId.Value : null,
            CreatedAt  = DateTime.UtcNow
        };

        var created = await _courseRepo.CreateAsync(course);
        return ToDto(await _courseRepo.GetByIdAsync(created.CourseId) ?? created);
    }

    public async Task<CourseDto> UpdateAsync(int courseId, UpdateCourseRequest req)
    {
        var course = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");

        if (string.IsNullOrWhiteSpace(req.CourseName))
            throw new ArgumentException("Course name is required.");
        if (req.CourseCode is not null)
        {
            if (string.IsNullOrWhiteSpace(req.CourseCode))
                throw new ArgumentException("Course code is required.");
            var code = req.CourseCode.Trim().ToUpperInvariant();
            if (await _courseRepo.ExistsCodeAsync(code, courseId))
                throw new InvalidOperationException("Course code already exists.");
            course.CourseCode = code;
        }
        course.CourseName = req.CourseName.Trim();

        course.Teacher = null;
        course.CreatedByNavigation = null;
        var updated = await _courseRepo.UpdateAsync(course);
        return ToDto(await _courseRepo.GetByIdAsync(updated.CourseId) ?? updated);
    }

    public async Task<CourseDto> AssignTeacherAsync(int courseId, AssignTeacherRequest req)
    {
        var course = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");
        var teacher = await _userRepo.GetByIdAsync(req.TeacherId)
            ?? throw new KeyNotFoundException($"User {req.TeacherId} not found.");

        if (teacher.Role != UserRole.STAFF)
            throw new InvalidOperationException($"User {req.TeacherId} is not academic staff.");

        course.TeacherId = req.TeacherId;
        course.Teacher = null;
        course.CreatedByNavigation = null;
        var updated = await _courseRepo.UpdateAsync(course);
        return ToDto(await _courseRepo.GetByIdAsync(updated.CourseId) ?? updated);
    }

    public async Task DeleteAsync(int courseId)
    {
        _ = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");
        await _courseRepo.DeleteAsync(courseId);
    }

    private static CourseDto ToDto(Course c) => new(
        c.CourseId,
        c.CourseCode,
        c.CourseName,
        c.TeacherId,
        c.Teacher?.FullName,
        c.CreatedBy,
        c.CreatedAt
    );
}
