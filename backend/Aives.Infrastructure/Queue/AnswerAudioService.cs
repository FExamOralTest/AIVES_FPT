using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Implementation của <see cref="IAnswerAudioService"/>.
/// Upload audio lên Cloudflare R2, persist audio_url, enqueue Hangfire job STT + AI eval.
/// Nhận byte[] thay vì IFormFile để tránh "Cannot access a closed Stream" —
/// stream của IFormFile bị tied to request body và có thể bị ASP.NET dispose sớm.
/// </summary>
public class AnswerAudioService : IAnswerAudioService
{
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/webm", "audio/wav", "audio/wave", "audio/x-wav",
        "audio/mpeg", "audio/mp3", "audio/mp4", "audio/ogg",
        "audio/aac", "audio/flac", "audio/x-flac",
        "application/octet-stream"
    };

    private readonly IAnswerRepository _answerRepository;
    private readonly IStorageService _storageService;
    private readonly IQueueService _queueService;
    private readonly ILogger<AnswerAudioService> _logger;

    public AnswerAudioService(
        IAnswerRepository answerRepository,
        IStorageService storageService,
        IQueueService queueService,
        ILogger<AnswerAudioService> logger)
    {
        _answerRepository = answerRepository;
        _storageService = storageService;
        _queueService = queueService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<UploadAnswerAudioResponse> UploadAudioAsync(
        int answerId,
        byte[] fileBytes,
        string fileName,
        string contentType,
        int currentUserId,
        UserRole currentUserRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate.
        if (fileBytes == null || fileBytes.Length == 0)
            throw new ArgumentException("File audio không được để trống.");
        if (fileBytes.Length > MaxFileSizeBytes)
            throw new ArgumentException($"File vượt quá giới hạn {MaxFileSizeBytes / 1024 / 1024} MB.");
        if (!AllowedContentTypes.Contains(contentType ?? string.Empty))
            throw new ArgumentException(
                $"Định dạng '{contentType}' không được hỗ trợ. Dùng: webm, wav, mp3, ogg, aac, flac.");

        // 2. Load Answer + quan hệ để kiểm tra quyền.
        var answer = await _answerRepository.GetByIdWithDetailsAsync(answerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Answer {answerId} không tồn tại.");

        var result = answer.Result
            ?? throw new KeyNotFoundException($"Result cho Answer {answerId} không tìm thấy.");

        // 3. Kiểm tra quyền.
        EnsureCanUpload(result, currentUserId, currentUserRole);

        if (result.Status is SubmissionStatus.CANCELLED or SubmissionStatus.PUBLISHED)
            throw new InvalidOperationException("Invalidated or published attempts cannot accept audio.");

        // 4. ADMIN bypass check đã chấm điểm; STUDENT/các role khác thì không được đè.
        if (currentUserRole != UserRole.ADMIN
            && answer.Status == AnswerStatus.EVALUATED
            && answer.FinalScore.HasValue)
        {
            throw new InvalidOperationException(
                "Câu trả lời này đã được giáo viên chấm điểm. Không thể upload audio mới.");
        }

        // 5. Xây dựng object key.
        var session = result.Session
            ?? throw new KeyNotFoundException("Session cho Answer không tìm thấy.");
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".webm";
        var objectKey = $"exams/{session.ExamId}/sessions/{result.SessionId}" +
                        $"/students/{result.StudentId}/q_{answer.QuestionId}_{DateTime.UtcNow.Ticks}{extension}";

        // 6. Upload — dùng MemoryStream từ byte[] đã buffer sẵn, hoàn toàn độc lập với request.
        using var memoryStream = new MemoryStream(fileBytes, writable: false);
        var audioKey = await _storageService.UploadAudioAsync(
            objectKey,
            memoryStream,
            contentType ?? "audio/webm",
            cancellationToken);

        _logger.LogInformation(
            "Audio uploaded: AnswerId={AnswerId}, Key={Key}, UserId={UserId}",
            answerId, audioKey, currentUserId);

        // 7. Persist audio_url vào DB.
        await _answerRepository.UpdateAudioUrlAsync(answerId, audioKey, cancellationToken);

        // 8. Enqueue Hangfire job STT + AI eval.
        // Metadata được tạo qua factory của contract (task 1.4) để mọi call site
        // sinh ra metadata giống hệt nhau.
        var metadata = QueueJobContract.Create(
            answerId,
            answer.ResultId,
            audioKey,
            attempt: 1);
        var jobId = _queueService.EnqueueAnswerProcessing(metadata);

        if (string.IsNullOrEmpty(jobId))
            _logger.LogWarning("Job dedup triggered for AnswerId={AnswerId}.", answerId);
        else
        {
            // Lưu JobId vào DB để tra cứu job không chỉ từ Hangfire dashboard.
            await _answerRepository.SetJobIdAsync(answerId, jobId, cancellationToken);
            _logger.LogInformation("Hangfire job enqueued: AnswerId={AnswerId}, JobId={JobId}", answerId, jobId);
        }

        return new UploadAnswerAudioResponse
        {
            AnswerId = answerId,
            AudioKey = audioKey,
            JobId = jobId,
            Status = AnswerStatus.PROCESSING.ToString(),
            UploadedAt = DateTime.UtcNow
        };
    }

    private static void EnsureCanUpload(Result result, int userId, UserRole role)
    {
        if (role == UserRole.ADMIN) return;

        if (role == UserRole.STUDENT)
        {
            if (result.StudentId != userId)
                throw new UnauthorizedAccessException(
                    "Bạn chỉ có thể upload audio cho câu trả lời của chính mình.");
            return;
        }

        throw new UnauthorizedAccessException(
            "Chỉ học sinh hoặc admin mới có thể upload audio nộp bài.");
    }
}
