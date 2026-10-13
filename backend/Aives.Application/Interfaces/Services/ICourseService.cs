using Aives.Application.DTOs;

namespace Aives.Application.Interfaces.Services;

public interface ICourseService
{
    Task<IEnumerable<CourseDto>> GetAllAsync();
    Task<IEnumerable<CourseDto>> GetByTeacherIdAsync(int teacherId);
    Task<CourseDto> GetByIdAsync(int courseId);
    Task<CourseDto> CreateAsync(CreateCourseRequest request);
    Task<CourseDto> UpdateAsync(int courseId, UpdateCourseRequest request);
    Task<CourseDto> AssignTeacherAsync(int courseId, AssignTeacherRequest request);
    Task DeleteAsync(int courseId);
}
