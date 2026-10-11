using Aives.Application.Results.DTOs;

namespace Aives.Application.Results.Interfaces;

public interface IExamHistoryService
{
    Task<IEnumerable<ExamSummaryDto>> GetExamsByCourseAsync(int courseId);
    Task<IEnumerable<ExamSessionDetailDto>> GetSessionsByExamAsync(int examId);
    Task<ExamSessionDetailDto> GetSessionDetailAsync(int sessionId);
    Task<StudentResultDetailDto> GetResultDetailAsync(int resultId);
}
