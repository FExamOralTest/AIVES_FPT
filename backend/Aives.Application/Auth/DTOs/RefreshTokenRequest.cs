using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Auth.DTOs;

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}
