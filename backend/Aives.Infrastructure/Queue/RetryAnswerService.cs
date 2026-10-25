using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Implementation của <see cref="IRetryAnswerService"/>.
/// </summary>
public class RetryAnswerService : IRetryAnswerService
{
    /// <summary>
    /// Ngưỡng tối đa để coi 1 PROCESSING là "orphan" (job worker chết mà không update).
    /// Sau ngưỡng này, retry sẽ chấp nhận cả PROCESSING.
    /// </summary>
    private static readonly TimeSpan OrphanProcessingThreshold = TimeSpan.FromMinutes(30);

    private readonly IAnswerRepository _answerRepository;
    private readonly IQueueService _queueService;
    private readonly ILogger<RetryAnswerService> _logger;

    public RetryAnswerService(
        IAnswerRepository answerRepository,
        IQueueService queueService,
        ILogger<RetryAnswerService> logger)
    {
        _answerRepository = answerRepository;
        _queueService = queueService;
        _logger = logger;
    }

    public async Task<RetryAnswerResponse> RetryAsync(
        int answerId,
        int? currentUserId,
        UserRole? currentUserRole,
        RetryAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId is null || currentUserRole is null)
            throw new UnauthorizedAccessException("Authentication required.");

        // 1. Load answer với session + exam để check quyền.
        var answer = await _answerRepository.GetByIdWithDetailsAsync(answerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Answer {answerId} not found.");

        var result = answer.Result;
        if (result == null)
            throw new KeyNotFoundException($"Result for Answer {answerId} not found.");

        // 2. Phân quyền.
        EnsureCanRetry(answer, result, currentUserId.Value, currentUserRole.Value);

        // 3. Dedup check TRƯỚC status check: queue là source of truth về "đã có job chưa".
        // Nếu user spam retry khi job đang chạy, trả ALREADY_QUEUED thay vì throw lỗi khó hiểu.
        if (_queueService.HasPendingOrProcessingJob(answerId))
        {
            _logger.LogInformation(
                "Retry skipped for Answer {AnswerId}: a job is already in flight.", answerId);
            return new RetryAnswerResponse
            {
                AnswerId = answerId,
                PreviousStatus = answer.Status.ToString(),
                Status = "ALREADY_QUEUED",
                EnqueuedAt = DateTime.UtcNow
            };
        }

        // 4. Kiểm tra status có retry được không.
        var previousStatus = answer.Status;
        var isOrphan = previousStatus == AnswerStatus.PROCESSING
                       && answer.AnsweredAt is DateTime answered
                       && DateTime.UtcNow - answered > OrphanProcessingThreshold;

        if (previousStatus == AnswerStatus.EVALUATED)
        {
            throw new InvalidOperationException(
                "Answer is already evaluated. Use teacher review to adjust the score instead of retrying.");
        }

        if (previousStatus == AnswerStatus.PROCESSING && !isOrphan)
        {
            throw new InvalidOperationException(
                "Answer is currently being processed. Wait for the in-flight job to finish or use the dashboard to inspect progress.");
        }

        // 4.1 BR-17 — giới hạn "retry tối đa 3 lần thử tổng cộng".
        // AttemptCount là bộ đếm bền vững (không bị reset trong ResetForRetryAsync),
        // nên retry thủ công cũng phải nằm trong cùng ngân sách 3 lần thử.
        if (QueueLimits.IsExhausted(answer.AttemptCount))
        {
            throw new InvalidOperationException(
                $"Answer has already been attempted {answer.AttemptCount} time(s). " +
                $"Maximum is {QueueLimits.MaxTotalAttempts} attempts (BR-17).");
        }

        // 5. Reset answer fields (giữ nguyên AttemptCount — xem IAnswerRepository.ResetForRetryAsync).
        await _answerRepository.ResetForRetryAsync(answerId, cancellationToken);

        // 6. Enqueue job mới. Attempt trong metadata = lần thử tiếp theo (1-based).
        var objectKey = answer.ObjectKey ?? answer.AudioUrl ?? string.Empty;
        var metadata = QueueJobContract.Create(
            answerId,
            answer.ResultId,
            objectKey,
            answer.AttemptCount + 1);
        var jobId = _queueService.EnqueueAnswerProcessing(metadata);

        if (string.IsNullOrEmpty(jobId))
        {
            // Dedup thắng race → trả về response idempotent.
            return new RetryAnswerResponse
            {
                AnswerId = answerId,
                PreviousStatus = previousStatus.ToString(),
                Status = "ALREADY_QUEUED",
                EnqueuedAt = DateTime.UtcNow
            };
        }

        // Lưu JobId vào DB để tra cứu job không chỉ từ Hangfire dashboard.
        await _answerRepository.SetJobIdAsync(answerId, jobId, cancellationToken);

        _logger.LogInformation(
            "Retry enqueued: AnswerId={AnswerId}, JobId={JobId}, UserId={UserId}, Reason={Reason}",
            answerId, jobId, currentUserId, request.Reason ?? "(none)");

        return new RetryAnswerResponse
        {
            AnswerId = answerId,
            JobId = jobId,
            PreviousStatus = previousStatus.ToString(),
            Status = "QUEUED",
            EnqueuedAt = DateTime.UtcNow
        };
    }

    private static void EnsureCanRetry(
        Answer answer, Result result, int userId, UserRole role)
    {
        switch (role)
        {
            case UserRole.STUDENT:
                // Student chỉ retry được answer của mình.
                if (result.StudentId != userId)
                    throw new UnauthorizedAccessException(
                        "You can only retry answers for your own submissions.");
                break;

            case UserRole.STAFF:
            case UserRole.EXAMINER:
                // Staff/Examiner retry được answer thuộc session do mình tạo (scheduledBy).
                if (result.Session?.ScheduledBy != userId)
                    throw new UnauthorizedAccessException(
                        "You can only retry answers for sessions you have scheduled.");
                break;

            case UserRole.ADMIN:
                // Admin: tất cả.
                break;

            default:
                throw new UnauthorizedAccessException(
                    $"Role {role} is not allowed to retry answers.");
        }
    }
}
