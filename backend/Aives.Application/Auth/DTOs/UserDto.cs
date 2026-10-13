namespace Aives.Application.Auth.DTOs;

public class UserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Email { get; set; }
    public string Role { get; set; } = null!;
}
