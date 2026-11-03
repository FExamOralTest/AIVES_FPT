namespace Aives.Application.Exams.Interfaces;

public interface IExamUnitOfWork
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> action);
}
