using Aives.Application.Exams.Interfaces;
using Aives.Domain.Entities;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using System.Linq.Expressions;
using Aives.Application.Exams.DTOs;

namespace Aives.Infrastructure.Repositories;

public class QuestionBankRepository : IQuestionBankRepository
{
    private readonly AivesDbContext _db;

    public QuestionBankRepository(AivesDbContext db) => _db = db;

    public async Task<QuestionBankPageDto> GetPageAsync(int courseId, QuestionBankQuery query)
    {
        var filtered = _db.QuestionBanks.AsNoTracking().Where(q => q.CourseId == courseId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(q => q.QuestionText.ToLower().Contains(search)
                || q.Topic != null && q.Topic.ToLower().Contains(search));
        }

        var total = await filtered.CountAsync();
        var items = await filtered.OrderByDescending(q => q.CreatedAt).ThenByDescending(q => q.QuestionId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(q => new QuestionBankDto
            {
                QuestionId = q.QuestionId,
                CourseId = q.CourseId,
                DocumentId = q.DocumentId,
                QuestionText = q.QuestionText,
                Topic = q.Topic,
                SampleAnswer = q.SampleAnswer,
                LearningOutcome = q.LearningOutcome,
                Rubric = q.Rubric.OrderBy(c => c.Position).Select(c => new RubricCriterionDto
                {
                    Description = c.Description,
                    MaxScore = c.MaxScore
                }).ToList(),
                Level = q.Level,
                MaxScore = q.MaxScore ?? 1,
                IsFollowUpAllowed = q.IsFollowUpAllowed ?? true,
                CreatedAt = q.CreatedAt,
                IsInUse = q.ExamQuestions.Any() || q.Answers.Any()
            }).ToListAsync();
        return new QuestionBankPageDto(items, total, query.Page, query.PageSize);
    }

    public async Task<List<QuestionBank>> GetSuggestionsAsync(int courseId, string? focusTopic, int maxCount)
    {
        var query = _db.QuestionBanks.AsNoTracking().Where(q => q.CourseId == courseId);
        var keywords = focusTopic?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(k => k.ToLowerInvariant()).Distinct().ToList() ?? [];

        var parameter = Expression.Parameter(typeof(QuestionBank), "q");
        var text = Expression.Call(Expression.Property(parameter, nameof(QuestionBank.QuestionText)),
            nameof(string.ToLower), Type.EmptyTypes);
        Expression score = Expression.Constant(0);
        foreach (var keyword in keywords)
        {
            var match = Expression.Call(text, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(keyword));
            score = Expression.Add(score, Expression.Condition(match, Expression.Constant(1), Expression.Constant(0)));
        }
        var rank = Expression.Lambda<Func<QuestionBank, int>>(score, parameter);
        return await query.OrderByDescending(rank).ThenByDescending(q => q.MaxScore)
            .ThenByDescending(q => q.CreatedAt).ThenByDescending(q => q.QuestionId)
            .Take(maxCount).ToListAsync();
    }

    public async Task<List<QuestionBank>> GetByIdsForCourseAsync(int courseId, IEnumerable<int> questionIds)
    {
        var ids = questionIds.ToList();
        return await _db.QuestionBanks.AsNoTracking()
            .Where(q => q.CourseId == courseId && ids.Contains(q.QuestionId)).ToListAsync();
    }

    public async Task<bool> IsInUseAsync(int questionId)
        => await _db.ExamQuestions.AnyAsync(q => q.QuestionId == questionId)
            || await _db.Answers.AnyAsync(a => a.QuestionId == questionId);

    public async Task AddAsync(QuestionBank question) => await _db.QuestionBanks.AddAsync(question);

    public void Remove(QuestionBank question) => _db.QuestionBanks.Remove(question);

    public async Task<IEnumerable<QuestionBank>> GetByCourseIdAsync(int courseId, int limit = 50)
        => await _db.QuestionBanks
            .Where(q => q.CourseId == courseId)
            .OrderByDescending(q => q.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<List<int>> GetValidQuestionIdsForCourseAsync(int courseId, IEnumerable<int> questionIds)
    {
        var idList = questionIds.ToList();
        return await _db.QuestionBanks
            .Where(q => q.CourseId == courseId && idList.Contains(q.QuestionId))
            .Select(q => q.QuestionId)
            .ToListAsync();
    }

    public async Task<QuestionBank?> GetByIdAsync(int questionId)
        => await _db.QuestionBanks
            .Include(q => q.Rubric)
            .Include(q => q.Course)
            .FirstOrDefaultAsync(q => q.QuestionId == questionId);

    public async Task<IEnumerable<QuestionBank>> GetRandomByLevelAsync(int courseId, int level, int count)
    {
        if (count <= 0) return [];
        return await _db.QuestionBanks.AsNoTracking()
            .Where(q => q.CourseId == courseId && q.Level == level)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync();
    }

    public async Task SaveGeneratedAsync(IEnumerable<QuestionBank> questions, CancellationToken cancellationToken = default)
    {
        var batch = questions.ToList();
        try
        {
            await _db.QuestionBanks.AddRangeAsync(batch, cancellationToken);
            // A single SaveChanges commits the entire batch in EF Core's transaction.
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var question in batch)
            {
                foreach (var criterion in question.Rubric.ToList())
                    _db.Entry(criterion).State = EntityState.Detached;
                _db.Entry(question).State = EntityState.Detached;
            }
            throw;
        }
    }

    public async Task SaveChangesAsync() => await _db.SaveChangesAsync();
}
