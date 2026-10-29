using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Interfaces.Services;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Queue.Interfaces;
using Aives.Application.Viva.DTOs;
using Aives.Application.Viva.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Application.Viva.Services;

public class VivaSessionService : IVivaSessionService
{
    private readonly IExamSessionRepository _sessionRepository;
    private readonly IExamRepository _examRepository;
    private readonly IQuestionBankRepository _questionRepository;
    private readonly IResultRepository _resultRepository;
    private readonly IAiServiceClient _aiServiceClient;
    private readonly IStorageService _storageService;
    private readonly IQueueService _queueService;
    private readonly IMediaUploadRepository _mediaUploadRepo;
    private readonly ILogger<VivaSessionService> _logger;
    private readonly IRetakeService? _retakes;

    public VivaSessionService(
        IExamSessionRepository sessionRepository,
        IExamRepository examRepository,
        IQuestionBankRepository questionRepository,
        IResultRepository resultRepository,
        IAiServiceClient aiServiceClient,
        IStorageService storageService,
        IQueueService queueService,
        IMediaUploadRepository mediaUploadRepo,
        ILogger<VivaSessionService> logger, IRetakeService? retakes = null)
    {
        _sessionRepository = sessionRepository;
        _examRepository = examRepository;
        _questionRepository = questionRepository;
        _resultRepository = resultRepository;
        _aiServiceClient = aiServiceClient;
        _storageService = storageService;
        _queueService = queueService;
        _mediaUploadRepo = mediaUploadRepo;
        _logger = logger;
        _retakes = retakes;
    }

    public async Task<StartVivaSessionResponse> StartSessionAsync(int sessionId, int studentId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"Ca thi ID {sessionId} không tồn tại.");

        if (session.Status == SessionStatus.CANCELLED)
            throw new InvalidOperationException("Ca thi này đã bị hủy.");

        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)
            ?? throw new UnauthorizedAccessException("Sinh viên chưa ghi danh (enroll) vào ca thi này.");

        var retakeStart = _retakes is null ? null : await _retakes.StartAsync(sessionId, studentId);
        if (_retakes is not null) await _retakes.EnsureWritableAsync(sessionId, studentId, checkDeadline: false);

        if (enrollment.Status == SessionStatus.COMPLETED)
            throw new InvalidOperationException("Sinh viên đã hoàn thành ca thi này.");

        if (enrollment.Status == SessionStatus.CANCELLED)
            throw new InvalidOperationException("Đăng ký ca thi này đã bị hủy.");

        var now = DateTime.UtcNow;
        if (session.Status == SessionStatus.COMPLETED || now >= session.EndTime)
            throw new InvalidOperationException("Ca thi này đã kết thúc.");

        if (session.Status != SessionStatus.IN_PROGRESS || now < session.StartTime)
            throw new InvalidOperationException("Ca thi chưa mở. Vui lòng chờ đến giờ thi và giảng viên mở ca.");

        // Mark enrollment as IN_PROGRESS
        enrollment.Status = SessionStatus.IN_PROGRESS;
        enrollment.StartedAt ??= DateTime.UtcNow;
        enrollment.LastActivityAt = DateTime.UtcNow;
        enrollment.CurrentQuestionIndex ??= 0;
        await _sessionRepository.SaveChangesAsync();

        // Get or initialize Result record
        var result = await _resultRepository.GetBySessionAndStudentAsync(sessionId, studentId);
        if (result == null)
        {
            result = new Result
            {
                SessionId = sessionId,
                StudentId = studentId,
                Status = SubmissionStatus.PENDING_REVIEW,
                SubmittedAt = DateTime.UtcNow
            };
            await _resultRepository.CreateAsync(result);
        }

        var exam = await _examRepository.GetByIdWithQuestionsAsync(session.ExamId)
            ?? throw new KeyNotFoundException($"Đề thi ID {session.ExamId} không tồn tại.");

        var examQuestions = exam.ExamQuestions.OrderBy(q => q.QuestionId).ToList();
        var totalQuestions = examQuestions.Count;
        var currentIndex = enrollment.CurrentQuestionIndex.Value;

        VivaQuestionDto? currentQuestionDto = null;
        if (currentIndex < totalQuestions)
        {
            var currentEq = examQuestions[currentIndex];
            currentQuestionDto = new VivaQuestionDto
            {
                QuestionId = currentEq.QuestionId,
                QuestionIndex = currentIndex,
                TotalQuestions = totalQuestions,
                QuestionText = currentEq.Question?.QuestionText ?? "",
                Level = currentEq.Question?.Level ?? 1,
                MaxScore = currentEq.Question?.MaxScore ?? currentEq.QuestionScore,
                IsFollowUpAllowed = currentEq.Question?.IsFollowUpAllowed ?? true,
                IsFollowUp = false
            };
        }

        return new StartVivaSessionResponse
        {
            SessionId = session.SessionId,
            SessionName = session.SessionName,
            ExamTitle = exam.Title,
            DurationMinutes = retakeStart?.DurationMinutes ?? exam.DurationMinutes,
            EndsAt = retakeStart?.EndsAt ?? new[] { enrollment.StartedAt!.Value.AddMinutes(exam.DurationMinutes), session.EndTime }.Min(),
            TotalQuestions = totalQuestions,
            CurrentQuestionIndex = currentIndex,
            CurrentQuestion = currentQuestionDto,
            StartedAt = enrollment.StartedAt
        };
    }

    public async Task<VivaQuestionDto> GetCurrentQuestionAsync(int sessionId, int studentId)
    {
        if (_retakes is not null) await _retakes.EnsureWritableAsync(sessionId, studentId);
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"Ca thi ID {sessionId} không tồn tại.");

        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)
            ?? throw new UnauthorizedAccessException("Sinh viên chưa tham gia ca thi này.");

        if (enrollment.Status == SessionStatus.COMPLETED)
            throw new InvalidOperationException("Sinh viên đã kết thúc ca thi này.");

        var exam = await _examRepository.GetByIdWithQuestionsAsync(session.ExamId)
            ?? throw new KeyNotFoundException("Đề thi không tồn tại.");

        var examQuestions = exam.ExamQuestions.OrderBy(q => q.QuestionId).ToList();
        var totalQuestions = examQuestions.Count;
        var currentIndex = enrollment.CurrentQuestionIndex ?? 0;

        if (currentIndex >= totalQuestions)
            throw new InvalidOperationException("Đã hoàn thành tất cả các câu hỏi trong ca thi.");

        var currentEq = examQuestions[currentIndex];
        return new VivaQuestionDto
        {
            QuestionId = currentEq.QuestionId,
            QuestionIndex = currentIndex,
            TotalQuestions = totalQuestions,
            QuestionText = currentEq.Question?.QuestionText ?? "",
            Level = currentEq.Question?.Level ?? 1,
            MaxScore = currentEq.Question?.MaxScore ?? currentEq.QuestionScore,
            IsFollowUpAllowed = currentEq.Question?.IsFollowUpAllowed ?? true,
            IsFollowUp = false
        };
    }

    public async Task<SubmitVivaAnswerResponse> SubmitAnswerAsync(
        int sessionId, int studentId, SubmitVivaAnswerRequest request,
        CancellationToken ct = default)
    {
        if (_retakes is not null) await _retakes.EnsureWritableAsync(sessionId, studentId);
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"Ca thi ID {sessionId} không tồn tại.");

        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)
            ?? throw new UnauthorizedAccessException("Sinh viên chưa ghi danh vào ca thi này.");

        if (enrollment.Status == SessionStatus.COMPLETED)
            throw new InvalidOperationException("Ca thi đã hoàn thành, không thể nộp thêm câu trả lời.");

        var result = await _resultRepository.GetBySessionAndStudentAsync(sessionId, studentId);
        if (result == null)
        {
            result = new Result
            {
                SessionId = sessionId,
                StudentId = studentId,
                Status = SubmissionStatus.PENDING_REVIEW,
                SubmittedAt = DateTime.UtcNow
            };
            result = await _resultRepository.CreateAsync(result);
        }

        var question = await _questionRepository.GetByIdAsync(request.QuestionId)
            ?? throw new KeyNotFoundException($"Câu hỏi ID {request.QuestionId} không tồn tại.");

        string transcript = string.Empty;
        string? audioUrl = null;
        string? storedObjectKey = null;
        string? chunkedUploadId = null;
        float? audioDuration = null;

        // 1. Process voice audio if submitted
        if (request.AudioFile != null && request.AudioFile.Length > 0)
        {
            var extension = Path.GetExtension(request.AudioFile.FileName);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".webm";
            var objectKey = $"exams/{session.ExamId}/sessions/{sessionId}/students/{studentId}/q_{request.QuestionId}_{DateTime.UtcNow.Ticks}{extension}";
            storedObjectKey = objectKey;

            // Buffer IFormFile → byte[] TRƯỚC mọi await. IFormFile stream gắn với
            // request body; sau bất kỳ await nào ASP.NET có thể dispose form → "closed stream".
            byte[] audioBytes;
            using (var tmp = new MemoryStream())
            {
                await request.AudioFile.CopyToAsync(tmp);
                audioBytes = tmp.ToArray();
            }

            // Upload to Cloudflare R2 — dùng MemoryStream mới từ buffer.
            using (var uploadStream = new MemoryStream(audioBytes, writable: false))
            {
                audioUrl = await _storageService.UploadAudioAsync(
                    objectKey,
                    uploadStream,
                    request.AudioFile.ContentType ?? "audio/webm");
            }

            // STT — tạo MemoryStream mới từ cùng buffer (upload stream đã bị SDK close).
            using (var sttStream = new MemoryStream(audioBytes, writable: false))
            {
                var sttResponse = await _aiServiceClient.TranscribeAudioAsync(
                    sttStream,
                    Path.GetFileName(objectKey),
                    sessionId.ToString());

                if (sttResponse.Success && !string.IsNullOrWhiteSpace(sttResponse.Transcript))
                {
                    transcript = sttResponse.Transcript;
                    audioDuration = (float)sttResponse.Duration;
                }
            }
        }
        // 1b. Audio đã upload sẵn qua luồng chunked (R2 đã có 1 object gộp sẵn).
        // Không upload lại — chỉ gắn object key và chạy STT từ storage.
        // Client gửi audioUploadId (đúng) — audioObjectKey chỉ là fallback cho client cũ.
        else if (!string.IsNullOrWhiteSpace(request.AudioUploadId)
                 || !string.IsNullOrWhiteSpace(request.AudioObjectKey))
        {
            var chunkedRef = request.AudioUploadId ?? request.AudioObjectKey!;

            var resolved = await ResolveChunkedAudioKeyAsync(
                chunkedRef, sessionId, studentId, request.QuestionId, ct);

            storedObjectKey = resolved.ObjectKey;
            chunkedUploadId = resolved.UploadId;

            audioUrl = storedObjectKey;

            var sttResponse = await _aiServiceClient.TranscribeStoredAudioAsync(0, storedObjectKey, ct);
            if (sttResponse.Success && !string.IsNullOrWhiteSpace(sttResponse.Transcript))
            {
                transcript = sttResponse.Transcript;
                audioDuration = (float)sttResponse.Duration;
            }
        }

        var studentAnswerText = !string.IsNullOrWhiteSpace(transcript)
            ? transcript
            : (request.TextAnswer ?? string.Empty);

        // 2. Check follow-up AI question
        bool shouldFollowUp = false;
        string? followUpQuestionText = null;

        if (!request.IsFollowUp && (question.IsFollowUpAllowed ?? true))
        {
            var followUpRequest = new FollowUpRequest
            {
                ExamSessionId = sessionId.ToString(),
                CourseName = session.Exam?.Course?.CourseName ?? "",
                OriginalQuestion = question.QuestionText,
                StudentAnswer = studentAnswerText,
                QuestionIndex = (enrollment.CurrentQuestionIndex ?? 0) + 1
            };

            var followUpResponse = await _aiServiceClient.GenerateFollowUpAsync(followUpRequest);
            if (followUpResponse.Success && followUpResponse.ShouldFollowUp && !string.IsNullOrWhiteSpace(followUpResponse.FollowUpQuestion))
            {
                shouldFollowUp = true;
                followUpQuestionText = followUpResponse.FollowUpQuestion;
            }
        }

        if (_retakes is not null) await _retakes.EnsureWritableAsync(sessionId, studentId);

        // 3. Save Answer record
        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = request.QuestionId,
            StudentAnswer = studentAnswerText,
            Transcript = transcript,
            AudioUrl = audioUrl,
            ObjectKey = storedObjectKey,
            DurationSeconds = audioDuration,
            Status = !string.IsNullOrWhiteSpace(transcript) ? AnswerStatus.EVALUATED : AnswerStatus.PROCESSING,
            IsFollowUp = request.IsFollowUp,
            AnsweredAt = DateTime.UtcNow
        };
        var savedAnswer = await _resultRepository.AddOrUpdateAnswerAsync(answer);
        await _sessionRepository.SaveChangesAsync();

        // Gắn answerId vào media_upload để liên kết 2 chiều (audit).
        if (!string.IsNullOrWhiteSpace(chunkedUploadId) && savedAnswer.AnswerId > 0)
        {
            await _mediaUploadRepo.LinkAnswerAsync(chunkedUploadId, savedAnswer.AnswerId, ct);
        }

        // 4. Update student enrollment state
        enrollment.LastActivityAt = DateTime.UtcNow;
        if (!shouldFollowUp)
        {
            enrollment.CurrentQuestionIndex = (enrollment.CurrentQuestionIndex ?? 0) + 1;
        }
        await _sessionRepository.SaveChangesAsync();

        var exam = await _examRepository.GetByIdWithQuestionsAsync(session.ExamId)
            ?? throw new KeyNotFoundException("Đề thi không tồn tại.");

        var examQuestions = exam.ExamQuestions.OrderBy(q => q.QuestionId).ToList();
        var totalQuestions = examQuestions.Count;
        var nextIndex = enrollment.CurrentQuestionIndex ?? 0;
        var isCompleted = nextIndex >= totalQuestions && !shouldFollowUp;

        VivaQuestionDto? nextQuestion = null;
        if (shouldFollowUp)
        {
            nextQuestion = new VivaQuestionDto
            {
                QuestionId = request.QuestionId,
                QuestionIndex = enrollment.CurrentQuestionIndex ?? 0,
                TotalQuestions = totalQuestions,
                QuestionText = followUpQuestionText!,
                Level = question.Level,
                MaxScore = question.MaxScore,
                IsFollowUpAllowed = false,
                IsFollowUp = true
            };
        }
        else if (!isCompleted && nextIndex < totalQuestions)
        {
            var nextEq = examQuestions[nextIndex];
            nextQuestion = new VivaQuestionDto
            {
                QuestionId = nextEq.QuestionId,
                QuestionIndex = nextIndex,
                TotalQuestions = totalQuestions,
                QuestionText = nextEq.Question?.QuestionText ?? "",
                Level = nextEq.Question?.Level ?? 1,
                MaxScore = nextEq.Question?.MaxScore ?? nextEq.QuestionScore,
                IsFollowUpAllowed = nextEq.Question?.IsFollowUpAllowed ?? true,
                IsFollowUp = false
            };
        }

        return new SubmitVivaAnswerResponse
        {
            Success = true,
            Message = shouldFollowUp ? "Cần trả lời thêm câu hỏi phụ từ AI." : "Đã ghi nhận câu trả lời thành công.",
            Transcript = transcript,
            ShouldFollowUp = shouldFollowUp,
            FollowUpQuestion = followUpQuestionText,
            CurrentQuestionIndex = enrollment.CurrentQuestionIndex ?? 0,
            TotalQuestions = totalQuestions,
            IsCompleted = isCompleted,
            NextQuestion = nextQuestion,
            AnswerId = savedAnswer.AnswerId
        };
    }

    /// <summary>
    /// Kiểm chứng object key do client gửi lên thực sự là audio mà học sinh đã upload
    /// cho đúng (session, student, question) này qua luồng chunked.
    ///
    /// Không tin giá trị của client: tra bảng media_uploads theo uploadId,
    /// rồi đối chiếu SessionId / StudentId / QuestionId và trạng thái COMPLETED.
    /// </summary>
    private async Task<(string ObjectKey, string UploadId)> ResolveChunkedAudioKeyAsync(
        string uploadIdOrObjectKey,
        int sessionId,
        int studentId,
        int questionId,
        CancellationToken ct)
    {
        // Ưu tiên tra theo uploadId; nếu client gửi object key (client cũ) thì tra
        // theo objectKey trong bảng media_uploads.
        var upload = await _mediaUploadRepo.GetByIdAsync(uploadIdOrObjectKey, ct)
            ?? await _mediaUploadRepo.GetCompletedByObjectKeyAsync(uploadIdOrObjectKey, ct)
            ?? throw new InvalidOperationException(
                $"Phiên upload không tồn tại (đã tra theo uploadId và objectKey). Giá trị nhận: '{uploadIdOrObjectKey}'.");

        if (upload.SessionId != sessionId || upload.StudentId != studentId || upload.QuestionId != questionId)
            throw new UnauthorizedAccessException("Audio không thuộc phiên thi của bạn.");

        if (upload.UploadStatus != "COMPLETED")
            throw new InvalidOperationException("Audio chưa upload xong.");

        if (string.IsNullOrWhiteSpace(upload.ObjectKey))
            throw new InvalidOperationException("Audio chưa được lưu lên storage.");

        return (upload.ObjectKey, upload.UploadId);
    }

    public async Task<FinishVivaSessionResponse> FinishSessionAsync(
        int sessionId, int studentId, CancellationToken cancellationToken = default)
    {
        if (_retakes is not null) await _retakes.EnsureWritableAsync(sessionId, studentId, checkDeadline: false, ct: cancellationToken);
        if (await _sessionRepository.GetByIdAsync(sessionId) is null)
            throw new KeyNotFoundException($"Ca thi ID {sessionId} không tồn tại.");

        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)
            ?? throw new UnauthorizedAccessException("Sinh viên chưa ghi danh vào ca thi này.");

        var result = await _resultRepository.GetBySessionAndStudentAsync(sessionId, studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy kết quả thi của sinh viên.");

        if (result.Status is SubmissionStatus.REVIEWED or SubmissionStatus.PUBLISHED)
            throw new InvalidOperationException("Kết quả đã được giảng viên duyệt, không thể chấm AI lại.");

        if (result.AiEvaluationStatus == AiEvaluationStatus.SUCCEEDED)
        {
            enrollment.Status = SessionStatus.COMPLETED;
            enrollment.LastActivityAt = DateTime.UtcNow;
            await _sessionRepository.SaveChangesAsync();
            return MapFinishResponse(result);
        }

        // A second finish while the exam is already waiting in the queue must not
        // wipe scores or create a second grading job.
        if (_queueService.HasPendingSessionGrading(result.ResultId))
        {
            enrollment.Status = SessionStatus.COMPLETED;
            enrollment.LastActivityAt = DateTime.UtcNow;
            await _sessionRepository.SaveChangesAsync();
            return MapFinishResponse(result);
        }

        enrollment.LastActivityAt = DateTime.UtcNow;
        result.Status = SubmissionStatus.PENDING_REVIEW;
        result.SubmittedAt ??= DateTime.UtcNow;
        result.AiEvaluationStatus = AiEvaluationStatus.PENDING;
        result.AiEvaluationError = null;
        result.AiScore = null;
        result.AiFeedback = null;
        result.AiAnalysis = null;

        var answers = result.Answers.OrderBy(a => a.QuestionId).ToList();
        foreach (var answer in answers)
        {
            answer.AiScore = null;
            answer.AiFeedback = null;
            answer.AiAnalysis = null;
        }

        // Persist the submission before enqueueing so a queue outage cannot lose it.
        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();

        if (answers.Count == 0)
        {
            result.AiEvaluationStatus = AiEvaluationStatus.FAILED;
            result.AiEvaluationError = "No answers were submitted for AI evaluation.";
            enrollment.Status = SessionStatus.COMPLETED;
            await _resultRepository.UpdateAsync(result);
            await _resultRepository.SaveChangesAsync();
            await _sessionRepository.SaveChangesAsync();
            return MapFinishResponse(result);
        }

        if (_retakes is not null)
            await _retakes.EnsureWritableAsync(sessionId, studentId, checkDeadline: false);

        // Enqueue before marking the enrollment complete. If the queue rejects the
        // job, the exam stays open and the desktop client can submit again.
        _queueService.EnqueueSessionGrading(result.ResultId);
        enrollment.Status = SessionStatus.COMPLETED;
        await _sessionRepository.SaveChangesAsync();

        _logger.LogInformation(
            "Queued session grading: ResultId={ResultId}, SessionId={SessionId}, StudentId={StudentId}",
            result.ResultId, sessionId, studentId);
        return MapFinishResponse(result);
    }

    private static FinishVivaSessionResponse MapFinishResponse(Result result) => new()
    {
        SessionId = result.SessionId,
        StudentId = result.StudentId,
        Status = result.Status?.ToString() ?? "",
        Message = result.AiEvaluationStatus switch
        {
            AiEvaluationStatus.SUCCEEDED =>
                "Bài thi đã được nộp và AI đã hoàn thành chấm sơ bộ. Vui lòng đợi giảng viên duyệt kết quả.",
            AiEvaluationStatus.FAILED =>
                "Bài thi đã được lưu nhưng AI chưa chấm được. Có thể thử lại hoặc đợi giảng viên chấm và duyệt.",
            _ =>
                "Bài thi đã được ghi nhận. Hệ thống sẽ chấm lần lượt từng bài. Vui lòng đợi giảng viên duyệt kết quả."
        },
        SubmittedAt = result.SubmittedAt!.Value,
        AiScore = result.AiScore,
        AiEvaluationStatus = result.AiEvaluationStatus?.ToString()
    };
}
