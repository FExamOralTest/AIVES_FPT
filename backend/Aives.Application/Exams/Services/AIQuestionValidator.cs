using System.Text;
using System.Text.RegularExpressions;
using Aives.Application.Exams.DTOs;
using Aives.Application.Exams.Exceptions;

namespace Aives.Application.Exams.Services;

public static class AIQuestionValidator
{
    public static void Validate(List<AIQuestionDto>? questions, int expectedCount, float maxScore,
        string? learningOutcomesContent = null)
    {
        if (questions is null || questions.Count != expectedCount)
            throw InvalidResponse("AI returned an incorrect number of questions.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var question in questions)
        {
            if (question is null || !HasContent(question.QuestionText)
                || !HasContent(question.Topic) || !HasContent(question.SampleAnswer)
                || question.QuestionText.Length > 10000 || question.Topic.Length > 1000
                || !float.IsFinite(question.MaxScore) || question.MaxScore <= 0
                || question.MaxScore > 100 || question.MaxScore != maxScore
                || question.Level is < 1 or > 3)
                throw InvalidResponse("AI returned a question with invalid or missing fields.");

            var rubric = question.Rubric?.Select(c => c is null ? null! : new RubricCriterionDto
            {
                Description = c.Description,
                MaxScore = c.MaxScore
            }).ToList();
            var rubricErrors = QuestionRubricValidator.GetErrors(
                question.SampleAnswer, question.LearningOutcome, rubric, question.MaxScore).ToList();
            if (rubricErrors.Count > 0)
                throw InvalidResponse(string.Join(" ", rubricErrors.Select(e => e.ErrorMessage)));
            if (learningOutcomesContent is not null && !Regex.Replace(learningOutcomesContent, @"\s+", " ")
                .Contains(Regex.Replace(question.LearningOutcome, @"\s+", " ").Trim(), StringComparison.Ordinal))
                throw InvalidResponse("AI returned a learning outcome outside the supplied document.");

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
