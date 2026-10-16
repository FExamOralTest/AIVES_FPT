using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AivesDbContext _db;

    public UserRepository(AivesDbContext db) => _db = db;

    public async Task<IEnumerable<User>> GetAllAsync()
        => await _db.Users.AsNoTracking().ToListAsync();

    public async Task<IEnumerable<User>> GetByRoleAsync(UserRole role)
        => await _db.Users.AsNoTracking()
                          .Where(u => u.Role == role)
                          .ToListAsync();

    public async Task<User?> GetByIdAsync(int userId)
        => await _db.Users.AsNoTracking()
                          .FirstOrDefaultAsync(u => u.UserId == userId);

    public async Task<User?> GetByUsernameAsync(string username)
        => await _db.Users.AsNoTracking()
                          .FirstOrDefaultAsync(u => u.Username == username);

    public async Task<bool> ExistsUsernameAsync(string username)
        => await _db.Users.AnyAsync(u => u.Username == username);

    public async Task<bool> ExistsEmailAsync(string email, int? excludeUserId = null)
        => await _db.Users.AnyAsync(u =>
            u.Email == email &&
            (excludeUserId == null || u.UserId != excludeUserId));

    public async Task<User> CreateAsync(User user)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
        return user;
    }

    public async Task DeleteAsync(int userId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        if (user.Role == UserRole.ADMIN && !await _db.Users.AnyAsync(u => u.Role == UserRole.ADMIN && u.UserId != userId))
            throw new InvalidOperationException("Cannot delete the last admin user.");
        if (await _db.Courses.AnyAsync(c => c.CreatedBy == userId || c.TeacherId == userId)
            || await _db.Exams.AnyAsync(e => e.CreatedBy == userId)
            || await _db.ExamSessions.AnyAsync(s => s.ScheduledBy == userId)
            || await _db.MarkdownDocuments.AnyAsync(d => d.UploadedBy == userId)
            || await _db.Enrollments.AnyAsync(e => e.StudentId == userId)
            || await _db.SessionEnrollments.AnyAsync(e => e.StudentId == userId)
            || await _db.Results.AnyAsync(r => r.StudentId == userId || r.ReviewedBy == userId))
            throw new InvalidOperationException("This user has related courses, exams, documents, enrollments or results and cannot be deleted.");
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
