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
}

public class FinishVivaSessionResponse
{
    public int SessionId { get; set; }
    public int StudentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public float? AiScore { get; set; }
}
