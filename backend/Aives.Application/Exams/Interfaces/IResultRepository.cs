using Aives.Application.Exams.DTOs;
using Aives.Domain.Entities;

namespace Aives.Application.Exams.Interfaces;

public interface IResultRepository
{
    Task<Result?> GetByIdAsync(int resultId);
    Task<Result?> GetByIdWithDetailsAsync(int resultId);
    Task<IEnumerable<Result>> GetBySessionIdWithDetailsAsync(int sessionId);
    Task<Result?> GetBySessionAndStudentAsync(int sessionId, int studentId);
    Task<Result> CreateAsync(Result result);
    Task AddOrUpdateAnswerAsync(Answer answer);
    Task UpdateAsync(Result result);
    Task SaveChangesAsync();
}