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

    public async Task<IEnumerable<ResultForReviewDto>> GetAllPendingReviewResultsAsync(int teacherId)
    {
        var results = await _resultRepository.GetPendingReviewResultsForTeacherAsync(teacherId);
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

        // Chỉ review được kết quả đang ở trạng thái PENDING_REVIEW
        if (result.Status != SubmissionStatus.PENDING_REVIEW)
            throw new InvalidOperationException(
                $"Cannot review result with status '{result.Status}'. Only PENDING_REVIEW results can be reviewed.");

        // BR-15: Không ghi đè ai_score/ai_feedback
        // Chỉ cập nhật final_score và final_feedback
        if (request.FinalScore.HasValue)
            result.FinalScore = request.FinalScore.Value;
        if (!string.IsNullOrWhiteSpace(request.FinalFeedback))
            result.FinalFeedback = request.FinalFeedback;

        // Cập nhật reviewed_by và reviewed_at
        result.Status = SubmissionStatus.REVIEWED;
        result.ReviewedBy = teacherId;
        result.ReviewedAt = DateTime.UtcNow;

        // BR-15: Cập nhật điểm từng câu - sử dụng answers.final_score và answers.teacher_comment
        foreach (var ansReq in request.AnswerReviews)
        {
            var answer = result.Answers.FirstOrDefault(a => a.AnswerId == ansReq.AnswerId);
            if (answer != null)
            {
                if (ansReq.FinalScore.HasValue)
                    answer.FinalScore = ansReq.FinalScore.Value;
                if (!string.IsNullOrWhiteSpace(ansReq.TeacherComment))
                    answer.TeacherComment = ansReq.TeacherComment;
                // BR-15: Không ghi đè ai_score/ai_feedback của answer
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

        // BR-10: Không publish kết quả chưa được approve/review
        if (result.Status == SubmissionStatus.PUBLISHED)
            throw new InvalidOperationException("Result is already published.");

        if (result.Status != SubmissionStatus.REVIEWED)
            throw new InvalidOperationException(
                $"Cannot publish result with status '{result.Status}'. Result must be REVIEWED before publishing.");

        // BR-10: Teacher approve và publish Final Result
        // BR-11: Final Result được giao cho Student
        result.Status = SubmissionStatus.PUBLISHED;
        result.PublishedAt = DateTime.UtcNow;
        // Nếu chưa được review trước đó (trường hợp approve + publish cùng lúc)
        if (!result.ReviewedAt.HasValue)
        {
            result.ReviewedBy = teacherId;
            result.ReviewedAt = DateTime.UtcNow;
        }

        // Nếu không có final score, sử dụng AI score
        if (!result.FinalScore.HasValue)
            result.FinalScore = result.AiScore;

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();

        return MapToReviewDto(result);
    }

    public async Task<IEnumerable<ResultForReviewDto>> GetStudentPublishedResultsAsync(int studentId)
    {
        var results = await _resultRepository.GetStudentPublishedResultsAsync(studentId);
        return results.Select(MapToReviewDto);
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