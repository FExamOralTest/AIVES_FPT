using System.Collections.Concurrent;
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
/// Flow (local-first):
///   Init     → tạo MediaUpload record + chuẩn bị thư mục local C:\audio_review\{student}\{course}\{question}
///   Chunk    → ghi chunk tạm local (chunk_{index}.bin) — CHƯA đụng tới R2
///   Complete → gộp chunks thành 1 file local hoàn chỉnh → upload CHÍNH FILE LOCAL đó lên R2
///              → xóa chunk tạm → giữ file local để review → UpdateAudioUrl trong Answer → EnqueueJob
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
            // Bản ghi mới có kích thước khác bản ghi đang dở → không resume được.
            // Hủy phiên cũ (dọn chunk tạm) và tạo phiên mới từ đầu.
            var sameFile = existing.TotalChunks == request.TotalChunks
                           && existing.TotalSize == request.TotalSize;

            if (sameFile)
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

            _logger.LogInformation(
                "Discarding stale upload {UploadId} for student={StudentId} q={QuestionId}: " +
                "chunks {OldChunks}->{NewChunks}, size {OldSize}->{NewSize}",
                existing.UploadId, currentUserId, request.QuestionId,
                existing.TotalChunks, request.TotalChunks,
                existing.TotalSize, request.TotalSize);

            DeleteAllLocalFiles(existing);
            existing.UploadStatus = "ABORTED";
            existing.ReceivedChunkIndexes = new List<int>();
            await _uploadRepo.UpdateAsync(existing, ct);
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

        // File local cuối cùng: q_{questionId}.webm (gộp từ chunks)
        var localFilePath = Path.Combine(localDir, $"q_{request.QuestionId}{ext}");

        var upload = new MediaUpload
        {
            // UploadId client giữ nguyên (≤64 ký tự).
            UploadId = request.UploadId,
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
            "Upload session created (local-first): uploadId={UploadId}, objectKey={Key}, localPath={Path}",
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
        {
            // Phiên đã xong → trả idempotent (không phải lỗi).
            // User có thể nhấn "Submit" 2 lần / retry mạng → không được trả 400.
            if (upload.UploadStatus == "COMPLETED")
            {
                _logger.LogInformation(
                    "Chunk {ChunkIndex} sent to an already-COMPLETED upload {UploadId} — ignoring (idempotent).",
                    request.ChunkIndex, request.UploadId);
                return BuildChunkResponse(upload);
            }

            throw new InvalidOperationException($"Phiên upload đã ở trạng thái {upload.UploadStatus}.");
        }

        // Idempotent: chunk đã nhận rồi thì bỏ qua
        if (upload.ReceivedChunkIndexes.Contains(request.ChunkIndex))
        {
            _logger.LogDebug("Chunk {ChunkIndex} already received for {UploadId} — skipping.",
                request.ChunkIndex, request.UploadId);
            return BuildChunkResponse(upload);
        }

        var partNumber = request.ChunkIndex + 1; // S3 part number bắt đầu từ 1

        // 1. Lưu chunk file local: chunk_{uploadId}_{index}.bin
        // Luồng local-first: chunk CHƯA đẩy lên R2. R2 chỉ nhận 1 file hoàn chỉnh sau khi gộp.
        //
        // TÊN FILE PHẢI CHỨA uploadId: nếu dùng chung "chunk_{index}.bin" cho mọi phiên
        // upload của cùng (student, course, question) thì 2 phiên chạy SONG SONG sẽ tranh
        // nhau trên cùng 1 file → "The process cannot access the file ... being used by
        // another process" và gộp ra file âm thanh sai.
        var localDir = Path.GetDirectoryName(upload.LocalFilePath)!;
        var chunkPath = Path.Combine(localDir, BuildChunkFileName(upload.UploadId, request.ChunkIndex));

        // Serialize theo uploadId: merge (complete) không được đọc chunk đang ghi dở.
        var uploadLock = GetUploadLock(upload.UploadId);
        await uploadLock.WaitAsync(ct);
        try
        {
            // Ghi ATOMIC: tạm file → move đè. Nếu 2 request cùng uploadId chạy song song
            // (double-click submit / retry), ghi thẳng vào file đích sẽ ném
            // "The process cannot access the file ... being used by another process".
            // Với temp+move, request thứ 2 chỉ ghi đè file tạm riêng rồi move — không đụng handle của nhau.
            var tempChunkPath = chunkPath + ".tmp";
            await File.WriteAllBytesAsync(tempChunkPath, chunkBytes, ct);
            File.Move(tempChunkPath, chunkPath, overwrite: true);
        }
        finally
        {
            uploadLock.Release();
        }

        // 2. Đánh dấu chunk đã nhận
        upload.ReceivedChunkIndexes.Add(request.ChunkIndex);
        upload.ReceivedChunkIndexes.Sort();
        upload.UploadStatus = "UPLOADING";
        await _uploadRepo.UpdateAsync(upload, ct);

        _logger.LogInformation("Chunk {ChunkIndex}/{Total} saved locally: uploadId={UploadId}, path={Path}",
            request.ChunkIndex, upload.TotalChunks, upload.UploadId, chunkPath);

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

        // Lock theo thư mục local: 2 phiên upload song song của cùng (student, course, question)
        // sẽ cùng ghi file q_{questionId}.webm → phải serialize để không đè lên nhau.
        var localDir = Path.GetDirectoryName(upload.LocalFilePath!)!;
        var dirLock = GetDirectoryLock(localDir.ToLowerInvariant());
        var uploadLock = GetUploadLock(upload.UploadId);
        await uploadLock.WaitAsync(ct);
        await dirLock.WaitAsync(ct);
        try
        {
            // RE-CHECK trạng thái BÊN TRONG lock.
            // Nếu không, N request 'complete' song song sẽ cùng đọc trạng thái PENDING
            // (đọc ở trên, ngoài lock) → cả N đều đi vào merge. Request thứ 2 merge lúc
            // request thứ 1 đã xoá chunk → file gộp ra RỖNG (0 bytes) rồi upload 0 bytes lên R2.
            upload = await _uploadRepo.GetByIdAsync(request.UploadId, ct)
                ?? throw new KeyNotFoundException($"Upload session '{request.UploadId}' không tồn tại.");

            if (upload.UploadStatus == "COMPLETED")
            {
                _logger.LogInformation(
                    "Upload {UploadId} already COMPLETED — returning existing result (idempotent).",
                    upload.UploadId);
                return new CompleteChunkedUploadResponse
                {
                    UploadId = upload.UploadId,
                    AnswerId = upload.AnswerId ?? 0,
                    AudioKey = upload.ObjectKey!,
                    LocalFilePath = upload.LocalFilePath,
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

            // 1. Gộp chunks thành 1 file local hoàn chỉnh (giữ nguyên file gốc để review offline).
            await MergeChunksToLocalFileAsync(upload, ct);

            // Guard: file gộp rỗng = chunk đã bị xoá/mất → KHÔNG upload 0 bytes lên R2
            // (sẽ ghi đè object hợp lệ bằng file rỗng).
            var mergedSize = new FileInfo(upload.LocalFilePath!).Length;
            if (mergedSize == 0)
            {
                // Dọn file rỗng để không gây nhầm lẫn cho lần upload sau.
                try { File.Delete(upload.LocalFilePath!); } catch { /* best-effort */ }

                throw new InvalidOperationException(
                    "File âm thanh gộp ra rỗng (0 bytes) — chunk đã bị mất hoặc đã được xoá. " +
                    "Vui lòng ghi lại và upload lại.");
            }

            // 2. Upload CHÍNH FILE LOCAL đó lên R2 (1 lần PutObject duy nhất).
            //    R2 giữ đúng 1 object = file gốc đã gộp, giống hệt bản ghi âm ban đầu.
            var uploadResult = await _storage.UploadLocalFileAsync(
                upload.ObjectKey!, upload.LocalFilePath!, upload.ContentType, ct);

            var objectKey = uploadResult.ObjectKey;

            // Nếu R2 lỗi (sai credentials / mạng) → file chỉ nằm trên đĩa local.
            // Ghi rõ vào log + trả cảnh báo về client để không ai tưởng audio đã lên R2.
            if (uploadResult.UsedFallback)
            {
                _logger.LogWarning(
                    "Audio for uploadId={UploadId} was NOT stored on Cloudflare R2 — only on local disk '{LocalPath}'. Reason: {Reason}. Hint: {Hint}",
                    upload.UploadId, upload.LocalFilePath, uploadResult.ErrorMessage, uploadResult.Hint);
            }

            // 3. Xóa chunk tạm local — chỉ giữ lại file gộp cuối cùng (bản gốc để review).
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
                LocalFilePath = upload.LocalFilePath,
                Status = "COMPLETED",
                CompletedAt = upload.CompletedAt.Value,
                StorageFallback = uploadResult.UsedFallback,
                StorageError = uploadResult.ErrorMessage,
                StorageHint = uploadResult.Hint
            };
        }
        finally
        {
            dirLock.Release();
            uploadLock.Release();
        }
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

        // Luồng local-first: không có multipart session nào trên R2 để abort.
        // (Nếu có phiên multipart cũ từ trước khi đổi flow thì abort cho sạch.)
        var r2UploadId = upload.R2UploadId;
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
    /// Gộp tất cả chunk_{uploadId}_{index}.bin theo thứ tự vào file LocalFilePath.
    ///
    /// Ghi vào file TẠM rồi move đè → nếu merge lỗi giữa chừng, file gốc đang có
    /// vẫn nguyên vẹn (không bị truncate thành 0 bytes).
    /// </summary>
    private async Task MergeChunksToLocalFileAsync(MediaUpload upload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(upload.LocalFilePath)) return;

        var localDir = Path.GetDirectoryName(upload.LocalFilePath)!;
        var tempOutput = upload.LocalFilePath + ".merging";

        await using (var output = new FileStream(
            tempOutput, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
        {
            for (int i = 0; i < upload.TotalChunks; i++)
            {
                var chunkPath = Path.Combine(localDir, BuildChunkFileName(upload.UploadId, i));
                if (!File.Exists(chunkPath))
                {
                    _logger.LogWarning("Chunk file missing during merge: {Path}", chunkPath);
                    continue;
                }

                await using var chunkStream = new FileStream(
                    chunkPath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                await chunkStream.CopyToAsync(output, ct);
            }
        }

        File.Move(tempOutput, upload.LocalFilePath, overwrite: true);

        _logger.LogInformation("Merged {TotalChunks} chunks → {LocalPath}",
            upload.TotalChunks, upload.LocalFilePath);
    }

    /// <summary>
    /// Tên file chunk tạm. PHẢI chứa uploadId để các phiên upload song song
    /// của cùng (student, course, question) không tranh nhau trên 1 file.
    /// </summary>
    private static string BuildChunkFileName(string uploadId, int chunkIndex)
        => $"chunk_{uploadId}_{chunkIndex}.bin";

    /// <summary>
    /// Lock theo thư mục local để 2 phiên upload của cùng (student, course, question)
    /// không ghi đè lên nhau file q_{questionId}.webm.
    /// Key = đường dẫn thư mục local (đã lowercase để không phân biệt hoa/thường trên Windows).
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> DirectoryLocks = new();

    private static SemaphoreSlim GetDirectoryLock(string dir)
        => DirectoryLocks.GetOrAdd(dir, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// Lock theo uploadId — serialize việc ghi chunk + gộp cho 1 phiên upload.
    /// Bảo đảm merge không đọc chunk đang được ghi dở.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> UploadLocks = new();

    private static SemaphoreSlim GetUploadLock(string uploadId)
        => UploadLocks.GetOrAdd(uploadId, _ => new SemaphoreSlim(1, 1));

    private static void DeleteChunkFiles(MediaUpload upload)
    {
        if (string.IsNullOrWhiteSpace(upload.LocalFilePath)) return;
        var localDir = Path.GetDirectoryName(upload.LocalFilePath)!;

        for (int i = 0; i < upload.TotalChunks; i++)
        {
            var chunkPath = Path.Combine(localDir, BuildChunkFileName(upload.UploadId, i));
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
}
