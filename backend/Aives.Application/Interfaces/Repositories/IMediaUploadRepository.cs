using Aives.Domain.Entities;

namespace Aives.Application.Interfaces.Repositories;

/// <summary>
/// Repository cho bảng media_uploads — theo dõi phiên upload chunked audio.
/// </summary>
public interface IMediaUploadRepository
{
    Task<MediaUpload?> GetByIdAsync(string uploadId, CancellationToken ct = default);

    /// <summary>Lấy upload đang PENDING hoặc UPLOADING cho (session, student, question) — để resume.</summary>
    Task<MediaUpload?> GetActiveAsync(int sessionId, int studentId, int questionId, CancellationToken ct = default);

    Task<MediaUpload> CreateAsync(MediaUpload upload, CancellationToken ct = default);

    Task UpdateAsync(MediaUpload upload, CancellationToken ct = default);

    /// <summary>Đánh dấu thêm chunkIndex đã nhận thành công.</summary>
    Task MarkChunkReceivedAsync(string uploadId, int chunkIndex, CancellationToken ct = default);

    /// <summary>
    /// Gắn answerId vào phiên upload sau khi Answer được tạo (liên kết 2 chiều để audit).
    /// </summary>
    Task LinkAnswerAsync(string uploadId, int answerId, CancellationToken ct = default);

    /// <summary>
    /// Lấy phiên upload đã COMPLETED theo (session, student, question).
    /// Dùng để tra cứu audio đã upload mà không cần client gửi uploadId.
    /// </summary>
    Task<Domain.Entities.MediaUpload?> GetCompletedAsync(
        int sessionId, int studentId, int questionId, CancellationToken ct = default);

    /// <summary>
    /// Lấy phiên upload đã COMPLETED theo object key trên R2.
    /// Fallback cho client cũ gửi objectKey thay vì uploadId.
    /// </summary>
    Task<Domain.Entities.MediaUpload?> GetCompletedByObjectKeyAsync(
        string objectKey, CancellationToken ct = default);

    /// <summary>Xóa các bản ghi FAILED/ABORTED cũ hơn N ngày (cleanup job).</summary>
    Task DeleteStaleAsync(TimeSpan olderThan, CancellationToken ct = default);
}
