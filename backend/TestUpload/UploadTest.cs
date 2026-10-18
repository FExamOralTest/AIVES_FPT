using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var baseUrl = "http://localhost:5116";
var filePath = @"C:\Users\thinh\Downloads\test.mp3";
var answerId = 3;

var http = new HttpClient();

// Login
Console.WriteLine("=== Login ===");
var loginResp = await http.PostAsync($"{baseUrl}/api/auth/login",
    new StringContent("""{"email":"admin@aives.edu.vn","password":"Admin@123"}""", Encoding.UTF8, "application/json"));
var loginDoc = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync());
var token = loginDoc.RootElement.EnumerateObject()
    .First(p => p.Value.GetString()?.StartsWith("eyJ") == true).Value.GetString()!;
Console.WriteLine($"Token: {token[..40]}...");

// Upload
Console.WriteLine($"\n=== Upload to answerId={answerId} ===");
var fileBytes = await File.ReadAllBytesAsync(filePath);
var form = new MultipartFormDataContent();
var fc = new ByteArrayContent(fileBytes);
fc.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");
form.Add(fc, "audioFile", "test.mp3");

var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/answers/{answerId}/audio");
req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
req.Content = form;

var uploadResp = await http.SendAsync(req);
var uploadBody = await uploadResp.Content.ReadAsStringAsync();
Console.WriteLine($"Status : {uploadResp.StatusCode}");
Console.WriteLine($"Response: {uploadBody}");
