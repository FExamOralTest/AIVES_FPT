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
}
