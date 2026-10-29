using Microsoft.AspNetCore.Http;

namespace Aives.Application.Queue.DTOs;

// -----------------------------------------------------------------------
// Init
// -----------------------------------------------------------------------

public class InitChunkedUploadRequest
{
    /// <summary>Client tự sinh CUID/GUID — dùng làm uploadId.</summary>
    public string UploadId { get; set; } = string.Empty;
    public int QuestionId { get; set; }
    public int SessionId { get; set; }
    public string FileName { get; set; } = "recording.webm";
    public string ContentType { get; set; } = "audio/webm";
    /// <summary>Tổng kích thước file (bytes) — client ước tính hoặc biết trước.</summary>
    public long TotalSize { get; set; }
    /// <summary>Kích thước mỗi chunk (bytes). Mặc định 5 MB nếu không truyền.</summary>
    public long ChunkSize { get; set; } = 5 * 1024 * 1024;
    /// <summary>Tổng số chunk.</summary>
    public int TotalChunks { get; set; }
}

public class InitChunkedUploadResponse
{
    public string UploadId { get; set; } = string.Empty;
    /// <summary>Danh sách chunkIndex đã nhận (dùng cho resume).</summary>
    public List<int> ReceivedChunks { get; set; } = new();
    public string Status { get; set; } = "PENDING";
    public string? LocalFilePath { get; set; }
}

// -----------------------------------------------------------------------
// Upload Chunk
// -----------------------------------------------------------------------

public class UploadChunkRequest
{
    public string UploadId { get; set; } = string.Empty;
    /// <summary>Index 0-based của chunk này.</summary>
    public int ChunkIndex { get; set; }
    /// <summary>Nội dung chunk dạng binary.</summary>
    public IFormFile ChunkData { get; set; } = null!;
}

public class UploadChunkResponse
{
    public string UploadId { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public List<int> ReceivedChunks { get; set; } = new();
    public int TotalChunks { get; set; }
    public bool IsComplete { get; set; }
}

// -----------------------------------------------------------------------
// Complete
// -----------------------------------------------------------------------

public class CompleteChunkedUploadRequest
{
    public string UploadId { get; set; } = string.Empty;

    /// <summary>
    /// answerId để gán audio vào Answer sau khi upload xong.
    /// Không bắt buộc — Answer thường được tạo ở bước submit-answer sau đó.
    /// </summary>
    public int AnswerId { get; set; }
}

public class CompleteChunkedUploadResponse
{
    public string UploadId { get; set; } = string.Empty;

    /// <summary>answerId đã gắn (0 nếu chưa có Answer nào được tạo).</summary>
    public int AnswerId { get; set; }

    /// <summary>Object key trên R2 sau khi gộp — dùng để gửi kèm submit-answer.</summary>
    public string AudioKey { get; set; } = string.Empty;

    public string? JobId { get; set; }
    public string Status { get; set; } = "COMPLETED";
    public DateTime CompletedAt { get; set; }
}

// -----------------------------------------------------------------------
// Abort
// -----------------------------------------------------------------------

public class AbortChunkedUploadResponse
{
    public string UploadId { get; set; } = string.Empty;
    public string Status { get; set; } = "ABORTED";
}
