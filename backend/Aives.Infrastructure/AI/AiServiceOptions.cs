namespace Aives.Infrastructure.AI;

/// <summary>
/// Cấu hình HTTP client gọi AI service (Faster-Whisper STT, evaluation, follow-up).
/// Backend gọi AI như 1 internal microservice — KHÔNG expose AI endpoint ra ngoài.
/// </summary>
/// <remarks>
/// Mọi request từ Backend → AI PHẢI mang <c>X-Internal-Token</c> header để AI verify
/// request đến từ backend AIVES chứ không phải attacker gọi trực tiếp.
/// Token được share qua env var / user-secrets, KHÔNG commit vào git.
/// </remarks>
public class AiServiceOptions
{
    public const string SectionName = "AI";

    /// <summary>Base URL của AI service, vd: <c>http://localhost:8000</c>.</summary>
    public string BaseUrl { get; set; } = "http://localhost:8000";

    /// <summary>Timeout cho STT/Eval call (giây). Mặc định 5 phút.</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Shared secret gửi qua header <c>X-Internal-Token</c>.
    /// Nếu trống → throw lúc startup (bắt buộc cho production, dev warning).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Header name cho internal auth (đặt cố định để AI service dễ đọc).</summary>
    public const string TokenHeaderName = "X-Internal-Token";

    /// <summary>Đã cấu hình token đủ dùng cho internal auth chưa.</summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKey);
}
