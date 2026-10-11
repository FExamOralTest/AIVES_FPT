using Aives.Application.Exams.Interfaces;
using Aives.Application.Results.DTOs;
using Aives.Application.Results.Interfaces;
using Aives.Domain.Entities;

namespace Aives.Application.Results.Services;

public class ExamHistoryService : IExamHistoryService
{
    private readonly IExamRepository _examRepo;

    public ExamHistoryService(IExamRepository examRepo) => _examRepo = examRepo;

    public async Task<IEnumerable<ExamSummaryDto>> GetExamsByCourseAsync(int courseId)
        => (await _examRepo.GetByCourseIdAsync(courseId)).Select(ToExamSummary);

    public async Task<IEnumerable<ExamSessionDetailDto>> GetSessionsByExamAsync(int examId)
    {
        var exam = await _examRepo.GetWithSessionsAsync(examId)
            ?? throw new KeyNotFoundException($"Exam {examId} not found.");
        return exam.ExamSessions.Select(ToSessionDetail);
    }

    public async Task<ExamSessionDetailDto> GetSessionDetailAsync(int sessionId)
        => ToSessionDetail(await _examRepo.GetSessionWithResultsAsync(sessionId)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found."));

    public async Task<StudentResultDetailDto> GetResultDetailAsync(int resultId)
    {
        var result = await _examRepo.GetResultDetailAsync(resultId)
            ?? throw new KeyNotFoundException($"Result {resultId} not found.");
        return new StudentResultDetailDto(
            result.ResultId, result.StudentId,
            result.Student.Username, result.Student.FullName,
            result.AiScore, result.AiFeedback,
            result.FinalScore, result.FinalFeedback,
            result.Status?.ToString(),
            result.SubmittedAt, result.ReviewedAt,
            result.Answers.OrderBy(a => a.AnsweredAt).Select(ToAnswerDetail).ToList()
        );
    }

    private static ExamSummaryDto ToExamSummary(Exam e) => new(
        e.ExamId, e.ExamCode, e.Title,
        e.CourseId, e.Course.CourseName,
        e.ExamStatus?.ToString(),
        e.DurationMinutes, e.MaxQuestions, e.CreatedAt
    );

    private static ExamSessionDetailDto ToSessionDetail(ExamSession s) => new(
        s.SessionId, s.SessionName, s.Room,
        s.StartTime, s.EndTime,
        s.Status?.ToString(),
        s.Results.Count,
        s.Results.OrderBy(r => r.SubmittedAt).Select(ToStudentSummary).ToList()
    );

    private static StudentResultSummaryDto ToStudentSummary(Result r) => new(
        r.ResultId, r.StudentId,
        r.Student.Username, r.Student.FullName,
        r.AiScore, r.FinalScore,
        r.Status?.ToString(), r.SubmittedAt
    );

    private static AnswerDetailDto ToAnswerDetail(Answer a) => new(
        a.AnswerId, a.QuestionId,
        a.Question.QuestionText, a.Question.Level,
        a.StudentAnswer, a.Transcript, a.AudioUrl,
        a.IsFollowUp, a.AiScore, a.AiFeedback,
        a.FinalScore, a.TeacherComment
    );
}
