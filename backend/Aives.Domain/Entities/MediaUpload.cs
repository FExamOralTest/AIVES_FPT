using System.Collections.Generic;

namespace Aives.Domain.Entities;

/// <summary>
/// Theo dõi phiên upload audio chunked của học sinh.
/// Mỗi bản ghi đại diện cho 1 lần upload (init → upload chunks → complete).
/// DB schema: bảng media_uploads (đã có migration).
/// </summary>
public class MediaUpload
{
    /// <summary>CUID/GUID do client sinh ra khi init upload.</summary>
    public string UploadId { get; set; } = string.Empty;

    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public int QuestionId { get; set; }

    /// <summary>Gán sau khi complete — FK tới answers.answer_id.</summary>
    public int? AnswerId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Tổng kích thước file (bytes) — học sinh khai báo khi init.</summary>
    public long TotalSize { get; set; }

    /// <summary>Kích thước mỗi chunk (bytes).</summary>
    public long ChunkSize { get; set; }

    /// <summary>Tổng số chunk — ceil(TotalSize / ChunkSize).</summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Danh sách index chunk đã nhận thành công, lưu dưới dạng JSONB.
    /// Dùng để hỗ trợ resume sau khi rớt mạng.
    /// </summary>
    public List<int> ReceivedChunkIndexes { get; set; } = new();

    /// <summary>PENDING | UPLOADING | COMPLETED | FAILED | ABORTED</summary>
    public string UploadStatus { get; set; } = "PENDING";

    /// <summary>
    /// UploadId của phiên multipart trên Cloudflare R2 (S3 multipart uploadId).
    /// CHỈ dùng cho các phiên tạo trước khi chuyển sang luồng local-first.
    /// Luồng hiện tại (local-first) gộp file local rồi PutObject 1 lần → không có multipart.
    /// </summary>
    public string? R2UploadId { get; set; }

    /// <summary>
    /// ETag của từng part đã upload (JSON). Không dùng trong luồng local-first.
    /// Format JSON: [{"p":1,"e":"\"etag1\""},{"p":2,"e":"\"etag2\""}]
    /// </summary>
    public string? PartETags { get; set; }

    /// <summary>
    /// R2 object key sau khi upload thành công.
    /// Format: exams/{examId}/sessions/{sessionId}/students/{studentId}/q_{questionId}.webm
    /// </summary>
    public string? ObjectKey { get; set; }

    /// <summary>
    /// Đường dẫn file local trên máy server (Windows).
    /// Format: C:\audio_review\{studentId}\{courseName}\{questionId}\q_{questionId}.webm
    ///
    /// Luồng local-first: file này được tạo BẰNG CÁCH GỘP các chunk nhận từ client,
    /// rồi chính file này được upload lên R2. File được GIỮ LẠI sau khi upload
    /// thành công (dùng để giáo viên review offline + backup).
    /// </summary>
    public string? LocalFilePath { get; set; }

    /// <summary>Lỗi gần nhất (message ngắn) để debug.</summary>
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
