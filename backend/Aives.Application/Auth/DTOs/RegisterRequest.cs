using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Auth.DTOs;

public class RegisterRequest
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
}
