using System.Collections.Concurrent;
using Aives.Application.Auth.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Auth;

public class JwtBlacklistService : IJwtBlacklistService
{
    private static readonly ConcurrentDictionary<string, DateTime> _blacklist = new();
    private readonly ILogger<JwtBlacklistService> _logger;

    public JwtBlacklistService(ILogger<JwtBlacklistService> logger)
    {
        _logger = logger;
    }

    public Task<bool> IsTokenBlacklistedAsync(string token)
    {
        var isBlacklisted = _blacklist.TryGetValue(token, out var expiry);
        return Task.FromResult(isBlacklisted);
    }

    public Task AddToBlacklistAsync(string token, DateTime expiry)
    {
        _blacklist[token] = expiry;
        _logger.LogInformation("Token added to blacklist, expires at {Expiry}", expiry);
        return Task.CompletedTask;
    }

    public Task CleanupExpiredTokensAsync()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _blacklist.Where(kvp => kvp.Value <= now).Select(kvp => kvp.Key).ToList();

        foreach (var key in expiredKeys)
        {
            _blacklist.TryRemove(key, out _);
        }

        if (expiredKeys.Any())
        {
            _logger.LogInformation("Cleaned up {Count} expired tokens from blacklist", expiredKeys.Count);
        }

        return Task.CompletedTask;
    }
}
