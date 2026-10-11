namespace Aives.Application.Documents.Interfaces;

public interface IAIQuestionService
{
    Task<List<AIQuestionDto>> GenerateQuestionsAsync(
        List<string> markdownContents,
        int count,
        string? focusTopics);
}

public class AIQuestionDto
{
    public string QuestionText      { get; set; } = null!;
    public float  MaxScore          { get; set; } = 1.0f;
    public bool   IsFollowUpAllowed { get; set; } = true;
}
