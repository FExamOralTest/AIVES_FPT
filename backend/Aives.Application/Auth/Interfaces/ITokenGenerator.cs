namespace Aives.Application.Auth.Interfaces;

public interface ITokenGenerator
{
    string GenerateRefreshToken();
}
