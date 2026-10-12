using Aives.Application.Exams.DTOs;

namespace Aives.Application.Interfaces.Services;

public interface IExamGeneratorService
{
    /// <summary>
    /// Sinh de thi ngau nhien tu QuestionBank theo courseId va so luong tung level.
    /// Teacher chi duoc generate cho course cua minh; Admin bypass.
    /// </summary>
    Task<GenerateExamResponse> GenerateAsync(GenerateExamRequest request, int callerId, bool isAdmin);
}
