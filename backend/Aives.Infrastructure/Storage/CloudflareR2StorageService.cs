using System;
using System.IO;
using System.Linq;
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

    /// <summary>
    /// Serialize mọi thao tác PUT lên R2.
    ///
    /// Lý do: AWS SDK v4 + Cloudflare R2 có thể trả
    /// "The request signature we calculated does not match the signature you provided"
    /// khi nhiều request chạy ĐỒNG THỜI trên cùng 1 AmazonS3Client —
    /// SDK ghi header runtime vào HttpClient dùng chung, header của request trước
    /// còn sót lại → SignedHeaders của request sau lệch với chữ ký server tính ra.
    /// Serialize upload loại bỏ hoàn toàn khả năng này.
    /// (Lưu ý: đây là hàng đợi 1 luồng — throughput R2 không phải điểm nghẽn ở đây.)
    /// </summary>
    private static readonly SemaphoreSlim UploadGate = new(1, 1);

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
                var s3Config = new AmazonS3Config
                {
                    ServiceURL = serviceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = "auto",
                    // Cloudflare R2 KHÔNG hỗ trợ aws-chunked streaming signature với
                    // trailer (STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER).
                    // AWS SDK v4 mặc định bật checksum → sinh payload trailer → R2 trả lỗi
                    // "not implemented" khi upload part.
                    // WHEN_REQUIRED = chỉ tính checksum cho operation bắt buộc (S3 yêu cầu),
                    // bỏ qua checksum tùy chọn (CRC32/CRC32C) cho UploadPart/UploadPart.
                    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
                    // KHÔNG dùng custom HttpClientFactory.
                    // Lý do: HttpClientFactory dùng chung 1 HttpClient cho MỌI request →
                    // AWS SDK v4 ghi các header runtime (amz-sdk-invocation-id, amz-sdk-request,
                    // x-amz-user-agent) vào DefaultRequestHeaders của HttpClient đó. Những
                    // header này tồn tại qua các request sau và bị tính vào SignedHeaders
                    // → R2 tính lại chữ ký KHÁC với chữ ký client gửi đi → lỗi
                    // "The request signature we calculated does not match the signature you provided".
                    // .NET 8 tự negotiate TLS 1.2/1.3 với endpoint R2 nên không cần ép TLS thủ công.
                };

                _s3Client = new AmazonS3Client(_options.AccessKeyId, _options.SecretAccessKey, s3Config);
                _logger.LogInformation(
                    "Cloudflare R2 Storage Service initialized successfully for bucket '{Bucket}' at '{ServiceUrl}' (accessKeyId={KeyPrefix}...)",
                    _options.BucketName, serviceUrl, MaskKey(_options.AccessKeyId));
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

    /// <summary>
    /// Chuẩn hóa Content-Type trước khi gửi lên R2.
    ///
    /// VÌ SAO CẦN: MediaRecorder trả về file.type = "audio/webm;codecs=opus".
    /// Header "Content-Type: audio/webm;codecs=opus" (có tham số) khiến R2
    /// TỪ CHỐI request với "The request signature we calculated does not match
    /// the signature you provided" — vì chữ ký SigV4 được tính trên header gốc,
    /// còn R2 parse lại header canonical theo chuẩn media-type (bỏ tham số).
    /// → Mọi tham số phải được bỏ trước khi ký.
    ///
    /// Chỉ giữ lại phần "type/subtype" và ép về chữ thường.
    /// </summary>
    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return "audio/webm";

        // Cắt tại dấu ';' để loại bỏ toàn bộ tham số (codecs, charset, ...)
        var mainType = contentType.Split(';')[0].Trim().ToLowerInvariant();

        return string.IsNullOrWhiteSpace(mainType) ? "audio/webm" : mainType;
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
            await UploadGate.WaitAsync(cancellationToken);
            try
            {
                using var s3Stream = new MemoryStream(buffer, writable: false);
                var putRequest = new PutObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey,
                    InputStream = s3Stream,
                    ContentType = NormalizeContentType(contentType),
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
            finally
            {
                UploadGate.Release();
            }
        }

        // Local Storage Fallback — tạo MemoryStream MỚI từ buffer, không reuse stream cũ.
        using var localStream = new MemoryStream(buffer, writable: false);
        return await SaveToLocalDiskAsync(objectKey, localStream, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<LocalFileUploadResult> UploadLocalFileAsync(
        string objectKey,
        string localFilePath,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(localFilePath);

        if (!File.Exists(localFilePath))
            throw new FileNotFoundException($"File local không tồn tại: {localFilePath}", localFilePath);

        objectKey = objectKey.TrimStart('/');

        if (_s3Client != null)
        {
            await UploadGate.WaitAsync(cancellationToken);
            try
            {
                // Stream trực tiếp từ đĩa — không buffer toàn bộ file vào RAM.
                await using var fileStream = new FileStream(
                    localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);

                var putRequest = new PutObjectRequest
                {
                    BucketName = _options.BucketName,
                    Key = objectKey,
                    InputStream = fileStream,
                    ContentType = NormalizeContentType(contentType),
                    // R2 không hỗ trợ aws-chunked streaming signature → unsigned payload.
                    DisablePayloadSigning = true
                };

                var response = await _s3Client.PutObjectAsync(putRequest, cancellationToken);
                var fileInfo = new FileInfo(localFilePath);
                _logger.LogInformation(
                    "Local file uploaded to Cloudflare R2: key='{Key}', source='{Source}', size={Size} bytes, status={StatusCode}",
                    objectKey, localFilePath, fileInfo.Length, response.HttpStatusCode);

                return new LocalFileUploadResult { ObjectKey = objectKey, UsedFallback = false };
            }
            catch (Exception ex)
            {
                var hint = BuildCredentialHint(ex);

                _logger.LogError(
                    ex,
                    "Failed to upload local file to Cloudflare R2. key='{Key}', source='{Source}', bucket='{Bucket}', endpoint='{Endpoint}', accessKeyId={KeyPrefix}. " +
                    "Check the R2 credentials (Access Key Id / Secret Access Key / Account Id) and the bucket name. Hint: {Hint}",
                    objectKey, localFilePath, _options.BucketName, _options.GetEffectiveServiceUrl(),
                    MaskKey(_options.AccessKeyId), hint);

                if (!_options.UseLocalStorageFallback)
                    throw;

                _logger.LogWarning(
                    "Falling back to local disk storage for key '{Key}'. Audio KHÔNG có trên R2 — chỉ nằm trên đĩa local.",
                    objectKey);
            }
            finally
            {
                UploadGate.Release();
            }
        }

        // Fallback: copy sang wwwroot/uploads/audio để vẫn phát lại được.
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio");
        Directory.CreateDirectory(uploadsDir);
        var fileName = Path.GetFileName(objectKey);
        if (string.IsNullOrWhiteSpace(fileName)) fileName = $"{DateTime.UtcNow.Ticks}.webm";
        var fallbackPath = Path.Combine(uploadsDir, fileName);
        File.Copy(localFilePath, fallbackPath, overwrite: true);

        _logger.LogInformation("Audio copied to local disk fallback: '{FilePath}'", fallbackPath);

        return new LocalFileUploadResult
        {
            ObjectKey = $"/uploads/audio/{fileName}",
            UsedFallback = true,
            ErrorMessage = _s3Client == null
                ? "Cloudflare R2 chưa được cấu hình (thiếu AccountId / AccessKeyId / SecretAccessKey)."
                : "Không thể upload lên Cloudflare R2.",
            Hint = "Kiểm tra CloudflareR2:AccountId, AccessKeyId, SecretAccessKey, BucketName trong appsettings.Development.json."
        };
    }

    /// <summary>
    /// Dựng gợi ý khắc phục dựa trên loại lỗi S3 — giúp vận hành không phải đoán mò.
    /// </summary>
    private static string BuildCredentialHint(Exception ex)
    {
        if (ex is AmazonS3Exception s3Ex)
        {
            return s3Ex.StatusCode switch
            {
                System.Net.HttpStatusCode.Forbidden =>
                    "Access Key Id / Secret Access Key sai, hoặc key không có quyền trên bucket này.",
                System.Net.HttpStatusCode.NotFound =>
                    "Bucket không tồn tại — kiểm tra lại BucketName.",
                System.Net.HttpStatusCode.BadRequest =>
                    "Endpoint/Account Id sai, hoặc request không hợp lệ với R2.",
                _ => "Kiểm tra Account Id (endpoint), quyền của R2 API Token và tên bucket."
            };
        }

        var msg = ex.Message;
        if (msg.Contains("length", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("credential", StringComparison.OrdinalIgnoreCase))
            return "Key sai định dạng hoặc không hợp lệ — copy lại Access Key Id / Secret Access Key từ Cloudflare Dashboard (S3 API Token).";

        return "Kiểm tra kết nối mạng tới endpoint R2 và Account Id.";
    }

    /// <inheritdoc/>
    public async Task<StorageHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var result = new StorageHealthResult
        {
            ServiceUrl = _options.GetEffectiveServiceUrl(),
            BucketName = _options.BucketName,
            AccessKeyIdPrefix = MaskKey(_options.AccessKeyId)
        };

        if (_s3Client == null)
        {
            result.IsHealthy = false;
            result.ErrorMessage = _options.IsConfigured
                ? "Không khởi tạo được S3 client cho Cloudflare R2."
                : "Cloudflare R2 chưa được cấu hình (thiếu AccountId / AccessKeyId / SecretAccessKey).";
            result.Hint = "Điền CloudflareR2:AccountId, AccessKeyId, SecretAccessKey, BucketName trong appsettings.Development.json.";
            return result;
        }

        try
        {
            // ListObjects với MaxKeys=1 — xác nhận credentials + bucket + endpoint đều đúng.
            await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _options.BucketName,
                MaxKeys = 1
            }, cancellationToken);

            result.IsHealthy = true;
            _logger.LogInformation(
                "Storage health check OK: bucket='{Bucket}', endpoint='{Endpoint}', accessKeyId={KeyPrefix}",
                _options.BucketName, result.ServiceUrl, result.AccessKeyIdPrefix);
        }
        catch (AmazonS3Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = ex.Message;
            result.Hint = ex.StatusCode == System.Net.HttpStatusCode.Forbidden
                ? "Access Key Id / Secret Access Key sai, hoặc key không có quyền trên bucket này."
                : ex.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Bucket không tồn tại — kiểm tra lại BucketName."
                    : "Kiểm tra Account Id (endpoint) và quyền của R2 API Token.";
            _logger.LogError(ex, "Storage health check FAILED: bucket='{Bucket}', endpoint='{Endpoint}'",
                _options.BucketName, result.ServiceUrl);
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.ErrorMessage = ex.Message;
            result.Hint = "Không kết nối được tới endpoint Cloudflare R2 — kiểm tra mạng và Account Id.";
            _logger.LogError(ex, "Storage health check FAILED with unexpected error");
        }

        return result;
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

    // -----------------------------------------------------------------------
    // S3 Multipart Upload
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<string> InitiateMultipartUploadAsync(
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (_s3Client == null)
            throw new InvalidOperationException("Cloudflare R2 chưa được cấu hình — không thể dùng multipart upload.");

        objectKey = objectKey.TrimStart('/');
        var request = new Amazon.S3.Model.InitiateMultipartUploadRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            ContentType = NormalizeContentType(contentType)
        };

        var response = await _s3Client.InitiateMultipartUploadAsync(request, cancellationToken);
        _logger.LogInformation("Multipart upload initiated: key='{Key}', uploadId='{UploadId}'",
            objectKey, response.UploadId);
        return response.UploadId;
    }

    /// <inheritdoc/>
    public async Task<string> UploadPartAsync(
        string objectKey,
        string uploadId,
        int partNumber,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (_s3Client == null)
            throw new InvalidOperationException("Cloudflare R2 chưa được cấu hình.");

        objectKey = objectKey.TrimStart('/');
        using var stream = new MemoryStream(data, writable: false);
        var request = new Amazon.S3.Model.UploadPartRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            UploadId = uploadId,
            PartNumber = partNumber,
            InputStream = stream,
            // Cloudflare R2 không implement aws-chunked streaming signature
            // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD). Chỉ endpoint HTTPS mới cho phép
            // unsigned payload → R2 endpoint luôn là HTTPS nên an toàn.
            DisablePayloadSigning = true
        };

        var response = await _s3Client.UploadPartAsync(request, cancellationToken);
        _logger.LogDebug("Part {PartNumber} uploaded: key='{Key}', ETag={ETag}",
            partNumber, objectKey, response.ETag);
        return response.ETag;
    }

    /// <inheritdoc/>
    public async Task<string> CompleteMultipartUploadAsync(
        string objectKey,
        string uploadId,
        IReadOnlyList<(int PartNumber, string ETag)> parts,
        CancellationToken cancellationToken = default)
    {
        if (_s3Client == null)
            throw new InvalidOperationException("Cloudflare R2 chưa được cấu hình.");

        objectKey = objectKey.TrimStart('/');
        var partEtags = parts
            .OrderBy(p => p.PartNumber)
            .Select(p => new Amazon.S3.Model.PartETag(p.PartNumber, p.ETag))
            .ToList();

        var request = new Amazon.S3.Model.CompleteMultipartUploadRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            UploadId = uploadId,
            PartETags = partEtags
        };

        var response = await _s3Client.CompleteMultipartUploadAsync(request, cancellationToken);
        _logger.LogInformation("Multipart upload completed: key='{Key}', ETag={ETag}",
            objectKey, response.ETag);
        return objectKey;
    }

    /// <inheritdoc/>
    public async Task AbortMultipartUploadAsync(
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default)
    {
        if (_s3Client == null) return;

        objectKey = objectKey.TrimStart('/');
        try
        {
            await _s3Client.AbortMultipartUploadAsync(new Amazon.S3.Model.AbortMultipartUploadRequest
            {
                BucketName = _options.BucketName,
                Key = objectKey,
                UploadId = uploadId
            }, cancellationToken);
            _logger.LogInformation("Multipart upload aborted: key='{Key}', uploadId='{UploadId}'",
                objectKey, uploadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to abort multipart upload for key='{Key}'", objectKey);
        }
    }

    /// <inheritdoc/>
    public async Task<byte[]?> DownloadAudioAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(objectKey)) return null;
        objectKey = objectKey.TrimStart('/');

        if (_s3Client != null)
        {
            try
            {
                using var response = await _s3Client.GetObjectAsync(
                    new Amazon.S3.Model.GetObjectRequest
                    {
                        BucketName = _options.BucketName,
                        Key = objectKey
                    }, cancellationToken);

                using var buffer = new MemoryStream();
                await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
                return buffer.ToArray();
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Audio object '{Key}' not found in R2", objectKey);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download audio '{Key}' from Cloudflare R2", objectKey);
                throw;
            }
        }

        // Local fallback: đọc từ wwwroot/uploads/audio
        var fileName = Path.GetFileName(objectKey);
        var localPath = Path.Combine(
            Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio", fileName);
        if (!File.Exists(localPath)) return null;

        return await File.ReadAllBytesAsync(localPath, cancellationToken);
    }

    /// <summary>
    /// Che bớt Access Key Id khi log — chỉ giữ 4 ký tự đầu.
    /// TUYỆT ĐỐI không log Secret Access Key.
    /// </summary>
    private static string MaskKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "(none)";
        return key.Length <= 4 ? "****" : key[..4] + "...";
    }
}
