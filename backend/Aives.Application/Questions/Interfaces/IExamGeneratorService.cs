using Aives.Application.Questions.DTOs;

namespace Aives.Application.Questions.Interfaces;

public interface IExamGeneratorService
{
    Task<GenerateExamResponse> GenerateAsync(GenerateExamRequest request);
}
