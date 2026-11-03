namespace Aives.Application.Interfaces.Services;

/// <summary>
/// Kết quả upload một file local lên Object Storage.
///
/// Nếu <see cref="UsedFallback"/> = true thì R2 đã lỗi (sai credentials / mạng / bucket)
/// và file chỉ được lưu ở thư mục local của server. Backend PHẢI trả cảnh báo này
/// về client để học sinh/giáo viên biết audio chưa nằm trên R2.
/// </summary>
public class LocalFileUploadResult
{
    /// <summary>Object key trên storage (R2 key, hoặc đường dẫn /uploads/audio/... nếu fallback).</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>True nếu upload lên R2 thất bại và đã lưu fallback xuống đĩa local.</summary>
    public bool UsedFallback { get; set; }

    /// <summary>Thông báo lỗi gốc từ R2 (đã che secret) — dùng để hiển thị/log.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gợi ý cách khắc phục cho người vận hành.</summary>
    public string? Hint { get; set; }
}
