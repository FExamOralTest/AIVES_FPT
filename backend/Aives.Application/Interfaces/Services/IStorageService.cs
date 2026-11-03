using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Aives.Application.Interfaces.Services;

/// <summary>
/// Giao diện dịch vụ lưu trữ Object Storage (S3-compatible như Cloudflare R2).
/// Quản lý việc tải lên, xóa và tạo Presigned URL cho các tệp âm thanh bài thi.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Tải tệp âm thanh lên Object Storage.
    /// </summary>
    /// <param name="objectKey">Đường dẫn khóa đối tượng (vd: exams/1/sessions/2/students/3/q_4_timestamp.webm)</param>
    /// <param name="content">Stream nội dung âm thanh</param>
    /// <param name="contentType">MIME type (vd: audio/webm, audio/wav)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Chuỗi định danh / Object Key hoặc URL đã lưu</returns>
    Task<string> UploadAudioAsync(
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tải một file ĐÃ NẰM SẴN TRÊN ĐĨA cục bộ lên Object Storage.
    ///
    /// Dùng cho luồng "local-first": server nhận các chunk từ client → gộp thành 1 file
    /// hoàn chỉnh tại C:\audio_review\... → rồi upload chính file local đó lên R2.
    ///
    /// Stream đọc trực tiếp từ đĩa (không buffer toàn bộ vào RAM) nên file lớn vẫn OK.
    /// </summary>
    /// <param name="objectKey">Đường dẫn khóa đối tượng trên R2</param>
    /// <param name="localFilePath">Đường dẫn file nguồn trên đĩa cục bộ</param>
    /// <param name="contentType">MIME type của file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Kết quả upload gồm object key đã lưu và cờ báo fallback</returns>
    Task<LocalFileUploadResult> UploadLocalFileAsync(
        string objectKey,
        string localFilePath,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra kết nối + credentials tới Object Storage.
    /// Dùng để chẩn đoán sớm lỗi "sai key / sai bucket / endpoint sai" trước khi học sinh thi.
    /// </summary>
    Task<StorageHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo Presigned URL có thời hạn (GET) để nghe lại file âm thanh bảo mật từ trình duyệt.
    /// </summary>
    /// <param name="objectKey">Object key trong bucket</param>
    /// <param name="duration">Thời hạn hiệu lực của URL</param>
    /// <returns>Presigned URL hoàn chỉnh</returns>
    string GeneratePresignedGetUrl(string objectKey, TimeSpan duration);

    /// <summary>
    /// Phân giải chuỗi audioKeyOrUrl thành URL có thể phát trực tiếp trên trình duyệt hoặc client.
    /// Hỗ trợ cả Object Key trên R2 (trả về Presigned URL), URL tuyệt đối (http/https),
    /// hoặc đường dẫn fallback cục bộ (/uploads/audio/...).
    /// </summary>
    /// <param name="audioKeyOrUrl">Giá trị lưu trong database (answers.audio_url)</param>
    /// <param name="duration">Thời gian hiệu lực (mặc định 1 giờ)</param>
    /// <returns>URL phát âm thanh hoặc null</returns>
    string? ResolvePlaybackUrl(string? audioKeyOrUrl, TimeSpan? duration = null);

    /// <summary>
    /// Xóa tệp âm thanh khỏi Object Storage.
    /// </summary>
    Task<bool> DeleteAudioAsync(string objectKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra tệp âm thanh có tồn tại trên storage hay không.
    /// </summary>
    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // S3 Multipart Upload — dùng cho chunked audio upload từ client
    // -----------------------------------------------------------------------

    /// <summary>
    /// Khởi tạo multipart upload session trên R2, trả về uploadId.
    /// Phải gọi trước khi upload bất kỳ chunk nào.
    /// </summary>
    Task<string> InitiateMultipartUploadAsync(
        string objectKey,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload một part (chunk) trong phiên multipart.
    /// partNumber bắt đầu từ 1 (S3 convention).
    /// Trả về ETag của part — cần lưu để gọi CompleteMultipartUploadAsync.
    /// </summary>
    Task<string> UploadPartAsync(
        string objectKey,
        string uploadId,
        int partNumber,
        byte[] data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hoàn thành multipart upload, gộp tất cả parts thành 1 object trên R2.
    /// parts: danh sách (partNumber, etag) theo thứ tự tăng dần.
    /// Trả về object key đã được gộp.
    /// </summary>
    Task<string> CompleteMultipartUploadAsync(
        string objectKey,
        string uploadId,
        IReadOnlyList<(int PartNumber, string ETag)> parts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy multipart upload đang dở — dọn dẹp parts đã upload trên R2.
    /// </summary>
    Task AbortMultipartUploadAsync(
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken = default);

    // -----------------------------------------------------------------------
    // Đọc audio từ storage — dùng cho pipeline STT chạy nền.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Tải nội dung một object từ R2 về memory.
    /// Dùng khi pipeline xử lý audio (STT) cần đọc lại file đã lưu trên storage.
    /// </summary>
    /// <param name="objectKey">Object key trong bucket</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Nội dung file dạng byte[], hoặc null nếu object không tồn tại</returns>
    Task<byte[]?> DownloadAudioAsync(
        string objectKey,
        CancellationToken cancellationToken = default);
}
