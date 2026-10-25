using System;
using Aives.Application.Queue.Contracts;
using Aives.Application.Queue.Contracts.Stt;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 1.4 — Queue Job Contract tests.
/// Chốt metadata chuẩn của 1 background job: jobId, answerId, resultId, objectKey,
/// attempt và trạng thái job. Verify validate rules + mapping trạng thái Hangfire.
/// </summary>
public class QueueJobContractTests
{
    // ===================================================================
    //  Metadata validation
    // ===================================================================

    [Fact]
    public void TryValidate_ValidMetadata_ReturnsTrue()
    {
        var metadata = new QueueJobMetadata
        {
            JobId = "job-123",
            AnswerId = 42,
            ResultId = 7,
            ObjectKey = "exams/1/sessions/2/students/3/q_4.webm",
            Attempt = 1,
            Status = QueueJobStatus.Pending
        };

        Assert.True(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_NullMetadata_ReturnsFalse()
    {
        Assert.False(QueueJobContract.TryValidate(null, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_NonPositiveAnswerId_ReturnsFalse()
    {
        var metadata = new QueueJobMetadata { AnswerId = 0, Attempt = 1 };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("AnswerId", error);
    }

    [Fact]
    public void TryValidate_NegativeAnswerId_ReturnsFalse()
    {
        var metadata = new QueueJobMetadata { AnswerId = -5, Attempt = 1 };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_NullResultId_IsAllowed()
    {
        // Result có thể chưa được tạo ở attempt đầu → ResultId null là hợp lệ.
        var metadata = new QueueJobMetadata { AnswerId = 1, ResultId = null, Attempt = 1 };

        Assert.True(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_NonPositiveResultId_ReturnsFalse()
    {
        // ResultId = 0 là rác (FK không hợp lệ) → phải reject, không phải coi như null.
        var metadata = new QueueJobMetadata { AnswerId = 1, ResultId = 0, Attempt = 1 };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("ResultId", error);
    }

    [Fact]
    public void TryValidate_ZeroAttempt_ReturnsFalse()
    {
        // Attempt là 1-based → 0 là vi phạm contract.
        var metadata = new QueueJobMetadata { AnswerId = 1, Attempt = 0 };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("Attempt", error);
    }

    [Fact]
    public void TryValidate_NegativeAttempt_ReturnsFalse()
    {
        var metadata = new QueueJobMetadata { AnswerId = 1, Attempt = -1 };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_TooLongObjectKey_ReturnsFalse()
    {
        var metadata = new QueueJobMetadata
        {
            AnswerId = 1,
            Attempt = 1,
            ObjectKey = new string('k', QueueJobContract.MaxObjectKeyLength + 1)
        };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("ObjectKey", error);
    }

    [Fact]
    public void TryValidate_TooLongJobId_ReturnsFalse()
    {
        var metadata = new QueueJobMetadata
        {
            AnswerId = 1,
            Attempt = 1,
            JobId = new string('j', QueueJobContract.MaxJobIdLength + 1)
        };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("JobId", error);
    }

    [Fact]
    public void TryValidate_TooLongLastError_ReturnsFalse()
    {
        var metadata = new QueueJobMetadata
        {
            AnswerId = 1,
            Attempt = 1,
            LastError = new string('e', QueueJobContract.MaxLastErrorLength + 1)
        };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("LastError", error);
    }

    [Fact]
    public void TryValidate_UndefinedStatus_ReturnsFalse()
    {
        var metadata = new QueueJobMetadata { AnswerId = 1, Attempt = 1, Status = (QueueJobStatus)99 };

        Assert.False(QueueJobContract.TryValidate(metadata, out var error));
        Assert.Contains("Status", error);
    }

    [Fact]
    public void Validate_InvalidMetadata_ThrowsArgumentException()
    {
        var metadata = new QueueJobMetadata { AnswerId = 0, Attempt = 1 };

        Assert.Throws<ArgumentException>(() => QueueJobContract.Validate(metadata));
    }

    [Fact]
    public void Validate_ValidMetadata_DoesNotThrow()
    {
        var metadata = new QueueJobMetadata { AnswerId = 1, Attempt = 1 };

        var ex = Record.Exception(() => QueueJobContract.Validate(metadata));
        Assert.Null(ex);
    }

    // ===================================================================
    //  Factory
    // ===================================================================

    [Fact]
    public void Create_ProducesPendingMetadata_WithUtcTimestamp()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var metadata = QueueJobContract.Create(10, 20, "key.webm", 1);

        Assert.Equal(10, metadata.AnswerId);
        Assert.Equal(20, metadata.ResultId);
        Assert.Equal("key.webm", metadata.ObjectKey);
        Assert.Equal(1, metadata.Attempt);
        Assert.Equal(QueueJobStatus.Pending, metadata.Status);
        Assert.True(metadata.EnqueuedAt >= before);
        Assert.Equal(DateTimeKind.Utc, metadata.EnqueuedAt.Kind);
    }

    [Fact]
    public void Create_NullObjectKey_BecomesEmptyString()
    {
        var metadata = QueueJobContract.Create(10, 20, null, 1);

        Assert.Equal(string.Empty, metadata.ObjectKey);
    }

    [Fact]
    public void Create_NonPositiveAttempt_ClampsToOne()
    {
        // Attempt 0 là vi phạm contract, nhưng factory clamp về 1 để không ném exception
        // ở call site (upload audio luôn bắt đầu ở attempt 1).
        var metadata = QueueJobContract.Create(10, 20, "k", 0);

        Assert.Equal(1, metadata.Attempt);
    }

    [Fact]
    public void Create_InvalidAnswerId_Throws()
    {
        Assert.Throws<ArgumentException>(() => QueueJobContract.Create(0, 20, "k", 1));
    }

    // ===================================================================
    //  Hangfire state mapping
    // ===================================================================

    [Theory]
    [InlineData("Enqueued", QueueJobStatus.Pending)]
    [InlineData("Scheduled", QueueJobStatus.Pending)]
    [InlineData("Created", QueueJobStatus.Pending)]
    [InlineData("Processing", QueueJobStatus.Processing)]
    [InlineData("Succeeded", QueueJobStatus.Completed)]
    [InlineData("Failed", QueueJobStatus.Failed)]
    [InlineData("Deleted", QueueJobStatus.DeadLetter)]
    public void MapHangfireState_MapsKnownStates(string state, QueueJobStatus expected)
    {
        Assert.Equal(expected, QueueJobContract.MapHangfireState(state));
    }

    [Fact]
    public void MapHangfireState_IsCaseInsensitive()
    {
        Assert.Equal(QueueJobStatus.Processing, QueueJobContract.MapHangfireState("processing"));
        Assert.Equal(QueueJobStatus.Completed, QueueJobContract.MapHangfireState("SUCCEEDED"));
    }

    [Fact]
    public void MapHangfireState_UnknownState_FallsBackToPending()
    {
        Assert.Equal(QueueJobStatus.Pending, QueueJobContract.MapHangfireState("SomethingElse"));
        Assert.Equal(QueueJobStatus.Pending, QueueJobContract.MapHangfireState(null));
    }

    // ===================================================================
    //  Terminal state & retry decision
    // ===================================================================

    [Theory]
    [InlineData(QueueJobStatus.Completed)]
    [InlineData(QueueJobStatus.DeadLetter)]
    public void IsTerminal_CompletedAndDeadLetter_AreTerminal(QueueJobStatus status)
    {
        Assert.True(QueueJobContract.IsTerminal(status));
    }

    [Theory]
    [InlineData(QueueJobStatus.Pending)]
    [InlineData(QueueJobStatus.Processing)]
    [InlineData(QueueJobStatus.Failed)]
    public void IsTerminal_OtherStates_AreNotTerminal(QueueJobStatus status)
    {
        Assert.False(QueueJobContract.IsTerminal(status));
    }

    [Fact]
    public void CanRetry_FailedWithRemainingBudget_ReturnsTrue()
    {
        Assert.True(QueueJobContract.CanRetry(QueueJobStatus.Failed, attemptCount: 1));
        Assert.True(QueueJobContract.CanRetry(QueueJobStatus.Failed, attemptCount: 2));
    }

    [Fact]
    public void CanRetry_FailedWithExhaustedBudget_ReturnsFalse()
    {
        Assert.False(QueueJobContract.CanRetry(QueueJobStatus.Failed, QueueLimits.MaxTotalAttempts));
        Assert.False(QueueJobContract.CanRetry(QueueJobStatus.Failed, QueueLimits.MaxTotalAttempts + 5));
    }

    [Theory]
    [InlineData(QueueJobStatus.Pending)]
    [InlineData(QueueJobStatus.Processing)]
    [InlineData(QueueJobStatus.Completed)]
    [InlineData(QueueJobStatus.DeadLetter)]
    public void CanRetry_NonFailedStatus_ReturnsFalse(QueueJobStatus status)
    {
        Assert.False(QueueJobContract.CanRetry(status, attemptCount: 1));
    }

    // ===================================================================
    //  Attempt budget (BR-17) — contract 1.4 liên kết trực tiếp
    // ===================================================================

    [Fact]
    public void QueueLimits_MaxTotalAttempts_IsThree()
    {
        // BR-17: "retry tối đa 3 lần thử tổng cộng".
        Assert.Equal(3, QueueLimits.MaxTotalAttempts);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    public void QueueLimits_IsExhausted_MatchesBudget(int attemptCount, bool expected)
    {
        // attemptCount = 0 nghĩa là chưa thử lần nào → budget còn nguyên.
        Assert.Equal(expected, QueueLimits.IsExhausted(attemptCount));
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 120)]
    [InlineData(3, 300)]
    public void QueueLimits_GetRetryDelaySeconds_FollowsBackoffSequence(int attempt, int expected)
    {
        Assert.Equal(expected, QueueLimits.GetRetryDelaySeconds(attempt));
    }

    [Fact]
    public void QueueLimits_GetRetryDelaySeconds_OutOfRange_ClampsToConfiguredRange()
    {
        Assert.Equal(QueueLimits.RetryDelay1Seconds, QueueLimits.GetRetryDelaySeconds(0));
        Assert.Equal(QueueLimits.RetryDelay3Seconds, QueueLimits.GetRetryDelaySeconds(99));
    }
}
