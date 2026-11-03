using System;
using Aives.Application.AI.DTOs;
using Aives.Application.Queue.Contracts.Stt;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Task 1.5 — STT Response Contract tests.
/// Chốt format Faster-Whisper trả về: transcript, language, duration và error format.
/// Verify validate rules, error taxonomy và quyết định retry / dead-letter.
/// </summary>
public class SttContractTests
{
    // ===================================================================
    //  Success response validation
    // ===================================================================

    [Fact]
    public void TryValidate_ValidSuccessResponse_ReturnsTrue()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "Xin chào thầy, em trả lời câu này",
            LanguageDetected = "vi",
            Duration = 12.5
        };

        Assert.True(SttContract.TryValidate(response, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_NullResponse_ReturnsFalse()
    {
        Assert.False(SttContract.TryValidate(null, out var error));
        Assert.NotNull(error);
        Assert.Equal(SttErrorCode.InternalError, error!.Code);
    }

    [Fact]
    public void TryValidate_EmptyTranscript_IsContractValid()
    {
        // Audio im lặng → transcript rỗng là hợp lệ về mặt contract.
        // Việc "không có tiếng nói" là lỗi nghiệp vụ, xử lý ở AnswerOrchestrator.
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = string.Empty,
            LanguageDetected = "vi",
            Duration = 3.0
        };

        Assert.True(SttContract.TryValidate(response, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_MissingLanguage_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = string.Empty,
            Duration = 1.0
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.Contains("language", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_TooLongLanguage_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = new string('v', SttContract.MaxLanguageLength + 1),
            Duration = 1.0
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_UnsupportedLanguage_ReturnsFalse()
    {
        // Mã ngôn ngữ phải là ISO-639-1 (2 chữ cái) hoặc BCP-47 dạng "vi-VN".
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = "vietnamese",
            Duration = 1.0
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_NegativeDuration_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = "vi",
            Duration = -1.0
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.Contains("duration", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_NonFiniteDuration_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = "vi",
            Duration = double.NaN
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_ConfidenceAboveOne_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = "vi",
            Duration = 1.0,
            Confidence = 1.5f
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.Contains("confidence", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_NegativeConfidence_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = "vi",
            Duration = 1.0,
            Confidence = -0.1f
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_ConfidenceWithinRange_ReturnsTrue()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = "vi",
            Duration = 1.0,
            Confidence = 0.92f
        };

        Assert.True(SttContract.TryValidate(response, out var error));
        Assert.Null(error);
    }

    // ===================================================================
    //  Error response validation
    // ===================================================================

    [Fact]
    public void TryValidate_FailureWithoutError_ReturnsFalse()
    {
        var response = new SpeechToTextResponse { Success = false, Message = "failed" };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.Contains("error", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_FailureWithoutCode_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload { Code = string.Empty, Message = "bad" }
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_UnknownErrorCode_IsAccepted_ForForwardCompatibility()
    {
        // AI1 có thể deploy code mới mà Backend chưa biết. Contract KHÔNG reject —
        // code lạ mặc định retryable (thử lại 1 lần rẻ hơn bỏ luôn bài sinh viên).
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload { Code = "TOTALLY_MADE_UP", Message = "?", Retryable = true }
        };

        Assert.True(SttContract.TryValidate(response, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_UnknownErrorCode_WithRetryableFalse_ReturnsFalse()
    {
        // Code lạ mà AI1 khai retryable=false → phải chờ Backend cập nhật taxonomy
        // trước khi chấp nhận, vì mình không biết lỗi đó là tạm thời hay vĩnh viễn.
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload { Code = "TOTALLY_MADE_UP", Message = "?", Retryable = false }
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_TooLongErrorCode_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload
            {
                Code = new string('c', SttContract.MaxErrorCodeLength + 1),
                Message = "?",
                Retryable = true
            }
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void IsKnownCode_DistinguishesKnownFromNewCodes()
    {
        Assert.True(SttContract.IsKnownCode(SttContract.CodeTimeout));
        Assert.True(SttContract.IsKnownCode("audio_corrupted"));
        Assert.False(SttContract.IsKnownCode("BRAND_NEW_CODE"));
        Assert.False(SttContract.IsKnownCode(null));
    }

    [Fact]
    public void TryValidate_EmptyErrorMessage_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload { Code = SttContract.CodeAudioCorrupted, Message = string.Empty }
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_TooLongErrorMessage_ReturnsFalse()
    {
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload
            {
                Code = SttContract.CodeAudioCorrupted,
                Message = new string('m', SttContract.MaxErrorMessageLength + 1)
            }
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryValidate_RetryableFlagMismatch_ReturnsFalse()
    {
        // AUDIO_CORRUPTED là lỗi vĩnh viễn → retryable phải là false.
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload
            {
                Code = SttContract.CodeAudioCorrupted,
                Message = "bad file",
                Retryable = true
            }
        };

        Assert.False(SttContract.TryValidate(response, out var error));
        Assert.Contains("retryable", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_RetryableFlagMatches_ReturnsTrue()
    {
        var response = new SpeechToTextResponse
        {
            Success = false,
            Error = new SttErrorPayload
            {
                Code = SttContract.CodeTimeout,
                Message = "timed out",
                Retryable = true
            }
        };

        Assert.True(SttContract.TryValidate(response, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(SttContract.CodeAudioCorrupted)]
    [InlineData(SttContract.CodeEmptyAudio)]
    [InlineData(SttContract.CodeEmptyTranscript)]
    [InlineData(SttContract.CodeNoSpeech)]
    [InlineData(SttContract.CodeUnsupportedFormat)]
    [InlineData(SttContract.CodeTimeout)]
    [InlineData(SttContract.CodeInternalError)]
    [InlineData(SttContract.CodeInvalidResponse)]
    [InlineData(SttContract.CodeAuthFailed)]
    public void AllCodes_ArePartOfTheContract(string code)
    {
        Assert.Contains(code, SttContract.AllCodes);
    }

    [Fact]
    public void Validate_InvalidResponse_ThrowsInvalidDataException()
    {
        var response = new SpeechToTextResponse { Success = false };

        Assert.Throws<InvalidDataException>(() => SttContract.Validate(response));
    }

    [Fact]
    public void Validate_ValidResponse_DoesNotThrow()
    {
        var response = new SpeechToTextResponse
        {
            Success = true,
            Transcript = "ok",
            LanguageDetected = "vi",
            Duration = 1.0
        };

        var ex = Record.Exception(() => SttContract.Validate(response));
        Assert.Null(ex);
    }

    // ===================================================================
    //  Error taxonomy mapping
    // ===================================================================

    [Theory]
    [InlineData(SttContract.CodeAudioCorrupted, SttErrorCode.AudioCorrupted)]
    [InlineData(SttContract.CodeEmptyAudio, SttErrorCode.AudioCorrupted)]
    [InlineData(SttContract.CodeEmptyTranscript, SttErrorCode.EmptyTranscript)]
    [InlineData(SttContract.CodeNoSpeech, SttErrorCode.EmptyTranscript)]
    [InlineData(SttContract.CodeUnsupportedFormat, SttErrorCode.UnsupportedFormat)]
    [InlineData(SttContract.CodeTimeout, SttErrorCode.Timeout)]
    [InlineData(SttContract.CodeInternalError, SttErrorCode.InternalError)]
    [InlineData(SttContract.CodeInvalidResponse, SttErrorCode.Unknown)]
    [InlineData(SttContract.CodeAuthFailed, SttErrorCode.Unknown)]
    [InlineData("random_code", SttErrorCode.Unknown)]
    [InlineData(null, SttErrorCode.Unknown)]
    public void MapToErrorCode_MapsToInternalTaxonomy(string? code, SttErrorCode expected)
    {
        Assert.Equal(expected, SttContract.MapToErrorCode(code));
    }

    [Fact]
    public void MapToErrorCode_IsCaseInsensitive()
    {
        Assert.Equal(SttErrorCode.AudioCorrupted, SttContract.MapToErrorCode("audio_corrupted"));
        Assert.Equal(SttErrorCode.Timeout, SttContract.MapToErrorCode("Timeout"));
    }

    // ===================================================================
    //  Retry decision — the whole point of the contract
    // ===================================================================

    [Theory]
    [InlineData(SttContract.CodeTimeout)]
    [InlineData(SttContract.CodeInternalError)]
    public void IsRetryableCode_TransientErrors_AreRetryable(string code)
    {
        Assert.True(SttContract.IsRetryableCode(code));
    }

    [Theory]
    [InlineData(SttContract.CodeAudioCorrupted)]
    [InlineData(SttContract.CodeEmptyAudio)]
    [InlineData(SttContract.CodeEmptyTranscript)]
    [InlineData(SttContract.CodeNoSpeech)]
    [InlineData(SttContract.CodeUnsupportedFormat)]
    [InlineData(SttContract.CodeInvalidResponse)]
    [InlineData(SttContract.CodeAuthFailed)]
    public void IsRetryableCode_PermanentErrors_AreNotRetryable(string code)
    {
        Assert.False(SttContract.IsRetryableCode(code));
    }

    [Fact]
    public void IsRetryableCode_UnknownCode_IsRetryable()
    {
        // Code lạ → thử lại 1 lần rẻ hơn bỏ luôn bài (AI1 có thể vừa deploy code mới).
        Assert.True(SttContract.IsRetryableCode("BRAND_NEW_CODE"));
    }

    // ===================================================================
    //  HTTP status mapping (fallback khi body không theo contract)
    // ===================================================================

    [Theory]
    [InlineData(400, SttContract.CodeAudioCorrupted)]
    [InlineData(404, SttContract.CodeAudioCorrupted)]
    [InlineData(413, SttContract.CodeUnsupportedFormat)]
    [InlineData(415, SttContract.CodeUnsupportedFormat)]
    [InlineData(422, SttContract.CodeEmptyTranscript)]
    [InlineData(408, SttContract.CodeTimeout)]
    [InlineData(504, SttContract.CodeTimeout)]
    [InlineData(401, SttContract.CodeAuthFailed)]
    [InlineData(403, SttContract.CodeAuthFailed)]
    [InlineData(500, SttContract.CodeInternalError)]
    [InlineData(503, SttContract.CodeInternalError)]
    public void MapHttpStatusToCode_MapsToCanonicalCodes(int status, string expected)
    {
        Assert.Equal(expected, SttContract.MapHttpStatusToCode(status));
    }

    // ===================================================================
    //  Language helpers
    // ===================================================================

    [Theory]
    [InlineData("vi", true)]
    [InlineData("en", true)]
    [InlineData("vi-VN", true)]
    [InlineData("vi_VN", true)]
    [InlineData("VI", true)]
    [InlineData("vietnamese", false)]
    [InlineData("v", false)]
    [InlineData("123", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupportedLanguage_ValidatesIso639(string? language, bool expected)
    {
        Assert.Equal(expected, SttContract.IsSupportedLanguage(language));
    }

    [Theory]
    [InlineData("vi", "vi")]
    [InlineData("VI", "vi")]
    [InlineData("vi-VN", "vi")]
    [InlineData("en-US", "en")]
    [InlineData("vietnamese", SttContract.DefaultLanguage)]
    [InlineData(null, SttContract.DefaultLanguage)]
    public void NormalizeLanguage_CollapsesToIso639(string? language, string expected)
    {
        Assert.Equal(expected, SttContract.NormalizeLanguage(language));
    }

    // ===================================================================
    //  Contract stability — enum values are persisted in logs/metrics
    // ===================================================================

    [Fact]
    public void SttErrorCode_ValuesRemainStable()
    {
        Assert.Equal(0, (int)SttErrorCode.Unknown);
        Assert.Equal(1, (int)SttErrorCode.AudioCorrupted);
        Assert.Equal(2, (int)SttErrorCode.EmptyTranscript);
        Assert.Equal(3, (int)SttErrorCode.UnsupportedFormat);
        Assert.Equal(4, (int)SttErrorCode.Timeout);
        Assert.Equal(5, (int)SttErrorCode.InternalError);
    }
}
