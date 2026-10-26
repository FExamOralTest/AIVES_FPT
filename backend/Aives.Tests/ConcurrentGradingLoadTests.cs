using System.Net;
using System.Text;
using System.Text.Json;
using Aives.Application.AI.DTOs;
using Aives.Application.Queue.Contracts;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.AI;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Queue;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Aives.Tests;

/// <summary>
/// Demo load test — 20 luồng chấm bài chạy SONG SONG (concurrent grading).
///
/// Mục tiêu: kiểm chứng pipeline chấm bài (STT → AI Eval → persist) chịu được tải
/// nhiều luồng cùng lúc mà không lẫn dữ liệu giữa các luồng.
///
/// Mỗi "luồng" = 1 sinh viên với N câu trả lời; mỗi câu trả lời được xử lý qua đúng
/// <see cref="AnswerProcessingJob"/> (giống hệt job Hangfire chạy thật), nên:
/// - <c>answers.attempt_count</c> được tăng đúng 1 lần / câu (nguồn sự thật BR-17).
/// - Metadata trả về <c>QueueJobStatus.Completed</c> khi chấm thành công.
/// - Answer trong DB chuyển sang <c>EVALUATED</c> kèm <c>ai_score</c> / <c>ai_feedback</c>.
///
/// Toàn bộ phụ thuộc ngoài (AI service, R2) được thay bằng fake trong RAM nên test
/// chạy offline, không cần Postgres, không cần GPU, không tốn tiền API.
/// </summary>
public class ConcurrentGradingLoadTests : IDisposable
{
    private const int StudentCount = 20;      // 20 luồng chấm bài
    private const int QuestionsPerStudent = 3; // mỗi luồng có 3 câu → 60 câu được chấm

    private readonly AivesDbContext _seedDb;
    private readonly DbContextOptions<AivesDbContext> _options;
    private readonly FakeStorageService _storage = new();
    private readonly ITestOutputHelper _output;

    public ConcurrentGradingLoadTests(ITestOutputHelper output)
    {
        _output = output;

        // Một options instance duy nhất → các DbContext con dùng CHUNG store in-memory.
        _options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase(databaseName: $"ConcurrentGrading_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _seedDb = new AivesDbContext(_options);
    }

    public void Dispose()
    {
        _storage.Clear();
        _seedDb.Database.EnsureDeleted();
        _seedDb.Dispose();
    }

    // ===================================================================
    //  Test 1 — 20 luồng chấm bài, tất cả thành công
    // ===================================================================

    [Fact(Timeout = 300_000)]
    public async Task Twenty_Concurrent_Grading_Flows_All_Succeed_And_Persist_Evaluations()
    {
        // 1. Seed: 1 course → 1 exam (3 câu) → 1 session → 20 sinh viên, mỗi SV 1 result + 3 answers.
        var seed = await SeedAsync(StudentCount, QuestionsPerStudent);

        // 2. AI service giả: STT trả transcript, Eval trả điểm hợp lệ theo EvaluationContract.
        var aiHandler = new ConcurrentAiHandler(_storage);
        var aiClient = BuildAiClient(aiHandler);

        // 3. Chạy 20 luồng SONG SONG.
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var tasks = seed.Students.Select(student => RunStudentFlowAsync(student, aiClient)).ToArray();
        var flows = await Task.WhenAll(tasks);
        sw.Stop();

        // 4. Assert: mọi luồng đều hoàn tất, không luồng nào lỗi.
        Assert.All(flows, f => Assert.True(f.Success, $"Luồng của SV {f.StudentId} thất bại: {f.Error}"));

        // 5. Assert: đúng 60 câu được chấm, tất cả EVALUATED trong DB.
        var answers = await _seedDb.Answers.AsNoTracking().ToListAsync();
        Assert.Equal(StudentCount * QuestionsPerStudent, answers.Count);
        Assert.All(answers, a =>
        {
            Assert.Equal(AnswerStatus.EVALUATED, a.Status);
            Assert.NotNull(a.AiScore);
            Assert.False(string.IsNullOrWhiteSpace(a.AiFeedback));
            Assert.NotNull(a.Transcript);
            Assert.Equal(1, a.AttemptCount); // đúng 1 lần thử, không retry
            Assert.Null(a.ErrorMessage);
        });

        // 6. Assert: điểm nằm trong khoảng hợp lệ (0 < score < max).
        Assert.All(answers, a => Assert.InRange(a.AiScore!.Value, 0.01f, 99f));

        _output.WriteLine($"--- 20 luồng chấm bài chạy song song ---");
        _output.WriteLine($"Số câu được chấm : {answers.Count}");
        _output.WriteLine($"Tổng thời gian   : {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Throughput        : {answers.Count / sw.Elapsed.TotalSeconds:F1} câu/giây");
    }

    // ===================================================================
    //  Test 2 — 20 luồng song song, một phần lỗi retryable / non-retryable
    // ===================================================================

    [Fact(Timeout = 300_000)]
    public async Task Twenty_Concurrent_Grading_Flows_Isolate_Failures_Per_Answer()
    {
        var seed = await SeedAsync(StudentCount, QuestionsPerStudent);

        // Chế độ lỗi theo từng câu: "retryable" / "fatal" / không lỗi.
        // Câu (studentIndex + questionIndex) % 3:
        //   0 → STT lỗi tạm thời (retryable)  → job throw RetryableJobException
        //   1 → STT lỗi vĩnh viễn             → job dead-letter
        //   2 → OK
        var failureByAnswer = new Dictionary<int, string>();
        foreach (var student in seed.Students)
        {
            var sIndex = seed.Students.FindIndex(s => s.StudentId == student.StudentId);
            for (var qIndex = 0; qIndex < student.AnswerIds.Count; qIndex++)
            {
                failureByAnswer[student.AnswerIds[qIndex]] = ((sIndex + qIndex) % 3) switch
                {
                    0 => "retryable",
                    1 => "fatal",
                    _ => "ok"
                };
            }
        }

        var aiHandler = new ConcurrentAiHandler(_storage)
        {
            FailureMode = answerId => failureByAnswer.TryGetValue(answerId, out var kind) ? kind : null
        };
        var aiClient = BuildAiClient(aiHandler);

        var flows = await Task.WhenAll(
            seed.Students.Select(student => RunStudentFlowAsync(student, aiClient)).ToArray());

        Assert.All(flows, f => Assert.True(f.Success, $"Luồng của SV {f.StudentId} lỗi ngoài dự kiến: {f.Error}"));

        var answers = await _seedDb.Answers.AsNoTracking().ToDictionaryAsync(a => a.AnswerId);
        var retryableCount = 0;
        var fatalCount = 0;
        var okCount = 0;

        foreach (var flow in flows)
        {
            foreach (var outcome in flow.Outcomes)
            {
                var answer = answers[outcome.AnswerId];
                switch (outcome.Kind)
                {
                    case "OK":
                        okCount++;
                        Assert.Equal(AnswerStatus.EVALUATED, answer.Status);
                        Assert.Equal(QueueJobStatus.Completed, outcome.Status);
                        break;
                    case "RETRYABLE":
                        retryableCount++;
                        Assert.Equal(AnswerStatus.ERROR, answer.Status);
                        Assert.Equal(QueueJobStatus.Failed, outcome.Status);
                        break;
                    default:
                        fatalCount++;
                        Assert.Equal(AnswerStatus.ERROR, answer.Status);
                        Assert.Equal(QueueJobStatus.DeadLetter, outcome.Status);
                        break;
                }
            }
        }

        _output.WriteLine($"OK={okCount} | retryable={retryableCount} | dead-letter={fatalCount}");
        _output.WriteLine($"Tổng = {okCount + retryableCount + fatalCount}");

        Assert.Equal(StudentCount * QuestionsPerStudent, okCount + retryableCount + fatalCount);
        Assert.True(okCount > 0 && retryableCount > 0 && fatalCount > 0,
            $"Kỳ vọng có đủ 3 nhóm kết quả để chứng minh lỗi không lan sang luồng khác. " +
            $"OK={okCount}, retryable={retryableCount}, fatal={fatalCount}.");
    }

    // ===================================================================
    //  Helpers
    // ===================================================================

    /// <summary>
    /// Mô phỏng đúng luồng chấm bài của 1 sinh viên: lặp qua các câu, chạy
    /// <see cref="AnswerProcessingJob"/> (giống job Hangfire thật) cho từng câu.
    /// </summary>
    private async Task<FlowResult> RunStudentFlowAsync(StudentSeed student, IAiServiceClient aiClient)
    {
        var outcomes = new List<AnswerOutcome>();
        var errors = new List<string>();

        foreach (var answer in student.AnswerIds)
        {
            // Mỗi luồng dùng DbContext RIÊNG — EF Core không thread-safe, mỗi luồng
            // chạy trên thread pool khác nhau nên không được dùng chung instance.
            await using var db = new AivesDbContext(_options);
            var answerRepo = new AnswerRepository(db);
            var resultRepo = new ResultRepository(db);

            var orchestrator = new AnswerOrchestrator(
                answerRepo, resultRepo, _storage, aiClient,
                NullLogger<AnswerOrchestrator>.Instance);

            var job = new AnswerProcessingJob(
                orchestrator, answerRepo, NullLogger<AnswerProcessingJob>.Instance);

            var metadata = QueueJobContract.Create(answer, null, student.ObjectKeys[answer], attempt: 1);

            try
            {
                await job.ExecuteAsync(metadata, CancellationToken.None);
                outcomes.Add(new AnswerOutcome(answer, "OK", metadata.Status));
            }
            catch (AnswerProcessingJob.NonRetryableJobException ex)
            {
                // Kết quả dự kiến (dead-letter) — KHÔNG phải lỗi của luồng.
                outcomes.Add(new AnswerOutcome(answer, "FATAL", metadata.Status));
                _output.WriteLine($"[student {student.StudentId}] answer {answer} dead-letter: {ex.Message}");
            }
            catch (AnswerProcessingJob.RetryableJobException ex)
            {
                // Kết quả dự kiến (sẽ retry) — KHÔNG phải lỗi của luồng.
                outcomes.Add(new AnswerOutcome(answer, "RETRYABLE", metadata.Status));
                _output.WriteLine($"[student {student.StudentId}] answer {answer} retryable: {ex.Message}");
            }
            catch (Exception ex)
            {
                errors.Add($"#{answer}: lỗi ngoài dự kiến {ex.GetType().Name}: {ex.Message}");
            }
        }

        return new FlowResult(student.StudentId, outcomes, errors);
    }

    private IAiServiceClient BuildAiClient(HttpMessageHandler handler)
    {
        var opts = Options.Create(new AiServiceOptions
        {
            BaseUrl = "http://fake-ai",
            TimeoutSeconds = 60,
            ApiKey = "test-key"
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://fake-ai") };
        return new AiServiceClient(http, opts, NullLogger<AiServiceClient>.Instance);
    }

    private async Task<SeedResult> SeedAsync(int studentCount, int questionsPerStudent)
    {
        var course = new Course { CourseCode = "LOAD", CourseName = "Concurrent Grading Demo" };
        _seedDb.Courses.Add(course);
        await _seedDb.SaveChangesAsync();

        var exam = new Exam
        {
            ExamCode = "LOAD-EX",
            Title = "Bài kiểm tra chấm đồng thời",
            CourseId = course.CourseId,
            DurationMinutes = 60,
            MaxQuestions = questionsPerStudent
        };
        _seedDb.Exams.Add(exam);
        await _seedDb.SaveChangesAsync();

        var session = new ExamSession
        {
            ExamId = exam.ExamId,
            SessionCode = "LOAD-SESS",
            SessionName = "Ca thi load test",
            Status = SessionStatus.IN_PROGRESS,
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(1),
            ScheduledBy = 1
        };
        _seedDb.ExamSessions.Add(session);
        await _seedDb.SaveChangesAsync();

        var questions = new List<QuestionBank>();
        for (var i = 1; i <= questionsPerStudent; i++)
        {
            var q = new QuestionBank
            {
                CourseId = course.CourseId,
                QuestionText = $"Câu hỏi {i}: Trình bày khái niệm chính của chương {i}.",
                SampleAnswer = $"Câu trả lời mẫu cho chương {i}.",
                LearningOutcome = $"Hiểu và trình bày được chương {i}.",
                Topic = $"Chương {i}",
                MaxScore = 10f,
                Level = 1,
                IsFollowUpAllowed = false,
                ApprovalStatus = QuestionApprovalStatus.APPROVED
            };
            _seedDb.QuestionBanks.Add(q);
            questions.Add(q);
        }
        await _seedDb.SaveChangesAsync();

        foreach (var q in questions)
        {
            _seedDb.ExamQuestions.Add(new ExamQuestion
            {
                ExamId = exam.ExamId,
                QuestionId = q.QuestionId,
                QuestionScore = q.MaxScore ?? 10f
            });
        }
        await _seedDb.SaveChangesAsync();

        var students = new List<StudentSeed>();
        for (var s = 0; s < studentCount; s++)
        {
            var user = new User
            {
                Username = $"load_student_{s + 1}",
                Email = $"load_student_{s + 1}@aives.test",
                FullName = $"Load Student {s + 1}",
                Role = UserRole.STUDENT,
                PasswordHash = "x"
            };
            _seedDb.Users.Add(user);
            await _seedDb.SaveChangesAsync();

            var result = new Result
            {
                SessionId = session.SessionId,
                StudentId = user.UserId,
                Status = SubmissionStatus.PENDING_REVIEW,
                SubmittedAt = DateTime.UtcNow
            };
            _seedDb.Results.Add(result);
            await _seedDb.SaveChangesAsync();

            var answerIds = new List<int>();
            var objectKeys = new Dictionary<int, string>();
            foreach (var q in questions)
            {
                var answer = new Answer
                {
                    ResultId = result.ResultId,
                    QuestionId = q.QuestionId,
                    StudentAnswer = $"Câu trả lời của SV {s + 1} cho câu {q.QuestionId}.",
                    Status = AnswerStatus.PROCESSING
                };
                _seedDb.Answers.Add(answer);
                await _seedDb.SaveChangesAsync();

                // Audio giả nằm trong fake storage → orchestrator dùng đường STT "stored audio".
                var objectKey = $"load-tests/{session.SessionId}/students/{user.UserId}/q_{q.QuestionId}.wav";
                await _storage.UploadAudioAsync(objectKey, new MemoryStream(WavHeader()), "audio/wav");

                // ObjectKey + AudioUrl là điều kiện để orchestrator gọi STT (đường stored audio).
                answer.ObjectKey = objectKey;
                answer.AudioUrl = objectKey;
                await _seedDb.SaveChangesAsync();

                answerIds.Add(answer.AnswerId);
                objectKeys[answer.AnswerId] = objectKey;
            }

            students.Add(new StudentSeed(user.UserId, answerIds, objectKeys));
        }

        return new SeedResult(session.SessionId, exam.ExamId, students, questions);
    }

    /// <summary>44-byte WAV header tối thiểu — đủ để provider STT nhận diện định dạng RIFF/WAVE.</summary>
    private static byte[] WavHeader() =>
    [
        0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00,
        0x57, 0x41, 0x56, 0x45, 0x66, 0x6D, 0x74, 0x20,
        0x10, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00,
        0x44, 0xAC, 0x00, 0x00, 0x88, 0x58, 0x01, 0x00,
        0x02, 0x00, 0x10, 0x00
    ];

    private sealed record StudentSeed(int StudentId, List<int> AnswerIds, Dictionary<int, string> ObjectKeys);
    private sealed record SeedResult(int SessionId, int ExamId, List<StudentSeed> Students, List<QuestionBank> Questions);
    private sealed record AnswerOutcome(int AnswerId, string Kind, QueueJobStatus Status);
    private sealed record FlowResult(int StudentId, List<AnswerOutcome> Outcomes, List<string> Errors)
    {
        public bool Success => Errors.Count == 0;
        public string? Error => Errors.Count == 0 ? null : string.Join(" | ", Errors);
    }

    /// <summary>
    /// Handler HTTP giả đóng vai AI service: xử lý "/ai/speech/transcribe-object" và
    /// "/ai/evaluate/session". Response eval được sinh động từ request để luôn thỏa
    /// <c>EvaluationContract</c> (điểm tổng, max, % và nội dung phải khớp tuyệt đối).
    /// </summary>
    private sealed class ConcurrentAiHandler : HttpMessageHandler
    {
        private readonly FakeStorageService _storage;
        private int _counter;

        public ConcurrentAiHandler(FakeStorageService storage) => _storage = storage;

        /// <summary>Trả "retryable" / "fatal" / null để ép lỗi cho câu tương ứng.</summary>
        public Func<int, string?>? FailureMode { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.Contains("transcribe"))
                return await HandleTranscribeAsync(request);

            if (path.Contains("evaluate"))
                return await HandleEvaluateAsync(request);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private async Task<HttpResponseMessage> HandleTranscribeAsync(HttpRequestMessage request)
        {
            var payload = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var answerId = root.GetProperty("answerId").GetInt32();

            var failure = FailureMode?.Invoke(answerId);
            if (failure is not null && failure != "ok")
            {
                var (code, retryable, message) = failure == "fatal"
                    ? ("AUDIO_CORRUPTED", false, "Audio file is corrupted.")
                    : ("INTERNAL_ERROR", true, "AI service is temporarily unavailable.");

                var errorPayload = new
                {
                    success = false,
                    answerId,
                    transcript = "",
                    language = "",
                    duration = 0,
                    message,
                    error = new { code, message, retryable }
                };
                return Json(errorPayload);
            }

            var n = Interlocked.Increment(ref _counter);
            return Json(new
            {
                success = true,
                answerId,
                transcript = $"Câu trả lời mẫu số {n} của sinh viên trong bài kiểm tra chấm đồng thời.",
                language = "vi",
                duration = 8.5,
                error = (object?)null
            });
        }

        private async Task<HttpResponseMessage> HandleEvaluateAsync(HttpRequestMessage request)
        {
            var payload = await request.Content!.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var sessionId = root.GetProperty("exam_session_id").GetString() ?? string.Empty;
            var pairs = root.GetProperty("question_answers").EnumerateArray().ToList();

            var scores = new List<object>();
            float total = 0f;
            float maxTotal = 0f;

            foreach (var pair in pairs)
            {
                var questionId = pair.GetProperty("question_id").GetString() ?? string.Empty;
                var questionText = pair.GetProperty("question_text").GetString() ?? string.Empty;
                var studentAnswer = pair.GetProperty("student_answer").GetString() ?? string.Empty;
                var maxScore = pair.TryGetProperty("max_score", out var msEl) && msEl.ValueKind == JsonValueKind.Number
                    ? msEl.GetSingle()
                    : 10f;

                var score = maxScore * 0.75f;
                total += score;
                maxTotal += maxScore;

                scores.Add(new
                {
                    question_id = questionId,
                    question_text = questionText,
                    student_answer = studentAnswer,
                    score,
                    max_score = maxScore,
                    feedback = "Đáp án nêu đúng ý chính, cần bổ sung ví dụ thực tế.",
                    strengths = new[] { "Nắm đúng khái niệm cốt lõi" },
                    weaknesses = new[] { "Thiếu ví dụ minh hoạ" },
                    missing_content = new[] { "Ví dụ ứng dụng" },
                    sentence_feedback = Array.Empty<object>(),
                    criterion_analyses = Array.Empty<object>(),
                    improvements = new[] { "Bổ sung ví dụ thực tế" }
                });
            }

            var percentage = maxTotal > 0 ? total / maxTotal * 100f : 0f;

            return Json(new
            {
                success = true,
                message = "Chấm thành công.",
                exam_session_id = sessionId,
                total_score = total,
                max_total_score = maxTotal,
                percentage,
                overall_feedback = "Bài làm đạt yêu cầu, nội dung đầy đủ và trình bày rõ ràng.",
                question_scores = scores,
                status = "PendingReview",
                study_suggestions = new[] { "Ôn lại chương đã học" }
            });
        }

        private static HttpResponseMessage Json(object payload) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
    }
}
