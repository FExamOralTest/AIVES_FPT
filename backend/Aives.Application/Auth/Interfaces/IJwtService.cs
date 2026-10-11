using Aives.Domain.Entities;

namespace Aives.Application.Auth.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    DateTime GetTokenExpiration();
    int GetExpirationSeconds();
}
