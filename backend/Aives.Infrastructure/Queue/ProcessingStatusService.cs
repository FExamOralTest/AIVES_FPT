using Aives.Application.Exams.Interfaces;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Read-only status queries for the answer/grading queues.
///
/// Nguồn dữ liệu được kết hợp có chủ đích:
/// <list type="bullet">
///   <item><b>DB</b> — trạng thái nghiệp vụ bền vững: <c>answers.status</c>,
///         <c>answers.attempt_count</c>, <c>answers.job_id</c>, <c>answers.error_message</c>,
///         <c>results.ai_evaluation_status</c>.</item>
///   <item><b>Hangfire</b> — trạng thái vận hành tức thời của job (Enqueued/Processing/Succeeded/
///         Failed/Deleted) để client biết job còn trong queue hay đang chạy.</item>
/// </list>
/// </summary>
public class ProcessingStatusService : IProcessingStatusService
{
    private readonly IAnswerRepository _answerRepository;
    private readonly IResultRepository _resultRepository;
    private readonly IQueueService _queueService;

    public ProcessingStatusService(
        IAnswerRepository answerRepository,
        IResultRepository resultRepository,
        IQueueService queueService)
    {
        _answerRepository = answerRepository;
        _resultRepository = resultRepository;
        _queueService = queueService;
    }

    public async Task<AnswerProcessingStatusDto> GetAnswerStatusAsync(
        int answerId,
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var answer = await _answerRepository.GetByIdWithDetailsAsync(answerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Answer {answerId} not found.");

        EnsureCanView(answer.Result, userId, role);

        return BuildAnswerStatus(answer, probeQueue: true);
    }

    public async Task<ResultGradingStatusDto> GetResultStatusAsync(
        int resultId,
        int userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException($"Result {resultId} not found.");

        EnsureCanView(result, userId, role);

        return BuildResultStatus(result);
    }

    public async Task<SubmissionStatusDto> GetSubmissionStatusAsync(
        int sessionId,
        int studentId,
        CancellationToken cancellationToken = default)
    {
        var result = await _resultRepository.GetBySessionAndStudentAsync(sessionId, studentId)
            ?? throw new KeyNotFoundException(
                "No submission was found for this session. Start the exam and submit at least one answer first.");

        var grading = BuildResultStatus(result);

        return new SubmissionStatusDto
        {
            SessionId = result.SessionId,
            StudentId = result.StudentId,
            ResultId = result.ResultId,
            SubmissionStatus = result.Status?.ToString() ?? string.Empty,
            AiEvaluationStatus = result.AiEvaluationStatus?.ToString() ?? string.Empty,
            Message = BuildSubmissionMessage(result),
            Grading = grading
        };
    }

    // -----------------------------------------------------------------------
    // Builders
    // -----------------------------------------------------------------------

    private AnswerProcessingStatusDto BuildAnswerStatus(Answer answer, bool probeQueue)
    {
        var jobState = string.IsNullOrWhiteSpace(answer.JobId)
            ? null
            : _queueService.GetJobState(answer.JobId);

        var jobStatus = jobState?.Status.ToString() ?? MapAnswerJobStatus(answer);
        var canRetry = answer.Status == AnswerStatus.ERROR
                       && !QueueLimits.IsExhausted(answer.AttemptCount)
                       && (!probeQueue || !_queueService.HasPendingOrProcessingJob(answer.AnswerId));

        return new AnswerProcessingStatusDto
        {
            AnswerId = answer.AnswerId,
            ResultId = answer.ResultId,
            QuestionId = answer.QuestionId,
            StudentId = answer.Result?.StudentId ?? 0,
            SessionId = answer.Result?.SessionId ?? 0,
            AnswerStatus = answer.Status.ToString(),
            JobStatus = jobStatus,
            HangfireState = jobState?.State,
            JobId = string.IsNullOrWhiteSpace(answer.JobId) ? null : answer.JobId,
            AttemptCount = answer.AttemptCount,
            MaxAttempts = QueueLimits.MaxTotalAttempts,
            CanRetry = canRetry,
            Error = answer.ErrorMessage ?? jobState?.Error,
            AnsweredAt = answer.AnsweredAt,
            JobCreatedAt = jobState?.CreatedAt,
            JobStateChangedAt = jobState?.StateChangedAt
        };
    }

    private ResultGradingStatusDto BuildResultStatus(Result result)
    {
        var gradingState = _queueService.GetSessionGradingState(result.ResultId);
        var answers = result.Answers
            .OrderBy(answer => answer.QuestionId)
            .ThenBy(answer => answer.AnswerId)
            .ToList();

        return new ResultGradingStatusDto
        {
            ResultId = result.ResultId,
            SessionId = result.SessionId,
            StudentId = result.StudentId,
            SubmissionStatus = result.Status?.ToString() ?? string.Empty,
            AiEvaluationStatus = result.AiEvaluationStatus?.ToString() ?? string.Empty,
            AiEvaluationError = result.AiEvaluationError,
            AiScore = result.AiScore,
            SubmittedAt = result.SubmittedAt,
            GradingJobId = gradingState?.JobId,
            GradingJobStatus = gradingState?.Status.ToString() ?? MapResultJobStatus(result),
            HangfireState = gradingState?.State,
            JobCreatedAt = gradingState?.CreatedAt,
            JobStateChangedAt = gradingState?.StateChangedAt,
            AnswerCount = answers.Count,
            EvaluatedAnswerCount = answers.Count(answer => answer.Status == AnswerStatus.EVALUATED),
            FailedAnswerCount = answers.Count(answer => answer.Status == AnswerStatus.ERROR),
            Answers = answers.Select(answer => new AnswerStatusSummaryDto
            {
                AnswerId = answer.AnswerId,
                QuestionId = answer.QuestionId,
                AnswerStatus = answer.Status.ToString(),
                JobStatus = MapAnswerJobStatus(answer),
                AttemptCount = answer.AttemptCount,
                // Tóm tắt không probe Hangfire cho từng answer (tránh N query mỗi lần poll).
                // Endpoint /api/answers/{answerId}/processing-status trả chi tiết + probe.
                CanRetry = answer.Status == AnswerStatus.ERROR
                           && !QueueLimits.IsExhausted(answer.AttemptCount),
                Error = answer.ErrorMessage
            }).ToList()
        };
    }

    private static string MapAnswerJobStatus(Answer answer)
    {
        if (answer.Status == AnswerStatus.EVALUATED)
            return QueueJobStatus.Completed.ToString();

        if (answer.Status == AnswerStatus.ERROR)
            return QueueLimits.IsExhausted(answer.AttemptCount)
                ? QueueJobStatus.DeadLetter.ToString()
                : QueueJobStatus.Failed.ToString();

        // PROCESSING: chưa có attempt nào = còn chờ worker; đã có attempt = đã/đang chạy.
        return QueueJobStatus.Pending.ToString();
    }

    private static string MapResultJobStatus(Result result) =>
        result.AiEvaluationStatus switch
        {
            AiEvaluationStatus.SUCCEEDED => QueueJobStatus.Completed.ToString(),
            AiEvaluationStatus.FAILED => QueueJobStatus.Failed.ToString(),
            AiEvaluationStatus.PENDING => QueueJobStatus.Pending.ToString(),
            _ => "NotQueued"
        };

    private static string BuildSubmissionMessage(Result result) =>
        result.AiEvaluationStatus switch
        {
            AiEvaluationStatus.SUCCEEDED =>
                "AI đã chấm xong bài thi. Vui lòng đợi giảng viên duyệt và công bố kết quả.",
            AiEvaluationStatus.FAILED =>
                "AI chưa chấm được bài thi. Giảng viên có thể chấm thủ công hoặc thử lại sau.",
            AiEvaluationStatus.PENDING =>
                "Bài thi đang chờ chấm. Hệ thống chấm lần lượt từng bài để tránh quá tải.",
            _ => "Chưa có thông tin chấm bài cho ca thi này."
        };

    // -----------------------------------------------------------------------
    // Authorization
    // -----------------------------------------------------------------------

    /// <summary>
    /// Cùng quy tắc với các màn hình review hiện có: sinh viên chỉ xem bài của mình,
    /// staff xem session mình phụ trách, examiner/admin xem toàn hệ thống.
    /// </summary>
    private static void EnsureCanView(Result? result, int userId, UserRole role)
    {
        if (result is null)
            throw new KeyNotFoundException("The answer is not linked to a result.");

        if (role is UserRole.ADMIN or UserRole.EXAMINER)
            return;

        if (role == UserRole.STUDENT && result.StudentId == userId)
            return;

        if (role == UserRole.STAFF && result.Session?.ScheduledBy == userId)
            return;

        throw new UnauthorizedAccessException(
            "You do not have permission to view the processing status of this submission.");
    }
}
