using Aives.Application.Exams;
using Aives.Domain.Entities;
using Xunit;

namespace Aives.Tests;

public class ExamPercentageTests
{
    [Fact]
    public void Scale_ConvertsRubricAwardIntoTheExamPercentage()
    {
        Assert.Equal(32f, ExamPercentage.Scale(awarded: 8, rubricMax: 10, questionPercent: 40));
        Assert.Equal(0f, ExamPercentage.Scale(awarded: 0, rubricMax: 10, questionPercent: 40));
        Assert.Equal(40f, ExamPercentage.Scale(awarded: 10, rubricMax: 10, questionPercent: 40));
    }

    [Fact]
    public void SplitEvenly_SumsToOneHundred()
    {
        var weights = ExamPercentage.SplitEvenly(3);
        Assert.Equal(100f, weights.Sum());
        Assert.All(weights, weight => Assert.True(weight > 0));
    }

    [Fact]
    public void EnsureTotal_RejectsAPaperThatDoesNotSumToOneHundred()
    {
        var error = Assert.Throws<ArgumentException>(() => ExamPercentage.EnsureTotal([30, 30], "Exam"));
        Assert.Contains("100%", error.Message);
    }

    [Fact]
    public void WeightFor_UsesTheAssignedExamPercentage()
    {
        var result = new Result
        {
            Exam = new Exam
            {
                ExamQuestions =
                [
                    new ExamQuestion { QuestionId = 7, QuestionScore = 25 }
                ]
            }
        };

        Assert.Equal(25f, ExamPercentage.WeightFor(result, 7, fallback: 10));
        Assert.Equal(10f, ExamPercentage.WeightFor(result, 8, fallback: 10));
    }
}
