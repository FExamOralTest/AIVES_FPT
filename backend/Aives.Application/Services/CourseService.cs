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

    // ------------------------------------------------------------------ //
    //  Queries                                                             //
    // ------------------------------------------------------------------ //

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

    // ------------------------------------------------------------------ //
    //  Commands                                                            //
    // ------------------------------------------------------------------ //

    public async Task<CourseDto> CreateAsync(CreateCourseRequest req)
    {
        if (await _courseRepo.ExistsCodeAsync(req.CourseCode))
            throw new InvalidOperationException($"Course code '{req.CourseCode}' already exists.");

        // Kiểm tra creator tồn tại và có quyền Admin
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
        // Reload để lấy navigation property Teacher
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

        // Kiểm tra user tồn tại và có role TEACHER
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

    // ------------------------------------------------------------------ //
    //  Mapping                                                             //
    // ------------------------------------------------------------------ //

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
