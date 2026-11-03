using Aives.Application.Exams.DTOs;
using Aives.Domain.Entities;
using Aives.Domain.Enums;

namespace Aives.Application.Exams.Interfaces;

public interface IExamRepository
{
    Task<Exam> CreateAsync(Exam exam);
    Task<Exam?> GetByIdAsync(int examId);
    Task<Exam?> GetByIdWithQuestionsAsync(int examId);
    Task<bool> ExamCodeExistsAsync(string examCode);
    Task<IEnumerable<Exam>> GetByTeacherIdAsync(int teacherId);
    Task AddQuestionsAsync(IEnumerable<ExamQuestion> questions);
    Task ReplaceQuestionsAsync(int examId, IEnumerable<ExamQuestion> questions);
    Task SetSlotTemplatesAsync(int examId, IEnumerable<ExamSlotTemplate> templates, Dictionary<int, List<int>> slotExamsMap);
    Task<bool> IsOwnerAsync(int examId, int teacherId);
    Task<bool> HasSessionsAsync(int examId);
    void Remove(Exam exam);
    Task SaveChangesAsync();
}
