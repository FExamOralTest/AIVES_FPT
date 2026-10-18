using Aives.Application.Exams.DTOs;
using Aives.Application.AI.DTOs;
using System.Text.Json;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aives.Application.Exams.Services;

public class ResultService : IResultService
{
    private readonly IResultRepository _resultRepository;
    private readonly IExamSessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStorageService _storageService;

    public ResultService(
        IResultRepository resultRepository,
        IExamSessionRepository sessionRepository,
        IUserRepository userRepository,
        IStorageService storageService)
    {
        _resultRepository = resultRepository;
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _storageService = storageService;
    }

    public async Task<IEnumerable<ResultForReviewDto>> GetResultsForReviewAsync(int sessionId, int teacherId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException("Session not found.");

        await EnsureAccessAsync(teacherId, session.ScheduledBy);

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

        await EnsureAccessAsync(teacherId, session?.ScheduledBy ?? -1);

        return MapToReviewDto(result);
    }

    public async Task<ResultForReviewDto> ReviewResultAsync(int resultId, ReviewResultRequest request, int teacherId)
    {
        var result = await _resultRepository.GetByIdWithDetailsAsync(resultId)
            ?? throw new KeyNotFoundException("Result not found.");
        var session = await _sessionRepository.GetByIdAsync(result.SessionId);

        await EnsureAccessAsync(teacherId, session?.ScheduledBy ?? -1);

        // BR-10: Chỉ review được kết quả đang ở trạng thái PENDING_REVIEW hoặc REVIEWED
        if (result.Status != SubmissionStatus.PENDING_REVIEW && result.Status != SubmissionStatus.REVIEWED)
            throw new InvalidOperationException(
                $"Cannot review result with status '{result.Status}'. Only PENDING_REVIEW or REVIEWED results can be reviewed.");

        // BR-15: Không ghi đè ai_score/ai_feedback, chỉ cập nhật final_score và final_feedback
        if (request.FinalScore.HasValue)
            result.FinalScore = request.FinalScore.Value;
        if (!string.IsNullOrWhiteSpace(request.FinalFeedback))
            result.FinalFeedback = request.FinalFeedback;

        result.Status = SubmissionStatus.REVIEWED;
        result.ReviewedBy = teacherId;
        result.ReviewedAt = DateTime.UtcNow;

        // BR-15: Cập nhật answers.final_score và answers.teacher_comment
        foreach (var ansReq in request.AnswerReviews)
        {
            var answer = result.Answers.FirstOrDefault(a => a.AnswerId == ansReq.AnswerId);
            if (answer != null)
            {
                if (ansReq.FinalScore.HasValue)
                    answer.FinalScore = ansReq.FinalScore.Value;
                if (!string.IsNullOrWhiteSpace(ansReq.TeacherComment))
                    answer.TeacherComment = ansReq.TeacherComment;
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

        // BR-10: Không publish kết quả đã publish hoặc chưa review
        if (result.Status == SubmissionStatus.PUBLISHED)
            throw new InvalidOperationException("Result is already published.");

        if (result.Status != SubmissionStatus.REVIEWED && result.Status != SubmissionStatus.PENDING_REVIEW)
            throw new InvalidOperationException(
                $"Cannot publish result with status '{result.Status}'. Result must be REVIEWED before publishing.");

        // BR-10: Teacher approve và publish Final Result
        // BR-11: Final Result được giao cho Student
        if (!result.FinalScore.HasValue && !result.AiScore.HasValue)
            throw new InvalidOperationException("A teacher score is required when AI evaluation is unavailable.");

        result.Status = SubmissionStatus.PUBLISHED;
        result.PublishedAt = DateTime.UtcNow;
        result.PublishedBy = teacherId;

        if (!result.ReviewedAt.HasValue)
        {
            result.ReviewedBy = teacherId;
            result.ReviewedAt = DateTime.UtcNow;
        }

        if (!result.FinalScore.HasValue)
            result.FinalScore = result.AiScore;

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
                Message = "Kết quả bài thi đang được khảo thí thẩm định và duyệt. Vui lòng quay lại sau.",
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
                AudioUrl = _storageService.ResolvePlaybackUrl(a.AudioUrl),
                FinalScore = a.FinalScore ?? a.AiScore,
                TeacherComment = a.TeacherComment ?? a.AiFeedback,
                IsFollowUp = a.IsFollowUp ?? false
            }).ToList()
        };
    }

    public async Task<IEnumerable<ResultForReviewDto>> GetStudentPublishedResultsAsync(int studentId)
    {
        var results = await _resultRepository.GetStudentPublishedResultsAsync(studentId);
        return results.Select(MapToReviewDto);
    }

    // ── BR-09 & BR-10: Chi EXAMINER hoac ADMIN moi co quyen review & publish ──
    private async Task EnsureAccessAsync(int callerId, int scheduledBy)
    {
        var caller = await _userRepository.GetByIdAsync(callerId)
            ?? throw new KeyNotFoundException($"User {callerId} not found.");

        if (caller.Role == UserRole.ADMIN || caller.Role == UserRole.EXAMINER) return;

        throw new UnauthorizedAccessException("Only examiners or admins can review and publish exam results.");
    }

    private ResultForReviewDto MapToReviewDto(Result result) => new()
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
        AiAnalysis = result.AiAnalysis == null ? null : JsonSerializer.Deserialize<SessionAnalysisDto>(result.AiAnalysis),
        AiEvaluationStatus = result.AiEvaluationStatus?.ToString(),
        AiEvaluationError = result.AiEvaluationError,
        FinalScore = result.FinalScore,
        FinalFeedback = result.FinalFeedback,
        Status = result.Status?.ToString() ?? "",
        SubmittedAt = result.SubmittedAt,
        ReviewedAt = result.ReviewedAt,
        PublishedAt = result.PublishedAt,
        PublishedBy = result.PublishedBy,
        Answers = result.Answers.Select(a => new AnswerForReviewDto
        {
            AnswerId = a.AnswerId,
            Status = a.Status.ToString(),
            QuestionId = a.QuestionId,
            QuestionText = a.Question?.QuestionText ?? "",
            StudentAnswer = a.StudentAnswer,
            Transcript = a.Transcript,
            AudioUrl = _storageService.ResolvePlaybackUrl(a.AudioUrl),
            AiScore = a.AiScore,
            AiFeedback = a.AiFeedback,
            AiAnalysis = a.AiAnalysis == null ? null : JsonSerializer.Deserialize<StoredAnswerAnalysisDto>(a.AiAnalysis),
            FinalScore = a.FinalScore,
            TeacherComment = a.TeacherComment,
            IsFollowUp = a.IsFollowUp ?? false
        }).ToList()
    };

}