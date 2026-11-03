$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5000'

function Login($identifier, $password) {
    Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -Body (@{
        identifier = $identifier
        password   = $password
    } | ConvertTo-Json) -ContentType 'application/json'
}

# ── Teacher ─────────────────────────────────────────────────────────
$teacher = Login 'demo-teacher@aives.local' 'Demo@12345'
$th = @{ Authorization = "Bearer $($teacher.AccessToken)" }
Write-Host "[1] Teacher: $($teacher.User.Username) id=$($teacher.User.UserId)"

$sourceExam = Invoke-RestMethod -Uri "$base/api/exams/18" -Headers $th
if (-not $sourceExam.questions -or $sourceExam.questions.Count -eq 0) {
    throw "Exam nguồn không có câu hỏi."
}
Write-Host "[2] Nguồn câu hỏi: exam '$($sourceExam.title)' ($($sourceExam.questions.Count) câu)"
$sourceExam.questions | ForEach-Object { Write-Host ("      q{0}: {1}" -f $_.questionId, $_.questionText) }

$stamp = Get-Date -Format 'HHmmss'
$newExam = Invoke-RestMethod -Uri "$base/api/exams" -Method Post -Headers $th -ContentType 'application/json' -Body (@{
    title           = "Viva Media Upload Demo ($stamp)"
    courseId        = $sourceExam.courseId
    durationMinutes = 30
    maxQuestions    = $sourceExam.questions.Count
    questions       = @($sourceExam.questions | ForEach-Object {
        @{ questionId = $_.questionId; questionScore = $_.questionScore }
    })
} | ConvertTo-Json -Depth 5)
Write-Host "[3] Exam tạo mới: id=$($newExam.ExamId) q=$($newExam.questions.Count)"

$now = [DateTime]::UtcNow
$session = Invoke-RestMethod -Uri "$base/api/exam-sessions" -Method Post -Headers $th -ContentType 'application/json' -Body (@{
    examId      = $newExam.ExamId
    sessionName = "Viva Media Upload Session ($stamp)"
    room        = 'ROOM-A'
    startTime   = $now.AddMinutes(-3).ToString('o')
    endTime     = $now.AddHours(2).ToString('o')
    maxCapacity = 30
} | ConvertTo-Json)
Write-Host "[4] Ca thi: id=$($session.SessionId) code=$($session.SessionCode)"

$upd = Invoke-RestMethod -Uri "$base/api/exam-sessions/$($session.SessionId)/status" -Method Put -Headers $th -ContentType 'application/json' -Body (@{
    status = 'IN_PROGRESS'
} | ConvertTo-Json)
Write-Host "[5] Ca thi -> $($upd.Status)"

# ── Student demo ────────────────────────────────────────────────────
$sLogin = Login 'demo-student@aives.local' 'Demo@12345'
$sh = @{ Authorization = "Bearer $($sLogin.AccessToken)" }
Write-Host "[6] Student login OK: $($sLogin.User.Username) id=$($sLogin.User.UserId)"

$enroll = Invoke-RestMethod -Uri "$base/api/enroll/session" -Method Post -Headers $sh -ContentType 'application/json' -Body (@{
    sessionCode = $session.SessionCode
} | ConvertTo-Json)
Write-Host "[7] Enroll OK: enrollmentId=$($enroll.SessionEnrollmentId) status=$($enroll.Status)"

Write-Host "`n================ THÔNG TIN DEMO ================"
Write-Host "  URL        : http://localhost:5173/student/viva?sessionId=$($session.SessionId)"
Write-Host "  Login      : demo-student@aives.local / Demo@12345"
Write-Host "  Session ID : $($session.SessionId)"
Write-Host "  Session code: $($session.SessionCode)"
Write-Host "  Số câu hỏi : $($newExam.questions.Count)"
Write-Host "=============================================="
