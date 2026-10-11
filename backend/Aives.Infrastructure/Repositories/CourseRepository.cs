using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly AivesDbContext _db;

    public CourseRepository(AivesDbContext db) => _db = db;

    public async Task<IEnumerable<Course>> GetAllAsync()
        => await _db.Courses.AsNoTracking()
                            .Include(c => c.Teacher)
                            .ToListAsync();

    public async Task<IEnumerable<Course>> GetByTeacherIdAsync(int teacherId)
        => await _db.Courses.AsNoTracking()
                            .Include(c => c.Teacher)
                            .Where(c => c.TeacherId == teacherId)
                            .ToListAsync();

    public async Task<Course?> GetByIdAsync(int courseId)
        => await _db.Courses.AsNoTracking()
                            .Include(c => c.Teacher)
                            .Include(c => c.CreatedByNavigation)
                            .FirstOrDefaultAsync(c => c.CourseId == courseId);

    public async Task<Course?> GetByCodeAsync(string courseCode)
        => await _db.Courses.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CourseCode == courseCode);

    public async Task<bool> ExistsCodeAsync(string courseCode, int? excludeCourseId = null)
        => await _db.Courses.AnyAsync(c =>
            c.CourseCode == courseCode &&
            (excludeCourseId == null || c.CourseId != excludeCourseId));

    public async Task<Course> CreateAsync(Course course)
    {
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();
        return course;
    }

    public async Task<Course> UpdateAsync(Course course)
    {
        _db.Courses.Update(course);
        await _db.SaveChangesAsync();
        return course;
    }

    public async Task DeleteAsync(int courseId)
    {
        var course = await _db.Courses.FindAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");
        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
    }
}
