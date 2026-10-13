using System.ComponentModel.DataAnnotations;
using Aives.Domain.Enums;

namespace Aives.Application.Auth.DTOs;

/// <summary>
/// Chỉ ADMIN dùng — cập nhật thông tin user, bao gồm đổi Role.
/// Các field null = giữ nguyên giá trị cũ.
/// </summary>
public class UpdateUserWithRoleRequest
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MinLength(6)]
    public string? Password { get; set; }   // null = không đổi

    public UserRole? Role { get; set; }     // null = không đổi
}
