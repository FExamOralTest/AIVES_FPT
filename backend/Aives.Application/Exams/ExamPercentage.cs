using Aives.Domain.Entities;

namespace Aives.Application.Exams;

/// <summary>
/// Exam question weights are percentages of a 100% paper.
/// Rubric awards stay on the question-bank scale, then each question is scaled to its weight.
/// </summary>
public static class ExamPercentage
{
    public const float Total = 100f;
    private const float Tolerance = 0.05f;

    public static bool IsTotal(float sum) =>
        float.IsFinite(sum) && Math.Abs(sum - Total) <= Tolerance;

    public static void EnsureTotal(IEnumerable<float> scores, string paperName)
    {
        var values = scores.ToList();
        if (values.Count == 0) return;
        if (values.Any(score => !float.IsFinite(score) || score <= 0 || score > Total))
            throw new ArgumentException($"{paperName} question percentages must be between 0.01 and 100.");

        var sum = values.Sum();
        if (!IsTotal(sum))
            throw new ArgumentException($"{paperName} question percentages must add up to 100%. Current total is {sum:0.####}%.");
    }

    public static IReadOnlyList<float> SplitEvenly(int count)
    {
        if (count <= 0) return [];
        var weights = new float[count];
        var used = 0f;
        for (var index = 0; index < count - 1; index++)
        {
            weights[index] = MathF.Round(Total / count, 4);
            used += weights[index];
        }

        weights[^1] = MathF.Round(Total - used, 4);
        return weights;
    }

    public static float Scale(float awarded, float rubricMax, float questionPercent)
    {
        if (!float.IsFinite(awarded) || !float.IsFinite(rubricMax) || !float.IsFinite(questionPercent))
            return 0;
        if (questionPercent <= 0 || rubricMax <= 0) return 0;

        var ratio = awarded / rubricMax;
        if (ratio < 0) ratio = 0;
        if (ratio > 1) ratio = 1;
        return MathF.Round(ratio * questionPercent, 4);
    }

    public static float WeightFor(Result result, int questionId, float? fallback)
    {
        var weight = Find(result.Exam, questionId) ?? Find(result.Session?.Exam, questionId);
        if (weight is > 0) return weight.Value;
        return fallback is > 0 ? fallback.Value : 10f;
    }

    private static float? Find(Exam? exam, int questionId)
    {
        var match = exam?.ExamQuestions?.FirstOrDefault(item => item.QuestionId == questionId);
        return match == null ? null : match.QuestionScore;
    }
}
