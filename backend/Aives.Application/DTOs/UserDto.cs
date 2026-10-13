using Aives.Domain.Enums;

namespace Aives.Application.DTOs;

/// <summary>Dữ liệu trả về cho User — không bao giờ trả về PasswordHash.</summary>
public record UserDto(
    int    UserId,
    string Username,
    string FullName,
    string? Email,
    string Role,
    DateTime? CreatedAt
);

/// <summary>Admin tạo mới User (Teacher hoặc Student).</summary>
public record CreateUserRequest(
    string Username,
    string FullName,
    string? Email,
    string Password,
    UserRole Role
);

/// <summary>Admin cập nhật thông tin User.</summary>
public record UpdateUserRequest(
    string? FullName,
    string? Email,
    string? Password   // null = không đổi password
);
