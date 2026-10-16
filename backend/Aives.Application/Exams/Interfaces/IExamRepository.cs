using Aives.Application.Exams.DTOs;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Interfaces;
public interface IExamRepository
{
    Task<Domain.Entities.Exam> CreateAsync(Domain.Entities.Exam exam);
    Task<Domain.Entities.Exam?> GetByIdAsync(int examId);
    Task<Domain.Entities.Exam?> GetByIdWithQuestionsAsync(int examId);
    Task<bool> ExamCodeExistsAsync(string examCode);
    Task<IEnumerable<Domain.Entities.Exam>> GetByTeacherIdAsync(int teacherId);
    Task AddQuestionsAsync(IEnumerable<Domain.Entities.ExamQuestion> questions);
    Task ReplaceQuestionsAsync(int examId, IEnumerable<Domain.Entities.ExamQuestion> questions);
    Task<bool> IsOwnerAsync(int examId, int teacherId);
    Task<bool> HasSessionsAsync(int examId);
    void Remove(Domain.Entities.Exam exam);
    Task SaveChangesAsync();
}
