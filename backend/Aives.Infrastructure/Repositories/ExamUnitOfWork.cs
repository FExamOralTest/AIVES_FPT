using System.Data;
using Aives.Application.Exams.Exceptions;
using Aives.Application.Exams.Interfaces;
using Aives.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Aives.Infrastructure.Repositories;

public class ExamUnitOfWork(AivesDbContext db) : IExamUnitOfWork
{
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
    {
        // Serialize question edits against exam creation so usage checks cannot race.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var result = await action();
            await transaction.CommitAsync();
            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            db.ChangeTracker.Clear();
            var cause = ex;
            while (cause.InnerException is not null)
                cause = cause.InnerException;
            if (cause is PostgresException { SqlState: "40001" or "40P01" or "23505" or "23503" })
                throw new QuestionBankConflictException("The data changed concurrently. Reload and try again.", ex);
            throw;
        }
    }
}
