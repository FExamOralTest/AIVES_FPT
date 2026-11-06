<#
.SYNOPSIS
    Công cụ chạy load test AIVES — dùng để TINH CHỈNH số lượng VU / replica.

.DESCRIPTION
    Script này gom các thao tác cần thiết cho 1 lần demo/đo lường:

      1. (tuỳ chọn) Reset DB local về trạng thái sạch + seed dữ liệu nền.
      2. (tuỳ chọn) Scale backend lên N replica.
      3. Chuẩn bị fixture (học sinh ảo + course + question + exam + session).
      4. Chạy k6 theo ma trận:  `direct` (1 backend) rồi `nginx` (N replica).
      5. Tổng hợp ra bảng Markdown + CSV.

    Mỗi lượt `direct` và `nginx` dùng **riêng một ca thi** (fixture riêng) nhưng
    chạy trên **cùng trạng thái DB** → điều kiện so sánh công bằng.

.PARAMETER VusList
    Các mức VU cần đo.  Mặc định @(50, 100, 200).
    Muốn nhanh hơn: -VusList @(50).   Muốn thử tải nặng: -VusList @(50,100,200,500).

.PARAMETER Replicas
    Số backend replica khi chạy qua Nginx.  Mặc định 3.

.PARAMETER Variants
    "direct" và/hoặc "nginx".  Mặc định cả hai.

.PARAMETER ResetDb
    Xoá & tạo lại database `aives_dev` trước khi chạy (bắt buộc nếu muốn
    số liệu so sánh được với lần chạy trước).

.PARAMETER SkipFixture
    Bỏ qua bước tạo dữ liệu ảo (dùng lại fixture cũ).

.EXAMPLE
    # Chạy nhanh 1 mức để kiểm tra script còn chạy được không
    $env:LOADTEST_TEST_PASSWORD = "Dev@12345"
    .\monitoring\k6\run-matrix.ps1 -VusList @(50) -ResetDb

.EXAMPLE
    # Đo đầy đủ 3 mức, 3 replica
    .\monitoring\k6\run-matrix.ps1 -VusList @(50,100,200) -Replicas 3 -ResetDb

.EXAMPLE
    # Chỉ đo qua Nginx (không cần baseline)
    .\monitoring\k6\run-matrix.ps1 -VusList @(100) -Variants nginx
#>
[CmdletBinding()]
param(
    [int[]]$VusList = @(50, 100, 200),
    [int]$Replicas = 3,
    [ValidateSet("direct", "nginx")]
    [string[]]$Variants = @("direct", "nginx"),
    [switch]$ResetDb,
    [switch]$SkipFixture,
    [switch]$SkipScale,
    [string]$TestPassword = $env:LOADTEST_TEST_PASSWORD
)

$ErrorActionPreference = "Stop"

    # Docker/compose in progress ra stderr → PowerShell coi là lỗi khi
    # $ErrorActionPreference = "Stop".  Bỏ qua lỗi stderr trong các lệnh
    # docker (đầu ra vẫn in ra console bình thường).
    function Invoke-Docker {
        param([string[]]$DockerArgs)
        $prev = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            & docker @DockerArgs
        } finally {
            $ErrorActionPreference = $prev
        }
    }

if ([string]::IsNullOrWhiteSpace($TestPassword)) {
    throw "Set LOADTEST_TEST_PASSWORD (mật khẩu user ảo do prepare-demo-fixture.ps1 tạo)."
}
if ($Replicas -lt 1) { throw "Replicas phải >= 1." }
foreach ($v in $VusList) {
    if ($v -lt 1 -or $v -gt 2000) { throw "Mỗi mức VU phải trong khoảng 1..2000 (giới hạn của PostgreSQL local)." }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$scriptDir = $PSScriptRoot
$today = Get-Date -Format 'yyyy-MM-dd'
$resultsRoot = Join-Path $repoRoot "monitoring\results\$today"

$composeArgs = @(
    "-f", (Join-Path $repoRoot "docker-compose.yml"),
    "-f", (Join-Path $repoRoot "docker-compose.scaling.yml"),
    "--profile", "local-db",
    "--profile", "scaling"
)

function Write-Step([string]$message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

# ─────────────────────────────────────────────────────────────────────
#  1. Reset DB (tuỳ chọn)
# ─────────────────────────────────────────────────────────────────────
if ($ResetDb) {
    Write-Step "Reset database local (aives_dev)"

    Invoke-Docker (@("compose") + $composeArgs + @("stop", "backend")) | Out-Null
    Start-Sleep -Seconds 8

    Invoke-Docker @("exec", "aives-dev-postgres-1", "psql", "-U", "aives", "-d", "postgres", "-c", "DROP DATABASE IF EXISTS aives_dev WITH (FORCE);") | Out-Null
    Invoke-Docker @("exec", "aives-dev-postgres-1", "psql", "-U", "aives", "-d", "postgres", "-c", "CREATE DATABASE aives_dev OWNER aives;") | Out-Null

    Invoke-Docker (@("compose") + $composeArgs + @("run", "--rm", "--no-deps",
        "-e", "ConnectionStrings__DefaultConnection=Host=postgres;Database=aives_dev;Username=aives;Password=aives-local-development",
        "db-init")) | Select-Object -Last 1

    $tables = @(Invoke-Docker @("exec", "aives-dev-postgres-1", "psql", "-U", "aives", "-d", "aives_dev", "-At", "-c", "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"))
    Write-Host "  schema: $($tables[-1]) bảng" -ForegroundColor Green
}

# ─────────────────────────────────────────────────────────────────────
#  2. Scale backend + khởi động Nginx
# ─────────────────────────────────────────────────────────────────────
if (-not $SkipScale) {
    Write-Step "Scale backend lên $Replicas replica + khởi động Nginx"

    $env:POSTGRES_MAX_CONNECTIONS = "600"

    # Bước 2a: khởi động DUY NHẤT 1 replica với SeedOnStartup=true để seed
    # dữ liệu nền (admin/staff/student).  Nếu bật seed cho cả N replica cùng
    # lúc, các replica chạy seeder song song → race condition
    # `duplicate key value violates unique constraint "users_email_key"` → app crash.
    if ($ResetDb) {
        Write-Host "  seed dữ liệu nền bằng 1 replica..." -ForegroundColor Yellow
        $env:Database__SeedOnStartup = "true"
        Invoke-Docker (@("compose") + $composeArgs + @("up", "-d", "--scale", "backend=1", "--force-recreate", "backend")) |
            Select-Object -Last 2
        Start-Sleep -Seconds 45

        $seeded = @(Invoke-Docker @("exec", "aives-dev-postgres-1", "psql", "-U", "aives", "-d", "aives_dev", "-At", "-c", "SELECT count(*) FROM users;"))
        Write-Host "  đã seed $($seeded[-1]) user" -ForegroundColor Green
    }

    # Bước 2b: scale lên N replica, TẮT seed (data đã có sẵn ở bước 2a).
    $env:Database__SeedOnStartup = "false"

    Invoke-Docker (@("compose") + $composeArgs + @("up", "-d", "--scale", "backend=$Replicas", "--force-recreate", "backend", "nginx")) |
        Select-Object -Last 3
    Start-Sleep -Seconds 50

    # `docker compose up --scale` đôi khi chỉ tạo 1 container mới (container
    # cũ đã tồn tại từ lần trước) → chạy thêm 1 vòng để đủ N replica.
    $running = @(Invoke-Docker @("ps", "--filter", "name=aives-dev-backend", "--format", "{{.Names}}"))
    if ($running.Count -lt $Replicas) {
        Write-Host "  đang bổ sung thêm replica ($($running.Count)/$Replicas)..." -ForegroundColor Yellow
        Invoke-Docker (@("compose") + $composeArgs + @("up", "-d", "--scale", "backend=$Replicas", "--no-recreate", "backend")) |
            Select-Object -Last 2
        Start-Sleep -Seconds 40
        $running = @(Invoke-Docker @("ps", "--filter", "name=aives-dev-backend", "--format", "{{.Names}}"))
    }

    Write-Host "  backend đang chạy: $($running.Count) replica" -ForegroundColor Green

    if ($running.Count -lt $Replicas) {
        Write-Warning "Chỉ có $($running.Count)/$Replicas replica healthy — chạy thêm lần `n    docker compose ... up -d --scale backend=$Replicas --no-recreate backend"
    }

    # Phải đủ replica thì mới chạy test (nếu không lượt "nginx" sẽ chỉ đi qua
    # 1 backend → số liệu so sánh sai).
    if ($running.Count -lt $Replicas) {
        throw "Không đủ replica ($($running.Count)/$Replicas). Abort — số liệu sẽ không đúng."
    }

    try {
        $health = Invoke-RestMethod -Uri "http://localhost:8088/api/health/db" -TimeoutSec 20
        Write-Host "  health: $($health.status) (db=$($health.databaseConnected))" -ForegroundColor Green
    } catch {
        Write-Warning "Health check qua Nginx thất bại: $($_.Exception.Message)"
    }
}

# ─────────────────────────────────────────────────────────────────────
#  3. Chạy ma trận
# ─────────────────────────────────────────────────────────────────────
Write-Step "Ma trận load test"
Write-Host "  VUs      : $($VusList -join ', ')"
Write-Host "  Cấu hình : $($Variants -join ', ')"
Write-Host "  Replicas : $Replicas"
Write-Host "  Kết quả  : $resultsRoot"
Write-Host ""

$summaryRows = @()

foreach ($vus in $VusList) {
    foreach ($variant in $Variants) {
        $baseUrl = if ($variant -eq "direct") { "http://backend:8080" } else { "http://nginx:8088" }

        Write-Host ""
        Write-Host "───── $variant / $vus VUs ─────" -ForegroundColor Yellow

        if (-not $SkipFixture) {
            & (Join-Path $scriptDir "prepare-demo-fixture.ps1") -Students $vus -Graders 5 | Out-Host
        }

        & (Join-Path $scriptDir "run-nginx-demo.ps1") `
            -Vus $vus -Label $variant -BaseUrl $baseUrl -TestPassword $TestPassword | Out-Host

        $summaryRows += [pscustomobject]@{ Variant = $variant; VUs = $vus }
    }
}

# ─────────────────────────────────────────────────────────────────────
#  4. Tổng hợp báo cáo
# ─────────────────────────────────────────────────────────────────────
Write-Step "Tổng hợp báo cáo"
& python (Join-Path $scriptDir "summarize-nginx-results.py")
Write-Host ""
Write-Host "  Markdown : docs\nginx-loadtest-results.md"
Write-Host "  CSV      : monitoring\results\nginx-summary.csv"
Write-Host "  Grafana  : http://localhost:3000/d/aives-nginx-lb" -ForegroundColor Green
