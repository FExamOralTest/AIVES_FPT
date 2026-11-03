using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aives.Application.AI.DTOs;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Moq;
using Xunit;

namespace Aives.Tests;

public class ResultStateMachineAndAuditTests
{
    private readonly Mock<IResultRepository> _resultRepoMock = new();
    private readonly Mock<IExamSessionRepository> _sessionRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly Mock<IAuditLogRepository> _auditLogRepoMock = new();

    private readonly ResultService _resultService;

    public ResultStateMachineAndAuditTests()
    {
        _resultService = new ResultService(
            _resultRepoMock.Object,
            _sessionRepoMock.Object,
            _userRepoMock.Object,
            _storageServiceMock.Object,
            _auditLogRepoMock.Object);
    }

    [Fact]
    public async Task ReviewResultAsync_WhenCalledByExaminer_UpdatesScores_And_RecordsAuditTrail()
    {
        // Arrange
        const int resultId = 101;
        const int examinerId = 99;
        const int sessionId = 1;

        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var answer = new Answer { AnswerId = 1, QuestionId = 10, AiScore = 7.0f };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW,
            AiScore = 7.0f,
            AiFeedback = "Good answer",
            Answers = new List<Answer> { answer }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        var request = new ReviewResultRequest
        {
            FinalScore = 8.5f,
            FinalFeedback = "Clear explanation with examples.",
            AnswerReviews = new List<AnswerReviewItem>
            {
                new() { AnswerId = 1, FinalScore = 8.5f, TeacherComment = "Excellent pronunciation" }
            }
        };

        // Act
        var reviewedDto = await _resultService.ReviewResultAsync(resultId, request, examinerId);

        // Assert
        Assert.Equal(SubmissionStatus.REVIEWED.ToString(), reviewedDto.Status);
        Assert.Equal(8.5f, reviewedDto.FinalScore);
        Assert.Equal(7.0f, result.AiScore); // Ensure AI score is NOT overwritten (BR-15)

        // Verify Audit Log recorded
        _auditLogRepoMock.Verify(a => a.LogAsync(
            "Result",
            resultId,
            "REVIEW_SCORE_OVERRIDE",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            examinerId,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GetStudentResultAsync_HidesAnalysisUntilPublished()
    {
        const int sessionId = 7;
        const int studentId = 12;
        var storedAnalysis = JsonSerializer.Serialize(
            StoredAnswerAnalysisDto.Create(
                "gemini",
                "gemini-test",
                "A transcript.",
                new[] { new AnswerRubricCriterionDto { CriterionId = "1", Description = "Criterion", MaxScore = 5 } },
                new QuestionScoreDetailDto
                {
                    QuestionId = "9",
                    QuestionText = "Question",
                    StudentAnswer = "A transcript.",
                    Score = 5,
                    MaxScore = 5,
                    Feedback = "Good.",
                    Strengths = new() { "Clear" },
                    Weaknesses = new(),
                    MissingContent = new(),
                    SentenceFeedback = new(),
                    CriterionAnalyses = new()
                    {
                        new()
                        {
                            CriterionId = "1",
                            AwardedScore = 5,
                            Judgment = "met",
                            Explanation = "Met.",
                            Errors = new(),
                            Omissions = new(),
                            EvidenceQuotes = new() { "A transcript." }
                        }
                    },
                    Improvements = new()
                }),
            StoredAnswerAnalysisDto.SerializerOptions);
        var answer = new Answer
        {
            AnswerId = 2,
            QuestionId = 9,
            Question = new QuestionBank { QuestionId = 9, QuestionText = "Question", SampleAnswer = "Private sample answer" },
            StudentAnswer = "A transcript.",
            AiAnalysis = storedAnalysis
        };
        var unpublished = new Result
        {
            ResultId = 3,
            SessionId = sessionId,
            StudentId = studentId,
            Status = SubmissionStatus.PENDING_REVIEW,
            Answers = new List<Answer> { answer }
        };
        _resultRepoMock.Setup(r => r.GetBySessionAndStudentAsync(sessionId, studentId)).ReturnsAsync(unpublished);

        var beforePublish = await _resultService.GetStudentResultAsync(sessionId, studentId);

        Assert.False(beforePublish.IsPublished);
        Assert.Empty(beforePublish.Answers);

        unpublished.Status = SubmissionStatus.PUBLISHED;
        var afterPublish = await _resultService.GetStudentResultAsync(sessionId, studentId);

        Assert.True(afterPublish.IsPublished);
        var studentAnalysis = Assert.Single(afterPublish.Answers).AiAnalysis;
        Assert.NotNull(studentAnalysis);
        Assert.Equal("2.0", studentAnalysis.SchemaVersion);
        Assert.Equal("gemini-test", studentAnalysis.Model);
        Assert.Single(studentAnalysis.Evaluation.CriterionAnalyses);
        var serialized = JsonSerializer.Serialize(studentAnalysis, StoredAnswerAnalysisDto.SerializerOptions);
        Assert.DoesNotContain("sampleAnswer", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"context\"", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetResultForReviewAsync_ReturnsAnswerProcessingError()
    {
        const int resultId = 11;
        const int examinerId = 99;
        const int sessionId = 5;
        const string processingError = "AI evaluation response validation failed: Invalid session evaluation.";
        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW,
            Answers = new List<Answer>
            {
                new()
                {
                    AnswerId = 7,
                    QuestionId = 3,
                    Status = AnswerStatus.ERROR,
                    ErrorMessage = processingError
                }
            }
        };
        _userRepoMock.Setup(repository => repository.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _sessionRepoMock.Setup(repository => repository.GetByIdAsync(sessionId)).ReturnsAsync(session);
        _resultRepoMock.Setup(repository => repository.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);

        var response = await _resultService.GetResultForReviewAsync(resultId, examinerId);

        Assert.Equal(processingError, Assert.Single(response.Answers).ErrorMessage);
    }

    [Fact]
    public async Task ReviewResultAsync_RejectsScoresAboveConfiguredMaximum()
    {
        const int resultId = 104;
        const int examinerId = 99;
        const int sessionId = 1;
        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var answer = new Answer
        {
            AnswerId = 1,
            QuestionId = 10,
            Question = new QuestionBank { QuestionId = 10, MaxScore = 5 },
            AiScore = 4
        };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW,
            Answers = new List<Answer> { answer }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _resultService.ReviewResultAsync(
            resultId,
            new ReviewResultRequest
            {
                FinalScore = 5.1f,
                AnswerReviews = new List<AnswerReviewItem>()
            },
            examinerId));

        _resultRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Result>()), Times.Never);
    }

    [Fact]
    public async Task ReviewResultAsync_RejectsAnswerScoresAboveQuestionMaximum()
    {
        const int resultId = 106;
        const int examinerId = 99;
        const int sessionId = 1;
        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var answer = new Answer
        {
            AnswerId = 1,
            QuestionId = 10,
            Question = new QuestionBank { QuestionId = 10, MaxScore = 5 }
        };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW,
            Answers = new List<Answer> { answer }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _resultService.ReviewResultAsync(
            resultId,
            new ReviewResultRequest
            {
                FinalScore = 5,
                AnswerReviews = new List<AnswerReviewItem>
                {
                    new() { AnswerId = answer.AnswerId, FinalScore = 5.1f }
                }
            },
            examinerId));

        _resultRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Result>()), Times.Never);
    }

    [Fact]
    public async Task ReviewResultAsync_RejectsAnswerIdsOutsideTheResult()
    {
        const int resultId = 107;
        const int examinerId = 99;
        const int sessionId = 1;
        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW,
            Answers = new List<Answer>
            {
                new() { AnswerId = 1, QuestionId = 10, Question = new QuestionBank { MaxScore = 5 } }
            }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _resultService.ReviewResultAsync(
            resultId,
            new ReviewResultRequest
            {
                FinalScore = 5,
                AnswerReviews = new List<AnswerReviewItem>
                {
                    new() { AnswerId = 999, FinalScore = 4 }
                }
            },
            examinerId));

        _resultRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Result>()), Times.Never);
    }

    [Fact]
    public async Task ReviewResultAsync_ClearsFeedbackAndReturnsConfiguredMaximum()
    {
        const int resultId = 105;
        const int examinerId = 99;
        const int sessionId = 1;
        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var answer = new Answer
        {
            AnswerId = 1,
            QuestionId = 10,
            Question = new QuestionBank { QuestionId = 10, MaxScore = 5 },
            TeacherComment = "Previous comment"
        };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW,
            FinalFeedback = "Previous feedback",
            Answers = new List<Answer> { answer }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        var reviewed = await _resultService.ReviewResultAsync(
            resultId,
            new ReviewResultRequest
            {
                FinalScore = 4,
                FinalFeedback = "",
                AnswerReviews = new List<AnswerReviewItem>
                {
                    new() { AnswerId = answer.AnswerId, FinalScore = 4, TeacherComment = "" }
                }
            },
            examinerId);

        Assert.Equal("", result.FinalFeedback);
        Assert.Equal("", answer.TeacherComment);
        Assert.Equal(5, reviewed.Answers[0].MaxScore);
    }

    [Fact]
    public async Task PublishResultAsync_WhenCalledOnReviewedResult_SetsPublished_And_LogsAudit()
    {
        // Arrange
        const int resultId = 102;
        const int examinerId = 99;
        const int sessionId = 1;

        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.REVIEWED,
            FinalScore = 8.5f,
            FinalFeedback = "Approved"
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        // Act
        var publishedDto = await _resultService.PublishResultAsync(resultId, examinerId);

        // Assert
        Assert.Equal(SubmissionStatus.PUBLISHED.ToString(), publishedDto.Status);
        Assert.NotNull(result.PublishedAt);
        Assert.Equal(examinerId, result.PublishedBy);

        // Verify Audit Log recorded
        _auditLogRepoMock.Verify(a => a.LogAsync(
            "Result",
            resultId,
            "PUBLISH_RESULT",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            examinerId,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task PublishResultAsync_RejectsAnswerWithoutAiOrExaminerScore()
    {
        const int resultId = 104;
        const int examinerId = 99;
        const int sessionId = 1;
        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PENDING_REVIEW,
            AiScore = 7.0f,
            Answers = new List<Answer>
            {
                new() { AnswerId = 1, Status = AnswerStatus.EVALUATED, AiScore = 7.0f },
                new() { AnswerId = 2, Status = AnswerStatus.ERROR, AiScore = null }
            }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _resultService.PublishResultAsync(resultId, examinerId));

        Assert.Contains("Every answer must have an AI score or an examiner score", exception.Message);
        _resultRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Result>()), Times.Never);
    }

    [Fact]
    public async Task PublishResultAsync_AllowsExaminerScoresForAiFailedAnswers()
    {
        const int resultId = 105;
        const int examinerId = 99;
        const int sessionId = 1;
        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.REVIEWED,
            FinalScore = 8.0f,
            Answers = new List<Answer>
            {
                new() { AnswerId = 1, Status = AnswerStatus.ERROR, FinalScore = 2.0f }
            }
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        var published = await _resultService.PublishResultAsync(resultId, examinerId);

        Assert.Equal(SubmissionStatus.PUBLISHED.ToString(), published.Status);
        Assert.Equal(examinerId, result.PublishedBy);
    }

    [Fact]
    public async Task PublishResultAsync_WhenAlreadyPublished_ThrowsInvalidOperationException()
    {
        // Arrange
        const int resultId = 103;
        const int examinerId = 99;
        const int sessionId = 1;

        var examiner = new User { UserId = examinerId, Role = UserRole.EXAMINER };
        var session = new ExamSession { SessionId = sessionId, ScheduledBy = examinerId };
        var result = new Result
        {
            ResultId = resultId,
            SessionId = sessionId,
            Status = SubmissionStatus.PUBLISHED,
            FinalScore = 9.0f
        };

        _userRepoMock.Setup(u => u.GetByIdAsync(examinerId)).ReturnsAsync(examiner);
        _resultRepoMock.Setup(r => r.GetByIdWithDetailsAsync(resultId)).ReturnsAsync(result);
        _sessionRepoMock.Setup(s => s.GetByIdAsync(sessionId)).ReturnsAsync(session);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _resultService.PublishResultAsync(resultId, examinerId));
    }
}
