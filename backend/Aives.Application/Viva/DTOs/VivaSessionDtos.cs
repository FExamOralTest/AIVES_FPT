using Microsoft.AspNetCore.Http;

namespace Aives.Application.Viva.DTOs;

public class StartVivaSessionResponse
{
    public int SessionId { get; set; }
    public string SessionName { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int TotalQuestions { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public VivaQuestionDto? CurrentQuestion { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndsAt { get; set; }
}

public class VivaQuestionDto
{
    public int QuestionId { get; set; }
    public int QuestionIndex { get; set; }
    public int TotalQuestions { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int Level { get; set; }
    public float? MaxScore { get; set; }
    public bool IsFollowUpAllowed { get; set; }
    public bool IsFollowUp { get; set; }
}

public class SubmitVivaAnswerRequest
{
    public int QuestionId { get; set; }
    public IFormFile? AudioFile { get; set; }
    public string? TextAnswer { get; set; }
    public bool IsFollowUp { get; set; }

    /// <summary>
    /// UploadId của phiên upload chunked đã COMPLETED (POST /api/audio/chunked/*).
    /// Khi có giá trị này, backend KHÔNG upload lại audio mà gắn thẳng object đã có
    /// trên R2 vào Answer và chạy STT.
    /// </summary>
    /// <remarks>
    /// Backend tra cứu bảng media_uploads theo uploadId, rồi đối chiếu
    /// SessionId / StudentId / QuestionId và trạng thái COMPLETED — không tin
    /// giá trị client gửi lên.
    /// </remarks>
    public string? AudioUploadId { get; set; }

    /// <summary>
    /// Object key trên R2 của audio đã upload chunked (fallback cho client cũ).
    /// Backend vẫn kiểm chứng qua media_uploads trước khi dùng.
    /// </summary>
    public string? AudioObjectKey { get; set; }
}

public class SubmitVivaAnswerResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Transcript { get; set; }
    public bool ShouldFollowUp { get; set; }
    public string? FollowUpQuestion { get; set; }
    public int CurrentQuestionIndex { get; set; }
    public int TotalQuestions { get; set; }
    public bool IsCompleted { get; set; }
    public VivaQuestionDto? NextQuestion { get; set; }

    /// <summary>PK của Answer vừa được tạo/cập nhật — dùng để tra cứu audio sau này.</summary>
    public int AnswerId { get; set; }
}

public class FinishVivaSessionResponse
{
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public float? AiScore { get; set; }
    public string? AiEvaluationStatus { get; set; }
}
