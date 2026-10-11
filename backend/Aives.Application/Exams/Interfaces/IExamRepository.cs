using Aives.Domain.Entities;

namespace Aives.Application.Exams.Interfaces;

public interface IExamRepository
{
    Task<Exam> CreateAsync(Exam exam);
    Task<Exam?> GetByIdAsync(int examId);
    Task<Exam?> GetByIdWithQuestionsAsync(int examId);
    Task<bool> ExamCodeExistsAsync(string examCode);
    Task<IEnumerable<Exam>> GetByTeacherIdAsync(int teacherId);
    Task<IEnumerable<Exam>> GetByCourseIdAsync(int courseId);
    Task AddQuestionsAsync(IEnumerable<ExamQuestion> questions);
    Task<bool> IsOwnerAsync(int examId, int teacherId);

    // History queries
    Task<Exam?> GetWithSessionsAsync(int examId);
    Task<ExamSession?> GetSessionWithResultsAsync(int sessionId);
    Task<Result?> GetResultDetailAsync(int resultId);

    Task SaveChangesAsync();
}
