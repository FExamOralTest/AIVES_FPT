using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aives.Tests;

public class ResultRepositoryFollowUpTests
{
    [Fact]
    public async Task AddOrUpdateAnswerAsync_FollowUpAppendsToOriginalAndClearsStaleEvaluation()
    {
        var options = new DbContextOptionsBuilder<AivesDbContext>()
            .UseInMemoryDatabase($"FollowUp_{Guid.NewGuid()}")
            .Options;
        await using var db = new AivesDbContext(options);
        db.Answers.Add(new Answer
        {
            AnswerId = 1,
            ResultId = 2,
            QuestionId = 3,
            StudentAnswer = "Initial answer",
            Transcript = "Initial transcript",
            Status = AnswerStatus.EVALUATED,
            AiScore = 2,
            AiFeedback = "Old evaluation",
            AiAnalysis = """{"schemaVersion":"2.0"}"""
        });
        await db.SaveChangesAsync();

        var repository = new ResultRepository(db);
        await repository.AddOrUpdateAnswerAsync(new Answer
        {
            ResultId = 2,
            QuestionId = 3,
            StudentAnswer = "Follow-up answer",
            Transcript = "Follow-up transcript",
            Status = AnswerStatus.PROCESSING,
            IsFollowUp = true
        });

        var storedAnswer = await db.Answers.SingleAsync();
        Assert.Equal($"Initial answer{Environment.NewLine}{Environment.NewLine}Follow-up answer", storedAnswer.StudentAnswer);
        Assert.Equal($"Initial transcript{Environment.NewLine}{Environment.NewLine}Follow-up transcript", storedAnswer.Transcript);
        Assert.True(storedAnswer.IsFollowUp);
        Assert.Equal(AnswerStatus.PROCESSING, storedAnswer.Status);
        Assert.Null(storedAnswer.AiScore);
        Assert.Null(storedAnswer.AiFeedback);
        Assert.Null(storedAnswer.AiAnalysis);
    }
}
