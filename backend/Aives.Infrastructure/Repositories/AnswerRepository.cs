using Aives.Application.Queue.Interfaces;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation cho <c>IAnswerRepository</c>.
/// Dùng load + update thay vì <c>ExecuteUpdate</c> để tương thích với InMemory provider (test).
/// Performance vẫn đủ tốt vì mỗi lần chỉ touch 1 answer (background job, không hot path).
/// </summary>
public class AnswerRepository : IAnswerRepository
{
    private readonly AivesDbContext _db;

    public AnswerRepository(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<Domain.Entities.Answer?> GetByIdWithDetailsAsync(
        int answerId, CancellationToken cancellationToken = default)
    {
        return await _db.Answers
            .Include(a => a.Question)
                .ThenInclude(q => q.Rubric)
            .Include(a => a.Result)
                .ThenInclude(r => r.Session)
                    .ThenInclude(s => s.Exam)
                        .ThenInclude(e => e.Course)
            .FirstOrDefaultAsync(a => a.AnswerId == answerId, cancellationToken);
    }

    public async Task UpdateTranscriptAsync(
        int answerId, string transcript, AnswerStatus status, CancellationToken cancellationToken = default,
        string? detectedLanguage = null, float? durationSeconds = null)
    {
        var answer = await WritableAsync(answerId, cancellationToken);
        if (answer == null) return;
        answer.Transcript = transcript;
        if (detectedLanguage != null) answer.DetectedLanguage = detectedLanguage;
        if (durationSeconds.HasValue) answer.DurationSeconds = durationSeconds;
        answer.Status = status;
        answer.ErrorMessage = null;
        answer.AnsweredAt ??= DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAiEvaluationAsync(
        int answerId,
        float? aiScore,
        string? aiFeedback,
        string? aiAnalysisJson,
        AnswerStatus status,
        CancellationToken cancellationToken = default)
    {
        var answer = await WritableAsync(answerId, cancellationToken);
        if (answer == null) return;
        answer.AiScore = aiScore;
        answer.AiFeedback = aiFeedback;
        answer.AiAnalysis = aiAnalysisJson;
        answer.Status = status;
        answer.ErrorMessage = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        int answerId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var answer = await WritableAsync(answerId, cancellationToken);
        if (answer == null) return;
        answer.Status = AnswerStatus.ERROR;
        answer.ErrorMessage = errorMessage;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetForRetryAsync(int answerId, CancellationToken cancellationToken = default)
    {
        var answer = await WritableAsync(answerId, cancellationToken);
        if (answer == null) return;
        answer.Transcript = null;
        answer.DetectedLanguage = null;
        answer.DurationSeconds = null;
        answer.AiScore = null;
        answer.AiFeedback = null;
        answer.AiAnalysis = null;
        answer.Status = AnswerStatus.PROCESSING;
        answer.AnsweredAt = null;
        // AttemptCount và JobId được giữ nguyên có chủ đích:
        // BR-17 giới hạn "retry tối đa 3 lần thử tổng cộng" — bộ đếm phải tích luỹ qua
        // mọi lần retry (cả retry tự động của Hangfire lẫn retry thủ công từ UI).
        answer.ErrorMessage = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> IncrementAttemptAsync(
        int answerId, string? jobId, CancellationToken cancellationToken = default)
    {
        var answer = await WritableAsync(answerId, cancellationToken);
        if (answer == null) return 0;

        answer.AttemptCount += 1;
        if (!string.IsNullOrWhiteSpace(jobId))
            answer.JobId = jobId;

        await _db.SaveChangesAsync(cancellationToken);
        return answer.AttemptCount;
    }

    public async Task SetJobIdAsync(
        int answerId, string? jobId, CancellationToken cancellationToken = default)
    {
        var answer = await WritableAsync(answerId, cancellationToken);
        if (answer == null) return;
        if (!string.IsNullOrWhiteSpace(jobId))
            answer.JobId = jobId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAudioUrlAsync(int answerId, string audioUrl, CancellationToken cancellationToken = default)
    {
        var answer = await WritableAsync(answerId, cancellationToken);
        if (answer == null) return;
        answer.AudioUrl = audioUrl;
        answer.ObjectKey = Uri.TryCreate(audioUrl, UriKind.Absolute, out _) || audioUrl.StartsWith('/')
            ? null : audioUrl;
        answer.Transcript = null;
        answer.DetectedLanguage = null;
        answer.DurationSeconds = null;
        answer.AiScore = null;
        answer.AiFeedback = null;
        answer.AiAnalysis = null;
        answer.Status = AnswerStatus.PROCESSING;
        answer.AnsweredAt = DateTime.UtcNow;
        answer.ErrorMessage = null;
        // Upload audio mới = một vòng xử lý mới → reset ngân sách attempt (BR-17).
        // Bộ đếm chỉ tích luỹ trong vòng đời của một lần submit, không qua các lần
        // upload lại (vì nội dung audio đã thay đổi hoàn toàn).
        answer.AttemptCount = 0;
        answer.JobId = null;
        await _db.SaveChangesAsync(cancellationToken);
    }
    private async Task<Domain.Entities.Answer?> WritableAsync(int answerId, CancellationToken ct)
    {
        var answer = await _db.Answers.Include(a => a.Result).SingleOrDefaultAsync(a => a.AnswerId == answerId, ct);
        if (answer is null) return null;
        await _db.Entry(answer.Result).ReloadAsync(ct);
        if (answer.Result.Status is SubmissionStatus.CANCELLED or SubmissionStatus.PUBLISHED)
            throw new InvalidOperationException("Invalidated or published attempts cannot be changed.");
        _db.Entry(answer.Result).Property(r => r.Status).IsModified = true;
        return answer;
    }

}
