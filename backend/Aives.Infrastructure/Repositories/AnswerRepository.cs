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
            .FirstOrDefaultAsync(a => a.AnswerId == answerId, cancellationToken);
    }

    public async Task UpdateTranscriptAsync(
        int answerId, string transcript, AnswerStatus status, CancellationToken cancellationToken = default)
    {
        var answer = await _db.Answers.FindAsync(new object?[] { answerId }, cancellationToken);
        if (answer == null) return;
        answer.Transcript = transcript;
        answer.Status = status;
        answer.AnsweredAt = DateTime.UtcNow;
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
        var answer = await _db.Answers.FindAsync(new object?[] { answerId }, cancellationToken);
        if (answer == null) return;
        answer.AiScore = aiScore;
        answer.AiFeedback = aiFeedback;
        answer.AiAnalysis = aiAnalysisJson;
        answer.Status = status;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(
        int answerId, string errorMessage, CancellationToken cancellationToken = default)
    {
        var answer = await _db.Answers.FindAsync(new object?[] { answerId }, cancellationToken);
        if (answer == null) return;
        answer.Status = AnswerStatus.ERROR;
        answer.TeacherComment = errorMessage;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetForRetryAsync(int answerId, CancellationToken cancellationToken = default)
    {
        var answer = await _db.Answers.FindAsync(new object?[] { answerId }, cancellationToken);
        if (answer == null) return;
        answer.Transcript = null;
        answer.AiScore = null;
        answer.AiFeedback = null;
        answer.AiAnalysis = null;
        answer.Status = AnswerStatus.PROCESSING;
        answer.AnsweredAt = null;
        answer.TeacherComment = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAudioUrlAsync(int answerId, string audioUrl, CancellationToken cancellationToken = default)
    {
        var answer = await _db.Answers.FindAsync(new object?[] { answerId }, cancellationToken);
        if (answer == null) return;
        answer.AudioUrl = audioUrl;
        answer.Transcript = null;
        answer.AiScore = null;
        answer.AiFeedback = null;
        answer.AiAnalysis = null;
        answer.Status = AnswerStatus.PROCESSING;
        answer.AnsweredAt = DateTime.UtcNow;
        answer.TeacherComment = null;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
