using Aives.Domain.Entities;

namespace Aives.Application.Auth.Interfaces;

public interface IUserRepository
{
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<bool> ExistsByUsernameAsync(string username);
    Task<bool> ExistsByEmailAsync(string email);
    Task<int> GetAdminCountAsync();
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}
