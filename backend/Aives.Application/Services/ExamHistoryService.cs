using Aives.Application.DTOs;
using Aives.Application.Interfaces.Repositories;
using Aives.Application.Interfaces.Services;
using Aives.Domain.Entities;

namespace Aives.Application.Services;

public class ExamHistoryService : IExamHistoryService
{
    private readonly IExamRepository _examRepo;

    public ExamHistoryService(IExamRepository examRepo) => _examRepo = examRepo;

    // ── 1. Danh sách bài thi theo Course ────────────────────────────────────

    public async Task<IEnumerable<ExamSummaryDto>> GetExamsByCourseAsync(int courseId)
    {
        var exams = await _examRepo.GetByCourseIdAsync(courseId);
        return exams.Select(ToExamSummary);
    }

    // ── 2. Tất cả phiên thi + kết quả của một bài thi ───────────────────────

    public async Task<IEnumerable<ExamSessionDetailDto>> GetSessionsByExamAsync(int examId)
    {
        var exam = await _examRepo.GetWithSessionsAsync(examId)
            ?? throw new KeyNotFoundException($"Exam {examId} not found.");

        return exam.ExamSessions.Select(ToSessionDetail);
    }

    // ── 3. Chi tiết một phiên thi ────────────────────────────────────────────

    public async Task<ExamSessionDetailDto> GetSessionDetailAsync(int sessionId)
    {
        var session = await _examRepo.GetSessionWithResultsAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found.");

        return ToSessionDetail(session);
    }

    // ── 4. Chi tiết kết quả + từng câu trả lời của một student ──────────────

    public async Task<StudentResultDetailDto> GetResultDetailAsync(int resultId)
    {
        var result = await _examRepo.GetResultDetailAsync(resultId)
            ?? throw new KeyNotFoundException($"Result {resultId} not found.");

        return new StudentResultDetailDto(
            ResultId:      result.ResultId,
            StudentId:     result.StudentId,
            StudentUsername: result.Student.Username,
            StudentFullName: result.Student.FullName,
            AiScore:       result.AiScore,
            AiFeedback:    result.AiFeedback,
            FinalScore:    result.FinalScore,
            FinalFeedback: result.FinalFeedback,
            Status:        result.Status?.ToString(),
            SubmittedAt:   result.SubmittedAt,
            ReviewedAt:    result.ReviewedAt,
            Answers:       result.Answers
                .OrderBy(a => a.AnsweredAt)
                .Select(ToAnswerDetail)
                .ToList()
        );
    }

    // ── Mapping helpers ──────────────────────────────────────────────────────

    private static ExamSummaryDto ToExamSummary(Exam e) => new(
        ExamId:          e.ExamId,
        ExamCode:        e.ExamCode,
        Title:           e.Title,
        CourseId:        e.CourseId,
        CourseName:      e.Course.CourseName,
        ExamStatus:      e.ExamStatus?.ToString(),
        DurationMinutes: e.DurationMinutes,
        MaxQuestions:    e.MaxQuestions,
        CreatedAt:       e.CreatedAt
    );

    private static ExamSessionDetailDto ToSessionDetail(ExamSession s) => new(
        SessionId:      s.SessionId,
        SessionName:    s.SessionName,
        Room:           s.Room,
        StartTime:      s.StartTime,
        EndTime:        s.EndTime,
        SessionStatus:  s.Status?.ToString(),
        TotalStudents:  s.Results.Count,
        Results:        s.Results
            .OrderBy(r => r.SubmittedAt)
            .Select(ToStudentResultSummary)
            .ToList()
    );

    private static StudentResultSummaryDto ToStudentResultSummary(Result r) => new(
        ResultId:        r.ResultId,
        StudentId:       r.StudentId,
        StudentUsername: r.Student.Username,
        StudentFullName: r.Student.FullName,
        AiScore:         r.AiScore,
        FinalScore:      r.FinalScore,
        Status:          r.Status?.ToString(),
        SubmittedAt:     r.SubmittedAt
    );

    private static AnswerDetailDto ToAnswerDetail(Answer a) => new(
        AnswerId:       a.AnswerId,
        QuestionId:     a.QuestionId,
        QuestionText:   a.Question.QuestionText,
        Level:          a.Question.Level,
        StudentAnswer:  a.StudentAnswer,
        Transcript:     a.Transcript,
        AudioUrl:       a.AudioUrl,
        IsFollowUp:     a.IsFollowUp,
        AiScore:        a.AiScore,
        AiFeedback:     a.AiFeedback,
        FinalScore:     a.FinalScore,
        TeacherComment: a.TeacherComment
    );
}
