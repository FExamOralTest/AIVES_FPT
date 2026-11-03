using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var baseUrl = "http://localhost:5116";
var filePath = @"C:\Users\thinh\Downloads\test.mp3";
var answerId = 3;

var http = new HttpClient();

// --- Step 1: Login ---
Console.WriteLine("=== Step 1: Login ===");
var loginJson = """{"email":"admin@aives.edu.vn","password":"Admin@123"}""";
var loginResp = await http.PostAsync(
    $"{baseUrl}/api/auth/login",
    new StringContent(loginJson, Encoding.UTF8, "application/json"));

var loginBody = await loginResp.Content.ReadAsStringAsync();
Console.WriteLine($"Status: {loginResp.StatusCode}");
Console.WriteLine($"Body: {loginBody}");

if (!loginResp.IsSuccessStatusCode)
{
    Console.WriteLine($"Login failed.");
    return;
}

// tìm property chứa JWT (accessToken hoặc token)
var doc = JsonDocument.Parse(loginBody).RootElement;
string? token = null;
foreach (var prop in doc.EnumerateObject())
{
    var v = prop.Value.GetString() ?? "";
    if (v.StartsWith("eyJ")) { token = v; break; }
}
if (token is null) { Console.WriteLine("Cannot find JWT in response."); return; }
Console.WriteLine($"Token: {token[..50]}...");

// --- Step 2: Upload audio ---
Console.WriteLine("\n=== Step 2: Upload audio ===");
if (!File.Exists(filePath))
{
    Console.WriteLine($"File not found: {filePath}");
    return;
}

var fileBytes = await File.ReadAllBytesAsync(filePath);
Console.WriteLine($"File size: {fileBytes.Length / 1024} KB");

var form = new MultipartFormDataContent();
var fileContent = new ByteArrayContent(fileBytes);
fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");
form.Add(fileContent, "audioFile", "test.mp3");

var uploadReq = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/answers/{answerId}/audio");
uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
uploadReq.Content = form;

var uploadResp = await http.SendAsync(uploadReq);
var uploadBody = await uploadResp.Content.ReadAsStringAsync();

Console.WriteLine($"Status: {uploadResp.StatusCode}");
Console.WriteLine($"Response: {uploadBody}");
