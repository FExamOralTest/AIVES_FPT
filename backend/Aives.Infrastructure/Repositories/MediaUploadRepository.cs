using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class MediaUploadRepository : IMediaUploadRepository
{
    private readonly AivesDbContext _db;

    public MediaUploadRepository(AivesDbContext db) => _db = db;

    public Task<MediaUpload?> GetByIdAsync(string uploadId, CancellationToken ct = default)
        => _db.MediaUploads.FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);

    public Task<MediaUpload?> GetActiveAsync(int sessionId, int studentId, int questionId, CancellationToken ct = default)
        => _db.MediaUploads.FirstOrDefaultAsync(
            u => u.SessionId == sessionId
              && u.StudentId == studentId
              && u.QuestionId == questionId
              && (u.UploadStatus == "PENDING" || u.UploadStatus == "UPLOADING"),
            ct);

    public async Task<MediaUpload> CreateAsync(MediaUpload upload, CancellationToken ct = default)
    {
        _db.MediaUploads.Add(upload);
        await _db.SaveChangesAsync(ct);
        return upload;
    }

    public async Task UpdateAsync(MediaUpload upload, CancellationToken ct = default)
    {
        _db.MediaUploads.Update(upload);
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkChunkReceivedAsync(string uploadId, int chunkIndex, CancellationToken ct = default)
    {
        var upload = await _db.MediaUploads.FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);
        if (upload == null) return;

        if (!upload.ReceivedChunkIndexes.Contains(chunkIndex))
        {
            upload.ReceivedChunkIndexes.Add(chunkIndex);
            upload.ReceivedChunkIndexes.Sort();
            upload.UploadStatus = "UPLOADING";
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task LinkAnswerAsync(string uploadId, int answerId, CancellationToken ct = default)
    {
        var upload = await _db.MediaUploads.FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);
        if (upload == null) return;

        upload.AnswerId = answerId;
        await _db.SaveChangesAsync(ct);
    }

    public Task<MediaUpload?> GetCompletedAsync(
        int sessionId, int studentId, int questionId, CancellationToken ct = default)
        => _db.MediaUploads
            .Where(u => u.SessionId == sessionId
                     && u.StudentId == studentId
                     && u.QuestionId == questionId
                     && u.UploadStatus == "COMPLETED")
            .OrderByDescending(u => u.CompletedAt)
            .FirstOrDefaultAsync(ct);

    public async Task DeleteStaleAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var stale = await _db.MediaUploads
            .Where(u => (u.UploadStatus == "FAILED" || u.UploadStatus == "ABORTED")
                     && u.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (stale.Count > 0)
        {
            _db.MediaUploads.RemoveRange(stale);
            await _db.SaveChangesAsync(ct);
        }
    }
}
