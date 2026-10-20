using Aives.Application.Queue.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Queue.Interfaces;

/// <summary>
/// Service retry answer: re-enqueue Hangfire job cho answer đã failed.
/// Phân quyền:
/// <list type="bullet">
///   <item>STUDENT: chỉ retry được answer của chính mình.</item>
///   <item>STAFF/EXAMINER: retry được answer thuộc session họ phụ trách (exam.ScheduledBy).</item>
///   <item>ADMIN: retry tất cả.</item>
/// </list>
/// </summary>
public interface IRetryAnswerService
{
    /// <summary>
    /// Retry 1 answer. Trả về response với JobId, throw <c>KeyNotFoundException</c> nếu answer không tồn tại,
    /// <c>UnauthorizedAccessException</c> nếu không đủ quyền, <c>InvalidOperationException</c> nếu answer
    /// không thể retry (vd: đang PROCESSING fresh, status EVALUATED đã final).
    /// </summary>
    Task<RetryAnswerResponse> RetryAsync(
        int answerId,
        int? currentUserId,
        UserRole? currentUserRole,
        RetryAnswerRequest request,
        CancellationToken cancellationToken = default);
}
