namespace Aives.Application.Auth.Interfaces;

public interface IJwtBlacklistService
{
    Task<bool> IsTokenBlacklistedAsync(string token);
    Task AddToBlacklistAsync(string token, DateTime expiry);
    Task CleanupExpiredTokensAsync();
}
