using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Auth.DTOs;

public class LoginRequest
{
    private string? _identifier;

    [Required(ErrorMessage = "Email or Username is required.")]
    public string Identifier
    {
        get => _identifier ?? Email ?? Username ?? string.Empty;
        set => _identifier = value;
    }

    public string? Email
    {
        get => _identifier;
        set { if (!string.IsNullOrWhiteSpace(value)) _identifier = value; }
    }

    public string? Username
    {
        get => _identifier;
        set { if (!string.IsNullOrWhiteSpace(value)) _identifier = value; }
    }

    [Required]
    public string Password { get; set; } = null!;
}

