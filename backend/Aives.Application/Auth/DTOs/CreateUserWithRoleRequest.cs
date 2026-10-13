using System.ComponentModel.DataAnnotations;
using Aives.Domain.Enums;

namespace Aives.Application.Auth.DTOs;

/// <summary>
/// Chỉ ADMIN mới được dùng — tạo user với role tuỳ chọn (ADMIN, TEACHER, STUDENT).
/// </summary>
public class CreateUserWithRoleRequest
{
    [Required]
    [MaxLength(50)]
    [MinLength(3)]
    public string Username { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = null!;

    [Required]
    [DataType(DataType.Password)]
    [StrongPassword]
    public string Password { get; set; } = null!;

    [Required]
    public UserRole Role { get; set; }
}
