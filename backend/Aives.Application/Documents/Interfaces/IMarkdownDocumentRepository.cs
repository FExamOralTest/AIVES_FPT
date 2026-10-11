using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Documents.Interfaces;

public interface IMarkdownDocumentRepository
{
    Task<IEnumerable<MarkdownDocument>> GetByCourseIdAsync(int courseId);
    Task<MarkdownDocument?> GetByIdAsync(int documentId);
    Task<bool> ExistsByTypeAsync(int courseId, DocType docType, int? excludeDocumentId = null);
    Task<MarkdownDocument> CreateAsync(MarkdownDocument document);
    Task<MarkdownDocument> UpdateAsync(MarkdownDocument document);
    Task DeleteAsync(int documentId);
    Task SaveChangesAsync();
}
