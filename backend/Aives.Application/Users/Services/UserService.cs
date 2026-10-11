using Aives.Application.Users.DTOs;
using Aives.Application.Users.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Users.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;

    public UserService(IUserRepository repo) => _repo = repo;

    public async Task<IEnumerable<UserDto>> GetAllAsync()
        => (await _repo.GetAllAsync()).Select(ToDto);

    public async Task<IEnumerable<UserDto>> GetByRoleAsync(UserRole role)
        => (await _repo.GetByRoleAsync(role)).Select(ToDto);

    public async Task<UserDto> GetByIdAsync(int userId)
        => ToDto(await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found."));

    public async Task<UserDto> CreateAsync(CreateUserRequest req)
    {
        if (await _repo.ExistsUsernameAsync(req.Username))
            throw new InvalidOperationException($"Username '{req.Username}' already exists.");
        if (!string.IsNullOrWhiteSpace(req.Email) && await _repo.ExistsEmailAsync(req.Email))
            throw new InvalidOperationException($"Email '{req.Email}' already exists.");

        var user = new User
        {
            Username     = req.Username.Trim(),
            FullName     = req.FullName.Trim(),
            Email        = req.Email?.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12),
            Role         = req.Role,
            CreatedAt    = DateTime.UtcNow
        };

        return ToDto(await _repo.CreateAsync(user));
    }

    public async Task<UserDto> UpdateAsync(int userId, UpdateUserRequest req)
    {
        var user = await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!string.IsNullOrWhiteSpace(req.FullName)) user.FullName = req.FullName.Trim();

        if (req.Email is not null)
        {
            var normalised = req.Email.Trim().ToLowerInvariant();
            if (await _repo.ExistsEmailAsync(normalised, excludeUserId: userId))
                throw new InvalidOperationException($"Email '{req.Email}' already in use.");
            user.Email = normalised;
        }

        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12);

        return ToDto(await _repo.UpdateAsync(user));
    }

    public async Task DeleteAsync(int userId)
    {
        _ = await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        await _repo.DeleteAsync(userId);
    }

    private static UserDto ToDto(User u) => new(
        u.UserId, u.Username, u.FullName,
        u.Email, u.Role.ToString(), u.CreatedAt
    );
}
