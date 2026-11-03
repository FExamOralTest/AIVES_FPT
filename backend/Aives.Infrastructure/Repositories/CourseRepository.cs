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
        var existing = await _db.Courses.FindAsync(course.CourseId)
            ?? throw new KeyNotFoundException($"Course {course.CourseId} not found.");

        existing.CourseCode = course.CourseCode;
        existing.CourseName = course.CourseName;
        existing.TeacherId  = course.TeacherId;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int courseId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var course = await _db.Courses.FindAsync(courseId)
            ?? throw new KeyNotFoundException($"Course {courseId} not found.");
        if (await _db.Exams.AnyAsync(e => e.CourseId == courseId)
            || await _db.MarkdownDocuments.AnyAsync(d => d.CourseId == courseId)
            || await _db.QuestionBanks.AnyAsync(q => q.CourseId == courseId)
            || await _db.Enrollments.AnyAsync(e => e.CourseId == courseId))
            throw new InvalidOperationException("This course has documents, questions, exams or enrollments and cannot be deleted.");
        _db.Courses.Remove(course);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
