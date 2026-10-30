using System.Text.Json;
using Aives.Application.AI;
using Aives.Application.AI.DTOs;
using Xunit;

namespace Aives.Tests;

public class EvaluationContractTests
{
    [Fact]
    public void Validate_RecalculatesQuestionAndSessionTotalsFromRubricScores()
    {
        var (request, response) = CreateValidEvaluation();
        var warnings = new List<string>();

        EvaluationContract.Validate(request, response, warnings.Add);

        var question = Assert.Single(response.QuestionScores);
        Assert.Equal(7.0f, question.Score);
        Assert.Equal(10.0f, question.MaxScore);
        Assert.Equal(7.0f, response.TotalScore);
        Assert.Equal(10.0f, response.MaxTotalScore);
        Assert.Equal(70.0f, response.Percentage);
        Assert.Equal(3, warnings.Count);
    }

    [Fact]
    public void Validate_RejectsAwardAboveCriterionMaximum()
    {
        var (request, response) = CreateValidEvaluation();
        response.QuestionScores[0].CriterionAnalyses[0].AwardedScore = 4.1f;

        Assert.Throws<InvalidDataException>(() => EvaluationContract.Validate(request, response));
    }

    [Fact]
    public void Validate_RejectsMissingCriterionAnalysis()
    {
        var (request, response) = CreateValidEvaluation();
        response.QuestionScores[0].CriterionAnalyses.RemoveAt(1);

        Assert.Throws<InvalidDataException>(() => EvaluationContract.Validate(request, response));
    }

    [Fact]
    public void Validate_AllowsEmptySentenceFeedbackWhenRubricAnalysisIsComplete()
    {
        var (request, response) = CreateValidEvaluation();
        response.QuestionScores[0].SentenceFeedback.Clear();

        EvaluationContract.Validate(request, response);

        Assert.Equal(7.0f, response.QuestionScores[0].Score);
    }

    [Fact]
    public void Validate_RejectsEvidenceOutsideTranscript()
    {
        var (request, response) = CreateValidEvaluation();
        response.QuestionScores[0].CriterionAnalyses[0].EvidenceQuotes = new() { "not in transcript" };

        Assert.Throws<InvalidDataException>(() => EvaluationContract.Validate(request, response));
    }

    [Fact]
    public void Validate_RejectsJudgmentThatDoesNotMatchAward()
    {
        var (request, response) = CreateValidEvaluation();
        response.QuestionScores[0].CriterionAnalyses[0].Judgment = "partial";

        Assert.Throws<InvalidDataException>(() => EvaluationContract.Validate(request, response));
    }

    [Fact]
    public void Validate_RejectsRubricMaximumThatDiffersFromRequest()
    {
        var (request, response) = CreateValidEvaluation();
        request.QuestionAnswers[0].MaxScore = 9.0f;

        Assert.Throws<InvalidDataException>(() => EvaluationContract.Validate(request, response));
    }

    [Fact]
    public void StoredAudit_UsesV2ShapeWithoutQuestionContextOrSampleAnswer()
    {
        var (_, response) = CreateValidEvaluation();
        var evaluation = response.QuestionScores[0];
        var audit = StoredAnswerAnalysisDto.Create(
            "gemini",
            "gemini-3.1-flash-lite",
            evaluation.StudentAnswer,
            new[]
            {
                new AnswerRubricCriterionDto { CriterionId = "c1", Description = "First criterion", MaxScore = 4 },
                new AnswerRubricCriterionDto { CriterionId = "c2", Description = "Second criterion", MaxScore = 6 }
            },
            evaluation);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(audit, StoredAnswerAnalysisDto.SerializerOptions));
        var root = document.RootElement;
        Assert.Equal("2.0", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("gemini", root.GetProperty("provider").GetString());
        Assert.Equal("gemini-3.1-flash-lite", root.GetProperty("model").GetString());
        Assert.True(root.GetProperty("gradedAt").GetDateTime() <= DateTime.UtcNow);
        Assert.Equal(2, root.GetProperty("rubric").GetArrayLength());
        Assert.Equal(2, root.GetProperty("evaluation").GetProperty("criterionAnalyses").GetArrayLength());
        Assert.Equal(
            4.0f,
            root.GetProperty("evaluation").GetProperty("criterionAnalyses")[0].GetProperty("awardedScore").GetSingle());
        Assert.False(root.TryGetProperty("context", out _));
        Assert.False(root.GetProperty("evaluation").TryGetProperty("sampleAnswer", out _));
    }

    private static (EvaluateSessionRequest Request, EvaluateSessionResponse Response) CreateValidEvaluation()
    {
        const string transcript = "Agile uses short iterations. Waterfall is sequential.";
        var rubric = new List<AnswerRubricCriterionDto>
        {
            new() { CriterionId = "c1", Description = "Explain Agile iterations", MaxScore = 4.0f },
            new() { CriterionId = "c2", Description = "Compare with Waterfall", MaxScore = 6.0f }
        };
        var request = new EvaluateSessionRequest
        {
            ExamSessionId = "session-1",
            MaxScorePerQuestion = 10.0f,
            QuestionAnswers = new()
            {
                new()
                {
                    QuestionId = "question-1",
                    QuestionText = "Compare Agile and Waterfall.",
                    StudentAnswer = transcript,
                    MaxScore = 10.0f,
                    Rubric = rubric
                }
            }
        };
        var response = new EvaluateSessionResponse
        {
            Success = true,
            ExamSessionId = "session-1",
            Status = "PendingReview",
            Provider = "mock",
            Model = "mock",
            SchemaVersion = "2.0",
            OverallFeedback = "Good response.",
            StudySuggestions = new(),
            TotalScore = 999.0f,
            MaxTotalScore = 999.0f,
            Percentage = 99.0f,
            QuestionScores = new()
            {
                new()
                {
                    QuestionId = "question-1",
                    QuestionText = "Compare Agile and Waterfall.",
                    StudentAnswer = transcript,
                    Score = 999.0f,
                    MaxScore = 999.0f,
                    Feedback = "Good response.",
                    Strengths = new() { "Agile iterations" },
                    Weaknesses = new() { "Comparison could be expanded" },
                    MissingContent = new() { "More comparison detail" },
                    Improvements = new() { "Expand the comparison" },
                    SentenceFeedback = new()
                    {
                        new() { Sentence = "Agile uses short iterations.", Judgment = "correct", Comment = "Relevant." },
                        new() { Sentence = "Waterfall is sequential.", Judgment = "omission", Comment = "Relevant but incomplete." }
                    },
                    CriterionAnalyses = new()
                    {
                        new()
                        {
                            CriterionId = "c1",
                            Judgment = "met",
                            AwardedScore = 4.0f,
                            Explanation = "Fully addressed.",
                            Errors = new(),
                            Omissions = new(),
                            EvidenceQuotes = new() { "Agile uses short iterations." }
                        },
                        new()
                        {
                            CriterionId = "c2",
                            Judgment = "partial",
                            AwardedScore = 3.0f,
                            Explanation = "Partly addressed.",
                            Errors = new(),
                            Omissions = new() { "Expand comparison." },
                            EvidenceQuotes = new() { "Waterfall is sequential." }
                        }
                    }
                }
            }
        };
        return (request, response);
    }
}
