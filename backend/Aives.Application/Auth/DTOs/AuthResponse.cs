namespace Aives.Application.Auth.DTOs;

public class AuthResponse
{
    public string AccessToken { get; set; } = null!;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public UserDto User { get; set; } = null!;
}
