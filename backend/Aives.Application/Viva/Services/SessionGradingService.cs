using System.Globalization;
using System.Text.Json;
using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Queue.Contracts;
using Aives.Application.Viva.DTOs;
using Aives.Application.Viva.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Application.Viva.Services;

/// <summary>
/// Chấm cả bài thi bằng một lần gọi AI. Worker hàng đợi gọi dịch vụ này
/// sau khi bài đã được lưu, nên request nộp bài của sinh viên không phải chờ AI.
/// </summary>
public class SessionGradingService : ISessionGradingService
{
    private readonly IResultRepository _resultRepository;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IMarkdownDocumentRepository _documentRepository;
    private readonly ILogger<SessionGradingService> _logger;

    public SessionGradingService(
        IResultRepository resultRepository,
        IAiServiceClient aiServiceClient,
        IMarkdownDocumentRepository documentRepository,
        ILogger<SessionGradingService> logger)
    {
        _resultRepository = resultRepository;
        _aiServiceClient = aiServiceClient;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<SessionGradingResult> GradeAsync(
        int resultId, bool finalAttempt, CancellationToken cancellationToken = default)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId);
        if (result == null)
            return SessionGradingResult.Failed("result_not_found", $"Result {resultId} was not found.", retryable: false);

        if (result.Status is SubmissionStatus.REVIEWED or SubmissionStatus.PUBLISHED or SubmissionStatus.CANCELLED)
            return SessionGradingResult.Completed();

        if (result.AiEvaluationStatus == AiEvaluationStatus.SUCCEEDED)
            return SessionGradingResult.Completed();

        var exam = result.Session?.Exam;
        if (exam == null)
            return await FailAsync(result, "exam_not_found", "The exam for this submission no longer exists.", retryable: false, finalAttempt);

        var answers = result.Answers.OrderBy(a => a.QuestionId).ToList();
        if (answers.Count == 0)
            return await FailAsync(result, "no_answers", "No answers were submitted for AI evaluation.", retryable: false, finalAttempt);

        var documents = await _documentRepository.GetByCourseIdAsync(exam.CourseId);
        var evalRequest = new EvaluateSessionRequest
        {
            ExamSessionId = result.SessionId.ToString(CultureInfo.InvariantCulture),
            StudentId = result.StudentId.ToString(CultureInfo.InvariantCulture),
            CourseName = exam.Course?.CourseName ?? "",
            Documents = documents.Select(d => new DocumentContextDto
            {
                DocumentId = d.DocumentId.ToString(CultureInfo.InvariantCulture),
                Title = d.FileName,
                Content = d.Content,
                DocumentType = d.DocType == DocType.CONTENT ? "course_content" : "learning_outcomes"
            }).ToList(),
            QuestionAnswers = answers.Select(a => new QuestionAnswerPairDto
            {
                QuestionId = a.QuestionId.ToString(CultureInfo.InvariantCulture),
                QuestionText = a.Question?.QuestionText ?? "",
                StudentAnswer = a.StudentAnswer ?? a.Transcript ?? "",
                IsFollowUp = a.IsFollowUp ?? false,
                SampleAnswer = a.Question?.SampleAnswer,
                LearningOutcome = a.Question?.LearningOutcome,
                Topic = a.Question?.Topic,
                MaxScore = a.Question?.MaxScore,
                Rubric = a.Question?.Rubric.OrderBy(c => c.Position).Select(c => new AnswerRubricCriterionDto
                {
                    CriterionId = c.CriterionId.ToString(CultureInfo.InvariantCulture),
                    Description = c.Description,
                    MaxScore = (float)c.MaxScore
                }).ToList() ?? new(),
                ExpectedKeywords = a.Question?.ExpectedKeywords ?? new()
            }).ToList()
        };

        var evalResponse = await _aiServiceClient.EvaluateSessionAsync(evalRequest, cancellationToken);
        if (!evalResponse.Success)
        {
            var code = evalResponse.ErrorCode ?? "evaluation_failed";
            var message = evalResponse.Message ?? "AI evaluation failed.";
            var retryable = QueueLimits.IsRetryableEvaluationError(code);
            _logger.LogWarning(
                "Session grading failed for result {ResultId}: {ErrorCode} (retryable={Retryable})",
                result.ResultId, code, retryable);
            return await FailAsync(result, code, message, retryable, finalAttempt);
        }

        var answersById = answers.ToDictionary(a => a.QuestionId.ToString(CultureInfo.InvariantCulture));
        var contextsById = evalRequest.QuestionAnswers.ToDictionary(q => q.QuestionId);
        if (evalResponse.QuestionScores.Count != answersById.Count
            || !answersById.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(
                evalResponse.QuestionScores.Select(item => item.QuestionId)))
        {
            return await FailAsync(
                result,
                "question_ids_mismatch",
                "AI evaluation did not return exactly one analysis for every submitted question.",
                retryable: false,
                finalAttempt);
        }

        foreach (var itemEval in evalResponse.QuestionScores)
        {
            if (!answersById.ContainsKey(itemEval.QuestionId) || !contextsById.ContainsKey(itemEval.QuestionId))
            {
                return await FailAsync(
                    result,
                    "question_not_found",
                    $"AI returned a score for question {itemEval.QuestionId}, which is not in this submission.",
                    retryable: false,
                    finalAttempt);
            }
        }

        foreach (var itemEval in evalResponse.QuestionScores)
        {
            var answer = answersById[itemEval.QuestionId];
            var context = contextsById[itemEval.QuestionId];
            itemEval.Score = MathF.Round(itemEval.CriterionAnalyses.Sum(criterion => criterion.AwardedScore), 4);
            itemEval.MaxScore = context.Rubric.Sum(criterion => criterion.MaxScore);
            answer.AiScore = itemEval.Score;
            answer.AiFeedback = itemEval.Feedback;
            answer.Status = AnswerStatus.EVALUATED;
            answer.ErrorMessage = null;
            answer.AiAnalysis = JsonSerializer.Serialize(
                StoredAnswerAnalysisDto.Create(
                    evalResponse.Provider,
                    evalResponse.Model,
                    context.StudentAnswer,
                    context.Rubric,
                    itemEval),
                StoredAnswerAnalysisDto.SerializerOptions);
        }

        result.AiScore = MathF.Round(evalResponse.QuestionScores.Sum(question => question.Score), 4);
        result.AiFeedback = evalResponse.OverallFeedback;
        result.AiAnalysis = JsonSerializer.Serialize(new SessionAnalysisDto
        {
            SchemaVersion = evalResponse.SchemaVersion,
            Provider = evalResponse.Provider,
            Model = evalResponse.Model,
            GradedAt = DateTime.UtcNow,
            MaxTotalScore = MathF.Round(evalResponse.QuestionScores.Sum(question => question.MaxScore), 4),
            Percentage = evalResponse.QuestionScores.Sum(question => question.MaxScore) > 0
                ? result.AiScore.Value / evalResponse.QuestionScores.Sum(question => question.MaxScore) * 100
                : 0,
            StudySuggestions = evalResponse.StudySuggestions
        }, StoredAnswerAnalysisDto.SerializerOptions);
        result.AiEvaluationStatus = AiEvaluationStatus.SUCCEEDED;
        result.AiEvaluationError = null;

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();
        _logger.LogInformation(
            "Session grading completed for result {ResultId}: score={Score}",
            result.ResultId, result.AiScore);
        return SessionGradingResult.Completed();
    }

    public async Task MarkFailedAsync(int resultId, string error, CancellationToken cancellationToken = default)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId);
        if (result == null
            || result.Status is SubmissionStatus.REVIEWED or SubmissionStatus.PUBLISHED
            || result.AiEvaluationStatus == AiEvaluationStatus.SUCCEEDED)
            return;

        await FailAsync(result, "internal_error", error, retryable: false, finalAttempt: true);
    }

    private async Task<SessionGradingResult> FailAsync(
        Result result,
        string code,
        string message,
        bool retryable,
        bool finalAttempt)
    {
        var giveUp = !retryable || finalAttempt;
        if (giveUp)
        {
            result.AiEvaluationStatus = AiEvaluationStatus.FAILED;
            result.AiEvaluationError = message;
            foreach (var answer in result.Answers)
            {
                if (answer.Status != AnswerStatus.EVALUATED || !answer.AiScore.HasValue)
                {
                    answer.Status = AnswerStatus.ERROR;
                    answer.ErrorMessage = message;
                }
            }

            await _resultRepository.UpdateAsync(result);
            await _resultRepository.SaveChangesAsync();
        }

        return SessionGradingResult.Failed(code, message, retryable && !giveUp);
    }
}
