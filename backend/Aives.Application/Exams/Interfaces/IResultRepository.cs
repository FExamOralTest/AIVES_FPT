using Aives.Application.Exams.DTOs;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Interfaces;

public interface IResultRepository
{
    Task<Result?> GetByIdAsync(int resultId);
    Task<Result?> GetByIdWithDetailsAsync(int resultId);
    Task<IEnumerable<Result>> GetBySessionIdWithDetailsAsync(int sessionId);
    Task<IEnumerable<Result>> GetPendingReviewResultsForTeacherAsync(int teacherId);
    Task<IEnumerable<Result>> GetStudentPublishedResultsAsync(int studentId);
    Task UpdateAsync(Result result);
    Task SaveChangesAsync();
}