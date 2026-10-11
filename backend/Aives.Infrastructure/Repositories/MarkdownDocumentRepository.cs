using Aives.Application.Interfaces.Repositories;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class MarkdownDocumentRepository : IMarkdownDocumentRepository
{
    private readonly AivesDbContext _db;

    public MarkdownDocumentRepository(AivesDbContext db) => _db = db;

    public async Task<IEnumerable<MarkdownDocument>> GetByCourseIdAsync(int courseId)
        => await _db.MarkdownDocuments.AsNoTracking()
                                          .Include(d => d.Course)
                                          .Where(d => d.CourseId == courseId)
                                          .ToListAsync();

    public async Task<MarkdownDocument?> GetByIdAsync(int documentId)
        => await _db.MarkdownDocuments.AsNoTracking()
                                          .Include(d => d.Course)
                                          .FirstOrDefaultAsync(d => d.DocumentId == documentId);

    public async Task<bool> ExistsByTypeAsync(int courseId, DocType docType, int? excludeDocumentId = null)
        => await _db.MarkdownDocuments.AnyAsync(d =>
            d.CourseId == courseId &&
            d.DocType  == docType  &&
            (excludeDocumentId == null || d.DocumentId != excludeDocumentId));

    public async Task<MarkdownDocument> CreateAsync(MarkdownDocument document)
    {
        _db.MarkdownDocuments.Add(document);
        await _db.SaveChangesAsync();
        return document;
    }

    public async Task<MarkdownDocument> UpdateAsync(MarkdownDocument document)
    {
        _db.MarkdownDocuments.Update(document);
        await _db.SaveChangesAsync();
        return document;
    }

    public async Task DeleteAsync(int documentId)
    {
        var doc = await _db.MarkdownDocuments.FindAsync(documentId)
            ?? throw new KeyNotFoundException($"Document {documentId} not found.");
        _db.MarkdownDocuments.Remove(doc);
        await _db.SaveChangesAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}