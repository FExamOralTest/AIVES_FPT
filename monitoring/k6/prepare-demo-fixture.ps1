<#
.SYNOPSIS
    Tạo dữ liệu ảo cho demo k6 (Thịnh — Nginx & Load Balancing).

.DESCRIPTION
    Script này CHỈ tạo dữ liệu, KHÔNG recreate container nào — nhờ vậy chạy
    được an toàn khi backend đang scale 3 replica qua Nginx.

    Tạo:
      - N học sinh ảo (copy password_hash của student_01 → bcrypt không cần hash lại)
      - 1 course + 1 câu hỏi + 1 đề thi
      - 4 ca thi tương ứng profile 50 / 100 / 200 / 500 VUs
      - Ghi danh học sinh vào từng ca thi

    Ghi fixture ra monitoring/results/<ngày>/nginx-demo-<runId>/fixture.json
    để k6 đọc vào.

.EXAMPLE
    $env:LOADTEST_TEST_PASSWORD = "Dev@12345"
    .\monitoring\k6\prepare-demo-fixture.ps1 -Students 50
#>
[CmdletBinding()]
param(
    [int]$Students = 50,
    [int]$Graders = 5,
    [string]$BaseUrl = "http://localhost:8088",
    [string]$DatabaseContainer = "aives-dev-postgres-1",
    [string]$DatabaseName = "aives_dev",
    [string]$ResultsRoot = (Join-Path $PSScriptRoot "..\results\$(Get-Date -Format 'yyyy-MM-dd')"),
    [string]$TestPassword = $env:LOADTEST_TEST_PASSWORD
)

$ErrorActionPreference = "Stop"

if ($Students -lt 1 -or $Students -gt 500) {
    throw "Students must be between 1 and 500."
}
if ([string]::IsNullOrWhiteSpace($TestPassword)) {
    throw "Set LOADTEST_TEST_PASSWORD for the local seeded test accounts."
}

# PowerShell 5.1 coi stderr của docker là terminating error → tạm tắt.
$prevPref = $ErrorActionPreference
$ErrorActionPreference = "Continue"

function Invoke-Docker {
    param([string[]]$Arguments)
    $output = docker @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        $ErrorActionPreference = $prevPref
        throw "docker $($Arguments -join ' ') failed:`n$($output -join "`n")"
    }
    return $output
}

try {
    Write-Host "== AIVES demo fixture ==" -ForegroundColor Cyan

    # ---------------------------------------------------------------- login
    $adminLogin = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" `
        -ContentType "application/json" `
        -Body (@{ identifier = "admin"; password = $TestPassword } | ConvertTo-Json)
    $staffLogin = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/auth/login" `
        -ContentType "application/json" `
        -Body (@{ identifier = "staff_01"; password = $TestPassword } | ConvertTo-Json)
    $adminHeaders = @{ Authorization = "Bearer $($adminLogin.accessToken)" }
    $staffHeaders = @{ Authorization = "Bearer $($staffLogin.accessToken)" }
    Write-Host "  [ok] dang nhap admin + staff"

    # ------------------------------------------------------- virtual students
    $runId = Get-Date -Format "yyyyMMdd-HHmmss"
    $userPrefix = "demo-$runId-"
    $insertSql = @"
INSERT INTO users (username, full_name, email, password_hash, role, created_at)
SELECT '$userPrefix' || lpad(n::text, 4, '0'),
       'Demo Student ' || n,
       '$userPrefix' || lpad(n::text, 4, '0') || '@aives.local',
       student.password_hash,
       'STUDENT'::user_role,
       CURRENT_TIMESTAMP
FROM generate_series(1, $Students) AS n
CROSS JOIN (SELECT password_hash FROM users WHERE username = 'student_01') AS student
ON CONFLICT DO NOTHING;
"@
    Invoke-Docker @("exec", $DatabaseContainer, "psql", "-v", "ON_ERROR_STOP=1", "-U", "aives", "-d", $DatabaseName, "-c", $insertSql) | Out-Null

    $idsRaw = Invoke-Docker @("exec", $DatabaseContainer, "psql", "-U", "aives", "-d", $DatabaseName, "-At", "-c",
        "SELECT user_id FROM users WHERE username LIKE '$userPrefix%' ORDER BY username;")
    $studentIds = @($idsRaw | Where-Object { $_ -match "^\d+$" } | ForEach-Object { [int]$_ })
    if ($studentIds.Count -ne $Students) {
        throw "Expected $Students demo students; found $($studentIds.Count)."
    }
    Write-Host "  [ok] tao $Students hoc sinh ao ($userPrefix)"

    # ------------------------------------------------------- course + question
    $courseCode = "DEMO-NGINX"
    $courses = Invoke-RestMethod -Uri "$BaseUrl/api/admin/courses" -Headers $adminHeaders
    $course = $courses | Where-Object { $_.courseCode -eq $courseCode } | Select-Object -First 1
    if (-not $course) {
        $course = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/admin/courses" `
            -Headers $adminHeaders -ContentType "application/json" `
            -Body (@{
                courseCode = $courseCode
                courseName = "Nginx Demo Course"
                teacherId = $staffLogin.user.userId
            } | ConvertTo-Json)
    }
    $courseId = [int]$course.courseId

    $questionText = "Nginx demo question: how does round-robin balancing distribute submit requests?"
    $questions = Invoke-RestMethod -Uri "$BaseUrl/api/courses/$courseId/questions?page=1&pageSize=100" -Headers $staffHeaders
    $question = $questions.items | Where-Object { $_.questionText -eq $questionText } | Select-Object -First 1
    if (-not $question) {
        $question = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/courses/$courseId/questions" `
            -Headers $staffHeaders -ContentType "application/json" `
            -Body (@{
                questionText = $questionText
                topic = "Load balancing"
                sampleAnswer = "Round robin sends each request to the next healthy backend replica in turn."
                learningOutcome = "Explain how the reverse proxy distributes traffic."
                rubric = @(@{ description = "Explains round robin distribution"; maxScore = 100 })
                level = 1
                maxScore = 100
                isFollowUpAllowed = $false
                expectedKeywords = @("round robin", "backend")
            } | ConvertTo-Json -Depth 8)
    }
    $questionId = [int]$question.questionId
    if ($question.approvalStatus -ne "APPROVED") {
        $question = Invoke-RestMethod -Method Post `
            -Uri "$BaseUrl/api/courses/$courseId/questions/$questionId/approve" -Headers $staffHeaders
    }

    # ----------------------------------------------------------------- exam
    $examTitle = "Nginx Demo Load Test Exam"
    $exams = Invoke-RestMethod -Uri "$BaseUrl/api/exams/my" -Headers $staffHeaders
    $exam = $exams | Where-Object { $_.title -eq $examTitle -and $_.courseId -eq $courseId } | Select-Object -First 1
    if (-not $exam) {
        $exam = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/exams" `
            -Headers $staffHeaders -ContentType "application/json" `
            -Body (@{
                title = $examTitle
                courseId = $courseId
                durationMinutes = 30
                maxQuestions = 1
                questions = @(@{ questionId = $questionId; questionScore = 100 })
            } | ConvertTo-Json -Depth 8)
    }
    $examId = [int]$exam.examId
    Write-Host "  [ok] course=$courseId question=$questionId exam=$examId"

    # -------------------------------------------------------------- sessions
    $resultDirectory = Join-Path $ResultsRoot "nginx-demo-$runId"
    New-Item -ItemType Directory -Force -Path $resultDirectory | Out-Null

    # StartTime: API tạo session chặn quá khứ quá 5 phút, còn API start yêu cầu
    # `now >= StartTime`.  → StartTime = now - 2 phút là vừa đúng cả hai.
    $startTime = [DateTime]::UtcNow.AddMinutes(-2)
    $endTime = [DateTime]::UtcNow.AddHours(3)
    $session = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/staff/sessions" `
        -Headers $staffHeaders -ContentType "application/json" `
        -Body (@{
            examId = $examId
            sessionName = "Nginx Demo $runId"
            room = "Docker Local"
            startTime = $startTime.ToString("o")
            endTime = $endTime.ToString("o")
            maxCapacity = $Students
        } | ConvertTo-Json)
    $sessionId = [int]$session.sessionId

    $enrollment = Invoke-RestMethod -Method Post `
        -Uri "$BaseUrl/api/staff/sessions/$sessionId/enrollments" `
        -Headers $staffHeaders -ContentType "application/json" `
        -Body (@{ studentIds = @($studentIds) } | ConvertTo-Json -Depth 5)
    if ($enrollment.addedCount -ne $Students) {
        throw "Expected to enroll $Students students; added $($enrollment.addedCount)."
    }

    $started = Invoke-RestMethod -Method Put -Uri "$BaseUrl/api/staff/sessions/$sessionId" `
        -Headers $staffHeaders -ContentType "application/json" `
        -Body (@{ status = "IN_PROGRESS" } | ConvertTo-Json)

    Write-Host "  [ok] session=$sessionId da ghi danh $Students sinh vien"

    $fixture = [pscustomobject]@{
        createdAt = [DateTimeOffset]::Now.ToString("o")
        baseUrl = $BaseUrl
        database = "local PostgreSQL / $DatabaseName"
        courseId = $courseId
        questionId = $questionId
        examId = $examId
        userPrefix = $userPrefix
        userCount = $studentIds.Count
        graders = $Graders
        sessionId = $sessionId
        startTime = $started.startTime
        endTime = $started.endTime
    }
    $fixturePath = Join-Path $resultDirectory "fixture.json"
    $fixture | ConvertTo-Json -Depth 8 | Set-Content -Path $fixturePath
    Write-Host ""
    Write-Host "Fixture: $fixturePath" -ForegroundColor Green
    Write-Host "Session : $sessionId   Students: $Students   Prefix: $userPrefix" -ForegroundColor Green
}
finally {
    $ErrorActionPreference = $prevPref
}
