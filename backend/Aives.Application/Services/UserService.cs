using Aives.Application.DTOs;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repo;

    public UserService(IUserRepository repo) => _repo = repo;

    // ------------------------------------------------------------------ //
    //  Queries                                                             //
    // ------------------------------------------------------------------ //

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await _repo.GetAllAsync();
        return users.Select(ToDto);
    }

    public async Task<IEnumerable<UserDto>> GetByRoleAsync(UserRole role)
    {
        var users = await _repo.GetByRoleAsync(role);
        return users.Select(ToDto);
    }

    public async Task<UserDto> GetByIdAsync(int userId)
    {
        var user = await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        return ToDto(user);
    }

    // ------------------------------------------------------------------ //
    //  Commands                                                            //
    // ------------------------------------------------------------------ //

    public async Task<UserDto> CreateAsync(CreateUserRequest req)
    {
        // Security: username và email phải unique
        if (await _repo.ExistsUsernameAsync(req.Username))
            throw new InvalidOperationException($"Username '{req.Username}' already exists.");

        if (!string.IsNullOrWhiteSpace(req.Email) &&
            await _repo.ExistsEmailAsync(req.Email))
            throw new InvalidOperationException($"Email '{req.Email}' already exists.");

        // Security: hash password với BCrypt work factor 12
        var hash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12);

        var user = new User
        {
            Username     = req.Username.Trim(),
            FullName     = req.FullName.Trim(),
            Email        = req.Email?.Trim().ToLowerInvariant(),
            PasswordHash = hash,
            Role         = req.Role,
            CreatedAt    = DateTime.UtcNow
        };

        var created = await _repo.CreateAsync(user);
        return ToDto(created);
    }

    public async Task<UserDto> UpdateAsync(int userId, UpdateUserRequest req)
    {
        var user = await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");

        if (!string.IsNullOrWhiteSpace(req.FullName))
            user.FullName = req.FullName.Trim();

        if (req.Email is not null)
        {
            var normalised = req.Email.Trim().ToLowerInvariant();
            if (await _repo.ExistsEmailAsync(normalised, excludeUserId: userId))
                throw new InvalidOperationException($"Email '{req.Email}' already in use.");
            user.Email = normalised;
        }

        // Security: chỉ hash khi có password mới được truyền vào
        if (!string.IsNullOrWhiteSpace(req.Password))
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password, workFactor: 12);

        var updated = await _repo.UpdateAsync(user);
        return ToDto(updated);
    }

    public async Task DeleteAsync(int userId)
    {
        // Xác nhận tồn tại trước khi xóa
        _ = await _repo.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException($"User {userId} not found.");
        await _repo.DeleteAsync(userId);
    }

    // ------------------------------------------------------------------ //
    //  Mapping — không bao giờ trả về PasswordHash                        //
    // ------------------------------------------------------------------ //

    private static UserDto ToDto(User u) => new(
        u.UserId,
        u.Username,
        u.FullName,
        u.Email,
        u.Role.ToString(),
        u.CreatedAt
    );
}
