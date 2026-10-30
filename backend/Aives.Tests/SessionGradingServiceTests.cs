using Aives.Application.Exams.Interfaces;
using Aives.Application.Queue.Interfaces;
using Aives.Application.Viva.DTOs;
using Aives.Application.Viva.Services;
using Aives.Application.AI.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Aives.Tests;

public class SessionGradingServiceTests
{
    [Fact]
    public async Task FinishSessionAsync_ResetsOldAnswerStatusUntilNewGradingCompletes()
    {
        var answer = new Answer
        {
            AnswerId = 1,
            Status = AnswerStatus.EVALUATED,
            AiScore = 4,
            AiFeedback = "Old feedback",
            AiAnalysis = """{"schemaVersion":"2.0"}"""
        };
        var result = new Result
        {
            ResultId = 10,
            SessionId = 20,
            StudentId = 30,
            Status = SubmissionStatus.PENDING_REVIEW,
            AiEvaluationStatus = AiEvaluationStatus.FAILED,
            Answers = new List<Answer> { answer }
        };
        var session = new ExamSession { SessionId = result.SessionId };
        var enrollment = new SessionEnrollment { SessionId = result.SessionId, StudentId = result.StudentId };

        var sessionRepository = new Mock<IExamSessionRepository>();
        sessionRepository.Setup(repository => repository.GetByIdAsync(result.SessionId)).ReturnsAsync(session);
        sessionRepository.Setup(repository => repository.GetEnrollmentAsync(result.SessionId, result.StudentId))
            .ReturnsAsync(enrollment);
        var resultRepository = new Mock<IResultRepository>();
        resultRepository.Setup(repository => repository.GetBySessionAndStudentAsync(result.SessionId, result.StudentId))
            .ReturnsAsync(result);
        var queueService = new Mock<IQueueService>();
        queueService.Setup(queue => queue.HasPendingSessionGrading(result.ResultId)).Returns(false);
        var service = new VivaSessionService(
            sessionRepository.Object,
            Mock.Of<IExamRepository>(),
            Mock.Of<IQuestionBankRepository>(),
            resultRepository.Object,
            Mock.Of<IAiServiceClient>(),
            Mock.Of<IStorageService>(),
            queueService.Object,
            NullLogger<VivaSessionService>.Instance);

        await service.FinishSessionAsync(result.SessionId, result.StudentId);

        Assert.Equal(AnswerStatus.PROCESSING, answer.Status);
        Assert.Null(answer.AiScore);
        Assert.Null(answer.AiFeedback);
        Assert.Null(answer.AiAnalysis);
        Assert.Null(answer.ErrorMessage);
        queueService.Verify(queue => queue.EnqueueSessionGrading(result.ResultId), Times.Once);
    }

    [Fact]
    public async Task MarkFailedAsync_MarksTranscriptOnlyAnswersAsError_AndPreservesScoredAnswers()
    {
        var transcriptOnlyAnswer = new Answer
        {
            AnswerId = 1,
            Status = AnswerStatus.EVALUATED,
            Transcript = "Student response"
        };
        var scoredAnswer = new Answer
        {
            AnswerId = 2,
            Status = AnswerStatus.EVALUATED,
            AiScore = 2.5f,
            AiAnalysis = """{"schemaVersion":"2.0"}"""
        };
        var result = new Result
        {
            ResultId = 10,
            Answers = new List<Answer> { transcriptOnlyAnswer, scoredAnswer }
        };

        var resultRepository = new Mock<IResultRepository>();
        resultRepository.Setup(repository => repository.GetByIdWithDetailsAsync(result.ResultId))
            .ReturnsAsync(result);
        var service = new SessionGradingService(
            resultRepository.Object,
            Mock.Of<IAiServiceClient>(),
            Mock.Of<IMarkdownDocumentRepository>(),
            NullLogger<SessionGradingService>.Instance);

        await service.MarkFailedAsync(result.ResultId, "AI response validation failed.");

        Assert.Equal(AnswerStatus.ERROR, transcriptOnlyAnswer.Status);
        Assert.Equal("AI response validation failed.", transcriptOnlyAnswer.ErrorMessage);
        Assert.Equal(AnswerStatus.EVALUATED, scoredAnswer.Status);
        Assert.Equal(2.5f, scoredAnswer.AiScore);
        Assert.Equal(AiEvaluationStatus.FAILED, result.AiEvaluationStatus);
        resultRepository.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }
}
