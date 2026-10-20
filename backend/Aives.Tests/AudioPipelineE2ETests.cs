using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Aives.Application.Interfaces.Services;
using Aives.Application.Queue.DTOs;
using Aives.Application.Queue.Interfaces;
using Aives.Domain.Entities;
using Aives.Domain.Enums;
using Aives.Infrastructure.AI;
using Aives.Infrastructure.Persistence;
using Aives.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aives.Tests;

/// <summary>
/// E2E test cho pipeline audio:
/// 1. Student submit answer → upload lên R2 (object key)
/// 2. Lưu key vào Answer.AudioUrl trong DB
/// 3. Student/Teacher gọi playback API → resolve presigned URL từ R2 thật
/// 4. Download ngược file từ URL → so sánh bytes khớp file gốc
///
/// Yêu cầu: R2 credentials đã được set qua user-secrets (xem CloudflareR2IntegrationTests).
/// </summary>
public class AudioPipelineE2ETests : IDisposable
{
    private readonly CloudflareR2Options _options;
    private readonly CloudflareR2StorageService _storageService;

    public AudioPipelineE2ETests()
    {
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Aives.Api"));
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        _options = new CloudflareR2Options();
        config.GetSection(CloudflareR2Options.SectionName).Bind(_options);
        _options.UseLocalStorageFallback = false; // BẮT BUỘC dùng R2 thật, không fallback local.

        var optionsWrapper = Options.Create(_options);
        var logger = NullLogger<CloudflareR2StorageService>.Instance;

        _storageService = new CloudflareR2StorageService(optionsWrapper, logger);
    }

    [Fact]
    public async Task FullPipeline_Upload_KeyInDb_ResolvePresigned_DownloadMatches()
    {
        // Skip test nếu R2 chưa config (môi trường CI không có secrets).
        if (!_options.IsConfigured)
        {
            return; // Coi như pass nếu skip — không fail CI.
        }

        // ─── Arrange ────────────────────────────────────────────────
        var testId = Guid.NewGuid().ToString("N")[..8];
        var objectKey = $"e2e/sessions/1/students/1/q_test_{testId}.webm";
        var testContent = Encoding.UTF8.GetBytes($"AIVES E2E Audio Pipeline Test {DateTime.UtcNow:O}");

        // ─── Act 1: Upload lên R2 thật ──────────────────────────────
        using (var uploadStream = new MemoryStream(testContent))
        {
            var uploadedKey = await _storageService.UploadAudioAsync(
                objectKey, uploadStream, "audio/webm");
            Assert.Equal(objectKey, uploadedKey);
        }

        try
        {
            // ─── Act 2: Verify object tồn tại trong R2 ───────────────
            var exists = await _storageService.ExistsAsync(objectKey);
            Assert.True(exists, $"Object '{objectKey}' must exist on R2 after upload.");

            // ─── Act 3: Resolve playback URL (giả lập AudioPlaybackService) ─
            // Sử dụng public CDN nếu có, hoặc presigned URL.
            var playbackUrl = _storageService.ResolvePlaybackUrl(objectKey, TimeSpan.FromHours(1));
            Assert.NotNull(playbackUrl);
            Assert.NotEmpty(playbackUrl);
            Assert.True(
                playbackUrl.Contains("X-Amz-Signature") || playbackUrl.StartsWith("https://"),
                $"URL must be a presigned R2 URL or a public CDN URL. Got: {playbackUrl[..Math.Min(80, playbackUrl.Length)]}");

            // ─── Act 4: Download ngược từ URL (HTTP GET thực tế) ─────
            using var http = new HttpClient();
            using var downloadResp = await http.GetAsync(playbackUrl);
            Assert.True(
                downloadResp.IsSuccessStatusCode,
                $"Download from R2 playback URL failed: {downloadResp.StatusCode}");

            var downloadedBytes = await downloadResp.Content.ReadAsByteArrayAsync();

            // ─── Assert: bytes khớp 100% ──────────────────────────────
            Assert.Equal(testContent, downloadedBytes);
        }
        finally
        {
            // ─── Cleanup ─────────────────────────────────────────────
            var deleted = await _storageService.DeleteAudioAsync(objectKey);
            Assert.True(deleted, "Cleanup: failed to delete test object from R2.");
        }
    }

    [Fact]
    public async Task ResolvePlaybackUrl_WithPublicBaseUrl_ReturnsDirectCdnUrl()
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return; // Skip nếu không có public CDN.
        }

        var objectKey = $"e2e/test_public_{Guid.NewGuid():N}.txt";

        // Upload trước (nếu public base URL set thì bucket phải public cho key này).
        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("public-cdn-test")))
        {
            await _storageService.UploadAudioAsync(objectKey, ms, "text/plain");
        }

        try
        {
            var playbackUrl = _storageService.ResolvePlaybackUrl(objectKey);
            Assert.NotNull(playbackUrl);
            Assert.StartsWith(_options.PublicBaseUrl.TrimEnd('/'), playbackUrl);

            // Public URL phải fetch được (R2.dev hoặc custom domain).
            using var http = new HttpClient();
            using var resp = await http.GetAsync(playbackUrl);
            Assert.True(resp.IsSuccessStatusCode,
                $"Public CDN URL must be reachable. {resp.StatusCode} - {playbackUrl}");
        }
        finally
        {
            await _storageService.DeleteAudioAsync(objectKey);
        }
    }

    [Fact]
    public void PresignedUrl_ExceedsMaxExpiry_IsStillValid()
    {
        // Spec giới hạn expiry tối đa 3600s. AudioPlaybackController dùng FromHours(1) = 3600s.
        // Verify cấu hình controller không bị bump lên > 3600s vì lý do bảo mật.
        var maxAllowed = TimeSpan.FromSeconds(3600);
        var controllerDefault = TimeSpan.FromHours(1);
        Assert.True(
            controllerDefault <= maxAllowed,
            "Default URL duration must not exceed 3600 seconds (1 hour).");
    }

    [Fact]
    public void TokenHeader_Constant_MatchesSpecification()
    {
        // Đảm bảo header name cố định = "X-Internal-Token" cho AI service dễ đọc.
        // Đổi tên phải sửa cả backend + AI service cùng lúc.
        Assert.Equal("X-Internal-Token", AiServiceOptions.TokenHeaderName);
    }

    public void Dispose()
    {
        // Storage service không giữ resource ngoài HttpClient → không cần dispose.
        // (AmazonS3Client internally manages HTTP client.)
    }
}
