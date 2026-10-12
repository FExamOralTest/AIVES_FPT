using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IAIQuestionService
{
    Task<List<AIQuestionDto>> GenerateQuestionsAsync(List<string> markdownContents, int count, string? focusTopics);
}

public class AIQuestionDto
{
    public string QuestionText { get; set; } = null!;
    public float MaxScore { get; set; } = 1.0f;
    public int Level { get; set; } = 1;
    public bool IsFollowUpAllowed { get; set; } = true;
}
