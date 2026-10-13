using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Aives.Application.Exams.Services;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.AI;

public class AIQuestionService : IAIQuestionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIQuestionService> _logger;

    public AIQuestionService(HttpClient httpClient, ILogger<AIQuestionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<AIQuestionDto>> GenerateQuestionsAsync(
        AIQuestionRequest request, CancellationToken cancellationToken = default)
    {
        var elapsed = Stopwatch.StartNew();
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/ai/questions/generate", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI question service returned HTTP {StatusCode}", (int)response.StatusCode);
                if (response.StatusCode == HttpStatusCode.GatewayTimeout)
                    throw new AIQuestionException(AIQuestionFailure.Timeout, "AI question generation timed out. Please retry later.");
                throw new AIQuestionException(AIQuestionFailure.Unavailable, "AI question generation failed. Please retry later.");
            }

            var result = await response.Content.ReadFromJsonAsync<AIQuestionResponse>(cancellationToken);
            AIQuestionValidator.Validate(result?.Questions, request.QuestionCount, request.MaxScore);
            _logger.LogInformation("AI generated {QuestionCount} questions in {ElapsedMs} ms", request.QuestionCount, elapsed.ElapsedMilliseconds);
            return result!.Questions!;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("AI question generation cancelled after {ElapsedMs} ms", elapsed.ElapsedMilliseconds);
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("AI question generation timed out after {ElapsedMs} ms", elapsed.ElapsedMilliseconds);
            throw new AIQuestionException(AIQuestionFailure.Timeout, "AI question generation timed out. Please retry later.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("AI question service connection failed after {ElapsedMs} ms", elapsed.ElapsedMilliseconds);
            throw new AIQuestionException(AIQuestionFailure.Unavailable, "AI question service is unavailable. Please retry later.", ex);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            _logger.LogWarning("AI question service returned an unreadable response");
            throw new AIQuestionException(AIQuestionFailure.InvalidResponse, "AI returned an invalid question generation response.", ex);
        }
        catch (AIQuestionException ex) when (ex.Failure == AIQuestionFailure.InvalidResponse)
        {
            _logger.LogWarning("AI question service response failed validation");
            throw;
        }
    }
}
