using System.Net.Http.Json;
using System.Text.Json;
using Aives.Application.Documents.Interfaces;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.AI;

public class AIQuestionService : IAIQuestionService
{
    private readonly HttpClient                  _httpClient;
    private readonly ILogger<AIQuestionService>  _logger;

    public AIQuestionService(HttpClient httpClient, ILogger<AIQuestionService> logger)
    {
        _httpClient = httpClient;
        _logger     = logger;
    }

    public async Task<List<AIQuestionDto>> GenerateQuestionsAsync(
        List<string> markdownContents, int count, string? focusTopics)
    {
        try
        {
            var request = new
            {
                markdown_contents = markdownContents,
                question_count    = count,
                focus_topics      = focusTopics?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    ?? Array.Empty<string>()
            };

            var response = await _httpClient.PostAsJsonAsync("/api/ai/generate-questions", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("AI service returned {StatusCode}: {Error}", response.StatusCode, error);
                return [];
            }

            var result = await response.Content.ReadFromJsonAsync<AIQuestionResponse>();
            return result?.Questions ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to connect to AI service at {BaseAddress}", _httpClient.BaseAddress);
            return [];
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI service response");
            return [];
        }
    }

    private class AIQuestionResponse
    {
        public List<AIQuestionDto> Questions { get; set; } = new();
    }
}
