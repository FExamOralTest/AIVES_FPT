using Aives.Application.DTOs;

namespace Aives.Application.Interfaces.Services;

public interface IExamGeneratorService
{
    /// <summary>
    /// Tạo đề thi ngẫu nhiên từ QuestionBank theo courseId và số lượng từng level.
    /// Câu hỏi được xáo trộn trước khi trả về.
    /// </summary>
    Task<GenerateExamResponse> GenerateAsync(GenerateExamRequest request);
}
