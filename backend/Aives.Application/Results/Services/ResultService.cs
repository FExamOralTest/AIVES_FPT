using Aives.Application.Exams.Interfaces;
using Aives.Application.Results.DTOs;
using Aives.Application.Results.Interfaces;
using Aives.Application.Users.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Results.Services;

public class ResultService : IResultService
{
    private readonly IResultRepository      _resultRepository;
    private readonly IExamSessionRepository _sessionRepository;
    private readonly IUserRepository        _userRepository;

    public ResultService(
        IResultRepository      resultRepository,
        IExamSessionRepository sessionRepository,
        IUserRepository        userRepository)
    {
        _resultRepository  = resultRepository;
        _sessionRepository = sessionRepository;
        _userRepository    = userRepository;
    }

    public async Task<IEnumerable<ResultForReviewDto>> GetResultsForReviewAsync(int sessionId, int callerId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        await EnsureAccessAsync(callerId, session.ScheduledBy);

        var results = await _resultRepository.GetBySessionIdWithDetailsAsync(sessionId);
        return results.Select(MapToReviewDto);
    }

    public async Task<ResultForReviewDto> GetResultForReviewAsync(int resultId, int callerId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");
        var session = await _sessionRepository.GetByIdAsync(result.SessionId);

        await EnsureAccessAsync(callerId, session?.ScheduledBy ?? -1);

        return MapToReviewDto(result);
    }

    public async Task<ResultForReviewDto> ReviewResultAsync(int resultId, ReviewResultRequest request, int callerId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");
        var session = await _sessionRepository.GetByIdAsync(result.SessionId);

        await EnsureAccessAsync(callerId, session?.ScheduledBy ?? -1);

        if (!string.IsNullOrWhiteSpace(request.FinalFeedback)) result.FinalFeedback = request.FinalFeedback;
        result.Status     = SubmissionStatus.REVIEWED;
        result.ReviewedBy = callerId;
        result.ReviewedAt = DateTime.UtcNow;

        foreach (var ansReq in request.AnswerReviews)
        {
            var answer = result.Answers.FirstOrDefault(a => a.AnswerId == ansReq.AnswerId);
            if (answer is null) continue;
            if (ansReq.FinalScore.HasValue) answer.FinalScore = ansReq.FinalScore.Value;
            if (!string.IsNullOrWhiteSpace(ansReq.TeacherComment)) answer.TeacherComment = ansReq.TeacherComment;
        }

        // FinalScore tổng luôn = tổng Answer.FinalScore (chỉ những câu đã có điểm).
        result.FinalScore = (float)Math.Round(
            result.Answers.Where(a => a.FinalScore.HasValue).Sum(a => a.FinalScore!.Value), 2);

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();
        return MapToReviewDto(result);
    }

    public async Task<ResultForReviewDto> PublishResultAsync(int resultId, int callerId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");
        var session = await _sessionRepository.GetByIdAsync(result.SessionId);

        await EnsureAccessAsync(callerId, session?.ScheduledBy ?? -1);

        if (result.Status != SubmissionStatus.REVIEWED && result.Status != SubmissionStatus.PENDING_REVIEW)
            throw new InvalidOperationException("Result must be reviewed before publishing.");

        result.Status      = SubmissionStatus.PUBLISHED;
        result.PublishedAt = DateTime.UtcNow;
        result.ReviewedBy  = result.ReviewedBy ?? callerId;
        result.ReviewedAt  = result.ReviewedAt ?? DateTime.UtcNow;

        // Tính FinalScore tổng = tổng FinalScore từng câu nếu chưa được set.
        // Fallback cuối cùng mới lấy AiScore tổng.
        if (!result.FinalScore.HasValue)
        {
            var answersWithScore = result.Answers.Where(a => a.FinalScore.HasValue).ToList();
            result.FinalScore = answersWithScore.Count > 0
                ? (float)Math.Round(answersWithScore.Sum(a => a.FinalScore!.Value), 2)
                : result.AiScore;
        }

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();
        return MapToReviewDto(result);
    }

    // ── Helper: Admin bypass, Teacher phải là người tạo session ──────────

    private async Task EnsureAccessAsync(int callerId, int scheduledBy)
    {
        if (callerId == scheduledBy) return; // Teacher sở hữu session → OK

        var caller = await _userRepository.GetByIdAsync(callerId)
            ?? throw new KeyNotFoundException($"User {callerId} not found.");

        if (caller.Role == UserRole.ADMIN) return; // Admin bypass mọi check

        throw new UnauthorizedAccessException("Not your session.");
    }

    // ── Mapping ───────────────────────────────────────────────────────────

    private static ResultForReviewDto MapToReviewDto(Result result) => new()
    {
        ResultId      = result.ResultId,
        SessionId     = result.SessionId,
        StudentId     = result.StudentId,
        StudentName   = result.Student?.FullName ?? "",
        StudentEmail  = result.Student?.Email ?? "",
        SessionName   = result.Session?.SessionName ?? "",
        ExamTitle     = result.Session?.Exam?.Title ?? "",
        AiScore       = result.AiScore,
        AiFeedback    = result.AiFeedback,
        FinalScore    = result.FinalScore,
        FinalFeedback = result.FinalFeedback,
        Status        = result.Status?.ToString() ?? "",
        SubmittedAt   = result.SubmittedAt,
        ReviewedAt    = result.ReviewedAt,
        Answers       = result.Answers.Select(a => new AnswerForReviewDto
        {
            AnswerId       = a.AnswerId,
            QuestionId     = a.QuestionId,
            QuestionText   = a.Question?.QuestionText ?? "",
            StudentAnswer  = a.StudentAnswer,
            Transcript     = a.Transcript,
            AudioUrl       = a.AudioUrl,
            AiScore        = a.AiScore,
            AiFeedback     = a.AiFeedback,
            FinalScore     = a.FinalScore,
            TeacherComment = a.TeacherComment,
            IsFollowUp     = a.IsFollowUp ?? false
        }).ToList()
    };
}
