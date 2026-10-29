using Aives.Application.Exams.Interfaces;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Implementation của <see cref="IChunkedAudioUploadService"/>.
///
/// Flow:
///   Init     → tạo MediaUpload record + InitiateMultipartUpload trên R2
///   Chunk    → lưu chunk local + UploadPart trên R2 (đồng thời)
///   Complete → gộp local thành 1 file → CompleteMultipartUpload R2 → xóa chunks local
///              → UpdateAudioUrl trong Answer → EnqueueJob
/// </summary>
public class ChunkedAudioUploadService : IChunkedAudioUploadService
{
    /// <summary>Thư mục gốc lưu audio review trên máy server (Windows).</summary>
    private const string AudioReviewRoot = @"C:\audio_review";

    private readonly IMediaUploadRepository _uploadRepo;
    private readonly IAnswerRepository _answerRepo;
    private readonly IExamSessionRepository _sessionRepo;
    private readonly IStorageService _storage;
    private readonly IQueueService _queue;
    private readonly ILogger<ChunkedAudioUploadService> _logger;

    public ChunkedAudioUploadService(
        IMediaUploadRepository uploadRepo,
        IAnswerRepository answerRepo,
        IExamSessionRepository sessionRepo,
        IStorageService storage,
        IQueueService queue,
        ILogger<ChunkedAudioUploadService> logger)
    {
        _uploadRepo = uploadRepo;
        _answerRepo = answerRepo;
        _sessionRepo = sessionRepo;
        _storage = storage;
        _queue = queue;
        _logger = logger;
    }

    // -----------------------------------------------------------------------
    // Init
    // -----------------------------------------------------------------------

    public async Task<InitChunkedUploadResponse> InitAsync(
        InitChunkedUploadRequest request,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken ct = default)
    {
        // Cho phép resume: nếu đã có phiên PENDING/UPLOADING cùng (session/student/question) → trả lại
        var existing = await _uploadRepo.GetActiveAsync(
            request.SessionId, currentUserId, request.QuestionId, ct);

        if (existing != null)
        {
            _logger.LogInformation(
                "Resuming upload {UploadId} for student={StudentId} q={QuestionId}",
                existing.UploadId, currentUserId, request.QuestionId);

            return new InitChunkedUploadResponse
            {
                UploadId = existing.UploadId,
                ReceivedChunks = existing.ReceivedChunkIndexes,
                Status = existing.UploadStatus,
                LocalFilePath = existing.LocalFilePath
            };
        }

        // Lấy tên môn học để xây đường dẫn local
        var session = await _sessionRepo.GetByIdAsync(request.SessionId)
            ?? throw new KeyNotFoundException($"Session {request.SessionId} không tồn tại.");
        var courseName = SanitizePath(session.Exam?.Course?.CourseName ?? $"session_{request.SessionId}");

        // Xây local dir: C:\audio_review\{studentId}\{courseName}\{questionId}
        var localDir = Path.Combine(AudioReviewRoot,
            currentUserId.ToString(), courseName, request.QuestionId.ToString());
        Directory.CreateDirectory(localDir);

        // R2 object key: exams/{examId}/sessions/{sessionId}/students/{studentId}/q_{questionId}.webm
        var ext = Path.GetExtension(request.FileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".webm";
        var objectKey = $"exams/{session.ExamId}/sessions/{request.SessionId}" +
                        $"/students/{currentUserId}/q_{request.QuestionId}{ext}";

        // Khởi tạo multipart session trên R2 và lấy uploadId từ R2
        // (uploadId của R2 khác với uploadId client — ta lưu cả hai)
        string r2UploadId;
        try
        {
            r2UploadId = await _storage.InitiateMultipartUploadAsync(objectKey, request.ContentType, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "R2 InitiateMultipartUpload failed for key={Key}", objectKey);
            throw new InvalidOperationException("Không thể khởi tạo upload session trên Cloudflare R2: " + ex.Message);
        }

        // File local cuối cùng: q_{questionId}.webm (gộp từ chunks)
        var localFilePath = Path.Combine(localDir, $"q_{request.QuestionId}{ext}");

        var upload = new MediaUpload
        {
            // UploadId client giữ nguyên (≤64 ký tự) — R2 uploadId nằm ở cột riêng R2UploadId.
            UploadId = request.UploadId,
            R2UploadId = r2UploadId,
            SessionId = request.SessionId,
            StudentId = currentUserId,
            QuestionId = request.QuestionId,
            FileName = request.FileName,
            ContentType = request.ContentType,
            TotalSize = request.TotalSize,
            ChunkSize = request.ChunkSize,
            TotalChunks = request.TotalChunks,
            ReceivedChunkIndexes = new List<int>(),
            UploadStatus = "PENDING",
            ObjectKey = objectKey,
            LocalFilePath = localFilePath,
            CreatedAt = DateTime.UtcNow
        };

        await _uploadRepo.CreateAsync(upload, ct);

        _logger.LogInformation(
            "Upload session created: uploadId={UploadId}, objectKey={Key}, localPath={Path}",
            upload.UploadId, objectKey, localFilePath);

        return new InitChunkedUploadResponse
        {
            UploadId = upload.UploadId,
            ReceivedChunks = new List<int>(),
            Status = "PENDING",
            LocalFilePath = localFilePath
        };
    }

    // -----------------------------------------------------------------------
    // Upload Chunk
    // -----------------------------------------------------------------------

    public async Task<UploadChunkResponse> UploadChunkAsync(
        UploadChunkRequest request,
        byte[] chunkBytes,
        int currentUserId,
        CancellationToken ct = default)
    {
        var upload = await _uploadRepo.GetByIdAsync(request.UploadId, ct)
            ?? throw new KeyNotFoundException($"Upload session '{request.UploadId}' không tồn tại.");

        if (upload.StudentId != currentUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền upload cho phiên này.");

        if (upload.UploadStatus is "COMPLETED" or "ABORTED" or "FAILED")
            throw new InvalidOperationException($"Phiên upload đã ở trạng thái {upload.UploadStatus}.");

        // Idempotent: chunk đã nhận rồi thì bỏ qua
        if (upload.ReceivedChunkIndexes.Contains(request.ChunkIndex))
        {
            _logger.LogDebug("Chunk {ChunkIndex} already received for {UploadId} — skipping.",
                request.ChunkIndex, request.UploadId);
            return BuildChunkResponse(upload);
        }

        var r2UploadId = upload.R2UploadId
            ?? throw new InvalidOperationException("Phiên upload không có R2 uploadId — hãy gọi init lại.");
        var partNumber = request.ChunkIndex + 1; // S3 part number bắt đầu từ 1

        // 1. Lưu chunk file local: chunk_{index}.bin
        var localDir = Path.GetDirectoryName(upload.LocalFilePath)!;
        var chunkPath = Path.Combine(localDir, $"chunk_{request.ChunkIndex}.bin");
        await File.WriteAllBytesAsync(chunkPath, chunkBytes, ct);

        // 2. Upload part lên R2
        var etag = await _storage.UploadPartAsync(
            upload.ObjectKey!, r2UploadId, partNumber, chunkBytes, ct);

        // Lưu etag vào cột PartETags (JSON array) — cần cho bước CompleteMultipartUpload.
        // Format: [{"p":1,"e":"\"etag1\""},{"p":2,"e":"\"etag2\""},...]
        upload.PartETags = AppendEtag(upload.PartETags, partNumber, etag);

        // 3. Đánh dấu chunk đã nhận
        upload.ReceivedChunkIndexes.Add(request.ChunkIndex);
        upload.ReceivedChunkIndexes.Sort();
        upload.UploadStatus = "UPLOADING";
        await _uploadRepo.UpdateAsync(upload, ct);

        _logger.LogInformation("Chunk {ChunkIndex}/{Total} received: uploadId={UploadId}",
            request.ChunkIndex, upload.TotalChunks, upload.UploadId);

        return BuildChunkResponse(upload);
    }

    // -----------------------------------------------------------------------
    // Complete
    // -----------------------------------------------------------------------

    public async Task<CompleteChunkedUploadResponse> CompleteAsync(
        CompleteChunkedUploadRequest request,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken ct = default)
    {
        var upload = await _uploadRepo.GetByIdAsync(request.UploadId, ct)
            ?? throw new KeyNotFoundException($"Upload session '{request.UploadId}' không tồn tại.");

        if (upload.StudentId != currentUserId && currentUserRole != UserRole.ADMIN)
            throw new UnauthorizedAccessException("Không có quyền complete phiên này.");

        if (upload.UploadStatus == "COMPLETED")
        {
            // Idempotent — đã complete rồi, trả lại kết quả cũ
            return new CompleteChunkedUploadResponse
            {
                UploadId = upload.UploadId,
                AnswerId = upload.AnswerId ?? 0,
                AudioKey = upload.ObjectKey!,
                Status = "COMPLETED",
                CompletedAt = upload.CompletedAt ?? DateTime.UtcNow
            };
        }

        // Kiểm tra đủ chunks chưa
        var missing = Enumerable.Range(0, upload.TotalChunks)
            .Except(upload.ReceivedChunkIndexes)
            .ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Còn thiếu {missing.Count} chunk(s): {string.Join(", ", missing.Take(10))}.");

        var r2UploadId = upload.R2UploadId
            ?? throw new InvalidOperationException("Phiên upload không có R2 uploadId — hãy gọi init lại.");

        // Parse etags từ cột PartETags
        var parts = ParseEtags(upload.PartETags);
        if (parts.Count == 0)
            throw new InvalidOperationException("Không tìm thấy ETag của các parts đã upload.");

        // 1. Complete multipart trên R2 → R2 giữ DUY NHẤT 1 object = file gốc đã gộp.
        var objectKey = await _storage.CompleteMultipartUploadAsync(
            upload.ObjectKey!, r2UploadId, parts, ct);

        // 2. Gộp chunks thành 1 file local hoàn chỉnh (để teacher review audio offline).
        await MergeChunksToLocalFileAsync(upload, ct);

        // 3. Xóa chunk files local — giữ lại file gộp cuối cùng (bản gốc để review).
        DeleteChunkFiles(upload);

        // 4. Cập nhật trạng thái upload session
        upload.UploadStatus = "COMPLETED";
        upload.CompletedAt = DateTime.UtcNow;
        upload.PartETags = null;

        // Nếu client đã có Answer sẵn (upload audio cho Answer cũ) → gắn luôn.
        if (request.AnswerId > 0)
        {
            upload.AnswerId = request.AnswerId;
            await _answerRepo.UpdateAudioUrlAsync(request.AnswerId, objectKey, ct);

            // Enqueue STT job cho Answer này.
            var answer = await _answerRepo.GetByIdWithDetailsAsync(request.AnswerId, ct);
            var metadata = QueueJobContract.Create(
                request.AnswerId,
                answer?.ResultId,
                objectKey,
                attempt: 1);
            var jobId = _queue.EnqueueAnswerProcessing(metadata);
            if (!string.IsNullOrEmpty(jobId))
                await _answerRepo.SetJobIdAsync(request.AnswerId, jobId, ct);
        }

        await _uploadRepo.UpdateAsync(upload, ct);

        _logger.LogInformation(
            "Upload completed: uploadId={UploadId}, objectKey={Key}, answerId={AnswerId}",
            upload.UploadId, objectKey, upload.AnswerId);

        return new CompleteChunkedUploadResponse
        {
            UploadId = upload.UploadId,
            AnswerId = upload.AnswerId ?? 0,
            AudioKey = objectKey,
            Status = "COMPLETED",
            CompletedAt = upload.CompletedAt.Value
        };
    }

    // -----------------------------------------------------------------------
    // Abort
    // -----------------------------------------------------------------------

    public async Task<AbortChunkedUploadResponse> AbortAsync(
        string uploadId,
        int currentUserId,
        CancellationToken ct = default)
    {
        var upload = await _uploadRepo.GetByIdAsync(uploadId, ct);
        if (upload == null)
            return new AbortChunkedUploadResponse { UploadId = uploadId, Status = "ABORTED" };

        if (upload.StudentId != currentUserId)
            throw new UnauthorizedAccessException("Không có quyền abort phiên này.");

        // R2 uploadId nằm ở cột riêng (R2UploadId) — KHÔNG phải client uploadId.
        var r2UploadId = upload.R2UploadId;

        // Abort R2 multipart (chỉ khi còn uploadId của R2)
        if (!string.IsNullOrWhiteSpace(upload.ObjectKey) && !string.IsNullOrWhiteSpace(r2UploadId))
            await _storage.AbortMultipartUploadAsync(upload.ObjectKey, r2UploadId, ct);

        // Xóa tất cả files local (chunks + file gộp nếu có)
        DeleteAllLocalFiles(upload);

        upload.UploadStatus = "ABORTED";
        upload.LastError = null;
        await _uploadRepo.UpdateAsync(upload, ct);

        return new AbortChunkedUploadResponse { UploadId = uploadId, Status = "ABORTED" };
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Gộp tất cả chunk_{index}.bin theo thứ tự vào file LocalFilePath.
    /// </summary>
    private async Task MergeChunksToLocalFileAsync(MediaUpload upload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(upload.LocalFilePath)) return;

        var localDir = Path.GetDirectoryName(upload.LocalFilePath)!;
        await using var output = new FileStream(
            upload.LocalFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

        for (int i = 0; i < upload.TotalChunks; i++)
        {
            var chunkPath = Path.Combine(localDir, $"chunk_{i}.bin");
            if (!File.Exists(chunkPath))
            {
                _logger.LogWarning("Chunk file missing during merge: {Path}", chunkPath);
                continue;
            }

            await using var chunkStream = new FileStream(
                chunkPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
            await chunkStream.CopyToAsync(output, ct);
        }

        _logger.LogInformation("Merged {TotalChunks} chunks → {LocalPath}",
            upload.TotalChunks, upload.LocalFilePath);
    }

    private static void DeleteChunkFiles(MediaUpload upload)
    {
        if (string.IsNullOrWhiteSpace(upload.LocalFilePath)) return;
        var localDir = Path.GetDirectoryName(upload.LocalFilePath)!;

        for (int i = 0; i < upload.TotalChunks; i++)
        {
            var chunkPath = Path.Combine(localDir, $"chunk_{i}.bin");
            if (File.Exists(chunkPath))
                File.Delete(chunkPath);
        }
    }

    private static void DeleteAllLocalFiles(MediaUpload upload)
    {
        DeleteChunkFiles(upload);
        if (!string.IsNullOrWhiteSpace(upload.LocalFilePath) && File.Exists(upload.LocalFilePath))
            File.Delete(upload.LocalFilePath);
    }

    /// <summary>Sanitize tên thư mục — loại bỏ ký tự không hợp lệ trên Windows.</summary>
    private static string SanitizePath(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray())
            .Replace(' ', '_')
            .ToLowerInvariant();
    }

    private static UploadChunkResponse BuildChunkResponse(MediaUpload upload) =>
        new()
        {
            UploadId = upload.UploadId,
            ChunkIndex = upload.ReceivedChunkIndexes.LastOrDefault(),
            ReceivedChunks = upload.ReceivedChunkIndexes,
            TotalChunks = upload.TotalChunks,
            IsComplete = upload.ReceivedChunkIndexes.Count >= upload.TotalChunks
        };

    // ETag storage: dùng JSON đơn giản trong LastError (field dự phòng).
    // Format: [{"p":1,"e":"\"abc\""},{"p":2,"e":"\"def\""}]
    private static string AppendEtag(string? existing, int partNumber, string etag)
    {
        var list = ParseEtagsRaw(existing);
        list.RemoveAll(x => x.p == partNumber); // replace nếu retry
        list.Add((partNumber, etag));
        return System.Text.Json.JsonSerializer.Serialize(
            list.OrderBy(x => x.p)
                .Select(x => new { p = x.p, e = x.e })
                .ToList());
    }

    private static List<(int PartNumber, string ETag)> ParseEtags(string? json)
    {
        var raw = ParseEtagsRaw(json);
        return raw.Select(x => (x.p, x.e)).ToList();
    }

    private static List<(int p, string e)> ParseEtagsRaw(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Select(el => (
                    p: el.GetProperty("p").GetInt32(),
                    e: el.GetProperty("e").GetString() ?? ""))
                .ToList();
        }
        catch { return new(); }
    }
}
