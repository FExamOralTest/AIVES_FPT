param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$BackendContainer = "aives-dev-backend-1",
    [string]$DatabaseContainer = "aives-dev-postgres-1",
    [string]$ResultsRoot = (Join-Path $PSScriptRoot "..\results\$(Get-Date -Format 'yyyy-MM-dd')"),
    [string]$DatabaseName = "aives_loadtest",
    [int]$MaxVus = 500,
    [string]$TestPassword = $env:LOADTEST_TEST_PASSWORD
)

$ErrorActionPreference = "Stop"

if ($MaxVus -ne 500) {
    throw "This fixture is sized for exactly 500 VUs."
}
if ([string]::IsNullOrWhiteSpace($TestPassword)) {
    throw "Set LOADTEST_TEST_PASSWORD for the local seeded test accounts."
}

$localConnection = "Host=postgres;Database=$DatabaseName;Username=aives;Password=aives-local-development;Maximum Pool Size=200;Timeout=30;Command Timeout=60"
$env:DATABASE_CONNECTION = $localConnection
$env:POSTGRES_MAX_CONNECTIONS = "250"
$env:LOGIN_RATE_LIMIT = "2000"
$env:Database__SeedOnStartup = "true"

$null = docker compose --profile local-db up -d --force-recreate postgres
if ($LASTEXITCODE -ne 0) {
    throw "Could not configure local PostgreSQL for the load-test profile."
}
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    $databaseHealth = docker inspect --format '{{.State.Health.Status}}' $DatabaseContainer
    if ($LASTEXITCODE -eq 0 -and $databaseHealth.Trim() -eq "healthy") {
        break
    }
    if ($attempt -eq 29) {
        throw "Local PostgreSQL did not become healthy after applying the load-test connection limit."
    }
    Start-Sleep -Seconds 2
}

$databaseExists = docker exec $DatabaseContainer psql -U aives -d postgres -At -c `
    "SELECT 1 FROM pg_database WHERE datname = '$DatabaseName';"
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect local PostgreSQL databases."
}
if ([string]$databaseExists -ne "1") {
    docker exec $DatabaseContainer psql -v ON_ERROR_STOP=1 -U aives -d postgres -c `
        "CREATE DATABASE $DatabaseName OWNER aives;" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create isolated local test database '$DatabaseName'."
    }
}

$null = docker compose --profile local-db run --rm --no-deps `
    -e "ConnectionStrings__DefaultConnection=$localConnection" db-init
if ($LASTEXITCODE -ne 0) {
    throw "Could not initialize the isolated local test database schema."
}

docker compose --profile local-db up -d --force-recreate backend | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Could not point the backend to the isolated local test database."
}
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    $healthState = docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $BackendContainer
    if ($LASTEXITCODE -eq 0 -and $healthState.Trim() -eq "healthy") {
        break
    }
    if ($attempt -eq 29) {
        throw "Backend did not become healthy on the isolated local test database."
    }
    Start-Sleep -Seconds 2
}

$backendEnvironment = docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' $BackendContainer
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect backend container '$BackendContainer'."
}
$connectionSetting = $backendEnvironment | Where-Object { $_ -like "ConnectionStrings__DefaultConnection=*" } | Select-Object -First 1
if ($connectionSetting -notmatch "(?i)Host=postgres;Database=$DatabaseName(;|$)") {
    throw "Refusing to seed load-test users: backend is not configured for the isolated local PostgreSQL database."
}

$connectedDatabase = docker exec $DatabaseContainer psql -U aives -d $DatabaseName -At -c "SELECT current_database();"
if ($LASTEXITCODE -ne 0 -or $connectedDatabase.Trim() -ne $DatabaseName) {
    throw "Refusing to seed fixture: local database verification failed."
}
$schemaReady = docker exec $DatabaseContainer psql -U aives -d $DatabaseName -At -c `
    "SELECT to_regclass('public.exam_slot_templates') IS NOT NULL;"
if ($LASTEXITCODE -ne 0 -or $schemaReady.Trim() -ne "t") {
    throw "The local test database schema is incomplete."
}

$health = Invoke-RestMethod -Uri "$BaseUrl/api/health/db" -TimeoutSec 10
if (-not $health.databaseConnected) {
    throw "AIVES backend does not report a connected local database."
}

$adminResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" `
    -ContentType "application/json" `
    -Body (@{ identifier = "admin"; password = $TestPassword } | ConvertTo-Json)
$staffResponse = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" `
    -ContentType "application/json" `
    -Body (@{ identifier = "staff_01"; password = $TestPassword } | ConvertTo-Json)
$studentLogin = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" `
    -ContentType "application/json" `
    -Body (@{ identifier = "student_01"; password = $TestPassword } | ConvertTo-Json)

$headers = @{ Authorization = "Bearer $($adminResponse.accessToken)" }
$staffHeaders = @{ Authorization = "Bearer $($staffResponse.accessToken)" }
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$userPrefix = "loadtest-$runId-"
$studentHashSql = @"
INSERT INTO users (username, full_name, email, password_hash, role, created_at)
SELECT '$userPrefix' || lpad(n::text, 4, '0'),
       'Local k6 Student ' || n,
       '$userPrefix' || lpad(n::text, 4, '0') || '@aives.local',
       student.password_hash,
       'STUDENT'::user_role,
       CURRENT_TIMESTAMP
FROM generate_series(1, 850) AS n
CROSS JOIN (SELECT password_hash FROM users WHERE username = 'student_01') AS student
ON CONFLICT DO NOTHING;
"@
docker exec $DatabaseContainer psql -v ON_ERROR_STOP=1 -U aives -d $DatabaseName -c $studentHashSql | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Could not create local load-test student accounts."
}

$studentIdsRaw = docker exec $DatabaseContainer psql -U aives -d $DatabaseName -At -c `
    "SELECT user_id FROM users WHERE username LIKE '$userPrefix%' ORDER BY username;"
if ($LASTEXITCODE -ne 0) {
    throw "Could not retrieve local load-test student IDs."
}
$studentIds = @($studentIdsRaw | Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ })
if ($studentIds.Count -ne 850 -or -not $studentLogin.accessToken) {
    throw "Expected 850 unique local test accounts and a valid seeded student login; found $($studentIds.Count)."
}

$courseCode = "K6-LOAD-LOCAL"
$courses = Invoke-RestMethod -Uri "$BaseUrl/api/admin/courses" -Headers $headers
$course = $courses | Where-Object { $_.courseCode -eq $courseCode } | Select-Object -First 1
if (-not $course) {
    $course = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/admin/courses" `
        -Headers $headers -ContentType "application/json" `
        -Body (@{
            courseCode = $courseCode
            courseName = "Local k6 Load Test"
            teacherId = $staffResponse.user.userId
        } | ConvertTo-Json)
}
$courseId = [int]$course.courseId

$headers = $staffHeaders
$questionText = "K6 load-test question: explain how latency changes under concurrent load."
$questions = Invoke-RestMethod -Uri "$BaseUrl/api/courses/$courseId/questions?page=1&pageSize=100" -Headers $headers
$question = $questions.items | Where-Object { $_.questionText -eq $questionText } | Select-Object -First 1
if (-not $question) {
    $question = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/courses/$courseId/questions" `
        -Headers $headers -ContentType "application/json" `
        -Body (@{
            questionText = $questionText
            topic = "Load testing"
            sampleAnswer = "Latency is the time to complete a request. It can rise when concurrent demand approaches a resource limit."
            learningOutcome = "Explain how concurrent load affects response time."
            rubric = @(@{ description = "Explains latency and concurrent demand"; maxScore = 100 })
            level = 1
            maxScore = 100
            isFollowUpAllowed = $false
            expectedKeywords = @("latency", "concurrent load")
        } | ConvertTo-Json -Depth 8)
}
$questionId = [int]$question.questionId
if ($question.approvalStatus -ne "APPROVED") {
    $question = Invoke-RestMethod -Method Post `
        -Uri "$BaseUrl/api/courses/$courseId/questions/$questionId/approve" `
        -Headers $headers
}

$examTitle = "K6 Local Load Test Exam"
$exams = Invoke-RestMethod -Uri "$BaseUrl/api/exams/my" -Headers $headers
$exam = $exams | Where-Object { $_.title -eq $examTitle -and $_.courseId -eq $courseId } | Select-Object -First 1
if (-not $exam) {
    $exam = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/exams" `
        -Headers $headers -ContentType "application/json" `
        -Body (@{
            title = $examTitle
            courseId = $courseId
            durationMinutes = 30
            maxQuestions = 1
            questions = @(@{ questionId = $questionId; questionScore = 100 })
        } | ConvertTo-Json -Depth 8)
}
$examId = [int]$exam.examId

$resultDirectory = Join-Path $ResultsRoot "member3-submit-$runId"
New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null
$profiles = @(
    @{ vus = 50; offset = 0 },
    @{ vus = 100; offset = 50 },
    @{ vus = 200; offset = 150 },
    @{ vus = 500; offset = 350 }
)
$fixtures = @()

foreach ($profile in $profiles) {
    $vus = $profile.vus
    $startTime = [DateTime]::UtcNow.AddMinutes(-1)
    $endTime = [DateTime]::UtcNow.AddHours(2)
    $session = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/staff/sessions" `
        -Headers $headers -ContentType "application/json" `
        -Body (@{
            examId = $examId
            sessionName = "K6 Member3 $runId $vus VUs"
            room = "Local Docker"
            startTime = $startTime.ToString("o")
            endTime = $endTime.ToString("o")
            maxCapacity = $vus
        } | ConvertTo-Json)
    $sessionId = [int]$session.sessionId

    $enrollmentBody = @{ studentIds = @($studentIds | Select-Object -Skip $profile.offset -First $vus) } | ConvertTo-Json -Depth 5
    $enrollment = Invoke-RestMethod -Method Post `
        -Uri "$BaseUrl/api/staff/sessions/$sessionId/enrollments" `
        -Headers $headers -ContentType "application/json" -Body $enrollmentBody
    if ($enrollment.addedCount -ne $vus) {
        throw "Expected to enroll $vus load-test students in session $sessionId; added $($enrollment.addedCount)."
    }

    $started = Invoke-RestMethod -Method Put -Uri "$BaseUrl/api/staff/sessions/$sessionId" `
        -Headers $headers -ContentType "application/json" `
        -Body (@{ status = "IN_PROGRESS" } | ConvertTo-Json)

    $profileDirectory = Join-Path $resultDirectory "$vus"
    New-Item -ItemType Directory -Force -Path $profileDirectory | Out-Null
    $fixtures += [pscustomobject]@{
        vus = $vus
        userOffset = $profile.offset
        sessionId = $sessionId
        startTime = $started.startTime
        endTime = $started.endTime
    }
}

$fixture = [pscustomobject]@{
    createdAt = [DateTimeOffset]::Now.ToString("o")
    baseUrl = $BaseUrl
    database = "local PostgreSQL / $DatabaseName"
    postgresMaxConnections = 250
    backendMaxPoolSize = 200
    courseId = $courseId
    examId = $examId
    userPrefix = $userPrefix
    userCount = $studentIds.Count
    sessions = $fixtures
}
$fixturePath = Join-Path $resultDirectory "fixture.json"
$fixture | ConvertTo-Json -Depth 8 | Set-Content -Path $fixturePath
Write-Output "Prepared $($studentIds.Count) local test accounts and four submit sessions."
Write-Output "Fixture: $fixturePath"
