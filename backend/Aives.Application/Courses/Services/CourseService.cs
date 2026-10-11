using Aives.Application.Courses.DTOs;
using Aives.Application.Courses.Interfaces;
using Aives.Application.Users.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Courses.Services;

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
        => (await _courseRepo.GetAllAsync()).Select(ToDto);

    public async Task<IEnumerable<CourseDto>> GetByTeacherIdAsync(int teacherId)
        => (await _courseRepo.GetByTeacherIdAsync(teacherId)).Select(ToDto);

    public async Task<CourseDto> GetByIdAsync(int courseId)
        => ToDto(await _courseRepo.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found."));

    public async Task<CourseDto> CreateAsync(CreateCourseRequest req)
    {
        if (await _courseRepo.ExistsCodeAsync(req.CourseCode))
            throw new InvalidOperationException($"Course code '{req.CourseCode}' already exists.");

        var creator = await _userRepo.GetByIdAsync(req.CreatedBy)
            ?? throw new KeyNotFoundException($"User {req.CreatedBy} not found.");

        if (creator.Role != UserRole.ADMIN)
            throw new UnauthorizedAccessException("Only admin can create courses.");

        var course = new Course
        {
            CourseCode = req.CourseCode.Trim().ToUpper(),
            CourseName = req.CourseName.Trim(),
            CreatedBy  = req.CreatedBy,
            CreatedAt  = DateTime.UtcNow
        };

        var created = await _courseRepo.CreateAsync(course);
        return ToDto(await _courseRepo.GetByIdAsync(created.CourseId) ?? created);
    }

    public async Task<CourseDto> UpdateAsync(int courseId, UpdateCourseRequest req)
    {
        var course = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");

        if (!string.IsNullOrWhiteSpace(req.CourseName))
            course.CourseName = req.CourseName.Trim();

        var updated = await _courseRepo.UpdateAsync(course);
        return ToDto(await _courseRepo.GetByIdAsync(updated.CourseId) ?? updated);
    }

    public async Task<CourseDto> AssignTeacherAsync(int courseId, AssignTeacherRequest req)
    {
        var course = await _courseRepo.GetByIdAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");

        var teacher = await _userRepo.GetByIdAsync(req.TeacherId)
            ?? throw new KeyNotFoundException($"User {req.TeacherId} not found.");

        if (teacher.Role != UserRole.TEACHER)
            throw new InvalidOperationException($"User {req.TeacherId} is not a teacher.");

        course.TeacherId = req.TeacherId;
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
        c.CourseId, c.CourseCode, c.CourseName,
        c.TeacherId, c.Teacher?.FullName,
        c.CreatedBy, c.CreatedAt
    );
}
