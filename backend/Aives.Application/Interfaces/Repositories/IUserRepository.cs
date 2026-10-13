using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Interfaces.Repositories;

public interface IUserRepository
{
    Task<IEnumerable<User>> GetAllAsync();
    Task<IEnumerable<User>> GetByRoleAsync(UserRole role);
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<bool> ExistsUsernameAsync(string username);
    Task<bool> ExistsEmailAsync(string email, int? excludeUserId = null);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(int userId);
}
