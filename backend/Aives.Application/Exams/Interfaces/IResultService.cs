using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Interfaces;

public interface IResultService
{
    Task<IEnumerable<ResultForReviewDto>> GetResultsForReviewAsync(int sessionId, int teacherId);
    Task<ResultForReviewDto> GetResultForReviewAsync(int resultId, int teacherId);
    Task<ResultForReviewDto> ReviewResultAsync(int resultId, ReviewResultRequest request, int teacherId);
    Task<ResultForReviewDto> PublishResultAsync(int resultId, int teacherId);
    Task<StudentResultDto> GetStudentResultAsync(int sessionId, int studentId);
    Task<IEnumerable<ResultForReviewDto>> GetAllPendingReviewResultsAsync(int teacherId);
    Task<IEnumerable<ResultForReviewDto>> GetStudentPublishedResultsAsync(int studentId);
}