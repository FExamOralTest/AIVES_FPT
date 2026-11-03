using Aives.Application.Queue.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Service xử lý flow upload audio theo chunks:
/// Init → UploadChunk (N lần) → Complete → enqueue STT job.
///
/// Flow:
/// 1. Client gọi Init để tạo phiên upload, nhận uploadId + path local.
/// 2. Client chia file audio thành chunks, gửi từng chunk qua UploadChunk.
///    - Mỗi chunk được lưu vào C:\audio_review\{studentId}\{courseName}\{questionId}\
///    - Đồng thời upload part lên R2 multipart session.
/// 3. Khi tất cả chunks đã nhận, client gọi Complete.
///    - Backend complete multipart trên R2 → 1 file hoàn chỉnh.
///    - Sau khi R2 xác nhận, xóa chunk files local.
///    - Gán audio_key vào Answer, enqueue Hangfire STT job.
/// </summary>
public interface IChunkedAudioUploadService
{
    /// <summary>
    /// Khởi tạo phiên upload. Nếu đã có phiên PENDING/UPLOADING cho cùng (sessionId, studentId, questionId)
    /// thì trả về phiên cũ để hỗ trợ resume sau rớt mạng.
    /// </summary>
    Task<InitChunkedUploadResponse> InitAsync(
        InitChunkedUploadRequest request,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken ct = default);

    /// <summary>
    /// Nhận và xử lý 1 chunk. Idempotent — có thể gửi lại cùng chunkIndex nếu retry.
    /// </summary>
    Task<UploadChunkResponse> UploadChunkAsync(
        UploadChunkRequest request,
        byte[] chunkBytes,
        int currentUserId,
        CancellationToken ct = default);

    /// <summary>
    /// Gộp tất cả chunks thành 1 file hoàn chỉnh trên R2, dọn dẹp local, enqueue STT job.
    /// </summary>
    Task<CompleteChunkedUploadResponse> CompleteAsync(
        CompleteChunkedUploadRequest request,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken ct = default);

    /// <summary>Hủy phiên upload, abort R2 multipart, xóa files local.</summary>
    Task<AbortChunkedUploadResponse> AbortAsync(
        string uploadId,
        int currentUserId,
        CancellationToken ct = default);
}
