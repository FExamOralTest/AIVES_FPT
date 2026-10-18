using Aives.Application.Queue.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Service trả về URL audio có thể phát trên browser.
/// Validate ownership trước khi cấp URL — chống leo thang đặc quyền.
/// </summary>
public interface IAudioPlaybackService
{
    /// <summary>
    /// Lấy presigned URL cho audio của 1 answer.
    /// Throw <c>KeyNotFoundException</c> nếu không tìm thấy,
    /// <c>UnauthorizedAccessException</c> nếu user không có quyền nghe audio này.
    /// </summary>
    Task<AudioPlaybackResponse> GetPlaybackUrlAsync(
        int answerId,
        int? currentUserId,
        UserRole? currentUserRole,
        TimeSpan? urlDuration = null,
        CancellationToken cancellationToken = default);
}
