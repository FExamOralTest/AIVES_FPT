using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.DTOs;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class ResultRepository : IResultRepository
{
    private readonly AivesDbContext _db;

    public ResultRepository(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<ResultSearchResponse> SearchAsync(ResultSearchQuery query, CancellationToken ct = default)
    {
        if (query.PeriodId.HasValue && !await _db.ExamPeriods.AnyAsync(p => p.PeriodId == query.PeriodId, ct))
            throw new KeyNotFoundException("Exam period not found.");
        var sessions = _db.ExamSessions.AsNoTracking().AsQueryable();
        if (query.PeriodId.HasValue) sessions = sessions.Where(s => s.PeriodId == query.PeriodId);
        if (query.Unassigned) sessions = sessions.Where(s => s.PeriodId == null);
        var options = await sessions.OrderByDescending(s => s.StartTime).ThenByDescending(s => s.SessionId)
            .Select(s => new ResultSessionOptionDto { SessionId = s.SessionId, SessionName = s.SessionName }).ToListAsync(ct);
        var results = _db.Results.AsNoTracking().Where(r => sessions.Select(s => s.SessionId).Contains(r.SessionId));
        if (query.SessionId.HasValue) results = results.Where(r => r.SessionId == query.SessionId);
        if (query.Status.HasValue) results = results.Where(r => r.Status == query.Status);
        if (query.AiStatus.HasValue) results = results.Where(r => r.AiEvaluationStatus == query.AiStatus);
        var term = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(term)) results = results.Where(r =>
            r.Student.FullName.ToLower().Contains(term) || (r.Student.Email != null && r.Student.Email.ToLower().Contains(term))
            || (r.Student.Username != null && r.Student.Username.ToLower().Contains(term)));
        var count = await results.CountAsync(ct);
        // Only project list fields; load transcripts/audio/analysis via the existing detail endpoint on demand.
        var items = await results.OrderByDescending(r => r.SubmittedAt.HasValue).ThenByDescending(r => r.SubmittedAt)
            .ThenByDescending(r => r.ResultId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(r => new ResultListItemDto {
                ResultId = r.ResultId, SessionId = r.SessionId, PeriodId = r.Session.PeriodId,
                PeriodName = r.Session.Period != null ? r.Session.Period.PeriodName : null,
                StudentName = r.Student.FullName, StudentEmail = r.Student.Email ?? "", StudentCode = r.Student.Username ?? "",
                SessionName = r.Session.SessionName,
                ExamTitle = r.Exam != null ? r.Exam.Title : r.Session.Exam != null ? r.Session.Exam.Title : "",
                Status = r.Status.HasValue ? r.Status.Value.ToString() : "",
                AiEvaluationStatus = r.AiEvaluationStatus.HasValue ? r.AiEvaluationStatus.Value.ToString() : null,
                AiScore = r.AiScore, FinalScore = r.FinalScore, SubmittedAt = r.SubmittedAt
            }).ToListAsync(ct);
        return new ResultSearchResponse { Items = items, Sessions = options, TotalCount = count, Page = query.Page, PageSize = query.PageSize };
    }

    public async Task<Result?> GetByIdAsync(int resultId)
    {
        return await _db.Results
            .Include(r => r.Session)
                .ThenInclude(s => s.Exam)
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .FirstOrDefaultAsync(r => r.ResultId == resultId);
    }

    public async Task<Result?> GetByIdWithDetailsAsync(int resultId)
    {
        return await _db.Results
            .Include(r => r.Session)
                .ThenInclude(s => s.Exam)
                    .ThenInclude(e => e.Course)
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
                    .ThenInclude(q => q.Rubric)
            .FirstOrDefaultAsync(r => r.ResultId == resultId);
    }

    public async Task<IEnumerable<Result>> GetBySessionIdWithDetailsAsync(int sessionId)
    {
        return await _db.Results
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .Where(r => r.SessionId == sessionId)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();
    }

    public async Task<Result?> GetBySessionAndStudentAsync(int sessionId, int studentId)
    {
        return await _db.Results
            .Include(r => r.Session)
                .ThenInclude(s => s.Exam)
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .FirstOrDefaultAsync(r => r.SessionId == sessionId && r.StudentId == studentId);
    }

    public async Task<Result> CreateAsync(Result result)
    {
        _db.Results.Add(result);
        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<Answer> AddOrUpdateAnswerAsync(Answer answer)
    {
        var existing = await _db.Answers.FirstOrDefaultAsync(a => a.ResultId == answer.ResultId && a.QuestionId == answer.QuestionId);
        if (existing == null)
        {
            _db.Answers.Add(answer);
        }
        else
        {
            var isFollowUp = answer.IsFollowUp == true;
            existing.StudentAnswer = isFollowUp
                ? AppendAnswer(existing.StudentAnswer, answer.StudentAnswer)
                : answer.StudentAnswer;
            existing.Transcript = isFollowUp
                ? AppendAnswer(existing.Transcript, answer.Transcript)
                : answer.Transcript;
            if (!string.IsNullOrEmpty(answer.AudioUrl))
                existing.AudioUrl = answer.AudioUrl;
            if (!string.IsNullOrEmpty(answer.ObjectKey))
                existing.ObjectKey = answer.ObjectKey;
            existing.IsFollowUp = existing.IsFollowUp == true || isFollowUp;
            existing.Status = answer.Status;
            existing.ErrorMessage = answer.ErrorMessage;
            if (isFollowUp || answer.AiScore.HasValue)
                existing.AiScore = answer.AiScore;
            if (isFollowUp || !string.IsNullOrEmpty(answer.AiFeedback))
                existing.AiFeedback = answer.AiFeedback;
            if (isFollowUp || !string.IsNullOrEmpty(answer.AiAnalysis))
                existing.AiAnalysis = answer.AiAnalysis;
            existing.AnsweredAt = answer.AnsweredAt ?? DateTime.UtcNow;
            _db.Answers.Update(existing);
        }
        await _db.SaveChangesAsync();

        // AnswerId do DB sinh (identity) → sau SaveChanges mới có giá trị.
        // Trả về bản đã persist để caller biết answerId để gắn media_upload / tra cứu audio.
        return await _db.Answers.AsNoTracking()
            .FirstAsync(a => a.ResultId == answer.ResultId && a.QuestionId == answer.QuestionId);
    }

    private static string? AppendAnswer(string? original, string? followUp)
    {
        if (string.IsNullOrEmpty(original)) return followUp;
        if (string.IsNullOrEmpty(followUp)) return original;
        return $"{original}{Environment.NewLine}{Environment.NewLine}{followUp}";
    }

    public async Task UpdateAsync(Result result)
    {
        _db.Results.Update(result);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Result>> GetPendingReviewResultsForTeacherAsync(int teacherId)
    {
        return await _db.Results
            .Include(r => r.Session)
                .ThenInclude(s => s.Exam)
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .Where(r => r.Status == SubmissionStatus.PENDING_REVIEW || r.Status == SubmissionStatus.REVIEWED)
            .Where(r => r.Session.ScheduledBy == teacherId)
            .OrderByDescending(r => r.SubmittedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Result>> GetStudentPublishedResultsAsync(int studentId)
    {
        return await _db.Results
            .Include(r => r.Session)
                .ThenInclude(s => s.Exam)
            .Include(r => r.Student)
            .Include(r => r.ReviewedByNavigation)
            .Include(r => r.Answers)
                .ThenInclude(a => a.Question)
            .Where(r => r.StudentId == studentId && r.Status == SubmissionStatus.PUBLISHED)
            .OrderByDescending(r => r.PublishedAt)
            .ToListAsync();
    }
}
