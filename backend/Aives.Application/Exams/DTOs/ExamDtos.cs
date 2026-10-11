using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

// ─── Request DTOs ───────────────────────────────────────────────

/// <summary>Payload để Teacher tạo đề thi mới.</summary>
public class CreateExamRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [Required]
    public int CourseId { get; set; }

    [Range(1, 480)]
    public int DurationMinutes { get; set; } = 30;

    [Range(1, 200)]
    public int MaxQuestions { get; set; } = 10;

    /// <summary>
    /// Danh sách câu hỏi được chọn thủ công hoặc từ AI gợi ý.
    /// Nếu null/rỗng, tạo đề thi chưa có câu hỏi (Draft).
    /// </summary>
    public List<ExamQuestionItem>? Questions { get; set; }
}

/// <summary>Một câu hỏi được gán vào đề thi kèm điểm số.</summary>
public class ExamQuestionItem
{
    [Required]
    public int QuestionId { get; set; }

    [Range(0.0, 100.0)]
    public float QuestionScore { get; set; } = 1.0f;
}

/// <summary>Request gợi ý câu hỏi từ AI cho đề thi.</summary>
public class AiSuggestQuestionsRequest
{
    [Required]
    public int CourseId { get; set; }

    [MaxLength(1000)]
    public string? FocusTopic { get; set; }

    [Range(1, 100)]
    public int MaxSuggestions { get; set; } = 10;
}

// ─── Response DTOs ──────────────────────────────────────────────

/// <summary>Thông tin đề thi trả về cho client.</summary>
public class ExamDto
{
    public int ExamId { get; set; }
    public string Title { get; set; } = null!;
    public string ExamCode { get; set; } = null!;
    public int CourseId { get; set; }
    public string? CourseName { get; set; }
    public int CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public int DurationMinutes { get; set; }
    public int MaxQuestions { get; set; }
    public string? ExamStatus { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<ExamQuestionDto> Questions { get; set; } = new();
}

/// <summary>Thông tin một câu hỏi trong đề thi.</summary>
public class ExamQuestionDto
{
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = null!;
    public float QuestionScore { get; set; }
    public float? MaxScore { get; set; }
    public bool? IsFollowUpAllowed { get; set; }
}

/// <summary>Kết quả gợi ý câu hỏi từ AI.</summary>
public class AiSuggestedQuestionsDto
{
    public List<ExamQuestionDto> SuggestedQuestions { get; set; } = new();
    public string? AiRationale { get; set; }
}
