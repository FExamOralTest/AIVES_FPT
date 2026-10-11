using Aives.Application.Users.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Users.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
    Task<IEnumerable<UserDto>> GetByRoleAsync(UserRole role);
    Task<UserDto> GetByIdAsync(int userId);
    Task<UserDto> CreateAsync(CreateUserRequest request);
    Task<UserDto> UpdateAsync(int userId, UpdateUserRequest request);
    Task DeleteAsync(int userId);
}
