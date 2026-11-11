using Aives.Application.Exams.Interfaces;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Queue;
using Moq;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Read-only status APIs — nguồn dữ liệu kết hợp DB + Hangfire, kèm phân quyền.
/// </summary>
public class ProcessingStatusServiceTests
{
    private readonly Mock<IAnswerRepository> _answers = new();
    private readonly Mock<IResultRepository> _results = new();
    private readonly Mock<IQueueService> _queue = new();

    private ProcessingStatusService CreateService() =>
        new(_answers.Object, _results.Object, _queue.Object);

    // ===================================================================
    // Answer status
    // ===================================================================

    [Fact]
    public async Task A1_StudentCannotViewAnotherStudentsAnswer()
    {
        var answer = OwnedAnswer(1, studentId: 5001, sessionId: 300);
        _answers.Setup(repo => repo.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetAnswerStatusAsync(1, userId: 5002, UserRole.STUDENT));
    }

    [Fact]
    public async Task A2_ExhaustedErrorAnswer_ReportsDeadLetterWithoutRetry()
    {
        var answer = OwnedAnswer(1, studentId: 5001, sessionId: 300);
        answer.Status = AnswerStatus.ERROR;
        answer.AttemptCount = QueueLimits.MaxTotalAttempts;
        answer.ErrorMessage = "Audio corrupted.";
        answer.JobId = "job-1";

        _answers.Setup(repo => repo.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);
        _queue.Setup(queue => queue.GetJobState("job-1"))
            .Returns(new QueueJobStateDto
            {
                JobId = "job-1",
                State = "Deleted",
                Status = QueueJobStatus.DeadLetter,
                Error = "Audio corrupted."
            });
        _queue.Setup(queue => queue.HasPendingOrProcessingJob(1)).Returns(false);

        var status = await CreateService().GetAnswerStatusAsync(1, 5001, UserRole.STUDENT);

        Assert.Equal("DeadLetter", status.JobStatus);
        Assert.Equal("Deleted", status.HangfireState);
        Assert.Equal(QueueLimits.MaxTotalAttempts, status.AttemptCount);
        Assert.False(status.CanRetry);
        Assert.Equal("Audio corrupted.", status.Error);
        Assert.Equal(5001, status.StudentId);
        Assert.Equal(300, status.SessionId);
    }

    [Fact]
    public async Task A3_EvaluatedAnswer_ReportsCompleted()
    {
        var answer = OwnedAnswer(1, studentId: 5001, sessionId: 300);
        answer.Status = AnswerStatus.EVALUATED;
        answer.AttemptCount = 1;

        _answers.Setup(repo => repo.GetByIdWithDetailsAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

        var status = await CreateService().GetAnswerStatusAsync(1, 5001, UserRole.STUDENT);

        Assert.Equal("Completed", status.JobStatus);
        Assert.False(status.CanRetry);
    }

    [Fact]
    public async Task A4_MissingAnswer_ThrowsKeyNotFound()
    {
        _answers.Setup(repo => repo.GetByIdWithDetailsAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Answer?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateService().GetAnswerStatusAsync(99, 5001, UserRole.STUDENT));
    }

    // ===================================================================
    // Result grading status
    // ===================================================================

    [Fact]
    public async Task B1_ResultStatus_AggregatesAnswerCountsAndQueueState()
    {
        var result = new Result
        {
            ResultId = 12,
            SessionId = 300,
            StudentId = 5001,
            Status = SubmissionStatus.PENDING_REVIEW,
            AiEvaluationStatus = AiEvaluationStatus.PENDING,
            SubmittedAt = DateTime.UtcNow
        };
        result.Answers.Add(AnswerFor(result, 1, AnswerStatus.EVALUATED, attempt: 1));
        result.Answers.Add(AnswerFor(result, 2, AnswerStatus.ERROR, attempt: 2));
        result.Answers.Add(AnswerFor(result, 3, AnswerStatus.PROCESSING, attempt: 0));

        _results.Setup(repo => repo.GetByIdWithDetailsAsync(12)).ReturnsAsync(result);
        _queue.Setup(queue => queue.GetSessionGradingState(12))
            .Returns(new QueueJobStateDto
            {
                JobId = "grade-12",
                State = "Enqueued",
                Status = QueueJobStatus.Pending
            });

        var status = await CreateService().GetResultStatusAsync(12, 5001, UserRole.STUDENT);

        Assert.Equal("Pending", status.GradingJobStatus);
        Assert.Equal("Enqueued", status.HangfireState);
        Assert.Equal(3, status.AnswerCount);
        Assert.Equal(1, status.EvaluatedAnswerCount);
        Assert.Equal(1, status.FailedAnswerCount);
        Assert.Equal(3, status.Answers.Count);
        // Answers được sắp theo QuestionId.
        Assert.Equal(new[] { 1, 2, 3 }, status.Answers.Select(answer => answer.QuestionId));
    }

    // ===================================================================
    // Student submission status
    // ===================================================================

    [Fact]
    public async Task C1_SubmissionStatus_ReturnsResultOfCurrentStudent()
    {
        var result = new Result
        {
            ResultId = 12,
            SessionId = 300,
            StudentId = 5001,
            Status = SubmissionStatus.PENDING_REVIEW,
            AiEvaluationStatus = AiEvaluationStatus.SUCCEEDED,
            SubmittedAt = DateTime.UtcNow
        };

        _results.Setup(repo => repo.GetBySessionAndStudentAsync(300, 5001)).ReturnsAsync(result);
        _queue.Setup(queue => queue.GetSessionGradingState(12)).Returns((QueueJobStateDto?)null);

        var status = await CreateService().GetSubmissionStatusAsync(300, 5001);

        Assert.Equal(12, status.ResultId);
        Assert.Equal(5001, status.StudentId);
        Assert.Equal("PENDING_REVIEW", status.SubmissionStatus);
        Assert.Equal("SUCCEEDED", status.AiEvaluationStatus);
        Assert.Contains("giảng viên duyệt", status.Message);
        Assert.Equal("Completed", status.Grading.GradingJobStatus);
    }

    [Fact]
    public async Task C2_SubmissionStatus_NoResultForStudent_ThrowsKeyNotFound()
    {
        _results.Setup(repo => repo.GetBySessionAndStudentAsync(300, 5002))
            .ReturnsAsync((Result?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            CreateService().GetSubmissionStatusAsync(300, 5002));
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static Answer OwnedAnswer(int answerId, int studentId, int sessionId)
    {
        var result = new Result
        {
            ResultId = 12,
            StudentId = studentId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW
        };

        return new Answer
        {
            AnswerId = answerId,
            ResultId = result.ResultId,
            QuestionId = answerId,
            Result = result
        };
    }

    private static Answer AnswerFor(
        Result result, int questionId, AnswerStatus status, int attempt)
    {
        var answer = new Answer
        {
            AnswerId = 100 + questionId,
            ResultId = result.ResultId,
            QuestionId = questionId,
            Status = status,
            AttemptCount = attempt,
            Result = result
        };
        return answer;
    }
}
