# Script test upload audio — chạy bằng pwsh (PowerShell 7+) hoặc dotnet
param(
    [string]$BaseUrl = "http://localhost:5116",
    [string]$Email = "admin@aives.edu.vn",
    [string]$Password = "Admin@123!",
    [int]$AnswerId = 3,
    [string]$FilePath = "C:\Users\thinh\Downloads\test.mp3"
)

Set-StrictMode -Off
$ErrorActionPreference = "Stop"

Write-Host "=== Step 1: Login ===" -ForegroundColor Cyan

$loginBody = [PSCustomObject]@{ email = $Email; password = $Password } | ConvertTo-Json
$loginResp = Invoke-RestMethod `
    -Uri "$BaseUrl/api/auth/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body $loginBody `
    -SkipCertificateCheck

$token = $loginResp.token
if (-not $token) { Write-Error "Login failed: $($loginResp | ConvertTo-Json)"; exit 1 }
Write-Host "Logged in. Token: $($token.Substring(0,40))..." -ForegroundColor Green

Write-Host ""
Write-Host "=== Step 2: Upload audio (answerId=$AnswerId) ===" -ForegroundColor Cyan

if (-not (Test-Path $FilePath)) { Write-Error "File not found: $FilePath"; exit 1 }

$uploadResp = Invoke-RestMethod `
    -Uri "$BaseUrl/api/answers/$AnswerId/audio" `
    -Method POST `
    -Headers @{ Authorization = "Bearer $token" } `
    -Form @{ audioFile = Get-Item $FilePath } `
    -SkipCertificateCheck

Write-Host "Upload response:" -ForegroundColor Green
$uploadResp | ConvertTo-Json -Depth 5
