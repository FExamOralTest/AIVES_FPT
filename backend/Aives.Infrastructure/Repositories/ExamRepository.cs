using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aives.Infrastructure.Repositories;

public class ExamRepository : IExamRepository
{
    private readonly AivesDbContext _db;

    public ExamRepository(AivesDbContext db)
    {
        _db = db;
    }

    public async Task<Exam> CreateAsync(Exam exam)
    {
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();
        return exam;
    }

    public async Task<Exam?> GetByIdAsync(int examId)
    {
        return await _db.Exams
            .Include(e => e.ExamSessions)
            .Include(e => e.Course)
            .Include(e => e.CreatedByNavigation)
            .Include(e => e.SlotTemplates)
                .ThenInclude(st => st.TemplateExams)
                    .ThenInclude(te => te.ExamPaper)
            .FirstOrDefaultAsync(e => e.ExamId == examId);
    }

    public async Task<Exam?> GetByIdWithQuestionsAsync(int examId)
    {
        return await _db.Exams
            .Include(e => e.ExamSessions)
            .Include(e => e.Course)
            .Include(e => e.CreatedByNavigation)
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
            .Include(e => e.SlotTemplates)
                .ThenInclude(st => st.TemplateExams)
                    .ThenInclude(te => te.ExamPaper)
            .FirstOrDefaultAsync(e => e.ExamId == examId);
    }

    public async Task<bool> ExamCodeExistsAsync(string examCode)
    {
        return await _db.Exams.AnyAsync(e => e.ExamCode == examCode);
    }

    public async Task<IEnumerable<Exam>> GetByTeacherIdAsync(int teacherId)
    {
        return await _db.Exams
            .Include(e => e.ExamSessions)
            .Include(e => e.Course)
            .Include(e => e.ExamQuestions)
                .ThenInclude(eq => eq.Question)
            .Include(e => e.SlotTemplates)
                .ThenInclude(st => st.TemplateExams)
                    .ThenInclude(te => te.ExamPaper)
            .Where(e => e.CreatedBy == teacherId && e.Course.TeacherId == teacherId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
    }

    public async Task AddQuestionsAsync(IEnumerable<ExamQuestion> questions)
    {
        await _db.ExamQuestions.AddRangeAsync(questions);
    }

    public async Task ReplaceQuestionsAsync(int examId, IEnumerable<ExamQuestion> questions)
    {
        var existing = await _db.ExamQuestions.Where(eq => eq.ExamId == examId).ToListAsync();
        var incoming = questions.ToDictionary(q => q.QuestionId);
        foreach (var item in existing)
        {
            if (incoming.Remove(item.QuestionId, out var replacement))
                item.QuestionScore = replacement.QuestionScore;
            else
                _db.ExamQuestions.Remove(item);
        }
        await _db.ExamQuestions.AddRangeAsync(incoming.Values);
        await _db.SaveChangesAsync();
    }

    public async Task SetSlotTemplatesAsync(
        int examId,
        IEnumerable<ExamSlotTemplate> templates,
        Dictionary<int, List<int>> slotExamsMap)
    {
        var existingTemplates = await _db.ExamSlotTemplates
            .Include(st => st.TemplateExams)
            .Where(st => st.ExamId == examId)
            .ToListAsync();

        var incomingList = templates.ToList();
        var incomingBySlotIndex = incomingList.ToDictionary(t => t.SlotIndex);

        // Remove templates whose SlotIndex is no longer in incoming
        foreach (var existing in existingTemplates)
        {
            if (!incomingBySlotIndex.ContainsKey(existing.SlotIndex))
            {
                _db.ExamSlotTemplates.Remove(existing);
            }
        }

        // Upsert by SlotIndex
        foreach (var incoming in incomingList)
        {
            var existing = existingTemplates.FirstOrDefault(t => t.SlotIndex == incoming.SlotIndex);
            ExamSlotTemplate target;
            if (existing != null)
            {
                existing.SlotName = incoming.SlotName;
                existing.DurationMinutes = incoming.DurationMinutes;
                existing.MaxCapacity = incoming.MaxCapacity;
                target = existing;
            }
            else
            {
                incoming.ExamId = examId;
                _db.ExamSlotTemplates.Add(incoming);
                target = incoming;
            }

            // Sync TemplateExams for this slot template
            var allowedExamIds = slotExamsMap.TryGetValue(incoming.SlotIndex, out var ids) && ids != null
                ? ids.Distinct().Where(id => id != examId).ToList() // Edge Case 1: validate not containing examId itself
                : new List<int>();

            var currentExams = target.TemplateExams.ToList();
            foreach (var te in currentExams)
            {
                if (!allowedExamIds.Contains(te.ExamPaperId))
                {
                    _db.ExamSlotTemplateExams.Remove(te);
                }
            }

            var currentExamIds = currentExams.Select(e => e.ExamPaperId).ToHashSet();
            foreach (var paperId in allowedExamIds)
            {
                if (!currentExamIds.Contains(paperId))
                {
                    target.TemplateExams.Add(new ExamSlotTemplateExam
                    {
                        ExamPaperId = paperId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> IsOwnerAsync(int examId, int teacherId)
    {
        return await _db.Exams
            .AnyAsync(e => e.ExamId == examId && e.CreatedBy == teacherId);
    }

    public Task<bool> HasSessionsAsync(int examId) => _db.ExamSessions.AnyAsync(s => s.ExamId == examId);

    public void Remove(Exam exam) => _db.Exams.Remove(exam);

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
