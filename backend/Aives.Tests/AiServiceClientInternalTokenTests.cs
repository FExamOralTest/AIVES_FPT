using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aives.Application.AI.DTOs;
using Aives.Infrastructure.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// Test xác nhận <see cref="AiServiceClient"/> luôn gắn <c>X-Internal-Token</c> header
/// cho mọi request gọi AI service. Stub HttpMessageHandler bắt mọi request và kiểm tra header.
/// </summary>
public class AiServiceClientInternalTokenTests
{
    [Fact]
    public async Task TranscribeAudio_SendsXInternalTokenHeader_WhenApiKeyConfigured()
    {
        var expectedToken = "test-shared-secret-abc123";
        var captured = new CapturedRequest();

        var handler = new StubHandler(req =>
        {
            captured.Method = req.Method;
            captured.Path = req.RequestUri?.AbsolutePath;
            captured.Headers[AiServiceOptions.TokenHeaderName] = req.Headers.TryGetValues(AiServiceOptions.TokenHeaderName, out var vals)
                ? string.Join(",", vals)
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new SpeechToTextResponse
                {
                    Success = true,
                    Transcript = "xin chào"
                }))
            });
        });

        var client = BuildClient(expectedToken, handler);
        using var audioStream = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await client.TranscribeAudioAsync(audioStream, "test.webm", "session-1", "vi-VN");

        Assert.True(result.Success);
        Assert.Equal("xin chào", result.Transcript);
        Assert.NotNull(captured.Headers[AiServiceOptions.TokenHeaderName]);
        Assert.Equal(expectedToken, captured.Headers[AiServiceOptions.TokenHeaderName]!);
    }

    [Fact]
    public async Task EvaluateSession_SendsXInternalTokenHeader()
    {
        var expectedToken = "another-secret-xyz";
        var captured = new CapturedRequest();

        var handler = new StubHandler(req =>
        {
            captured.Headers[AiServiceOptions.TokenHeaderName] = req.Headers.TryGetValues(AiServiceOptions.TokenHeaderName, out var vals)
                ? string.Join(",", vals)
                : null;
            // Return a response that satisfies EvaluationContract.Validate (1 question = 1 score).
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new EvaluateSessionResponse
                {
                    Success = true,
                    ExamSessionId = "1",
                    TotalScore = 8.0f,
                    MaxTotalScore = 10.0f,
                    Percentage = 80.0f,
                    OverallFeedback = "Bài làm tốt",
                    Status = "PendingReview",
                    Provider = "mock",
                    Model = "mock",
                    SchemaVersion = "2.0",
                    StudySuggestions = new List<string> { "Ôn lại chương 2" },
                    QuestionScores = new List<QuestionScoreDetailDto>
                    {
                        new()
                        {
                            QuestionId = "10",
                            QuestionText = "Câu hỏi test",
                            StudentAnswer = "Trả lời test",
                            Score = 8.0f,
                            MaxScore = 10.0f,
                            Feedback = "Tốt",
                            Strengths = new List<string> { "Đúng ý 1" },
                            Weaknesses = new List<string>(),
                            MissingContent = new List<string>(),
                            Improvements = new List<string>(),
                            SentenceFeedback = new List<SentenceFeedbackDto>
                            {
                                new()
                                {
                                    Sentence = "Trả lời test",
                                    Judgment = "correct",
                                    Comment = "Relevant."
                                }
                            },
                            CriterionAnalyses = new List<CriterionAnalysisDto>
                            {
                                new()
                                {
                                    CriterionId = "1",
                                    Judgment = "met",
                                    AwardedScore = 10.0f,
                                    Explanation = "Đạt",
                                    Errors = new List<string>(),
                                    Omissions = new List<string>(),
                                    EvidenceQuotes = new List<string> { "Trả lời test" }
                                }
                            }
                        }
                    }
                }))
            });
        });

        var client = BuildClient(expectedToken, handler);
        var request = new EvaluateSessionRequest
        {
            ExamSessionId = "1",
            StudentId = "1",
            CourseName = "Test",
            MaxScorePerQuestion = 10.0f,
            QuestionAnswers = new List<QuestionAnswerPairDto>
            {
                new()
                {
                    QuestionId = "10",
                    QuestionText = "Câu hỏi test",
                    StudentAnswer = "Trả lời test",
                    IsFollowUp = false,
                    MaxScore = 10.0f,
                    Rubric = new List<AnswerRubricCriterionDto>
                    {
                        new()
                        {
                            CriterionId = "1",
                            Description = "Trả lời test",
                            MaxScore = 10.0f
                        }
                    }
                }
            }
        };

        var result = await client.EvaluateSessionAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(expectedToken, captured.Headers[AiServiceOptions.TokenHeaderName]!);
    }

    [Fact]
    public async Task AiService_Unauthorized_ReturnsGracefulFailure_DoesNotThrow()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("invalid token")
        }));

        var client = BuildClient("wrong-key", handler);

        var result = await client.EvaluateSessionAsync(new EvaluateSessionRequest
        {
            ExamSessionId = "1",
            StudentId = "1"
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("internal_auth_failed", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateSession_InvalidJson_ReturnsDiagnosticValidationMessage()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{")
        }));
        var client = BuildClient("test-key", handler);

        var result = await client.EvaluateSessionAsync(new EvaluateSessionRequest
        {
            ExamSessionId = "session-1",
            QuestionAnswers = new List<QuestionAnswerPairDto>
            {
                new() { QuestionId = "question-1" }
            }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("invalid_response", result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
        Assert.DoesNotContain("AI evaluation returned an invalid response.", result.Message);
    }

    [Fact]
    public async Task EvaluateSession_HttpError_PreservesSafeApiDetail()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"detail\":\"AI provider request timed out.\"}")
        }));
        var client = BuildClient("test-key", handler);

        var result = await client.EvaluateSessionAsync(new EvaluateSessionRequest
        {
            ExamSessionId = "session-1",
            QuestionAnswers = new List<QuestionAnswerPairDto>
            {
                new() { QuestionId = "question-1" }
            }
        }, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("http_error", result.ErrorCode);
        Assert.Contains("HTTP 500", result.Message);
        Assert.Contains("AI provider request timed out.", result.Message);
    }

    [Fact]
    public void Constructor_NoApiKey_LogsWarning_DoesNotThrow()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var options = Options.Create(new AiServiceOptions { BaseUrl = "http://test", ApiKey = null });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://test") };

        var ex = Record.Exception(() => new AiServiceClient(http, options, NullLogger<AiServiceClient>.Instance));
        Assert.Null(ex);
    }

    private static AiServiceClient BuildClient(string apiKey, HttpMessageHandler handler)
    {
        var options = Options.Create(new AiServiceOptions
        {
            BaseUrl = "http://ai-test",
            TimeoutSeconds = 30,
            ApiKey = apiKey
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://ai-test") };
        return new AiServiceClient(http, options, NullLogger<AiServiceClient>.Instance);
    }

    private class CapturedRequest
    {
        public HttpMethod Method { get; set; } = HttpMethod.Get;
        public string? Path { get; set; }
        public Dictionary<string, string?> Headers { get; } = new();
        public string Body { get; set; } = string.Empty;
    }

    private class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
}
