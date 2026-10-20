using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Aives.Application.Interfaces.Services;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aives.Infrastructure.Storage;

/// <summary>
/// Dịch vụ lưu trữ âm thanh kết nối Cloudflare R2 qua chuẩn S3 API.
/// Tự động fallback sang thư mục cục bộ wwwroot/uploads/audio nếu chưa cấu hình R2 credentials ở môi trường dev.
/// </summary>
public class CloudflareR2StorageService : IStorageService
{
    private readonly CloudflareR2Options _options;
    private readonly ILogger<CloudflareR2StorageService> _logger;
    private readonly IAmazonS3? _s3Client;

    public CloudflareR2StorageService(
        IOptions<CloudflareR2Options> options,
        ILogger<CloudflareR2StorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (_options.IsConfigured)
        {
            try
            {
                var serviceUrl = _options.GetEffectiveServiceUrl();

                // Cloudflare R2 yêu cầu TLS 1.2+. Một số môi trường Windows không tự negotiate
                // được TLS 1.3 với R2 endpoint → dùng HttpClientFactory custom để ép TLS 1.2.
                var httpHandler = new HttpClientHandler
                {
                    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    // Cho phép tất cả server cert trong dev — production nên bỏ dòng này
                    // hoặc cấu hình cert pinning riêng.
                };

                var s3Config = new AmazonS3Config
                {
                    ServiceURL = serviceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = "auto",
                    HttpClientFactory = new R2HttpClientFactory(httpHandler)
                };

                _s3Client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, s3Config);
                _logger.LogInformation("Cloudflare R2 Storage Service initialized successfully for bucket '{Bucket}' at '{ServiceUrl}'",
                    _options.BucketName, serviceUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Cloudflare R2 S3 Client. Falling back to local storage if enabled.");
                _s3Client = null;
            }
        }
        else
        {
            _logger.LogInformation("Cloudflare R2 is not fully configured (AccountId/Keys missing). Operating in Local Storage Fallback mode.");
        }
    }

    /// <inheritdoc/>
    public async Task<string> UploadAudioAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentNullException.ThrowIfNull(content);

        // Normalize object key (loại bỏ leading slash)
        objectKey = objectKey.TrimStart('/');

        // Buffer toàn bộ content vào byte[] một lần.
        // AWS SDK CLOSE stream sau khi PutObjectAsync xong (hoặc fail) —
        // nên KHÔNG thể reuse cùng MemoryStream cho fallback.
        // Giải pháp: giữ byte[], tạo MemoryStream mới cho từng attempt.
        byte[] buffer;
        using (var tmp = new MemoryStream())
        {
            await content.CopyToAsync(tmp, cancellationToken);
            buffer = tmp.ToArray();
        }

        if (_s3Client != null)
        {
            try
            {
                using var s3Stream = new MemoryStream(buffer, writable: false);
                var putRequest = new PutObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey,
                    InputStream = s3Stream,
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "audio/webm" : contentType,
                    DisablePayloadSigning = true
                };

                var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken);
                _logger.LogInformation("Audio successfully uploaded to Cloudflare R2: key='{Key}', HttpStatusCode={StatusCode}",
                    objectKey, response.HttpStatusCode);

                return objectKey;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload audio to Cloudflare R2 for key '{Key}'. Error: {Message}", objectKey, ex.Message);
                if (!_options.UseLocalStorageFallback)
                    throw;

                _logger.LogWarning("Falling back to local disk storage for key '{Key}'", objectKey);
            }
        }

        // Local Storage Fallback — tạo MemoryStream MỚI từ buffer, không reuse stream cũ.
        using var localStream = new MemoryStream(buffer, writable: false);
        return await SaveToLocalDiskAsync(objectKey, localStream, cancellationToken);
    }

    /// <inheritdoc/>
    public string GeneratePresignedGetUrl(string objectKey, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        objectKey = objectKey.TrimStart('/');

        if (_s3Client != null)
        {
            try
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey,
                    Expires = DateTime.UtcNow.Add(duration),
                    Verb = HttpVerb.GET
                };

                return _s3Client.GetPreSignedURL(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Presigned GET URL for key '{Key}' from Cloudflare R2", objectKey);
            }
        }

        // Nếu là fallback cục bộ, trả về relative URL
        var fileName = Path.GetFileName(objectKey);
        return $"/uploads/audio/{fileName}";
    }

    /// <inheritdoc/>
    public string? ResolvePlaybackUrl(string? audioKeyOrUrl, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(audioKeyOrUrl))
            return null;

        var trimmed = audioKeyOrUrl.Trim();

        // Nếu đã là URL tuyệt đối có scheme (http/https) và không phải là key nội bộ
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // Nếu là relative path từ bản ghi cũ (/uploads/audio/...)
        if (trimmed.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        // Nếu đã cấu hình PublicBaseUrl (r2.dev) hoặc CustomDomain thì trả về URL direct qua CDN
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{trimmed.TrimStart('/')}";
        }

        if (!string.IsNullOrWhiteSpace(_options.CustomDomain))
        {
            return $"{_options.CustomDomain.TrimEnd('/')}/{trimmed.TrimStart('/')}";
        }

        // Nếu là Object Key trên Cloudflare R2 (ví dụ: exams/1/sessions/2/...)
        if (_s3Client != null)
        {
            return GeneratePresignedGetUrl(trimmed, duration ?? TimeSpan.FromHours(1));
        }

        // Trường hợp local fallback: ánh xạ key sang tệp cục bộ
        var fileName = Path.GetFileName(trimmed);
        return $"/uploads/audio/{fileName}";
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAudioAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return false;
        objectKey = objectKey.TrimStart('/');

        if (_s3Client != null)
        {
            try
            {
                var deleteRequest = new DeleteObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey
                };
                await _s3Client.DeleteObjectAsync(deleteRequest, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete audio '{Key}' from Cloudflare R2", objectKey);
            }
        }

        // Local cleanup
        try
        {
            var fileName = Path.GetFileName(objectKey);
            var localPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio", fileName);
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete local audio file for key '{Key}'", objectKey);
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return false;
        objectKey = objectKey.TrimStart('/');

        if (_s3Client != null)
        {
            try
            {
                await _s3Client.GetObjectMetadataAsync(_options.BucketName, objectKey, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking existence of key '{Key}' in Cloudflare R2", objectKey);
            }
        }

        var fileName = Path.GetFileName(objectKey);
        var localPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio", fileName);
        return File.Exists(localPath);
    }

    private async Task<string> SaveToLocalDiskAsync(string objectKey, Stream content, CancellationToken cancellationToken)
    {
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var fileName = Path.GetFileName(objectKey);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"{DateTime.UtcNow.Ticks}.webm";
        }

        var filePath = Path.Combine(uploadsDir, fileName);

        if (content.CanSeek)
        {
            content.Position = 0;
        }

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, cancellationToken);
        }

        _logger.LogInformation("Audio saved to local disk fallback: '{FilePath}'", filePath);
        return $"/uploads/audio/{fileName}";
    }
}

/// <summary>
/// HttpClientFactory tùy chỉnh để inject HttpClientHandler với cấu hình TLS vào AWS SDK.
/// Cần thiết vì AmazonS3Config không expose trực tiếp SslProtocols.
/// </summary>
internal sealed class R2HttpClientFactory : Amazon.Runtime.HttpClientFactory
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _client;

    public R2HttpClientFactory(HttpClientHandler handler)
    {
        _handler = handler;
        _client = new HttpClient(handler, disposeHandler: false);
    }

    public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        => _client;
}
