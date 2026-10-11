using Aives.Domain.Enums;

namespace Aives.Application.Users.DTOs;

public record UserDto(
    int       UserId,
    string    Username,
    string    FullName,
    string?   Email,
    string    Role,
    DateTime? CreatedAt
);

public record CreateUserRequest(
    string   Username,
    string   FullName,
    string?  Email,
    string   Password,
    UserRole Role
);

public record UpdateUserRequest(
    string? FullName,
    string? Email,
    string? Password
);
