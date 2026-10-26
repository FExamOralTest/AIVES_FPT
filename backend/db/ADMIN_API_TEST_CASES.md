# Test case — Admin APIs (chạy thẳng DB thật `neondb`)

> Endpoint `GET /api/admin/exams` **đã bỏ phân trang**: response chỉ còn `items` + `totalCount`.
> Tham số `page` / `pageSize` không còn tác dụng và được bỏ qua âm thầm.

---

## 0. Chuẩn bị

**0.1 — Chạy API**

Có một vấn đề đã biết: `Aives.Api/appsettings.json` chứa connection string placeholder
(`postgresql://user:password@host:5432/dbname`) và file này **ghi đè**
`appsettings.Development.json`. Phải override bằng biến môi trường, nếu không sẽ
lỗi `42P01: relation "users" does not exist`.

```powershell
cd backend\Aives.Api
$real = (Get-Content appsettings.Development.json -Raw | ConvertFrom-Json).ConnectionStrings.DefaultConnection
$env:ConnectionStrings__DefaultConnection = $real
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5199'
dotnet run
```

**0.2 — Lấy token ADMIN**

```powershell
$r = Invoke-RestMethod 'http://127.0.0.1:5199/api/auth/login' -Method Post `
    -Body '{"email":"admin@aives.edu.vn","password":"Admin@123"}' `
    -ContentType 'application/json'
$h = @{ Authorization = "Bearer $($r.accessToken)" }
```

`$h` này dùng cho toàn bộ test case dưới đây.

---

## 1. `GET /api/admin/exams`

### 1.1 Danh sách mặc định

| | |
|---|---|
| Request | `GET /api/admin/exams` |
| Expect | `200`, body có **đúng 2 key**: `items`, `totalCount` |
| Verify | `$r.PSObject.Properties.Name` → `items, totalCount` (không còn `page`, `pageSize`, `totalPages`) |
| Verify | `$r.items.Count -eq $r.totalCount` (không còn cắt trang) |

```powershell
$r = Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams' -Headers $h
$r | ConvertTo-Json -Depth 5
```

### 1.2 Mỗi item có đủ trường

Expect mỗi phần tử trong `items` có 15 field:

```powershell
$expected = 'examId','title','examCode','courseId','courseName','createdBy',
            'createdByName','durationMinutes','maxQuestions','questionCount',
            'sessionCount','examStatus','isActive','createdAt'
$missing = $expected | Where-Object { $_ -notin $r.items[0].PSObject.Properties.Name }
$missing   # rỗng = OK
```

### 1.3 Bộ lọc `courseId`

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?courseId=3' -Headers $h
```

Expect: mọi item có `courseId = 3`, và `items.Count < totalCount` của case 1.1.

### 1.4 Bộ lọc `examStatus`

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?examStatus=DRAFT' -Headers $h
```

Expect: mọi item có `examStatus = 'DRAFT'`.

Giá trị hợp lệ: `DRAFT`, `SCHEDULED`, `OPEN`, `CLOSED`, `CANCELLED`.
Gửi sai → `400`.

### 1.5 Bộ lọc `search` (title)

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?search=AI' -Headers $h
```

Expect: `title` chứa `AI` (không phân biệt hoa thường — dùng `ILIKE`).

### 1.6 Bộ lọc `search` (exam code)

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?search=EX-558544E0' -Headers $h
```

Expect: đúng 1 item, `examCode = 'EX-558544E0'`.

### 1.7 `includeInactive`

```powershell
# mặc định = true → gồm exam đã soft-delete
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams' -Headers $h
# chỉ exam đang hoạt động
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?includeInactive=false' -Headers $h
```

Expect: `includeInactive=false` có `totalCount ≤` case mặc định, và mọi item có `isActive = true`.

### 1.8 `isDescending`

```powershell
$a = Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?isDescending=true'  -Headers $h
$b = Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?isDescending=false' -Headers $h
$a.items[0].examId   # exam mới nhất
$b.items[0].examId   # exam cũ nhất
```

Expect: `a.items[0].examId -eq b.items[-1].examId`.

### 1.9 `page` / `pageSize` bị bỏ qua

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?page=0'      -Headers $h
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/exams?pageSize=9999' -Headers $h
```

Expect: **200**, `items.Count` giống hệt case 1.1. Trước đây 2 case này trả 400.
Đây là hành vi mới có chủ đích.

### 1.10 Validate sai

| Query | Expect |
|---|---|
| `?examStatus=BOGUS` | `400`, message liệt kê giá trị hợp lệ |
| `?courseId=0` | `400` |
| `?courseId=-1` | `400` |
| `?search=` (chuỗi 256 ký tự trở lên) | `400` |

### 1.11 Phân quyền

| Case | Expect |
|---|---|
| Không header `Authorization` | `401` |
| Token role `STUDENT` | `403` |
| Token role `STAFF` | `403` |
| Token role `ADMIN` | `200` |

---

## 2. `GET /api/admin/reports/summary`

### 2.1 Tổng quan mặc định

```powershell
$s = Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/reports/summary' -Headers $h
$s | ConvertTo-Json -Depth 3
```

Expect `200` + đủ 21 field:

```
totalSessions activeSessions completedSessions upcomingSessions cancelledSessions
totalEnrollments totalResults completedAttempts
averageAiScore averageFinalScore highestFinalScore lowestFinalScore
pendingReviewCount reviewedCount publishedCount submittedCount cancelledResultCount
processingAnswerCount evaluatedAnswerCount errorAnswerCount
fromDate toDate generatedAt
```

### 2.2 Tính nhất quán

```powershell
# tổng các trạng thái session = totalSessions
$s.activeSessions + $s.completedSessions + $s.upcomingSessions + $s.cancelledSessions -eq $s.totalSessions
```

Expect `True`. Tương tự cho `publishedCount + submittedCount + cancelledResultCount`.

### 2.3 Bộ lọc `courseId`

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/reports/summary?courseId=3' -Headers $h
```

Expect: `totalSessions` nhỏ hơn hoặc bằng case 2.1.

### 2.4 Bộ lọc thời gian

```powershell
# BẮT BUỘC ISO đầy đủ có timezone
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/reports/summary?fromDate=2026-10-01T00:00:00Z' -Headers $h
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/reports/summary?toDate=2026-12-31T23:59:59Z'   -Headers $h
```

| Query | Expect |
|---|---|
| `?fromDate=2026-10-01` (thiếu giờ) | `400` |
| `?fromDate=notadate` | `400` |
| `?fromDate=2026-12-31T00:00:00Z&toDate=2026-10-01T00:00:00Z` (ngược) | `400` |

### 2.5 Bộ lọc `sessionStatus`

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/reports/summary?sessionStatus=COMPLETED' -Headers $h
```

Giá trị hợp lệ: `SCHEDULED`, `IN_PROGRESS`, `COMPLETED`, `CANCELLED`
(Không có `OPEN`/`CLOSED` — enum khác `ExamStatus`.)

Gửi sai → `400` kèm danh sách giá trị hợp lệ.

### 2.6 `courseId` không tồn tại

```powershell
Invoke-RestMethod 'http://127.0.0.1:5199/api/admin/reports/summary?courseId=99999' -Headers $h
```

Expect: `200` với mọi counter bằng `0` (không phải `404`).

### 2.7 Phân quyền

Giống 1.11: không token → `401`, `STUDENT` → `403`, `ADMIN` → `200`.

---

## 3. Kiểm tra sau khi sửa

```powershell
# build + test
cd backend
dotnet build
dotnet test Aives.Tests\Aives.Tests.csproj --filter "FullyQualifiedName~AdminReportTests"

# không còn tham chiếu page/pageSize
rg "PageSize|\.Page\b|TotalPages" Aives.Application Aives.Infrastructure Aives.Api
```

Kết quả mong đợi: build 0 error, test pass, `rg` không ra kết quả nào
trong 3 project trên (`TotalPages`/`PageSize` còn ở `Aives.Tests` và feature
`teacher-questions` là đúng — đó là endpoint khác).