using Aives.Domain.Entities;

namespace Aives.Application.Interfaces.Repositories;

public interface ICourseRepository
{
    Task<IEnumerable<Course>> GetAllAsync();
    Task<IEnumerable<Course>> GetByTeacherIdAsync(int teacherId);
    Task<Course?> GetByIdAsync(int courseId);
    Task<Course?> GetByCodeAsync(string courseCode);
    Task<bool> ExistsCodeAsync(string courseCode, int? excludeCourseId = null);
    Task<Course> CreateAsync(Course course);
    Task<Course> UpdateAsync(Course course);
    Task DeleteAsync(int courseId);
}
