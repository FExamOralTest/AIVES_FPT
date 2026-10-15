using Aives.Application.DTOs;

namespace Aives.Application.Interfaces.Services;

public interface IMarkdownDocumentService
{
    Task<IEnumerable<DocumentDto>> GetByCourseIdAsync(int courseId);
    Task<DocumentDto> GetByIdAsync(int documentId);
    Task<DocumentDto> UploadAsync(UploadDocumentRequest request);
    /// <summary>
    /// De file .md cu bang file moi.
    /// Teacher chi duoc thay the document thuoc course cua minh; Admin bypass.
    /// </summary>
    Task<DocumentDto> ReplaceAsync(ReplaceDocumentRequest request);
    Task DeleteAsync(int documentId, int requestedByUserId);
}
