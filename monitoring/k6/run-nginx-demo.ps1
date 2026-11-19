<#
.SYNOPSIS
    Chạy demo k6 (submit-answer) qua Nginx load balancer — Thịnh.

.DESCRIPTION
    Chạy 1 lượt k6 với profile chỉ định, ghi kết quả vào
    monitoring/results/<ngày>/nginx-demo-<runId>/<vus>/

    Yêu cầu:
      - backend đang scale N replica + nginx đang chạy
        docker compose -f docker-compose.yml -f docker-compose.scaling.yml \
            --profile scaling up -d --scale backend=3 nginx
      - fixture đã tạo bằng prepare-demo-fixture.ps1
      - biến môi trường LOADTEST_TEST_PASSWORD

.EXAMPLE
    $env:LOADTEST_TEST_PASSWORD = "Dev@12345"
    .\monitoring\k6\run-nginx-demo.ps1 -Vus 50
#>
[CmdletBinding()]
param(
    [int]$Vus = 50,
    [string]$BaseUrl = "http://nginx:8088",
    [string]$Label = "nginx",
    [string]$ResultsRoot = (Join-Path $PSScriptRoot "..\results\$(Get-Date -Format 'yyyy-MM-dd')"),
    [string]$TestPassword = $env:LOADTEST_TEST_PASSWORD
)

$ErrorActionPreference = "Stop"

if ($Vus -lt 1 -or $Vus -gt 500) {
    throw "Vus must be between 1 and 500."
}
if ([string]::IsNullOrWhiteSpace($TestPassword)) {
    throw "Set LOADTEST_TEST_PASSWORD for the local seeded test accounts."
}

$fixture = Get-ChildItem -Path $ResultsRoot -Directory -Filter "nginx-demo-*" |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $fixture) {
    throw "No nginx-demo-* fixture found under $ResultsRoot. Run prepare-demo-fixture.ps1 first."
}
$fixtureFile = Join-Path $fixture.FullName "fixture.json"
$fixtureData = Get-Content $fixtureFile -Raw | ConvertFrom-Json

if ($Vus -gt $fixtureData.userCount) {
    throw "Fixture only has $($fixtureData.userCount) students; cannot run $Vus VUs."
}

$runDirectory = Join-Path $fixture.FullName "$Vus"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

# Nhãn riêng để phân biệt "trước tối ưu" (direct) và "sau tối ưu" (nginx).
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$summaryFile = Join-Path $runDirectory "k6-summary-$Label-$stamp.json"
Write-Host "== k6 demo: $Vus VUs via $BaseUrl ==" -ForegroundColor Cyan
Write-Host "  fixture : $($fixture.FullName)"
Write-Host "  session : $($fixtureData.sessionId)"
Write-Host "  summary : $summaryFile"

$prevPref = $ErrorActionPreference
$ErrorActionPreference = "Continue"

& docker run --rm --network aives-dev_default `
    --mount "type=bind,source=$($PSScriptRoot -replace '\\','/'),target=/scripts,readonly" `
    --mount "type=bind,source=$($fixture.FullName -replace '\\','/'),target=/results" `
    -e "VUS=$Vus" `
    -e "SESSION_ID=$($fixtureData.sessionId)" `
    -e "USER_PREFIX=$($fixtureData.userPrefix)" `
    -e "TEST_PASSWORD=$TestPassword" `
    -e "BASE_URL=$BaseUrl" `
    -e "FIXTURE_PATH=/results/fixture.json" `
    grafana/k6:0.53.0 run /scripts/nginx-submit.js `
    --summary-export "/results/$Vus/k6-summary-$Label-$stamp.json" `
    2>&1 | Tee-Object -FilePath (Join-Path $runDirectory "k6-console-$Label-$stamp.log")

$k6Exit = $LASTEXITCODE
$ErrorActionPreference = $prevPref

Write-Host ""
if (Test-Path $summaryFile) {
    $summary = Get-Content $summaryFile -Raw | ConvertFrom-Json
    $http = $summary.metrics.http_req_duration
    $failed = $summary.metrics.http_req_failed
    $checks = $summary.metrics.checks
    $reqs = $summary.metrics.http_reqs
    Write-Host "== k6 summary ==" -ForegroundColor Green
    Write-Host ("  requests    : {0} ({1} req/s)" -f $reqs.count, [math]::Round($reqs.rate, 2))
    Write-Host ("  http failed : {0}%" -f [math]::Round($failed.value * 100, 2))
    Write-Host ("  checks      : {0}%" -f [math]::Round($checks.value * 100, 2))
    Write-Host ("  latency avg : {0} ms" -f [math]::Round($http.avg, 2))
    Write-Host ("  latency p95 : {0} ms" -f [math]::Round($http.'p(95)', 2))
    Write-Host ("  latency p99 : {0} ms" -f [math]::Round($http.'p(99)', 2))
} else {
    Write-Warning "k6 did not write a summary file (exit code $k6Exit)."
}

if ($k6Exit -ne 0) {
    Write-Warning "k6 exit code $k6Exit (có thể do vượt threshold — kết quả vẫn được lưu)."
}
