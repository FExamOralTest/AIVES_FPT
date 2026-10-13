namespace Aives.Application.Exams.Exceptions;

public class QuestionBankConflictException : Exception
{
    public QuestionBankConflictException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
