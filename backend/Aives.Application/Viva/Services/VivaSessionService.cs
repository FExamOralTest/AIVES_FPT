using Aives.Application.AI.DTOs;
using Aives.Application.AI.Interfaces;
using Aives.Application.Exams.Interfaces;
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
    private readonly ILogger<VivaSessionService> _logger;

    public VivaSessionService(
        IExamSessionRepository sessionRepository,
        IExamRepository examRepository,
        IQuestionBankRepository questionRepository,
        IResultRepository resultRepository,
        IAiServiceClient aiServiceClient,
        ILogger<VivaSessionService> logger)
    {
        _sessionRepository = sessionRepository;
        _examRepository = examRepository;
        _questionRepository = questionRepository;
        _resultRepository = resultRepository;
        _aiServiceClient = aiServiceClient;
        _logger = logger;
    }

    public async Task<StartVivaSessionResponse> StartSessionAsync(int sessionId, int studentId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"Ca thi ID {sessionId} không tồn tại.");

        if (session.Status == SessionStatus.CANCELLED)
            throw new InvalidOperationException("Ca thi này đã bị hủy.");

        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)
            ?? throw new UnauthorizedAccessException("Sinh viên chưa ghi danh (enroll) vào ca thi này.");

        if (enrollment.Status == SessionStatus.COMPLETED)
            throw new InvalidOperationException("Sinh viên đã hoàn thành ca thi này.");

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
            DurationMinutes = exam.DurationMinutes,
            TotalQuestions = totalQuestions,
            CurrentQuestionIndex = currentIndex,
            CurrentQuestion = currentQuestionDto,
            StartedAt = enrollment.StartedAt
        };
    }

    public async Task<VivaQuestionDto> GetCurrentQuestionAsync(int sessionId, int studentId)
    {
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

    public async Task<SubmitVivaAnswerResponse> SubmitAnswerAsync(int sessionId, int studentId, SubmitVivaAnswerRequest request)
    {
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

        // 1. Process voice audio if submitted
        if (request.AudioFile != null && request.AudioFile.Length > 0)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "audio");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }

            var extension = Path.GetExtension(request.AudioFile.FileName);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".wav";
            var fileName = $"{sessionId}_{studentId}_{request.QuestionId}_{DateTime.UtcNow.Ticks}{extension}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await request.AudioFile.CopyToAsync(stream);
            }
            audioUrl = $"/uploads/audio/{fileName}";

            using var audioReadStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var sttResponse = await _aiServiceClient.TranscribeAudioAsync(audioReadStream, fileName, sessionId.ToString());
            if (sttResponse.Success && !string.IsNullOrWhiteSpace(sttResponse.Transcript))
            {
                transcript = sttResponse.Transcript;
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

        // 3. Save Answer record
        var answer = new Answer
        {
            ResultId = result.ResultId,
            QuestionId = request.QuestionId,
            StudentAnswer = studentAnswerText,
            Transcript = transcript,
            AudioUrl = audioUrl,
            IsFollowUp = request.IsFollowUp,
            AnsweredAt = DateTime.UtcNow
        };
        await _resultRepository.AddOrUpdateAnswerAsync(answer);

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
            NextQuestion = nextQuestion
        };
    }

    public async Task<FinishVivaSessionResponse> FinishSessionAsync(int sessionId, int studentId)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId)
            ?? throw new KeyNotFoundException($"Ca thi ID {sessionId} không tồn tại.");

        var enrollment = await _sessionRepository.GetEnrollmentAsync(sessionId, studentId)
            ?? throw new UnauthorizedAccessException("Sinh viên chưa ghi danh vào ca thi này.");

        var result = await _resultRepository.GetBySessionAndStudentAsync(sessionId, studentId)
            ?? throw new KeyNotFoundException("Không tìm thấy kết quả thi của sinh viên.");

        enrollment.Status = SessionStatus.COMPLETED;
        enrollment.LastActivityAt = DateTime.UtcNow;
        await _sessionRepository.SaveChangesAsync();

        // Evaluate session via AI
        var answers = result.Answers.ToList();
        float overallAiScore = 0f;
        string overallFeedback = "AI đánh giá sơ bộ hoàn tất.";

        if (answers.Count > 0)
        {
            var evalRequest = new EvaluateSessionRequest
            {
                ExamSessionId = sessionId.ToString(),
                StudentId = studentId.ToString(),
                CourseName = session.Exam?.Course?.CourseName ?? "",
                QuestionAnswers = answers.Select(a => new QuestionAnswerPairDto
                {
                    QuestionId = a.QuestionId.ToString(),
                    QuestionText = a.Question?.QuestionText ?? "",
                    StudentAnswer = a.StudentAnswer ?? a.Transcript ?? "",
                    IsFollowUp = a.IsFollowUp ?? false
                }).ToList()
            };

            var evalResponse = await _aiServiceClient.EvaluateSessionAsync(evalRequest);
            if (evalResponse.Success)
            {
                overallAiScore = evalResponse.TotalScore;
                if (!string.IsNullOrWhiteSpace(evalResponse.OverallFeedback))
                {
                    overallFeedback = evalResponse.OverallFeedback;
                }

                foreach (var itemEval in evalResponse.QuestionScores)
                {
                    if (int.TryParse(itemEval.QuestionId, out var qId))
                    {
                        var matchingAnswer = answers.FirstOrDefault(a => a.QuestionId == qId);
                        if (matchingAnswer != null)
                        {
                            matchingAnswer.AiScore = itemEval.Score;
                            matchingAnswer.AiFeedback = itemEval.Feedback;
                            await _resultRepository.AddOrUpdateAnswerAsync(matchingAnswer);
                        }
                    }
                }
            }
        }

        result.AiScore = overallAiScore;
        result.AiFeedback = overallFeedback;
        result.Status = SubmissionStatus.PENDING_REVIEW;
        result.SubmittedAt = DateTime.UtcNow;

        await _resultRepository.UpdateAsync(result);
        await _resultRepository.SaveChangesAsync();

        return new FinishVivaSessionResponse
        {
            SessionId = sessionId,
            StudentId = studentId,
            Status = SubmissionStatus.PENDING_REVIEW.ToString(),
            Message = "Bài thi đã được nộp thành công và AI đã hoàn thành chấm sơ bộ. Vui lòng đợi giảng viên duyệt và công bố kết quả.",
            SubmittedAt = result.SubmittedAt.Value,
            AiScore = overallAiScore
        };
    }
}
