using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IAIQuestionService
{
    Task<List<AIQuestionDto>> GenerateQuestionsAsync(AIQuestionRequest request, CancellationToken cancellationToken = default);
}
