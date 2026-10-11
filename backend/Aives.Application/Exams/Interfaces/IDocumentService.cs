using Aives.Application.Exams.DTOs;
using Microsoft.AspNetCore.Http;

namespace Aives.Application.Exams.Interfaces;

public interface IDocumentService
{
    Task<MarkdownDocumentDto> UploadMarkdownAsync(int courseId, UploadMarkdownRequest request, int teacherId);
    Task<IEnumerable<MarkdownDocumentDto>> GetDocumentsByCourseAsync(int courseId, int teacherId);
    Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request, int teacherId);
}