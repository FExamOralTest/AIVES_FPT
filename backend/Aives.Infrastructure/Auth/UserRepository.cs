using Aives.Application.Auth.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Auth;

public class UserRepository : IUserRepository
{
    private readonly AivesDbContext _context;

    public UserRepository(AivesDbContext context) => _context = context;

    public async Task<IEnumerable<User>> GetAllAsync()
        => await _context.Users.AsNoTracking().ToListAsync();

    public async Task<User?> GetByIdAsync(int userId)
        => await _context.Users.FindAsync(userId);

    public async Task<User?> GetByUsernameAsync(string username)
        => await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username);

    public async Task<User?> GetByEmailAsync(string email)
        => await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email);

    public async Task<bool> ExistsByUsernameAsync(string username)
        => await _context.Users.AnyAsync(u => u.Username == username);

    public async Task<bool> ExistsByEmailAsync(string email)
        => await _context.Users.AnyAsync(u => u.Email == email);

    public async Task<int> GetAdminCountAsync()
        => await _context.Users.CountAsync(u => u.Role == UserRole.ADMIN);

    public async Task AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
}
