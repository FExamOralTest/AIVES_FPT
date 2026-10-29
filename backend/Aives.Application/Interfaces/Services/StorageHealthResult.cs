namespace Aives.Application.Interfaces.Services;

/// <summary>
/// Kết quả kiểm tra kết nối Object Storage — dùng để chẩn đoán cấu hình
/// (sai Access Key, sai Secret Key, sai Account Id, sai bucket, endpoint sai).
/// </summary>
public class StorageHealthResult
{
    /// <summary>True khi kết nối + credentials hợp lệ (đã list được bucket).</summary>
    public bool IsHealthy { get; set; }

    /// <summary>Endpoint đang dùng (không chứa credentials).</summary>
    public string? ServiceUrl { get; set; }

    /// <summary>Bucket đang dùng.</summary>
    public string? BucketName { get; set; }

    /// <summary>4 ký tự đầu của Access Key Id để đối chiếu (KHÔNG log full key).</summary>
    public string? AccessKeyIdPrefix { get; set; }

    /// <summary>Thông báo lỗi thân thiện — đã che mọi secret.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gợi ý cách khắc phục.</summary>
    public string? Hint { get; set; }
}
