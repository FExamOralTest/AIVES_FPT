namespace Aives.Application.Exams.Exceptions;

public enum AIQuestionFailure
{
    Timeout,
    Unavailable,
    InvalidResponse
}

public class AIQuestionException : Exception
{
    public AIQuestionFailure Failure { get; }

    public AIQuestionException(AIQuestionFailure failure, string message, Exception? innerException = null)
        : base(message, innerException) => Failure = failure;
}
