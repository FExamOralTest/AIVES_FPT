using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Aives.Infrastructure.Queue;

/// <summary>
/// Service cấp URL audio có thời hạn, validate ownership trước khi trả URL.
/// </summary>
public class AudioPlaybackService : IAudioPlaybackService
{
    private static readonly TimeSpan DefaultUrlDuration = TimeSpan.FromHours(1);

    private readonly IAnswerRepository _answerRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<AudioPlaybackService> _logger;

    public AudioPlaybackService(
        IAnswerRepository answerRepository,
        IStorageService storageService,
        ILogger<AudioPlaybackService> logger)
    {
        _answerRepository = answerRepository;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<AudioPlaybackResponse> GetPlaybackUrlAsync(
        int answerId,
        int? currentUserId,
        UserRole? currentUserRole,
        TimeSpan? urlDuration = null,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId is null || currentUserRole is null)
            throw new UnauthorizedAccessException("Authentication required.");

        var answer = await _answerRepository.GetByIdWithDetailsAsync(answerId, cancellationToken)
            ?? throw new KeyNotFoundException($"Answer {answerId} not found.");

        var result = answer.Result;
        if (result == null)
            throw new KeyNotFoundException($"Result for Answer {answerId} not found.");

        // Phân quyền: cùng logic với Retry nhưng STAFF/EXAMINER được phép xem audio student nộp
        // (để review) — đây là workflow chính của teacher review.
        EnsureCanListen(result, currentUserId.Value, currentUserRole.Value);

        if (string.IsNullOrWhiteSpace(answer.AudioUrl))
            throw new KeyNotFoundException($"Answer {answerId} has no audio recording.");

        var duration = urlDuration ?? DefaultUrlDuration;
        var playbackUrl = _storageService.ResolvePlaybackUrl(answer.AudioUrl, duration);
        if (string.IsNullOrWhiteSpace(playbackUrl))
        {
            _logger.LogWarning("Storage service returned empty URL for Answer {AnswerId}", answerId);
            throw new InvalidOperationException("Unable to generate playback URL.");
        }

        // Tránh lộ log URL — chỉ log key.
        _logger.LogInformation(
            "Audio playback URL issued: AnswerId={AnswerId}, UserId={UserId}, DurationMinutes={DurationMinutes}",
            answerId, currentUserId, duration.TotalMinutes);

        return new AudioPlaybackResponse
        {
            AnswerId = answerId,
            ResultId = result.ResultId,
            PlaybackUrl = playbackUrl,
            ExpiresAt = DateTime.UtcNow.Add(duration),
            ContentType = "audio/webm"
        };
    }

    private static void EnsureCanListen(Result result, int userId, UserRole role)
    {
        switch (role)
        {
            case UserRole.STUDENT:
                if (result.StudentId != userId)
                    throw new UnauthorizedAccessException(
                        "You can only listen to your own recordings.");
                break;

            case UserRole.STAFF:
            case UserRole.EXAMINER:
                // Review workflow: staff/examiner của session mới được nghe.
                if (result.Session?.ScheduledBy != userId)
                    throw new UnauthorizedAccessException(
                        "You can only listen to recordings of sessions you have scheduled.");
                break;

            case UserRole.ADMIN:
                break;

            default:
                throw new UnauthorizedAccessException(
                    $"Role {role} is not allowed to access audio.");
        }
    }
}
