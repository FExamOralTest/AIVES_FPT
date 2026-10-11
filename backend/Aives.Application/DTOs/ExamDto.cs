using Aives.Domain.Enums;

namespace Aives.Application.DTOs;

// ── Exam History ─────────────────────────────────────────────────────────────

/// <summary>Thông tin tóm tắt một bài thi (dùng trong danh sách lịch sử).</summary>
public record ExamSummaryDto(
    int       ExamId,
    string    ExamCode,
    string    Title,
    int       CourseId,
    string    CourseName,
    string?   ExamStatus,
    int       DurationMinutes,
    int       MaxQuestions,
    DateTime? CreatedAt
);

/// <summary>Thông tin một phiên thi kèm danh sách kết quả của từng student.</summary>
public record ExamSessionDetailDto(
    int            SessionId,
    string         SessionName,
    string?        Room,
    DateTime       StartTime,
    DateTime       EndTime,
    string?        SessionStatus,
    int            TotalStudents,
    List<StudentResultSummaryDto> Results
);

/// <summary>Kết quả tóm tắt của một student trong một phiên thi.</summary>
public record StudentResultSummaryDto(
    int      ResultId,
    int      StudentId,
    string   StudentUsername,
    string   StudentFullName,
    float?   AiScore,
    float?   FinalScore,
    string?  Status,
    DateTime? SubmittedAt
);

/// <summary>Kết quả chi tiết của một student — gồm từng câu trả lời.</summary>
public record StudentResultDetailDto(
    int      ResultId,
    int      StudentId,
    string   StudentUsername,
    string   StudentFullName,
    float?   AiScore,
    string?  AiFeedback,
    float?   FinalScore,
    string?  FinalFeedback,
    string?  Status,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    List<AnswerDetailDto> Answers
);

/// <summary>Chi tiết một câu trả lời của student.</summary>
public record AnswerDetailDto(
    int     AnswerId,
    int     QuestionId,
    string  QuestionText,
    int     Level,
    string? StudentAnswer,
    string? Transcript,
    string? AudioUrl,
    bool?   IsFollowUp,
    float?  AiScore,
    string? AiFeedback,
    float?  FinalScore,
    string? TeacherComment
);

// ── Exam Generator ────────────────────────────────────────────────────────────

/// <summary>Request tạo đề thi ngẫu nhiên.</summary>
public record GenerateExamRequest(
    int CourseId,
    int EasyCount,    // Level 1
    int MediumCount,  // Level 2
    int HardCount     // Level 3
);

/// <summary>Một câu hỏi trong đề thi được tạo.</summary>
public record QuestionDto(
    int    QuestionId,
    int    CourseId,
    string QuestionText,
    int    Level,
    float? MaxScore,
    bool?  IsFollowUpAllowed
);

/// <summary>Response trả về đề thi ngẫu nhiên đã xáo trộn.</summary>
public record GenerateExamResponse(
    int               CourseId,
    string            CourseName,
    int               TotalQuestions,
    int               EasyCount,
    int               MediumCount,
    int               HardCount,
    List<QuestionDto> Questions
);
