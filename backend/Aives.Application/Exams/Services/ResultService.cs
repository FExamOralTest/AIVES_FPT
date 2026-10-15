using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aives.Application.Exams.Services;

public class ResultService : IResultService
{
    private readonly IResultRepository _resultRepository;
    private readonly IExamSessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;

    public ResultService(
        IResultRepository resultRepository,
        IExamSessionRepository sessionRepository,
        IUserRepository userRepository)
    {
        _resultRepository = resultRepository;
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
    }

    public async Task<IEnumerable<ResultForReviewDto>> GetResultsForReviewAsync(int sessionId, int teacherId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        await EnsureAccessAsync(teacherId, session.ScheduledBy);

        var results = await _resultRepository.GetBySessionIdWithDetailsAsync(sessionId);
        return results.Select(MapToReviewDto);
    }

    public async Task<ResultForReviewDto> GetResultForReviewAsync(int resultId, int teacherId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");
        var session = await _sessionRepository.GetByIdAsync(result.SessionId);

        await EnsureAccessAsync(teacherId, session?.ScheduledBy ?? -1);

        return MapToReviewDto(result);
    }

    public async Task<ResultForReviewDto> ReviewResultAsync(int resultId, ReviewResultRequest request, int teacherId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");
        var session = await _sessionRepository.GetByIdAsync(result.SessionId);

        await EnsureAccessAsync(teacherId, session?.ScheduledBy ?? -1);

        if (request.FinalScore.HasValue) result.FinalScore = request.FinalScore.Value;
        if (!string.IsNullOrWhiteSpace(request.FinalFeedback)) result.FinalFeedback = request.FinalFeedback;
        result.Status = SubmissionStatus.REVIEWED;
        result.ReviewedBy = teacherId;
        result.ReviewedAt = DateTime.UtcNow;

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

        await EnsureAccessAsync(teacherId, session?.ScheduledBy ?? -1);

        if (result.Status != SubmissionStatus.REVIEWED && result.Status != SubmissionStatus.PENDING_REVIEW)
            throw new InvalidOperationException("Result must be reviewed before publishing.");

        result.Status = SubmissionStatus.PUBLISHED;
        result.PublishedAt = DateTime.UtcNow;
        result.ReviewedBy = teacherId;
        result.ReviewedAt = result.ReviewedAt ?? DateTime.UtcNow;

        if (!result.FinalScore.HasValue) result.FinalScore = result.AiScore;

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();
        return MapToReviewDto(result);
    }

    public async Task<StudentResultDto> GetStudentResultAsync(int sessionId, int studentId)
    {
        var result = await _resultRepository.GetBySessionAndStudentAsync(sessionId, studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy kết quả thi của sinh viên cho ca thi này.");

        if (result.Status != SubmissionStatus.PUBLISHED)
        {
            return new StudentResultDto
            {
                ResultId = result.ResultId,
                SessionId = result.SessionId,
                SessionName = result.Session?.SessionName ?? "",
                ExamTitle = result.Session?.Exam?.Title ?? "",
                Status = result.Status?.ToString() ?? "",
                IsPublished = false,
                Message = "Kết quả bài thi đang được giảng viên chấm và duyệt. Vui lòng quay lại sau.",
                SubmittedAt = result.SubmittedAt
            };
        }

        return new StudentResultDto
        {
            ResultId = result.ResultId,
            SessionId = result.SessionId,
            SessionName = result.Session?.SessionName ?? "",
            ExamTitle = result.Session?.Exam?.Title ?? "",
            Status = result.Status?.ToString() ?? "",
            IsPublished = true,
            FinalScore = result.FinalScore ?? result.AiScore,
            FinalFeedback = result.FinalFeedback ?? result.AiFeedback,
            SubmittedAt = result.SubmittedAt,
            PublishedAt = result.PublishedAt,
            Answers = result.Answers.Select(a => new StudentAnswerItemDto
            {
                AnswerId = a.AnswerId,
                QuestionId = a.QuestionId,
                QuestionText = a.Question?.QuestionText ?? "",
                StudentAnswer = a.StudentAnswer,
                Transcript = a.Transcript,
                AudioUrl = a.AudioUrl,
                FinalScore = a.FinalScore ?? a.AiScore,
                TeacherComment = a.TeacherComment ?? a.AiFeedback,
                IsFollowUp = a.IsFollowUp ?? false
            }).ToList()
        };
    }

    // ── Admin bypass: neu callerId == scheduledBy (Teacher) hoac ADMIN -> OK ──
    private async Task EnsureAccessAsync(int callerId, int scheduledBy)
    {
        if (callerId == scheduledBy) return;

        var caller = await _userRepository.GetByIdAsync(callerId)
            ?? throw new KeyNotFoundException($"User {callerId} not found.");

        if (caller.Role == UserRole.ADMIN) return;

        throw new UnauthorizedAccessException("Not your session.");
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