using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aives.Application.Exams.Services;

public class ResultService : IResultService
{
    private readonly IResultRepository _resultRepository;
    private readonly IExamSessionRepository _sessionRepository;

    public ResultService(IResultRepository resultRepository, IExamSessionRepository sessionRepository)
    {
        _resultRepository = resultRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<IEnumerable<ResultForReviewDto>> GetResultsForReviewAsync(int sessionId, int teacherId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        if (session.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException("Not your session.");

        var results = await _resultRepository.GetBySessionIdWithDetailsAsync(sessionId);
        return results.Select(MapToReviewDto);
    }

    public async Task<ResultForReviewDto> GetResultForReviewAsync(int resultId, int teacherId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");

        var session = await _sessionRepository.GetByIdAsync(result.SessionId);
        if (session?.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException("Not your session.");

        return MapToReviewDto(result);
    }

    public async Task<ResultForReviewDto> ReviewResultAsync(int resultId, ReviewResultRequest request, int teacherId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");

        var session = await _sessionRepository.GetByIdAsync(result.SessionId);
        if (session?.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException("Not your session.");

        // Update result level
        if (request.FinalScore.HasValue) result.FinalScore = request.FinalScore.Value;
        if (!string.IsNullOrWhiteSpace(request.FinalFeedback)) result.FinalFeedback = request.FinalFeedback;
        result.Status = SubmissionStatus.REVIEWED;
        result.ReviewedBy = teacherId;
        result.ReviewedAt = DateTime.UtcNow;

        // Update answers
        foreach (var ansReq in request.AnswerReviews)
        {
            var answer = result.Answers.FirstOrDefault(a => a.AnswerId == ansReq.AnswerId);
            if (answer != null)
            {
                if (ansReq.FinalScore.HasValue) answer.FinalScore = ansReq.FinalScore.Value;
                if (!string.IsNullOrWhiteSpace(ansReq.TeacherComment)) answer.TeacherComment = ansReq.TeacherComment;
            }
        }

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();

        return MapToReviewDto(result);
    }

    public async Task<ResultForReviewDto> PublishResultAsync(int resultId, int teacherId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");

        var session = await _sessionRepository.GetByIdAsync(result.SessionId);
        if (session?.ScheduledBy != teacherId)
            throw new UnauthorizedAccessException("Not your session.");

        if (result.Status != SubmissionStatus.REVIEWED && result.Status != SubmissionStatus.PENDING_REVIEW)
            throw new InvalidOperationException("Result must be reviewed before publishing.");

        result.Status = SubmissionStatus.PUBLISHED;
        result.PublishedAt = DateTime.UtcNow;
        result.ReviewedBy = teacherId;
        result.ReviewedAt = result.ReviewedAt ?? DateTime.UtcNow;

        // If no final score set, use AI score
        if (!result.FinalScore.HasValue) result.FinalScore = result.AiScore;

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();

        return MapToReviewDto(result);
    }

    private static ResultForReviewDto MapToReviewDto(Result result) => new()
    {
        ResultId = result.ResultId,
        SessionId = result.SessionId,
        StudentId = result.StudentId,
        StudentName = result.Student?.FullName ?? "",
        StudentEmail = result.Student?.Email ?? "",
        SessionName = result.Session?.SessionName ?? "",
        ExamTitle = result.Session?.Exam?.Title ?? "",
        AiScore = result.AiScore,
        AiFeedback = result.AiFeedback,
        FinalScore = result.FinalScore,
        FinalFeedback = result.FinalFeedback,
        Status = result.Status?.ToString() ?? "",
        SubmittedAt = result.SubmittedAt,
        ReviewedAt = result.ReviewedAt,
        Answers = result.Answers.Select(a => new AnswerForReviewDto
        {
            AnswerId = a.AnswerId,
            QuestionId = a.QuestionId,
            QuestionText = a.Question?.QuestionText ?? "",
            StudentAnswer = a.StudentAnswer,
            Transcript = a.Transcript,
            AudioUrl = a.AudioUrl,
            AiScore = a.AiScore,
            AiFeedback = a.AiFeedback,
            FinalScore = a.FinalScore,
            TeacherComment = a.TeacherComment,
            IsFollowUp = a.IsFollowUp ?? false
        }).ToList()
    };
}