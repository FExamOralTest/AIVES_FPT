using System.ComponentModel.DataAnnotations;

namespace Aives.Application.Exams.DTOs;

public class CreateExamRequest
{
    [Required]
    [MaxLength(255)]
    public string Title { get; set; } = null!;

    [Range(1, int.MaxValue)]
    public int CourseId { get; set; }

    [Range(1, 480)]
    public int DurationMinutes { get; set; } = 30;

    [Range(1, 200)]
    public int MaxQuestions { get; set; } = 10;

    public List<ExamQuestionItem>? Questions { get; set; }

    /// <summary>Cấu hình Kíp thi mẫu (Slot Templates) cho bài thi.</summary>
    public List<CreateExamSlotTemplateInput>? SlotTemplates { get; set; }

    public bool AutoGenerateSlotPapers { get; set; }
    public int? QuestionsPerSlot { get; set; }
    public List<CreateSubPaperInput>? GeneratedSubPapers { get; set; }
}

public class ExamQuestionItem
{
    [Range(1, int.MaxValue)]
    public int QuestionId { get; set; }

    [Range(typeof(float), "0.01", "100")]
    public float QuestionScore { get; set; } = 1.0f;
}

public class UpdateExamRequest
{
    [Required, MaxLength(255)]
    public string Title { get; set; } = null!;
    [Range(1, 480)]
    public int DurationMinutes { get; set; }
    [Range(1, 200)]
    public int MaxQuestions { get; set; }
    [Required]
    public List<ExamQuestionItem> Questions { get; set; } = [];

    /// <summary>Cấu hình Kíp thi mẫu (Slot Templates) cho bài thi.</summary>
    public List<CreateExamSlotTemplateInput>? SlotTemplates { get; set; }

    public bool AutoGenerateSlotPapers { get; set; }
    public int? QuestionsPerSlot { get; set; }
    public List<CreateSubPaperInput>? GeneratedSubPapers { get; set; }
}

public class CreateExamSlotTemplateInput
{
    public int SlotIndex { get; set; }

    [Required, MaxLength(100)]
    public string SlotName { get; set; } = null!;

    [Range(1, 480)]
    public int DurationMinutes { get; set; } = 30;

    [Range(1, 500)]
    public int MaxCapacity { get; set; } = 20;

    /// <summary>Danh sách ExamId của các bộ đề thi con áp dụng cho kíp này.</summary>
    public List<int>? ExamPaperIds { get; set; }
}

public class ExamSlotTemplateDto
{
    public int SlotTemplateId { get; set; }
    public int SlotIndex { get; set; }
    public string SlotName { get; set; } = null!;
    public int DurationMinutes { get; set; }
    public int MaxCapacity { get; set; }
    public List<int> ExamPaperIds { get; set; } = new();
    public List<SessionExamSummaryDto> ExamPapers { get; set; } = new();
}

public class UpdateExamQuestionsRequest
{
    public List<ExamQuestionItem> Questions { get; set; } = [];
}

public class SuggestQuestionsRequest
{
    [Range(1, int.MaxValue)]
    public int CourseId { get; set; }

    [MaxLength(1000)]
    public string? FocusTopic { get; set; }

    [Range(1, 100)]
    public int MaxSuggestions { get; set; } = 10;
}

public class ExamDto
{
    public bool CanEdit { get; set; }
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
    public List<ExamSlotTemplateDto> SlotTemplates { get; set; } = new();
}

public class ExamQuestionDto
{
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = null!;
    public float QuestionScore { get; set; }
    public float? MaxScore { get; set; }
    public bool? IsFollowUpAllowed { get; set; }
    public int? Level { get; set; }
}

public class SuggestedQuestionsDto
{
    public List<ExamQuestionDto> SuggestedQuestions { get; set; } = new();
    public string SelectionMethod { get; set; } = "keyword";
    public string? Rationale { get; set; }
}

public record GenerateExamRequest(
    int CourseId,
    int EasyCount,
    int MediumCount,
    int HardCount
);

public record GenerateQuestionDto(
    int    QuestionId,
    int    CourseId,
    string QuestionText,
    int    Level,
    float? MaxScore,
    bool?  IsFollowUpAllowed
);

public record GenerateExamResponse(
    int                      CourseId,
    string                   CourseName,
    int                      TotalQuestions,
    int                      EasyCount,
    int                      MediumCount,
    int                      HardCount,
    List<GenerateQuestionDto> Questions
);

public class CreateSubPaperInput
{
    public int SlotIndex { get; set; }

    [Required, MaxLength(255)]
    public string Title { get; set; } = null!;

    [MaxLength(50)]
    public string? ExamCode { get; set; }

    public List<ExamQuestionItem> Questions { get; set; } = [];
}

public class AutoPartitionSlotPapersRequest
{
    [Range(1, int.MaxValue)]
    public int CourseId { get; set; }

    [MaxLength(255)]
    public string? ParentExamTitle { get; set; }

    [MaxLength(50)]
    public string? ParentExamCode { get; set; }

    [Range(1, 50)]
    public int SlotCount { get; set; }

    [Range(1, 100)]
    public int QuestionsPerSlot { get; set; }

    public List<int> QuestionIds { get; set; } = [];

    public List<CreateExamSlotTemplateInput>? SlotTemplates { get; set; }
}

public class SlotPartitionPreviewDto
{
    public int SlotIndex { get; set; }
    public string SlotName { get; set; } = null!;
    public string SubPaperCode { get; set; } = null!;
    public string SubPaperTitle { get; set; } = null!;
    public List<ExamQuestionDto> Questions { get; set; } = new();
    public int EasyCount { get; set; }
    public int MediumCount { get; set; }
    public int HardCount { get; set; }
}

public class AutoPartitionPreviewResponse
{
    public List<SlotPartitionPreviewDto> Slots { get; set; } = new();
    public List<ExamQuestionDto> ReserveQuestions { get; set; } = new();
    public int TotalSelected { get; set; }
    public int TotalAllocated { get; set; }
    public int TotalReserve { get; set; }
    public string Summary { get; set; } = null!;
}
