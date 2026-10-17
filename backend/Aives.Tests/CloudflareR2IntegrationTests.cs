using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Aives.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Aives.Tests;

public class CloudflareR2IntegrationTests
{
    private readonly CloudflareR2Options _options;
    private readonly CloudflareR2StorageService _storageService;

    public CloudflareR2IntegrationTests()
    {
        // Load configuration from appsettings.Development.json
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Aives.Api"));
        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        _options = new CloudflareR2Options();
        config.GetSection(CloudflareR2Options.SectionName).Bind(_options);
        _options.UseLocalStorageFallback = false; // We want to test real R2, not local fallback!

        var optionsWrapper = Options.Create(_options);
        var logger = NullLogger<CloudflareR2StorageService>.Instance;

        _storageService = new CloudflareR2StorageService(optionsWrapper, logger);
    }

    [Fact]
    public void Configuration_ShouldBeConfigured()
    {
        Assert.True(_options.IsConfigured, "R2 credentials should be configured in appsettings.Development.json");
        Assert.Equal("aives-media-bucket", _options.BucketName);
        Assert.False(string.IsNullOrWhiteSpace(_options.AccountId));
        Assert.False(string.IsNullOrWhiteSpace(_options.AccessKeyId));
        Assert.False(string.IsNullOrWhiteSpace(_options.SecretAccessKey));
    }

    [Fact]
    public async Task CloudflareR2_FullLifecycle_ShouldSucceed()
    {
        // 1. Arrange test object key & data
        var testId = Guid.NewGuid().ToString("N")[..8];
        var objectKey = $"tests/ping_{testId}.webm";
        var testContent = Encoding.UTF8.GetBytes($"AIVES Cloudflare R2 Connection Test Payload: {DateTime.UtcNow:O}");

        using var uploadStream = new MemoryStream(testContent);

        // 2. Upload audio to Cloudflare R2
        var uploadedKey = await _storageService.UploadAudioAsync(objectKey, uploadStream, "audio/webm");
        Assert.Equal(objectKey, uploadedKey);

        try
        {
            // 3. Verify object exists in bucket
            var exists = await _storageService.ExistsAsync(objectKey);
            Assert.True(exists, $"Object '{objectKey}' must exist in bucket '{_options.BucketName}' after upload.");

            // 4. Generate Presigned URL
            var presignedUrl = _storageService.GeneratePresignedGetUrl(objectKey, TimeSpan.FromMinutes(15));
            Assert.NotNull(presignedUrl);
            Assert.Contains("X-Amz-Signature", presignedUrl);
            Assert.Contains(_options.BucketName, presignedUrl);

            // 5. Test downloading directly via Presigned GET URL
            using var httpClient = new HttpClient();
            var presignedResponse = await httpClient.GetAsync(presignedUrl);
            Assert.True(presignedResponse.IsSuccessStatusCode,
                $"Presigned URL download failed with status {presignedResponse.StatusCode}");
            var downloadedBytes = await presignedResponse.Content.ReadAsByteArrayAsync();
            Assert.Equal(testContent, downloadedBytes);

            // 6. Test ResolvePlaybackUrl (PublicBaseUrl / Presigned)
            var playbackUrl = _storageService.ResolvePlaybackUrl(objectKey);
            Assert.NotNull(playbackUrl);

            // 7. If PublicBaseUrl is set, verify CDN download
            if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
            {
                Assert.StartsWith(_options.PublicBaseUrl.TrimEnd('/'), playbackUrl);
                var publicResponse = await httpClient.GetAsync(playbackUrl);
                Assert.True(publicResponse.IsSuccessStatusCode,
                    $"Public URL {playbackUrl} returned {publicResponse.StatusCode}. If R2.dev access is disabled in Cloudflare, leave PublicBaseUrl empty to use Presigned URLs.");
            }
        }
        finally
        {
            // 8. Cleanup test file
            var deleted = await _storageService.DeleteAudioAsync(objectKey);
            Assert.True(deleted, $"Failed to delete test object '{objectKey}'");

            var stillExists = await _storageService.ExistsAsync(objectKey);
            Assert.False(stillExists, $"Object '{objectKey}' should not exist after deletion.");
        }
    }
}
