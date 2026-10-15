using System.Text;
using System.Text.RegularExpressions;
using Aives.Application.AI.DTOs;

namespace Aives.Application.AI;

public static class EvaluationContract
{
    public static void Validate(EvaluateSessionRequest request, EvaluateSessionResponse response)
    {
        if (!response.Success || response.ExamSessionId != request.ExamSessionId
            || response.Status != "PendingReview" || string.IsNullOrWhiteSpace(response.OverallFeedback)
            || !ValidTexts(response.StudySuggestions) || response.QuestionScores == null
            || request.QuestionAnswers.Count == 0
            || response.QuestionScores.Count != request.QuestionAnswers.Count
            || !ValidScore(response.TotalScore, response.MaxTotalScore)
            || !float.IsFinite(response.Percentage) || response.Percentage < 0 || response.Percentage > 100)
            throw new InvalidDataException("Invalid session evaluation.");

        var questions = request.QuestionAnswers.ToDictionary(q => q.QuestionId, StringComparer.Ordinal);
        var received = new HashSet<string>(StringComparer.Ordinal);
        foreach (var score in response.QuestionScores)
        {
            if (score == null || string.IsNullOrWhiteSpace(score.QuestionId)
                || !received.Add(score.QuestionId) || !questions.TryGetValue(score.QuestionId, out var question))
                throw new InvalidDataException("Evaluation question IDs do not match the submission.");

            var maxScore = question.MaxScore ?? request.MaxScorePerQuestion;
            if (!ValidScore(score.Score, score.MaxScore) || !Close(score.MaxScore, maxScore)
                || Normalize(score.QuestionText) != Normalize(question.QuestionText)
                || Normalize(score.StudentAnswer) != Normalize(question.StudentAnswer)
                || string.IsNullOrWhiteSpace(score.Feedback)
                || !ValidTexts(score.Strengths) || !ValidTexts(score.Weaknesses)
                || !ValidTexts(score.MissingContent) || !ValidTexts(score.Improvements)
                || score.SentenceFeedback == null || score.CriterionAnalyses == null)
                throw new InvalidDataException("Invalid answer evaluation.");

            ValidateAnalysis(question, score);
        }

        var total = response.QuestionScores.Sum(q => q.Score);
        var maximum = response.QuestionScores.Sum(q => q.MaxScore);
        var percentage = maximum > 0 ? total / maximum * 100 : 0;
        if (!Close(response.TotalScore, total) || !Close(response.MaxTotalScore, maximum)
            || !Close(response.Percentage, percentage))
            throw new InvalidDataException("Evaluation totals do not match the answer scores.");
    }

    private static void ValidateAnalysis(QuestionAnswerPairDto question, QuestionScoreDetailDto score)
    {
        var transcript = Normalize(question.StudentAnswer);
        foreach (var sentence in score.SentenceFeedback)
        {
            if (sentence == null || string.IsNullOrWhiteSpace(sentence.Sentence)
                || string.IsNullOrWhiteSpace(sentence.Comment)
                || sentence.Judgment is not ("correct" or "error" or "omission" or "irrelevant")
                || !transcript.Contains(Normalize(sentence.Sentence), StringComparison.Ordinal))
                throw new InvalidDataException("Sentence feedback is not grounded in the transcript.");
        }

        var expected = question.Rubric.Select(c => c.CriterionId).ToHashSet(StringComparer.Ordinal);
        var received = new HashSet<string>(StringComparer.Ordinal);
        foreach (var criterion in score.CriterionAnalyses)
        {
            if (criterion == null || string.IsNullOrWhiteSpace(criterion.CriterionId)
                || !expected.Contains(criterion.CriterionId) || !received.Add(criterion.CriterionId)
                || string.IsNullOrWhiteSpace(criterion.Explanation)
                || criterion.Judgment is not ("met" or "partial" or "not_met")
                || !ValidTexts(criterion.Errors) || !ValidTexts(criterion.Omissions)
                || !ValidTexts(criterion.EvidenceQuotes))
                throw new InvalidDataException("Invalid rubric analysis.");

            if (criterion.Judgment == "met" && (criterion.Errors.Count > 0 || criterion.Omissions.Count > 0)
                || criterion.Judgment == "partial" && criterion.Omissions.Count == 0
                || criterion.Judgment == "not_met" && criterion.Errors.Count == 0 && criterion.Omissions.Count == 0
                || criterion.Judgment is "met" or "partial" && criterion.EvidenceQuotes.Count == 0)
                throw new InvalidDataException("Criterion judgment is missing supporting evidence.");

            foreach (var quote in criterion.EvidenceQuotes)
            {
                if (!transcript.Contains(Normalize(quote), StringComparison.Ordinal))
                    throw new InvalidDataException("Analysis cites text absent from the transcript.");
            }
        }

        if (!expected.SetEquals(received)
            || expected.Count > 0 && transcript.Length > 0 && score.SentenceFeedback.Count == 0
            || score.CriterionAnalyses.Any(c => c.Judgment != "met") && score.Improvements.Count == 0)
            throw new InvalidDataException("Evaluation is missing rubric feedback.");
    }

    private static bool ValidTexts(IEnumerable<string>? values) =>
        values != null && values.All(value => !string.IsNullOrWhiteSpace(value));

    private static bool ValidScore(float score, float maximum) =>
        float.IsFinite(score) && float.IsFinite(maximum) && maximum >= 0 && score >= 0 && score <= maximum;

    private static bool Close(float left, float right) => Math.Abs(left - right) <= 0.03f;

    private static string Normalize(string? value) =>
        Regex.Replace((value ?? "").Normalize(NormalizationForm.FormKC), @"\s+", " ").Trim();
}
