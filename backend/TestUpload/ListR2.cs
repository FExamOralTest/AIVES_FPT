using Amazon.S3;
using Amazon.S3.Model;

// Cấu hình từ appsettings.Development.json — test bucket "aives" (bucket thực trên dashboard)
var accountId = "9e7256bedb995a7382bd318df204157e";
var accessKey = "f90a3ac141e7ca8aefce28b1c7572dbf";
var secretKey = "58c2cb130292330569b4b1d5f3db54ce063f9d21f8d33fbeecb498810b588df8";

var buckets = new[] { "aives" };

var config = new AmazonS3Config
{
    ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
    ForcePathStyle = true,
    AuthenticationRegion = "auto"
};

var handler = new HttpClientHandler
{
    SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
};
config.HttpClientFactory = new R2HttpClientFactory(handler);

var s3 = new AmazonS3Client(accessKey, secretKey, config);

Console.WriteLine($"AccountId : {accountId}");
Console.WriteLine($"AccessKeyId: {accessKey}");
Console.WriteLine($"ServiceURL : https://{accountId}.r2.cloudflarestorage.com\n");

// 1. List tất cả buckets (kiểm tra key có hợp lệ không)
Console.WriteLine("=== List ALL buckets ===");
try
{
    var listBuckets = await s3.ListBucketsAsync();
    if (listBuckets.Buckets.Count == 0)
    {
        Console.WriteLine("(no buckets found — key may lack ListBuckets permission)");
    }
    foreach (var b in listBuckets.Buckets)
        Console.WriteLine($"  Bucket: {b.BucketName}  Created: {b.CreationDate:yyyy-MM-dd}");
}
catch (Exception ex)
{
    Console.WriteLine($"  ListBuckets ERROR: {ex.GetType().Name}: {ex.Message}");
}

// 2. Thử list objects trong từng bucket khả năng
Console.WriteLine("\n=== Try listing each candidate bucket ===");
foreach (var bucket in buckets)
{
    try
    {
        var resp = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, MaxKeys = 5 });
        Console.WriteLine($"  [{bucket}] OK — {resp.KeyCount} objects");
        foreach (var obj in resp.S3Objects)
            Console.WriteLine($"    - {obj.Key} ({obj.Size} bytes)");
    }
    catch (AmazonS3Exception ex)
    {
        Console.WriteLine($"  [{bucket}] {ex.StatusCode} — {ex.Message}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [{bucket}] ERROR: {ex.GetType().Name}: {ex.Message}");
    }
}

class R2HttpClientFactory : Amazon.Runtime.HttpClientFactory
{
    private readonly HttpClientHandler _handler;
    private readonly HttpClient _client;
    public R2HttpClientFactory(HttpClientHandler handler)
    { _handler = handler; _client = new HttpClient(handler, disposeHandler: false); }
    public override HttpClient CreateHttpClient(Amazon.Runtime.IClientConfig cfg) => _client;
}
