using System.Text.Json.Serialization;

namespace Aives.Application.Exams.DTOs;

public class AIQuestionDocumentDto
{
    [JsonPropertyName("document_id")]
    public int DocumentId { get; set; }

    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = null!;

    [JsonPropertyName("content")]
    public string Content { get; set; } = null!;
}

public class AIQuestionRequest
{
    [JsonPropertyName("documents")]
    public List<AIQuestionDocumentDto> Documents { get; set; } = new();

    [JsonPropertyName("question_count")]
    public int QuestionCount { get; set; }

    [JsonPropertyName("focus_topics")]
    public string[] FocusTopics { get; set; } = Array.Empty<string>();

    [JsonPropertyName("max_score")]
    public float MaxScore { get; set; } = 1.0f;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AIQuestionDto
{
    [JsonRequired, JsonPropertyName("question_text")]
    public string QuestionText { get; set; } = null!;

    [JsonRequired, JsonPropertyName("topic")]
    public string Topic { get; set; } = null!;

    [JsonRequired, JsonPropertyName("sample_answer")]
    public string SampleAnswer { get; set; } = null!;

    [JsonRequired, JsonPropertyName("learning_outcome")]
    public string LearningOutcome { get; set; } = null!;

    [JsonRequired, JsonPropertyName("rubric")]
    public List<AIRubricCriterionDto>? Rubric { get; set; }

    [JsonRequired, JsonPropertyName("max_score")]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public float MaxScore { get; set; }

    [JsonRequired, JsonPropertyName("level")]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public int Level { get; set; }

    [JsonRequired, JsonPropertyName("is_follow_up_allowed")]
    public bool IsFollowUpAllowed { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AIRubricCriterionDto
{
    [JsonRequired, JsonPropertyName("description")]
    public string Description { get; set; } = null!;

    [JsonRequired, JsonPropertyName("max_score")]
    [JsonNumberHandling(JsonNumberHandling.Strict)]
    public decimal MaxScore { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class AIQuestionResponse
{
    [JsonRequired, JsonPropertyName("questions")]
    public List<AIQuestionDto>? Questions { get; set; }
}
