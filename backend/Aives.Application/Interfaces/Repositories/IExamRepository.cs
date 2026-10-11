using Aives.Domain.Entities;

namespace Aives.Application.Interfaces.Repositories;

public interface IExamRepository
{
    /// <summary>Lấy tất cả bài thi theo courseId, kèm Course.</summary>
    Task<IEnumerable<Exam>> GetByCourseIdAsync(int courseId);

    /// <summary>Lấy chi tiết một bài thi kèm ExamSessions → Results → Student.</summary>
    Task<Exam?> GetWithSessionsAsync(int examId);

    /// <summary>Lấy một phiên thi cụ thể kèm Results → Answers → Question.</summary>
    Task<ExamSession?> GetSessionWithResultsAsync(int sessionId);

    /// <summary>Lấy kết quả chi tiết của một student trong một phiên thi.</summary>
    Task<Result?> GetResultDetailAsync(int resultId);
}
