using System.Text;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;

namespace Aives.Application.Exams.Services;

public static class AIQuestionValidator
{
    public static void Validate(List<AIQuestionDto>? questions, int expectedCount, float maxScore)
    {
        if (questions is null || questions.Count != expectedCount)
            throw InvalidResponse("AI returned an incorrect number of questions.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var question in questions)
        {
            if (question is null || !HasContent(question.QuestionText)
                || !HasContent(question.Topic) || !HasContent(question.SampleAnswer)
                || !float.IsFinite(question.MaxScore) || question.MaxScore <= 0
                || question.MaxScore > 100 || question.MaxScore != maxScore
                || question.Level is < 1 or > 3)
                throw InvalidResponse("AI returned a question with invalid or missing fields.");

            var key = string.Join(" ", new string(question.QuestionText.Normalize(NormalizationForm.FormKC)
                .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (!seen.Add(key))
                throw InvalidResponse("AI returned duplicate questions.");
        }
    }

    private static bool HasContent(string? value) => value?.Any(char.IsLetterOrDigit) == true;

    private static AIQuestionException InvalidResponse(string message)
        => new(AIQuestionFailure.InvalidResponse, message);
}
