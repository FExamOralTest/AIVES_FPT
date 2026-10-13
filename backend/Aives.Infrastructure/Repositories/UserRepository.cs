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
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
    }
}
