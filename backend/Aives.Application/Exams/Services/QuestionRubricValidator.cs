using System.ComponentModel.DataAnnotations;
using System.Text;
using Aives.Application.Exams.DTOs;

namespace Aives.Application.Exams.Services;

public static class QuestionRubricValidator
{
    public static IEnumerable<ValidationResult> GetErrors(
        string? sampleAnswer, string? learningOutcome, List<RubricCriterionDto>? rubric, float maxScore)
    {
        if (!HasContent(sampleAnswer) || sampleAnswer!.Length > 10000)
            yield return new ValidationResult("A model answer of at most 10000 characters is required.", ["SampleAnswer"]);
        if (!HasContent(learningOutcome) || learningOutcome!.Length > 2000)
            yield return new ValidationResult("A learning outcome of at most 2000 characters is required.", ["LearningOutcome"]);
        if (!IsValidMaxScore(maxScore))
            yield return new ValidationResult("Maximum score must be 0.01-100 with at most four decimal places.", ["MaxScore"]);
        if (rubric is null || rubric.Count is < 1 or > 20)
        {
            yield return new ValidationResult("Provide between 1 and 20 rubric criteria.", ["Rubric"]);
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal total = 0;
        var validScores = true;
        for (var i = 0; i < rubric.Count; i++)
        {
            var criterion = rubric[i];
            if (criterion is null || !HasContent(criterion.Description) || criterion.Description.Length > 2000)
                yield return new ValidationResult("Each criterion needs a description of at most 2000 characters.", [$"Rubric[{i}].Description"]);
            else if (!seen.Add(Normalize(criterion.Description)))
                yield return new ValidationResult("Rubric criteria must be distinct.", [$"Rubric[{i}].Description"]);

            if (criterion is null || criterion.MaxScore is <= 0 or > 100
                || decimal.Round(criterion.MaxScore, 4) != criterion.MaxScore)
            {
                validScores = false;
                yield return new ValidationResult("Criterion scores must be positive, at most 100, with at most four decimal places.", [$"Rubric[{i}].MaxScore"]);
            }
            else
                total += criterion.MaxScore;
        }
        if (validScores && IsValidMaxScore(maxScore) && total != (decimal)maxScore)
            yield return new ValidationResult("Rubric scores must sum exactly to the question maximum score.", ["Rubric", "MaxScore"]);
    }

    public static bool IsValidMaxScore(float value)
        => float.IsFinite(value) && value is >= 0.01f and <= 100
            && (float)decimal.Round((decimal)value, 4) == value;

    private static bool HasContent(string? value) => value?.Any(char.IsLetterOrDigit) == true;

    private static string Normalize(string value)
        => string.Join(" ", new string(value.Normalize(NormalizationForm.FormKC)
            .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
