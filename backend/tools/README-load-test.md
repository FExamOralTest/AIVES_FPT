# Demo test — 20 luồng chấm bài chạy đồng thời

Script: `backend/tools/load-test-20-flows.ps1`

Kịch bản được mô phỏng đúng luồng thật của hệ thống:

```
20 sinh viên (STUDENT) chạy SONG SONG
   → POST /api/student/viva-sessions/{id}/start
   → POST /api/student/viva-sessions/{id}/submit-answer   (3 câu mỗi người)
   → POST /api/student/viva-sessions/{id}/finish            (AI chấm toàn bộ ca)
   → GET  /api/results/session/{id}                        (kiểm tra kết quả trong DB)
```

Tổng cộng **20 luồng / 60 câu được chấm cùng lúc** — đúng bài toán "chấm bài 1 lúc".

---

## 1. Yêu cầu trước khi chạy

| Thành phần | Trạng thái | Ghi chú |
|---|---|---|
| Backend (`Aives.Api`) | Đang chạy tại `http://localhost:5116` | Bắt buộc |
| AI service (FastAPI) | Đang chạy tại `http://localhost:8000` | Bắt buộc |
| DB (Neon Postgres) | Tự động dùng `appsettings.Development.json` | Cần kết nối được |

Kiểm tra nhanh:

```powershell
Invoke-RestMethod http://localhost:5116/api/health/db
Invoke-RestMethod http://localhost:8000/health
```

---

## 2. Chạy nhanh (phổ biến nhất)

```powershell
cd C:\Users\thinh\Documents\GitHub\AIVES_FPT
powershell -NoProfile -ExecutionPolicy Bypass -File backend\tools\load-test-20-flows.ps1 `
  -BaseUrl http://localhost:5116 `
  -AdminUser loadadmin `
  -AdminPassword "Load@12345"
```

Kết quả mẫu (đã chạy thật, 20/20 thành công):

```
  Kết quả từng luồng:
    1. [OK]   loadstu1_105912                5996 ms  score=22.5  (login>start>submit>finish)
    2. [OK]   loadstu2_105912                6000 ms  score=22.5  (login>start>submit>finish)
   ...
   20. [OK]   loadstu20_105912               5509 ms  score=22.5  (login>start>submit>finish)

  [OK]   Phase 1 xong: thành công=20, lỗi=0, tổng câu đã trả lời=60, tổng thời gian=6180 ms
  [OK]   Throughput: 9.71 câu/giây
```

---

## 3. Tài khoản ADMIN cho load test

Script cần 1 tài khoản ADMIN để tạo user / course / ca thi.

### Cách A — dùng admin dev mặc định

`DatabaseSeeder` tự tạo user `admin` với mật khẩu `Dev@12345` khi:

- `ASPNETCORE_ENVIRONMENT=Development`
- **không** set `Admin:Password` trong config

Nếu DB Neon đã có sẵn user `admin` với mật khẩu khác, bạn **không** đăng nhập được.
Khi đó dùng Cách B.

### Cách B — tạo admin riêng cho load test (khuyến nghị)

```powershell
cd C:\Users\thinh\Documents\GitHub\AIVES_FPT\backend\Aives.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Admin__Username      = "loadadmin"
$env:Admin__Password      = "Load@12345"
$env:Admin__Email         = "loadadmin@aives.test"
dotnet run --no-launch-profile --urls http://localhost:5116
```

Rồi chạy script với `-AdminUser loadadmin -AdminPassword "Load@12345"`.

> `SeedAdminAsync` là idempotent — nếu user đã tồn tại thì **không** ghi đè mật khẩu.
> Muốn đổi mật khẩu thì dùng ADMIN có sẵn: `PUT /api/auth/admin/users/{id}`.

---

## 4. Các tham số

| Tham số | Mặc định | Ý nghĩa |
|---|---|---|
| `-BaseUrl` | `http://localhost:5116` | Địa chỉ backend |
| `-StudentCount` | `20` | Số luồng chấm bài chạy song song |
| `-QuestionsPerStudent` | `3` | Số câu hỏi trong đề (mỗi luồng trả lời hết) |
| `-AdminUser` / `-AdminPassword` | `admin` / `Dev@12345` | Tài khoản ADMIN |
| `-TeacherUser` / `-TeacherPassword` | `loadteacher` / `Load@12345` | Tài khoản STAFF tạo (random suffix mỗi lần chạy) |
| `-UseAudio` | off | Bật chế độ nộp audio thật + Phase 2 (enqueue job Hangfire) |
| `-AudioPath` | — | File wav/webm/mp3 để nộp (bắt buộc nếu `-UseAudio`) |
| `-TimeoutSeconds` | `900` | Timeout chờ job Hangfire ở Phase 2 |

### Chạy 50 luồng / 5 câu

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File backend\tools\load-test-20-flows.ps1 `
  -BaseUrl http://localhost:5116 -AdminUser loadadmin -AdminPassword "Load@12345" `
  -StudentCount 50 -QuestionsPerStudent 5
```

### Chạy kèm audio (kiểm tra hàng đợi Hangfire)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File backend\tools\load-test-20-flows.ps1 `
  -BaseUrl http://localhost:5116 -AdminUser loadadmin -AdminPassword "Load@12345" `
  -UseAudio -AudioPath "D:\audio\cau-tra-loi.wav"
```

Phase 2 sẽ upload lại audio cho từng câu trả lời → mỗi upload tạo **1 job Hangfire**
→ poll `/api/results/session/{id}` cho tới khi mọi answer sang `EVALUATED`.

> AI service hiện chạy `STT_PROVIDER=mock` → transcript là câu mẫu.
> Muốn STT thật: đổi `STT_PROVIDER=faster_whisper` trong `ai-service/.env`
> và cài `faster-whisper` (đã có trong `requirements.txt`).

---

## 5. Kiểm tra kết quả sau khi chạy

Script in ra `Session id` / `Course id` / `Exam id`. Dùng các URL đó:

| Mục đích | URL |
|---|---|
| Kết quả chấm (ADMIN/EXAMINER) | `GET http://localhost:5116/api/results/session/{sessionId}` |
| Dashboard hàng đợi job | `http://localhost:5116/hangfire` |
| Kết quả từng sinh viên (token STUDENT) | `GET http://localhost:5116/api/student/results/{sessionId}` |
| Swagger | `http://localhost:5116/swagger` |

Ví dụ kiểm tra bằng PowerShell:

```powershell
$tok = (Invoke-RestMethod -Uri http://localhost:5116/api/auth/login -Method Post `
  -Body (@{identifier='loadadmin';password='Load@12345'} | ConvertTo-Json) `
  -ContentType 'application/json').AccessToken

$r = Invoke-RestMethod -Uri http://localhost:5116/api/results/session/27 `
  -Headers @{ Authorization = "Bearer $tok" }

$r.Count                                    # => 20 kết quả
$r[0].status                                # => PENDING_REVIEW
$r[0].aiEvaluationStatus                    # => SUCCEEDED
$r[0].aiScore                               # => 22.5
$r[0].answers | Select-Object answerId, questionId, status, aiScore
```

---

## 6. Test offline (không cần Backend / AI service / DB)

Nếu chỉ muốn kiểm tra logic chấm bài song song (không lên tới HTTP), dùng test
.NET đã có sẵn:

```powershell
cd C:\Users\thinh\Documents\GitHub\AIVES_FPT\backend
dotnet test Aives.Tests/Aives.Tests.csproj --filter "FullyQualifiedName~ConcurrentGradingLoadTests"
```

File: `backend/Aives.Tests/ConcurrentGradingLoadTests.cs` — 20 luồng × 3 câu,
dùng AI service giả trong RAM, assert:
- cả 60 answer → `EVALUATED`, có `ai_score` / `ai_feedback`
- `attempt_count == 1` (không retry thừa)
- lỗi retryable/fatal **không lan** sang luồng khác

---

## 7. Xử lý sự cố

| Triệu chứng | Nguyên nhân | Cách xử lý |
|---|---|---|
| `Login 'xxx' thất bại (HTTP 401)` | Sai thông tin ADMIN | Xem mục 3 (Cách B) |
| `Backend OK — ... DB connected = False` | DB không kết nối được | Kiểm tra `appsettings.Development.json` / mạng |
| `Không kết nối được backend` | Backend chưa chạy | Chạy backend trước |
| `Start session thất bại: Ca thi chưa mở` | Ca thi chưa tới giờ / chưa `IN_PROGRESS` | Script đã tự set `startTime = now - 2 phút` rồi `PUT status=IN_PROGRESS`; nếu vẫn lỗi, kiểm tra server lệch múi giờ |
| `Nộp câu X thất bại (HTTP 415)` | Gửi JSON cho endpoint multipart | Script đã dùng `multipart/form-data`; bản cũ dùng JSON |
| Điểm `0` hoặc `aiEvaluationStatus=FAILED` | AI service không trả lệch đúng contract | Xem log AI: `%TEMP%\aives-ai.err` |

---

## 8. Ghi chú kỹ thuật đã xử lý trong script

Script là **PowerShell 5.1** (đã test trên Windows). Ba điểm khó đã được xử lý:

1. **RunspacePool không kế thừa scope cha** → helper `Invoke-Api`, `New-AuthHeader`,
   `Login` và biến `$BaseUrl`, `$script:Stamp` được "tiêm" vào từng runspace
   qua `$script:ParallelPrelude`.
2. **`AddScript()` nối các đoạn thành 1 script duy nhất** → block `param(...)` chỉ
   được nằm ở đầu, nên `Invoke-Parallel` gộp `param($__arg)` + prelude + scriptblock
   thành một chuỗi; scriptblock nhận dữ liệu qua `$__arg`.
3. **`(Get-Command F).Definition` chỉ trả về body** → phải bọc lại thành
   `"function F {`n" + Definition + "`n}"` nếu muốn nạp vào runspace khác.

Ngoài ra: `startTime`/`endTime` phải là **UTC** (Postgres `timestamptz` không nhận
`DateTime.Kind=Local`), và `submit-answer` chỉ nhận `multipart/form-data`.
RunspacePool không kế thừa scope cha → helper phải "tiêm" vào từng runspace.
AddScript() nối thành 1 script → param() chỉ hợp lệ ở đầu → gộp thành param($__arg).
(Get-Command F).Definition chỉ trả body → phải bọc lại "function F {n" + ...`.
submit-answer chỉ nhận multipart/form-data, script gửi JSON → 415.
startTime phải UTC (timestamptz từ chối Kind=Local), và phải nằm trong [now-5p, now] để StaffService cho phép tạo ca.
Lưu ý: ai-service/.env đã đổi sang AI_PROVIDER=mock + STT_PROVIDER=mock để chạy offline không tốn tiền API — muốn STT thật thì đổi sang faster_whisper (chi tiết ở mục 4 của README).