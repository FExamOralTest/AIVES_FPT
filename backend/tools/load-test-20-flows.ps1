<#
.SYNOPSIS
    AIVES - Load test 20 luồng chấm bài chạy đồng thời (demo test cho BR-17 / queue chấm bài).

.DESCRIPTION
    Script này tự động:
      1. Seed dữ liệu: teacher (STAFF) → course → câu hỏi (đã approve) → exam → ca thi (IN_PROGRESS).
      2. Tạo N sinh viên (mặc định 20), login và enroll tất cả vào cùng 1 ca thi.
      3. Chạy SONG SONG N luồng chấm bài: start → trả lời từng câu → finish (AI chấm cả bài).
      4. (Tuỳ chọn) Phase 2: upload audio lại vào từng câu trả lời để đẩy job vào hàng đợi
         Hangfire rồi poll lại kết quả — kiểm tra đường chấm bài bất đồng bộ (STT → Eval).
      5. In báo cáo: thời gian mỗi luồng, tổng thời gian, throughput, số câu lỗi.

    Mọi phụ thuộc ngoài (AI service, R2) đều KHÔNG bắt buộc:
      - Chế độ mặc định (text) chỉ cần Backend + AI service chạy được.
      - Phase 2 cần AI service có cấu hình R2 (để tải audio trong R2) và Backend có R2.

.PARAMETER BaseUrl
    Địa chỉ backend. Mặc định http://localhost:5116

.PARAMETER StudentCount
    Số luồng chấm bài chạy đồng thời. Mặc định 20.

.PARAMETER QuestionsPerStudent
    Số câu hỏi trong đề. Mặc định 3.

.PARAMETER AdminUser / AdminPassword
    Tài khoản ADMIN để tạo user/course.
    Mặc định: admin / Dev@12345 (được DatabaseSeeder tạo khi chạy ở Development với
    ASPNETCORE_ENVIRONMENT=Development và chưa set Admin:Password).

    LƯU Ý: nếu báo "Login thất bại (HTTP 401)", hãy chạy backend với biến môi trường
    Admin__Username / Admin__Password (xem README-load-test.md), hoặc truyền
    -AdminUser / -AdminPassword đúng tài khoản ADMIN bạn đã có.

.PARAMETER UseAudio
    Bật chế độ có audio: sinh viên nộp file âm thanh thật (cần -AudioPath) và chạy thêm
    Phase 2 (enqueue job Hangfire cho từng câu).

.PARAMETER AudioPath
    Đường dẫn file audio (wav/webm/mp3) dùng cho luồng nộp bài có âm thanh.

.EXAMPLE
    .\load-test-20-flows.ps1
    Chạy 20 luồng chấm bài song song bằng text.

.EXAMPLE
    .\load-test-20-flows.ps1 -StudentCount 50 -QuestionsPerStudent 5
    Chạy 50 luồng, mỗi luồng 5 câu.

.EXAMPLE
    .\load-test-20-flows.ps1 -UseAudio -AudioPath "D:\audio\cau-tra-loi.wav"
    Chạy kèm audio + Phase 2 kiểm tra hàng đợi Hangfire.
#>

[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5116",
    [int]$StudentCount = 20,
    [int]$QuestionsPerStudent = 3,
    [string]$AdminUser = "admin",
    [string]$AdminPassword = "Dev@12345",
    [string]$TeacherUser = "loadteacher",
    [string]$TeacherPassword = "Load@12345",
    [switch]$UseAudio,
    [string]$AudioPath,
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = "Stop"
$script:Stamp = Get-Date -Format "yyMMddHHmmss"

# ────────────────────────────────────────────────────────────────────────
#  Helpers
# ────────────────────────────────────────────────────────────────────────

function Write-Step { param([string]$Text) Write-Host "`n=== $Text ===" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Text) Write-Host "  [OK]   $Text" -ForegroundColor Green }
function Write-Warn { param([string]$Text) Write-Host "  [WARN] $Text" -ForegroundColor Yellow }
function Write-Err  { param([string]$Text) Write-Host "  [FAIL] $Text" -ForegroundColor Red }

function Invoke-Api {
    <#
      Gọi API và KHÔNG throw khi lỗi — trả về @{ Ok; Status; Body } để script xử lý tiếp.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [string]$ContentType = "application/json"
    )

    $uri = "$BaseUrl$Path"
    $params = @{
        Method      = $Method
        Uri         = $uri
        Headers     = $Headers
        TimeoutSec  = 120
        ErrorAction = "Stop"
    }
    if ($null -ne $Body) {
        if ($Body -is [string]) {
            $params["Body"] = $Body
            $params["ContentType"] = $ContentType
        } else {
            $params["Body"] = ($Body | ConvertTo-Json -Depth 12)
            $params["ContentType"] = "application/json; charset=utf-8"
        }
    }

    try {
        $resp = Invoke-RestMethod @params
        return @{ Ok = $true; Status = 200; Body = $resp }
    } catch {
        $status = 0
        $payload = $null
        if ($_.Exception.PSObject.Properties["Response"] -and $_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $raw = $reader.ReadToEnd()
                $reader.Close()
                if ($raw) { $payload = $raw | ConvertFrom-Json }
            } catch { $payload = $_.Exception.Message }
        } else {
            $payload = $_.Exception.Message
        }
        return @{ Ok = $false; Status = $status; Body = $payload }
    }
}

function New-AuthHeader { param([string]$Token) return @{ Authorization = "Bearer $Token" } }

function Login {
    param([string]$Identifier, [string]$Password)
    $r = Invoke-Api -Method POST -Path "/api/auth/login" -Body @{ identifier = $Identifier; password = $Password }
    if (-not $r.Ok) { throw "Login '$Identifier' thất bại (HTTP $($r.Status)): $($r.Body | ConvertTo-Json -Compress -Depth 4)" }
    return $r.Body
}

function New-WavSilence {
    <# Tạo file WAV 1 giây im lặng (chỉ hữu ích cho STT mock). #>
    param([string]$Path, [double]$Seconds = 1.0, [int]$Rate = 16000)
    $samples = [int]($Rate * $Seconds)
    $dataSize = $samples * 2
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([char[]]"RIFF"); $bw.Write([int](36 + $dataSize)); $bw.Write([char[]]"WAVE")
    $bw.Write([char[]]"fmt "); $bw.Write([int]16); $bw.Write([int16]1); $bw.Write([int16]1)
    $bw.Write([int]$Rate); $bw.Write([int]($Rate * 2)); $bw.Write([int16]2); $bw.Write([int16]16)
    $bw.Write([char[]]"data"); $bw.Write([int]$dataSize)
    for ($i = 0; $i -lt $samples; $i++) { $bw.Write([int16]0) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($Path, $ms.ToArray())
    $bw.Close(); $ms.Close()
    return $Path
}

# ────────────────────────────────────────────────────────────────────────
#  Prelude — định nghĩa helper + biến dùng chung để "tiêm" vào mỗi runspace.
#  (RunspacePool không kế thừa scope cha → phải nạp lại thủ công.)
# ────────────────────────────────────────────────────────────────────────

$script:ApiHelperSource   = "function Invoke-Api {`n" + (Get-Command Invoke-Api).Definition + "`n}"
$script:AuthHeaderSource = "function New-AuthHeader {`n" + (Get-Command New-AuthHeader).Definition + "`n}"
$script:LoginSource      = "function Login {`n" + (Get-Command Login).Definition + "`n}"

$script:ParallelPrelude = @(
    "`$BaseUrl = '$($BaseUrl -replace "'", "''")'",
    "`$script:Stamp = '$script:Stamp'",
    $script:ApiHelperSource,
    $script:AuthHeaderSource,
    $script:LoginSource
) -join "`n"

# Chạy N scriptblock song song bằng RunspacePool (PowerShell 5.1 compatible).
#
# LƯU Ý QUAN TRỌNG:
#   1. RunspacePool KHÔNG kế thừa function/variable của scope cha → helper
#      (Invoke-Api, New-AuthHeader, Login) và biến (BaseUrl, Stamp) phải được
#      "tiêm" vào từng runspace qua $Prelude.
#   2. PowerShell.AddScript() nối các đoạn script thành MỘT script duy nhất →
#      block `param(...)` chỉ được nằm ở đầu. Vì vậy ta gộp Prelude +
#      ScriptBlock thành 1 chuỗi, đặt `param($__arg)` ở đầu, và scriptblock
#      nhận dữ liệu qua biến `$__arg`.
function Invoke-Parallel {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$ScriptBlock,
        [Parameter(Mandatory = $true)][object[]]$ArgumentList,
        [string]$Prelude = "",
        [int]$ThrottleLimit = 0
    )
    if ($ThrottleLimit -le 0) { $ThrottleLimit = $ArgumentList.Count }

    $combined = "param(`$__arg)`n" + $Prelude + "`n" + $ScriptBlock.ToString()

    $pool = [runspacefactory]::CreateRunspacePool(1, $ThrottleLimit)
    $pool.Open()
    $jobs = @()

    foreach ($arg in $ArgumentList) {
        $ps = [powershell]::Create()
        $ps.RunspacePool = $pool
        $null = $ps.AddScript($combined)
        $null = $ps.AddArgument($arg)
        $jobs += [pscustomobject]@{ PS = $ps; Handle = $ps.BeginInvoke() }
    }

    $results = @()
    foreach ($job in $jobs) {
        try {
            $out = $job.PS.EndInvoke($job.Handle)
            $results += ,@($out)
        } catch {
            $results += ,@([pscustomobject]@{ Error = $_.Exception.Message })
        } finally {
            $job.PS.Dispose()
        }
    }
    $pool.Close(); $pool.Dispose()
    return $results
}

# ────────────────────────────────────────────────────────────────────────
#  0. Health check
# ────────────────────────────────────────────────────────────────────────

Write-Step "Kiểm tra backend tại $BaseUrl"
$health = Invoke-Api -Method GET -Path "/api/health/db"
if (-not $health.Ok) {
    Write-Err "Không kết nối được backend. Hãy chạy run-all.bat hoặc docker compose up trước."
    Write-Err "Chi tiết: $($health.Body)"
    exit 1
}
Write-Ok "Backend OK — $($health.Body.Status), DB connected = $($health.Body.DatabaseConnected)"

# ────────────────────────────────────────────────────────────────────────
#  1. Seed dữ liệu
# ────────────────────────────────────────────────────────────────────────

Write-Step "Đăng nhập ADMIN"
$admin = Login -Identifier $AdminUser -Password $AdminPassword
$adminToken = $admin.AccessToken
$adminH = New-AuthHeader $adminToken
Write-Ok "Admin '$($admin.User.Username)' (id=$($admin.User.UserId))"

Write-Step "Tạo giảng viên (STAFF)"
$teacherUsername = "$TeacherUser$($script:Stamp.Substring($script:Stamp.Length - 6))"
$teacherPassword = $TeacherPassword
$r = Invoke-Api -Method POST -Path "/api/auth/admin/create-user" -Headers $adminH -Body @{
    username = $teacherUsername; fullName = "Load Test Teacher $script:Stamp"
    email    = "$teacherUsername@aives.test"; password = $teacherPassword; role = "STAFF"
}
if (-not $r.Ok) {
    # User đã tồn tại từ lần chạy trước → thử login.
    $teacher = Login -Identifier $teacherUsername -Password $teacherPassword
} else {
    $teacher = $r.Body
}
$teacherToken = (Login -Identifier $teacherUsername -Password $teacherPassword).AccessToken
$teacherH = New-AuthHeader $teacherToken
$teacherId = if ($teacher.User) { $teacher.User.UserId } else { $teacher.userId }
Write-Ok "Teacher '$teacherUsername' (id=$teacherId)"

Write-Step "Tạo course + gán giảng viên"
$courseCode = "LD$($script:Stamp.Substring($script:Stamp.Length - 6))"
$r = Invoke-Api -Method POST -Path "/api/admin/courses" -Headers $adminH -Body @{
    courseCode = $courseCode; courseName = "Load Test $script:Stamp"; teacherId = $teacherId
}
if (-not $r.Ok) { Write-Err "Tạo course thất bại: $($r.Body | ConvertTo-Json -Compress)"; exit 1 }
$courseId = $r.Body.courseId
$r = Invoke-Api -Method PUT -Path "/api/admin/courses/$courseId/assign-teacher" -Headers $adminH -Body @{ teacherId = $teacherId }
if (-not $r.Ok) { Write-Err "Gán giảng viên thất bại: $($r.Body | ConvertTo-Json -Compress)"; exit 1 }
Write-Ok "Course id=$courseId code=$courseCode"

Write-Step "Tạo $QuestionsPerStudent câu hỏi + approve"
$questionIds = @()
for ($i = 1; $i -le $QuestionsPerStudent; $i++) {
    $r = Invoke-Api -Method POST -Path "/api/courses/$courseId/questions" -Headers $teacherH -Body @{
        questionText      = "Câu $i — Trình bày định nghĩa và vai trò của chủ đề $i trong môn học."
        topic             = "Chủ đề $i"
        sampleAnswer      = "Câu trả lời mẫu cho chủ đề ${i}: khái niệm cốt lõi, đặc điểm, ví dụ minh hoạ và ứng dụng thực tế."
        learningOutcome   = "Sinh viên hiểu và trình bày được chủ đề $i."
        level             = 1
        maxScore          = 10
        isFollowUpAllowed = $false
        expectedKeywords  = @("chủ đề $i")
        rubric            = @(@{ description = "Nêu đúng định nghĩa của chủ đề $i"; maxScore = 5 },
                              @{ description = "Đưa ra ví dụ minh hoạ về chủ đề $i"; maxScore = 5 })
    }
    if (-not $r.Ok) { Write-Err "Tạo câu $i thất bại: $($r.Body | ConvertTo-Json -Compress)"; exit 1 }
    $qid = $r.Body.questionId
    $ra = Invoke-Api -Method POST -Path "/api/courses/$courseId/questions/$qid/approve" -Headers $teacherH
    if (-not $ra.Ok) { Write-Warn "Approve câu $qid thất bại: $($ra.Body | ConvertTo-Json -Compress)" }
    $questionIds += $qid
}
Write-Ok "Đã tạo $($questionIds.Count) câu hỏi đã approve: $($questionIds -join ', ')"

Write-Step "Tạo đề thi"
$examItems = @()
foreach ($qid in $questionIds) { $examItems += @{ questionId = $qid; questionScore = 10 } }
$r = Invoke-Api -Method POST -Path "/api/exams" -Headers $teacherH -Body @{
    title = "Đề load test $script:Stamp"; courseId = $courseId
    durationMinutes = 60; maxQuestions = $questionIds.Count; questions = $examItems
}
if (-not $r.Ok) { Write-Err "Tạo đề thất bại: $($r.Body | ConvertTo-Json -Compress)"; exit 1 }
$examId = $r.Body.examId
Write-Ok "Exam id=$examId code=$($r.Body.examCode)"

Write-Step "Tạo ca thi (IN_PROGRESS)"
$now = Get-Date
$r = Invoke-Api -Method POST -Path "/api/staff/sessions" -Headers $teacherH -Body @{
    examId = $examId; sessionName = "Ca load test $script:Stamp"; room = "LOADTEST"
    # StartTime: -2 phút (đã mở ca, nhưng vẫn trong giới hạn "không quá 5 phút trong quá khứ" của StaffService)
    startTime = $now.AddMinutes(-2).ToUniversalTime().ToString("o"); endTime = $now.AddHours(3).ToUniversalTime().ToString("o")
    maxCapacity = 500
}
if (-not $r.Ok) { Write-Err "Tạo ca thi thất bại: $($r.Body | ConvertTo-Json -Compress)"; exit 1 }
$sessionId = $r.Body.sessionId
$sessionCode = $r.Body.sessionCode
$r = Invoke-Api -Method PUT -Path "/api/staff/sessions/$sessionId" -Headers $teacherH -Body @{ status = "IN_PROGRESS" }
if (-not $r.Ok) { Write-Err "Mở ca thi thất bại: $($r.Body | ConvertTo-Json -Compress)"; exit 1 }
Write-Ok "Session id=$sessionId code=$sessionCode status=IN_PROGRESS"

# ────────────────────────────────────────────────────────────────────────
#  2. Tạo + đăng nhập N sinh viên
# ────────────────────────────────────────────────────────────────────────

Write-Step "Tạo $StudentCount sinh viên + đăng nhập"
$students = @()
for ($i = 1; $i -le $StudentCount; $i++) {
    $u = "loadstu${i}_$($script:Stamp.Substring($script:Stamp.Length - 6))"
    $p = "Load@12345"
    $r = Invoke-Api -Method POST -Path "/api/auth/admin/create-user" -Headers $adminH -Body @{
        username = $u; fullName = "Load Student $i"; email = "$u@aives.test"; password = $p; role = "STUDENT"
    }
    if (-not $r.Ok) { Write-Warn "Tạo SV $u thất bại: $($r.Body | ConvertTo-Json -Compress)"; continue }
    $students += [pscustomobject]@{ Id = $r.Body.userId; Username = $u; Password = $p }
}
Write-Ok "Đã tạo $($students.Count) sinh viên"

Write-Step "Enroll toàn bộ sinh viên vào ca thi"
$r = Invoke-Api -Method POST -Path "/api/staff/sessions/$sessionId/enrollments" -Headers $teacherH -Body @{
    studentIds = @($students | ForEach-Object { $_.Id })
}
if (-not $r.Ok) { Write-Err "Bulk enroll thất bại: $($r.Body | ConvertTo-Json -Compress)"; exit 1 }
Write-Ok "Enrolled: added=$($r.Body.addedCount) skipped=$($r.Body.skippedCount)"

# ────────────────────────────────────────────────────────────────────────
#  3. Chuẩn bị audio (nếu bật UseAudio)
# ────────────────────────────────────────────────────────────────────────

$audioFile = $null
if ($UseAudio) {
    Write-Step "Chuẩn bị file audio"
    if ($AudioPath -and (Test-Path $AudioPath)) {
        $audioFile = (Resolve-Path $AudioPath).Path
        Write-Ok "Dùng file: $audioFile"
    } else {
        $tmp = Join-Path ([System.IO.Path]::GetTempPath()) "aives-load-$($script:Stamp).wav"
        $audioFile = New-WavSilence -Path $tmp -Seconds 2
        Write-Warn "Không có -AudioPath → dùng WAV im lặng 2s. Nếu STT thật (không mock) sẽ trả EMPTY_TRANSCRIPT."
    }
}

# ────────────────────────────────────────────────────────────────────────
#  4. Phase 1 — 20 luồng chấm bài chạy đồng thời
# ────────────────────────────────────────────────────────────────────────

Write-Step "PHASE 1 — chạy $($students.Count) luồng chấm bài SONG SONG"
Write-Host "  Mỗi luồng: start → trả lời $($questionIds.Count) câu → finish (AI chấm toàn bài ca)" -ForegroundColor DarkGray

$flowScript = {
    $ctx = $__arg

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $log = New-Object System.Collections.Generic.List[string]
    $result = [pscustomobject]@{
        StudentId = $ctx.Student.Id; Username = $ctx.Student.Username
        Ok = $false; Message = ""; Answers = 0; Score = $null
        DurationMs = 0; Steps = @()
    }

    try {
        # --- Login ---
        $lr = Invoke-Api -Method POST -Path "/api/auth/login" -Body @{ identifier = $ctx.Student.Username; password = $ctx.Student.Password }
        if (-not $lr.Ok) { throw "Login thất bại: $($lr.Body)" }
        $h = New-AuthHeader $lr.Body.accessToken
        $log.Add("login")

        # --- Start session ---
        $sr = Invoke-Api -Method POST -Path "/api/student/viva-sessions/$($ctx.SessionId)/start" -Headers $h
        if (-not $sr.Ok) { throw "Start session thất bại: $($sr.Body)" }
        $log.Add("start")

        # --- Trả lời lần lượt các câu đã biết ---
        $answered = @{}
        foreach ($qid in $ctx.QuestionIds) {
            $isFollowUp = $false
            $guard = 0
            while ($guard -lt 6) {
                $guard++
                $text = "Câu trả lời của $($ctx.Student.Username) cho câu hỏi $qid. " +
                        "Khái niệm cốt lõi được trình bày rõ ràng, kèm ví dụ minh hoạ cụ thể và ứng dụng thực tế."

                if ($ctx.AudioFile) {
                    $boundary = "----aivesload$($script:Stamp)"
                    $body = @(
                        "--$boundary",
                        "Content-Disposition: form-data; name=""questionId""",
                        "",
                        "$qid",
                        "--$boundary",
                        "Content-Disposition: form-data; name=""textAnswer""",
                        "",
                        $text,
                        "--$boundary",
                        "Content-Disposition: form-data; name=""isFollowUp""",
                        "",
                        "$isFollowUp",
                        "--$boundary",
                        "Content-Disposition: form-data; name=""audioFile""; filename=""answer.wav""",
                        "Content-Type: audio/wav",
                        "",
                        "--$boundary--"
                    ) -join "`r`n"

                    $fileBytes = [System.IO.File]::ReadAllBytes($ctx.AudioFile)
                    $head = [System.Text.Encoding]::UTF8.GetBytes($body)
                    $sep = [System.Text.Encoding]::UTF8.GetBytes("`r`n")

                    $ms = New-Object System.IO.MemoryStream
                    $ms.Write($head, 0, $head.Length)
                    $ms.Write($fileBytes, 0, $fileBytes.Length)
                    $ms.Write($sep, 0, $sep.Length)
                    $payload = $ms.ToArray(); $ms.Close()

                    $ar = Invoke-Api -Method POST -Path "/api/student/viva-sessions/$($ctx.SessionId)/submit-answer" `
                        -Headers $h -Body ([System.Text.Encoding]::UTF8.GetString($payload)) `
                        -ContentType "multipart/form-data; boundary=$boundary"
                } else {
                    # Endpoint yêu cầu multipart/form-data (không nhận application/json).
                    $boundary = "----aivesload$($script:Stamp)"
                    $mpBody = @(
                        "--$boundary",
                        "Content-Disposition: form-data; name=""questionId""",
                        "",
                        "$qid",
                        "--$boundary",
                        "Content-Disposition: form-data; name=""textAnswer""",
                        "",
                        $text,
                        "--$boundary",
                        "Content-Disposition: form-data; name=""isFollowUp""",
                        "",
                        "$isFollowUp",
                        "--$boundary--"
                    ) -join "`r`n"

                    $ar = Invoke-Api -Method POST -Path "/api/student/viva-sessions/$($ctx.SessionId)/submit-answer" `
                        -Headers $h -Body ([System.Text.Encoding]::UTF8.GetString([System.Text.Encoding]::UTF8.GetBytes($mpBody))) `
                        -ContentType "multipart/form-data; boundary=$boundary"
                }

                if (-not $ar.Ok) { throw "Nộp câu $qid thất bại (HTTP $($ar.Status)): $($ar.Body)" }
                $answered[$qid] = $true

                if ($ar.Body.shouldFollowUp -and -not $isFollowUp) {
                    $isFollowUp = $true
                    $log.Add("followup:$qid")
                    continue
                }
                break
            }
        }
        $result.Answers = $answered.Count
        $log.Add("submit")

        # --- Finish (AI chấm toàn bài ca) ---
        $fr = Invoke-Api -Method POST -Path "/api/student/viva-sessions/$($ctx.SessionId)/finish" -Headers $h
        if (-not $fr.Ok) { throw "Finish thất bại: $($fr.Body)" }
        $log.Add("finish")

        $result.Ok = $true
        $result.Score = $fr.Body.aiScore
        $result.Message = "AiEvaluationStatus=$($fr.Body.aiEvaluationStatus)"
    } catch {
        $result.Message = $_.Exception.Message
    }

    $sw.Stop()
    $result.DurationMs = $sw.ElapsedMilliseconds
    $result.Steps = $log.ToArray()
    return $result
}

$contexts = @()
foreach ($s in $students) {
    $contexts += [pscustomobject]@{
        Student    = $s
        SessionId  = $sessionId
        QuestionIds = $questionIds
        AudioFile  = $audioFile
    }
}

$totalSw = [System.Diagnostics.Stopwatch]::StartNew()
$flows = Invoke-Parallel -ScriptBlock $flowScript -ArgumentList $contexts -Prelude $script:ParallelPrelude
$totalSw.Stop()

Write-Host ""
Write-Host "  Kết quả từng luồng:" -ForegroundColor Cyan
$i = 1
$okCount = 0; $failCount = 0; $answerTotal = 0
$scoreSum = 0; $scoreCount = 0
foreach ($f in $flows) {
    if (-not $f) { continue }
    if ($f.Ok) {
        $okCount++
        $answerTotal += $f.Answers
        if ($null -ne $f.Score) { $scoreSum += [double]$f.Score; $scoreCount++ }
        Write-Host ("   {0,2}. [OK]   {1,-28} {2,6} ms  score={3}  ({4})" -f $i, $f.Username, $f.DurationMs, $f.Score, ($f.Steps -join ">")) -ForegroundColor Green
    } else {
        $failCount++
        Write-Host ("   {0,2}. [FAIL] {1,-28} {2,6} ms  {3}" -f $i, $f.Username, $f.DurationMs, $f.Message) -ForegroundColor Red
    }
    $i++
}

Write-Host ""
Write-Ok "Phase 1 xong: thành công=$okCount, lỗi=$failCount, tổng câu đã trả lời=$answerTotal, tổng thời gian=$($totalSw.ElapsedMilliseconds) ms"
if ($totalSw.Elapsed.TotalSeconds -gt 0) {
    Write-Ok "Throughput: $([math]::Round($answerTotal / $totalSw.Elapsed.TotalSeconds, 2)) câu/giây"
}
if ($scoreCount -gt 0) {
    Write-Ok "Điểm AI trung bình: $([math]::Round($scoreSum / $scoreCount, 2)) / $($questionIds.Count * 10) (tổng điểm toàn bộ đề)"
}

# ────────────────────────────────────────────────────────────────────────
#  5. Phase 2 — đẩy job chấm bài bất đồng bộ vào Hangfire
# ────────────────────────────────────────────────────────────────────────

if ($UseAudio) {
    Write-Step "PHASE 2 — enqueue job chấm bài bất đồng bộ (Hangfire) cho từng câu"

    Write-Host "  Lấy danh sách answer từ hệ thống..." -ForegroundColor DarkGray
    $rr = Invoke-Api -Method GET -Path "/api/results/session/$sessionId" -Headers $adminH
    $answerMap = @{}
    if ($rr.Ok) {
        foreach ($res in $rr.Body) {
            foreach ($a in $res.answers) { $answerMap["$($res.studentId)"] = @($answerMap["$($res.studentId)"]) + $a.answerId }
        }
    }
    if ($answerMap.Count -eq 0) {
        Write-Warn "Không lấy được danh sách answer (HTTP $($rr.Status)). Bỏ qua Phase 2."
    } else {
        $uploadArgs = @()
        foreach ($s in $students) {
            $ids = $answerMap["$($s.Id)"]
            if (-not $ids) { continue }
            foreach ($aid in $ids) {
                $uploadArgs += [pscustomobject]@{ AnswerId = $aid; Username = $s.Username; Password = $s.Password }
            }
        }
        Write-Host "  Upload audio cho $($uploadArgs.Count) answer (mỗi upload = 1 job Hangfire)" -ForegroundColor DarkGray

        $uploadScript = {
            $u = $__arg
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $lr = Invoke-Api -Method POST -Path "/api/auth/login" -Body @{ identifier = $u.Username; password = $u.Password }
            if (-not $lr.Ok) { return [pscustomobject]@{ AnswerId = $u.AnswerId; Ok = $false; JobId = $null; Message = "login fail" } }
            $h = New-AuthHeader $lr.Body.accessToken

            $fileBytes = [System.IO.File]::ReadAllBytes($u.AudioFile)
            $boundary = "----aivesup$($u.AnswerId)"
            $body = @(
                "--$boundary",
                "Content-Disposition: form-data; name=""audioFile""; filename=""answer.wav""",
                "Content-Type: audio/wav",
                "",
                "--$boundary--"
            ) -join "`r`n"
            $head = [System.Text.Encoding]::UTF8.GetBytes($body)
            $sep = [System.Text.Encoding]::UTF8.GetBytes("`r`n")
            $ms = New-Object System.IO.MemoryStream
            $ms.Write($head, 0, $head.Length)
            $ms.Write($fileBytes, 0, $fileBytes.Length)
            $ms.Write($sep, 0, $sep.Length)
            $payload = $ms.ToArray(); $ms.Close()

            $ar = Invoke-Api -Method POST -Path "/api/answers/$($u.AnswerId)/audio" -Headers $h `
                -Body ([System.Text.Encoding]::UTF8.GetString($payload)) `
                -ContentType "multipart/form-data; boundary=$boundary"
            $sw.Stop()
            return [pscustomobject]@{
                AnswerId = $u.AnswerId; Ok = $ar.Ok; JobId = $ar.Body.jobId
                Message = if ($ar.Ok) { "queued" } else { "$($ar.Status): $($ar.Body)" }
                DurationMs = $sw.ElapsedMilliseconds
            }
        }

        # Gắn AudioFile vào mỗi tham số
        foreach ($a in $uploadArgs) { $a | Add-Member -NotePropertyName AudioFile -NotePropertyValue $audioFile -Force }

        $uploadResults = Invoke-Parallel -ScriptBlock $uploadScript -ArgumentList $uploadArgs -Prelude $script:ParallelPrelude
        $queued = 0; $uploadFailed = 0
        foreach ($u in $uploadResults) {
            if (-not $u) { continue }
            if ($u.Ok) { $queued++ } else { $uploadFailed++; Write-Warn "answer $($u.AnswerId): $($u.Message)" }
        }
        Write-Ok "Đã enqueue $queued job (lỗi $uploadFailed). Theo dõi tại $BaseUrl/hangfire"

        Write-Host "  Chờ job chạy xong (timeout ${TimeoutSeconds}s)..." -ForegroundColor DarkGray
        $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
        $lastState = ""
        while ((Get-Date) -lt $deadline) {
            $rr = Invoke-Api -Method GET -Path "/api/results/session/$sessionId" -Headers $adminH
            if ($rr.Ok) {
                $total = 0; $evaluated = 0; $error = 0; $processing = 0
                foreach ($res in $rr.Body) {
                    foreach ($a in $res.answers) {
                        $total++
                        switch ($a.status) {
                            "EVALUATED" { $evaluated++ }
                            "ERROR"     { $error++ }
                            default     { $processing++ }
                        }
                    }
                }
                $state = "total=$total evaluated=$evaluated error=$error processing=$processing"
                if ($state -ne $lastState) { Write-Host "    $state" -ForegroundColor DarkGray; $lastState = $state }
                if ($total -gt 0 -and ($evaluated + $error) -eq $total) { break }
            }
            Start-Sleep -Seconds 5
        }
        Write-Ok "Phase 2 xong. Trạng thái cuối: $lastState"
    }
}

# ────────────────────────────────────────────────────────────────────────
#  6. Tổng kết + hướng dẫn quan sát
# ────────────────────────────────────────────────────────────────────────

Write-Step "Tổng kết"
Write-Host "  Base URL        : $BaseUrl"
Write-Host "  Course id       : $courseId"
Write-Host "  Exam id         : $examId"
Write-Host "  Session id      : $sessionId   code=$sessionCode"
Write-Host "  Số luồng chấm   : $StudentCount"
Write-Host "  Câu hỏi / luồng : $QuestionsPerStudent"
Write-Host ""
Write-Host "  Xem kết quả chấm (ADMIN):" -ForegroundColor Cyan
Write-Host "    GET $BaseUrl/api/results/session/$sessionId"
Write-Host "  Xem hàng đợi job:" -ForegroundColor Cyan
Write-Host "    $BaseUrl/hangfire"
Write-Host "  Xem kết quả từng sinh viên (token STUDENT):" -ForegroundColor Cyan
Write-Host "    GET $BaseUrl/api/student/results/$sessionId"
Write-Host ""
if ($failCount -gt 0) {
    Write-Warn "$failCount luồng bị lỗi — xem log backend (cửa sổ 'AIVES - Backend API') để biết nguyên nhân."
} else {
    Write-Ok "Tất cả $okCount luồng chấm bài đã chạy thành công."
}
