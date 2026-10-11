using Aives.Application.Documents.DTOs;

namespace Aives.Application.Documents.Interfaces;

public interface IMarkdownDocumentService
{
    Task<IEnumerable<DocumentDto>> GetByCourseIdAsync(int courseId);
    Task<DocumentDto> GetByIdAsync(int documentId);
    Task<DocumentDto> UploadAsync(UploadDocumentRequest request);
    Task DeleteAsync(int documentId, int requestedByUserId);
}
