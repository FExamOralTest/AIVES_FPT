using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Interfaces.Repositories;

public interface IMarkdownDocumentRepository
{
    Task<IEnumerable<MarkdownDocument>> GetByCourseIdAsync(int courseId);
    Task<MarkdownDocument?> GetByIdAsync(int documentId);

    /// <summary>
    /// Kiểm tra Course đã có document với DocType này chưa
    /// (mỗi course chỉ được có 1 CONTENT và 1 LEARNING_OUTCOMES).
    /// </summary>
    Task<bool> ExistsByTypeAsync(int courseId, DocType docType, int? excludeDocumentId = null);

    Task<MarkdownDocument> CreateAsync(MarkdownDocument document);
    Task<MarkdownDocument> UpdateAsync(MarkdownDocument document);
    Task DeleteAsync(int documentId);
    Task SaveChangesAsync();
}
