using System.Security.Cryptography;
using Aives.Application.Auth.Interfaces;

namespace Aives.Infrastructure.Auth;

public class TokenGenerator : ITokenGenerator
{
    private const int RefreshTokenSize = 32;

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[RefreshTokenSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
