using Aives.Application.Exams.DTOs;
using Microsoft.AspNetCore.Http;

namespace Aives.Application.Exams.Interfaces;

public interface IDocumentService
{
    Task<MarkdownDocumentDto> UploadMarkdownAsync(int courseId, UploadMarkdownRequest request, int teacherId);
    Task<IEnumerable<MarkdownDocumentDto>> GetDocumentsByCourseAsync(int courseId, int teacherId);
    Task<MarkdownDocumentDto> GetDocumentByIdAsync(int courseId, int documentId, int teacherId);
    Task<MarkdownDocumentDto> UpdateDocumentAsync(int courseId, int documentId, UpdateMarkdownRequest request, int teacherId);
    Task DeleteDocumentAsync(int courseId, int documentId, int teacherId);
    Task<GenerateQuestionsResponse> GenerateQuestionsAsync(GenerateQuestionsRequest request, int teacherId, CancellationToken cancellationToken = default);
}
