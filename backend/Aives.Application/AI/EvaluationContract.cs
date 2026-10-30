using System.Text;
using System.Text.RegularExpressions;
using Aives.Application.AI.DTOs;

namespace Aives.Application.AI;

public static class EvaluationContract
{
    private const float ScoreTolerance = 1e-4f;

    public static void Validate(
        EvaluateSessionRequest request,
        EvaluateSessionResponse response,
        Action<string>? logWarning = null)
    {
        if (!response.Success || response.ExamSessionId != request.ExamSessionId
            || response.Status != "PendingReview" || string.IsNullOrWhiteSpace(response.OverallFeedback)
            || string.IsNullOrWhiteSpace(response.Provider) || string.IsNullOrWhiteSpace(response.Model)
            || response.SchemaVersion != "2.0" || !ValidTexts(response.StudySuggestions)
            || response.QuestionScores == null || request.QuestionAnswers == null
            || request.QuestionAnswers.Count == 0
            || response.QuestionScores.Count != request.QuestionAnswers.Count)
            throw new InvalidDataException("Invalid session evaluation.");

        var questions = new Dictionary<string, QuestionAnswerPairDto>(StringComparer.Ordinal);
        foreach (var question in request.QuestionAnswers)
        {
            if (question == null || string.IsNullOrWhiteSpace(question.QuestionId)
                || !questions.TryAdd(question.QuestionId, question))
                throw new InvalidDataException("Submitted question IDs must be unique and non-empty.");
        }
        var received = new HashSet<string>(StringComparer.Ordinal);
        foreach (var score in response.QuestionScores)
        {
            if (score == null || string.IsNullOrWhiteSpace(score.QuestionId)
                || !received.Add(score.QuestionId) || !questions.TryGetValue(score.QuestionId, out var question))
                throw new InvalidDataException("Evaluation question IDs do not match the submission.");

            var requestedMax = question.MaxScore ?? request.MaxScorePerQuestion;
            var rubricMax = ValidateRubric(question, requestedMax);
            if (Normalize(score.QuestionText) != Normalize(question.QuestionText)
                || Normalize(score.StudentAnswer) != Normalize(question.StudentAnswer)
                || string.IsNullOrWhiteSpace(score.Feedback)
                || !ValidTexts(score.Strengths) || !ValidTexts(score.Weaknesses)
                || !ValidTexts(score.MissingContent) || !ValidTexts(score.Improvements)
                || score.SentenceFeedback == null || score.CriterionAnalyses == null)
                throw new InvalidDataException("Invalid answer evaluation.");

            ValidateAnalysis(question, score);

            var calculatedScore = MathF.Round(score.CriterionAnalyses.Sum(c => c.AwardedScore), 4);
            if (!Close(score.Score, calculatedScore))
                logWarning?.Invoke($"AI question score {score.Score} differs from rubric score {calculatedScore} for question {score.QuestionId}.");
            if (!Close(score.MaxScore, rubricMax))
                logWarning?.Invoke($"AI question maximum {score.MaxScore} differs from rubric maximum {rubricMax} for question {score.QuestionId}.");
            score.Score = calculatedScore;
            score.MaxScore = rubricMax;
        }

        var total = MathF.Round(response.QuestionScores.Sum(q => q.Score), 4);
        var maximum = MathF.Round(response.QuestionScores.Sum(q => q.MaxScore), 4);
        var percentage = maximum > 0 ? total / maximum * 100 : 0;
        if (!Close(response.TotalScore, total)
            || !Close(response.MaxTotalScore, maximum)
            || !Close(response.Percentage, percentage))
            logWarning?.Invoke("AI session totals differ from backend totals; backend-calculated values are being used.");

        response.TotalScore = total;
        response.MaxTotalScore = maximum;
        response.Percentage = percentage;
    }

    private static float ValidateRubric(QuestionAnswerPairDto question, float requestedMax)
    {
        if (!float.IsFinite(requestedMax) || requestedMax < 0
            || question.Rubric == null || question.Rubric.Count == 0)
            throw new InvalidDataException("A valid rubric is required for every evaluated question.");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var criterion in question.Rubric)
        {
            if (criterion == null || string.IsNullOrWhiteSpace(criterion.CriterionId)
                || !ids.Add(criterion.CriterionId) || string.IsNullOrWhiteSpace(criterion.Description)
                || !float.IsFinite(criterion.MaxScore) || criterion.MaxScore <= 0)
                throw new InvalidDataException("The submitted rubric is invalid.");
        }

        var rubricMax = question.Rubric.Sum(c => c.MaxScore);
        if (!float.IsFinite(rubricMax) || !Close(rubricMax, requestedMax))
            throw new InvalidDataException("Rubric maximum does not match the requested question maximum.");
        return rubricMax;
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

            var maximum = question.Rubric.Single(c => c.CriterionId == criterion.CriterionId).MaxScore;
            ValidateAwardedScore(criterion, maximum);

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
            || score.CriterionAnalyses.Any(c => c.Judgment != "met") && score.Improvements.Count == 0)
            throw new InvalidDataException("Evaluation is missing rubric feedback.");
    }

    private static void ValidateAwardedScore(CriterionAnalysisDto criterion, float maximum)
    {
        var score = criterion.AwardedScore;
        if (!float.IsFinite(score) || score < -ScoreTolerance || score > maximum + ScoreTolerance)
            throw new InvalidDataException("An awarded score must stay within the criterion maximum.");

        var validJudgment = criterion.Judgment switch
        {
            "met" => Math.Abs(score - maximum) <= ScoreTolerance,
            "not_met" => Math.Abs(score) <= ScoreTolerance,
            "partial" => score > ScoreTolerance && score < maximum - ScoreTolerance,
            _ => false
        };
        if (!validJudgment)
            throw new InvalidDataException("Criterion judgment does not match its awarded score.");
    }

    private static bool ValidTexts(IEnumerable<string>? values) =>
        values != null && values.All(value => !string.IsNullOrWhiteSpace(value));

    private static bool Close(float left, float right) =>
        float.IsFinite(left) && float.IsFinite(right) && Math.Abs(left - right) <= ScoreTolerance;

    private static string Normalize(string? value) =>
        Regex.Replace((value ?? "").Normalize(NormalizationForm.FormKC), @"\s+", " ").Trim();
}
