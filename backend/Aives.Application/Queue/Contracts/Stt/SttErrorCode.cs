namespace Aives.Application.Queue.Contracts.Stt;

/// <summary>
/// Mã lỗi chuẩn hoá từ STT (Faster-Whisper) — dùng để quyết định retry/dead-letter.
/// Phải đồng bộ với AI1 team; nếu họ đổi code phía họ, mapper sẽ map về enum này.
/// </summary>
public enum SttErrorCode
{
    /// <summary>Lỗi không xác định — mặc định, retry được.</summary>
    Unknown = 0,

    /// <summary>File audio hỏng, header sai, không decode được. KHÔNG retry.</summary>
    AudioCorrupted = 1,

    /// <summary>Whisper chạy xong nhưng transcript rỗng (im lặng / câm). KHÔNG retry.</summary>
    EmptyTranscript = 2,

    /// <summary>Audio quá dài hoặc sample rate không hỗ trợ. KHÔNG retry.</summary>
    UnsupportedFormat = 3,

    /// <summary>AI1 xử lý quá thời gian cho phép. CÓ retry.</summary>
    Timeout = 4,

    /// <summary>Lỗi nội bộ phía AI1 (GPU OOM, model load fail…). CÓ retry.</summary>
    InternalError = 5
}
