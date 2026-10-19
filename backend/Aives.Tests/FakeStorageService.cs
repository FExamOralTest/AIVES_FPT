using System.Collections.Concurrent;
using Aives.Application.Interfaces.Services;

namespace Aives.Tests;

/// <summary>
/// Fake <see cref="IStorageService"/> cho unit/integration test.
/// Lưu trữ in-memory, không cần R2 credentials.
/// </summary>
internal sealed class FakeStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new();
    private readonly ConcurrentDictionary<string, string> _contentTypes = new();

    public IReadOnlyDictionary<string, byte[]> Storage => _store;

    public void Clear() => _store.Clear();

    public string? LastGeneratedPresignedUrl { get; private set; }

    public Task<string> UploadAudioAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        var key = objectKey.TrimStart('/');
        _store[key] = ms.ToArray();
        _contentTypes[key] = contentType;
        return Task.FromResult(key);
    }

    public string GeneratePresignedGetUrl(string objectKey, TimeSpan duration)
    {
        var key = objectKey.TrimStart('/');
        var url = $"http://localhost:9999/storage/{Uri.EscapeDataString(key)}?expires={DateTime.UtcNow.Add(duration):O}&sig=fake";
        LastGeneratedPresignedUrl = url;
        return url;
    }

    public string? ResolvePlaybackUrl(string? audioKeyOrUrl, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(audioKeyOrUrl))
            return null;

        var trimmed = audioKeyOrUrl.Trim();

        // Nếu đã là URL tuyệt đối
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // Nếu là relative path cũ
        if (trimmed.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        // Object key → trả về presigned URL giả
        return GeneratePresignedGetUrl(trimmed, duration ?? TimeSpan.FromHours(1));
    }

    public Task<bool> DeleteAudioAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var key = objectKey.TrimStart('/');
        var removed = _store.TryRemove(key, out _);
        _contentTypes.TryRemove(key, out _);
        return Task.FromResult(removed);
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var key = objectKey.TrimStart('/');
        return Task.FromResult(_store.ContainsKey(key));
    }

    /// <summary>
    /// Hỗ trợ download giả: trả MemoryStream chứa bytes đã lưu.
    /// Dùng cho FakeHttpHandler để mock HTTP GET về presigned URL.
    /// </summary>
    public Stream? TryGetStream(string objectKey)
    {
        var key = objectKey.TrimStart('/');
        if (_store.TryGetValue(key, out var bytes))
            return new MemoryStream(bytes, writable: false);
        return null;
    }
}
