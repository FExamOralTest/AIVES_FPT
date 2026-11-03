namespace Aives.Infrastructure.Storage;

/// <summary>
/// Cấu hình kết nối Cloudflare R2 (S3-compatible Object Storage).
/// </summary>
public class CloudflareR2Options
{
    public const string SectionName = "CloudflareR2";

    /// <summary>
    /// Cloudflare Account ID (tìm trong Cloudflare Dashboard > R2 Overview).
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// R2 API Token Access Key ID.
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// R2 API Token Secret Access Key.
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Tên Bucket lưu trữ audio (mặc định: aives-exam-audios).
    /// </summary>
    public string BucketName { get; set; } = "aives-exam-audios";

    /// <summary>
    /// URL Endpoint tùy chỉnh nếu không dùng định dạng mặc định (https://{AccountId}.r2.cloudflarestorage.com).
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// Tùy chọn Custom Domain công khai nếu có (ví dụ: https://audio.aives.edu.vn).
    /// </summary>
    public string? CustomDomain { get; set; }

    /// <summary>
    /// R2 Public Bucket URL / R2.dev subdomain (ví dụ: https://pub-xxx.r2.dev).
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Nếu chưa cấu hình R2 credentials ở local dev, tự động lưu vào wwwroot/uploads/audio mà không crash app.
    /// </summary>
    public bool UseLocalStorageFallback { get; set; } = true;

    /// <summary>
    /// Kiểm tra cấu hình R2 đã đầy đủ để khởi tạo S3 client chưa.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccessKeyId) &&
        !string.IsNullOrWhiteSpace(SecretAccessKey) &&
        (!string.IsNullOrWhiteSpace(AccountId) || !string.IsNullOrWhiteSpace(ServiceUrl));

    /// <summary>
    /// Lấy Endpoint URL hoàn chỉnh cho S3 Client.
    /// </summary>
    public string GetEffectiveServiceUrl()
    {
        if (!string.IsNullOrWhiteSpace(ServiceUrl))
            return ServiceUrl.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(AccountId))
            return $"https://{AccountId.Trim()}.r2.cloudflarestorage.com";

        return string.Empty;
    }
}
