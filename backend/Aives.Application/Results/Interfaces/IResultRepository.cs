using Aives.Domain.Entities;

namespace Aives.Application.Results.Interfaces;

public interface IResultRepository
{
    Task<Result?> GetByIdAsync(int resultId);
    Task<Result?> GetByIdWithDetailsAsync(int resultId);
    Task<IEnumerable<Result>> GetBySessionIdWithDetailsAsync(int sessionId);
    Task UpdateAsync(Result result);
    Task SaveChangesAsync();
}
