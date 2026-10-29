$ErrorActionPreference = 'Stop'
$base = 'http://localhost:5000'

function Login($identifier, $password) {
    $r = Invoke-RestMethod -Uri "$base/api/auth/login" -Method Post -Body (@{
        identifier = $identifier
        password   = $password
    } | ConvertTo-Json) -ContentType 'application/json'
    return $r
}

# ─────────────────────────────────────────────────────────────────────
# 1. ADMIN — tạo student mới
# ─────────────────────────────────────────────────────────────────────
$admin = Login 'demo-admin@aives.local' 'Demo@12345'
$ah = @{ Authorization = "Bearer $($admin.AccessToken)" }
Write-Host "[1] Admin: $($admin.User.Username) id=$($admin.User.UserId)"

$stamp = Get-Date -Format 'HHmmss'
$studentUsername = "fe_test_student_$stamp"
$studentEmail    = "$studentUsername@aives.local"
$studentPassword = 'Test@12345'

# Xoá nếu đã tồn tại (idempotent khi chạy lại script)
$existing = Invoke-RestMethod -Uri "$base/api/admin/users" -Headers $ah |
    Where-Object { $_.Username -eq $studentUsername }
if ($existing) {
    Invoke-RestMethod -Uri "$base/api/admin/users/$($existing.UserId)" -Method Delete -Headers $ah | Out-Null
    Write-Host "    (đã xoá user cũ trùng tên)"
}

$student = Invoke-RestMethod -Uri "$base/api/admin/users" -Method Post -Headers $ah -ContentType 'application/json' -Body (@{
    username = $studentUsername
    fullName = 'FE Test Student'
    email    = $studentEmail
    password = $studentPassword
    role     = 'STUDENT'
} | ConvertTo-Json)
Write-Host "[2] Student tạo mới: id=$($student.UserId) username=$($student.Username) email=$($student.Email)"

# ─────────────────────────────────────────────────────────────────────
# 2. TEACHER — tạo exam có câu hỏi sẵn + ca thi đang chạy
# ─────────────────────────────────────────────────────────────────────
$teacher = Login 'demo-teacher@aives.local' 'Demo@12345'
$th = @{ Authorization = "Bearer $($teacher.AccessToken)" }
Write-Host "[3] Teacher: $($teacher.User.Username) id=$($teacher.User.UserId)"

# Lấy câu hỏi có sẵn từ exam 18 (đã có 3 câu hỏi thật trong DB)
$sourceExam = Invoke-RestMethod -Uri "$base/api/exams/18" -Headers $th
if (-not $sourceExam.questions -or $sourceExam.questions.Count -eq 0) {
    throw "Exam nguồn không có câu hỏi."
}
Write-Host "[4] Nguồn câu hỏi: exam '$($sourceExam.title)' ($($sourceExam.questions.Count) câu)"
$sourceExam.questions | ForEach-Object {
    Write-Host ("      q{0}: {1}" -f $_.questionId, $_.questionText)
}

# Tạo exam MỚI dùng lại bộ câu hỏi đó (tránh đụng exam DRAFT có sẵn)
$examTitle = "FE Test - Viva Media Upload ($stamp)"
$newExam = Invoke-RestMethod -Uri "$base/api/exams" -Method Post -Headers $th -ContentType 'application/json' -Body (@{
    title           = $examTitle
    courseId        = $sourceExam.courseId
    durationMinutes = 30
    maxQuestions    = $sourceExam.questions.Count
    questions       = @($sourceExam.questions | ForEach-Object {
        @{ questionId = $_.questionId; questionScore = $_.questionScore }
    })
} | ConvertTo-Json -Depth 5)
Write-Host "[5] Exam tạo mới: id=$($newExam.ExamId) status=$($newExam.examStatus) q=$($newExam.questions.Count)"

# Tạo ca thi bắt đầu ngay, kết thúc sau 2 giờ
$now = [DateTime]::UtcNow
$session = Invoke-RestMethod -Uri "$base/api/exam-sessions" -Method Post -Headers $th -ContentType 'application/json' -Body (@{
    examId      = $newExam.ExamId
    sessionName = "FE Test Session ($stamp)"
    room        = 'ROOM-A'
    # Backend chỉ cho StartTime >= (now - 5 phút), nhưng enroll yêu cầu now >= StartTime.
    # => dùng now - 3 phút: vừa hợp lệ khi tạo, vừa cho phép enroll ngay.
    startTime   = $now.AddMinutes(-3).ToString('o')
    endTime     = $now.AddHours(2).ToString('o')
    maxCapacity = 30
} | ConvertTo-Json)
Write-Host "[6] Ca thi tạo: id=$($session.SessionId) code=$($session.SessionCode) status=$($session.Status)"

# Chuyển ca thi sang IN_PROGRESS
$upd = Invoke-RestMethod -Uri "$base/api/exam-sessions/$($session.SessionId)/status" -Method Put -Headers $th -ContentType 'application/json' -Body (@{
    status = 'IN_PROGRESS'
} | ConvertTo-Json)
Write-Host "[7] Ca thi -> $($upd.Status)"

# ─────────────────────────────────────────────────────────────────────
# 3. STUDENT — enroll vào ca thi
# ─────────────────────────────────────────────────────────────────────
$sLogin = Login $studentEmail $studentPassword
$sh = @{ Authorization = "Bearer $($sLogin.AccessToken)" }
Write-Host "[8] Student login OK: $($sLogin.User.Username) id=$($sLogin.User.UserId)"

$enroll = Invoke-RestMethod -Uri "$base/api/enroll/session" -Method Post -Headers $sh -ContentType 'application/json' -Body (@{
    sessionCode = $session.SessionCode
} | ConvertTo-Json)
Write-Host "[9] Enroll OK: enrollmentId=$($enroll.SessionEnrollmentId) status=$($enroll.Status)"

# ─────────────────────────────────────────────────────────────────────
# Kết quả
# ─────────────────────────────────────────────────────────────────────
Write-Host "`n================ THÔNG TIN ĐỂ TEST TRÊN FRONTEND ================"
Write-Host "  Student login : $studentEmail"
Write-Host "  Password      : $studentPassword"
Write-Host "  Session code  : $($session.SessionCode)"
Write-Host "  Session ID    : $($session.SessionId)"
Write-Host "  Exam ID       : $($newExam.ExamId)"
Write-Host "  Số câu hỏi    : $($newExam.questions.Count)"
Write-Host "  Thời lượng    : $($newExam.durationMinutes) phút"
Write-Host "  Trạng thái    : $($upd.Status)"
Write-Host "  Câu hỏi:"
$newExam.questions | ForEach-Object { Write-Host ("      q{0}: {1}" -f $_.questionId, $_.questionText) }
Write-Host "===================================================================="
