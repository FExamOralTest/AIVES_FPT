# AIVES – Vấn đề API Teacher cần Backend xử lý

> **Từ:** Frontend (Huyền) · **Gửi:** Backend (Hưng, Thịnh, Bắc), AI (Trí, Hữu)
> **Ngày:** 2026-10-11 · **Nhánh kiểm tra:** `origin/dev` @ `c9f4acd` (fix_conflit)
> **Cách kiểm tra:** đọc code + `dotnet build Aives.Api/Aives.Api.csproj` (build OK). Chưa chạy server vì app kết nối DB Neon dùng chung khi khởi động.

## Tóm tắt

| Mức     | #    | Vấn đề                                                             | Ai xử lý |
| ------- | ---- | ------------------------------------------------------------------ | -------- |
| 🔴 Chặn | 1    | AI sinh câu hỏi luôn trả 0 câu (sai route AI + nuốt lỗi)           | BE + AI  |
| 🔴 Chặn | 2    | Không đổi được trạng thái ca thi (enum JSON dạng chuỗi bị 400)     | BE       |
| 🔴 Chặn | 3    | API Admin & `/api/documents` lỗi 500 (mất đăng ký DI sau merge)    | BE       |
| 🔴 Chặn | 4    | Teacher không có API lấy danh sách course của mình                 | BE       |
| 🟠      | 5–14 | Thiếu validate, thiếu API sửa/xoá, dữ liệu monitor/kết quả chưa có | BE       |
| 🟡      | 15   | Logout chưa thu hồi token (blacklist có nhưng không dùng)          | BE       |

Ưu tiên: **#2 và #3 sửa trong vài dòng**, nên làm ngay. Sau đó là #4 và #1.

---

## 🔴 1. `POST /api/courses/{courseId}/documents/generate-questions` luôn trả về 0 câu hỏi

**File:** `backend/Aives.Infrastructure/AI/AIQuestionService.cs`

- Backend gọi `POST /api/ai/generate-questions`, nhưng **AI service (FastAPI) không có route này**. Các route hiện có trên `dev`:
  - `POST /ai/interview/question`: sinh **1** câu hỏi
  - `POST /ai/interview/follow-up`
  - `POST /ai/markdown/process`
  - `POST /ai/evaluate/...`, `POST /ai/speech/...`
- Mọi lỗi (404, timeout, parse JSON) đều bị `catch` và trả về `new List<AIQuestionDto>()`. Vì vậy API trả **`200 { "generatedCount": 0, "questions": [] }`** và FE không phân biệt được "AI lỗi" với "không có câu nào".

**Đề xuất**

- BE và AI chốt **một endpoint sinh nhiều câu hỏi**, ví dụ `POST /ai/questions/generate` với body `{ markdown_contents, question_count, focus_topics }`. Hoặc BE gọi `/ai/interview/question` nhiều lần.
- Khi AI lỗi, **không trả 200 rỗng**. Ném exception để controller trả `502 Bad Gateway` hoặc `503` kèm `{ "message": "AI service không phản hồi" }`.
- Ghi chú: `HttpClient.Timeout` đang là 5 phút. FE sẽ đặt timeout riêng cho request này, nhưng nên giảm xuống khoảng 60–120 giây.

---

## 🔴 2. `PUT /api/exam-sessions/{id}/status` trả 400 khi gửi `"IN_PROGRESS"`

**File:** `backend/Aives.Api/Program.cs`

`UpdateSessionStatusRequest.Status` có kiểu enum `SessionStatus`, nhưng chưa cấu hình `JsonStringEnumConverter`. System.Text.Json mặc định chỉ nhận số, nên body như trong tài liệu sẽ lỗi:

```json
{ "status": "IN_PROGRESS" }   // ❌ 400: The JSON value could not be converted
{ "status": 1 }               // ✅ chạy, nhưng không đúng tài liệu
```

**Sửa (1 dòng):**

```csharp
using System.Text.Json.Serialization;

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

> Sau khi sửa, các enum trong response cũng sẽ là chuỗi. Hiện các DTO đã tự `.ToString()` nên không ảnh hưởng.

---

## 🔴 3. API Admin và `/api/documents/*` lỗi 500 (Unable to resolve service)

**File:** `backend/Aives.Api/Program.cs`

Trên nhánh `HungNguyen` có 3 dòng đăng ký DI, nhưng **bị mất khi merge vào `dev`** (commit `fix_conflit`):

```csharp
builder.Services.AddScoped<IUserService,             UserService>();
builder.Services.AddScoped<ICourseService,           CourseService>();
builder.Services.AddScoped<IMarkdownDocumentService, MarkdownDocumentService>();
```

Các controller bị ảnh hưởng: `AdminUserController`, `AdminCourseController`, `TeacherDocumentController`.

**Hệ quả:** Admin không tạo được course và không phân công teacher, nên Teacher không có course để upload tài liệu hay tạo đề. **Cần thêm lại 3 dòng trên** và kiểm tra xem các repository mà chúng phụ thuộc (`Aives.Application.Interfaces.Repositories.IUserRepository`, …) đã được đăng ký chưa.

---

## 🔴 4. Teacher không có API lấy course được phân công

Hiện chỉ có `GET /api/admin/courses/teacher/{teacherId}`, mà endpoint này yêu cầu **role ADMIN**. Teacher đăng nhập xong không biết mình có course nào, nên không có `courseId` để gọi các API khác.

**Đề xuất:**

```http
GET /api/teacher/courses            → CourseDto[] (lấy teacherId từ JWT)
GET /api/teacher/courses/{courseId} → CourseDto + trạng thái tài liệu
```

Nên trả thêm `hasContentDoc` và `hasLearningOutcomesDoc` (bool) để FE biết course đã đủ 2 file `.md` chưa.

---

## 🟠 Vấn đề về logic và thiết kế

### 5. Hai bộ API upload tài liệu trùng nhau

| Route                                                            | Role           | Lấy user từ đâu                                    |
| ---------------------------------------------------------------- | -------------- | -------------------------------------------------- |
| `POST /api/courses/{courseId}/documents` (`DocumentsController`) | TEACHER        | JWT ✅                                             |
| `POST /api/documents/upload` (`TeacherDocumentController`)       | TEACHER, ADMIN | Form field `uploadedBy` ❌ (client có thể giả mạo) |

`DELETE /api/documents/{id}?requestedBy=` cũng tin `userId` do client gửi lên.
**Đề xuất:** chốt **một** bộ API (nên dùng bộ lấy user từ JWT) và bỏ hoặc sửa bộ còn lại. FE sẽ dùng bộ được chốt.

### 6. `DocumentService.UploadMarkdownAsync` thiếu validate

**File:** `Aives.Application/Exams/Services/DocumentService.cs`

- Không giới hạn **1 file mỗi DocType mỗi course**, nên upload lại sẽ tạo bản trùng, và AI sẽ nhận tất cả các bản làm context.
- Không giới hạn **5MB** và không chặn file rỗng (bộ `/api/documents/upload` thì có).
- Không có API **xoá hoặc thay thế** file ở bộ này.

**Đề xuất:** upload trùng DocType thì trả `409`, hoặc cho phép thay thế qua `PUT`. Thêm `DELETE /api/courses/{courseId}/documents/{documentId}`.

### 7. Đề thi không sửa được và luôn ở trạng thái `DRAFT`

Hiện chỉ có tạo và xem. Còn thiếu:

```http
PUT    /api/exams/{examId}               // sửa title, duration, maxQuestions
PUT    /api/exams/{examId}/questions     // thay danh sách câu hỏi
PUT    /api/exams/{examId}/status        // DRAFT → SCHEDULED/OPEN/CLOSED/CANCELLED
DELETE /api/exams/{examId}               // chỉ khi DRAFT và chưa có session
```

### 8. `CreateExamAsync` thiếu kiểm tra

**File:** `Aives.Application/Exams/Services/ExamService.cs`

- Không kiểm tra teacher có được phân công `courseId` không. Teacher A có thể tạo đề cho course của Teacher B.
- Không kiểm tra số câu chọn so với `maxQuestions`.
- Không kiểm tra course đã đủ 2 file `.md` chưa (tiền điều kiện của UC-04 trong SRS).

### 9. `POST /api/exams/ai-suggest-questions` thực chất không dùng AI

- Endpoint này chỉ so khớp từ khoá, tách `focusTopic` theo **dấu cách**, nên `"OOP, Design Patterns"` thành `["oop,", "design", "patterns"]` và `"oop,"` không khớp câu nào. Nên tách theo cả `,` và dấu cách.
- Không kiểm tra teacher có sở hữu `courseId` không, nên có thể đọc câu hỏi của course khác.
- `GetByCourseIdAsync(courseId, max*3)` cắt danh sách **trước** khi lọc, nên có thể bỏ sót câu phù hợp.

### 10. Không có API xem ngân hàng câu hỏi

Câu hỏi chỉ xuất hiện trong response của lần sinh. FE cần danh sách này cho trang tạo đề:

```http
GET    /api/courses/{courseId}/questions?search=&page=&pageSize=
PUT    /api/courses/{courseId}/questions/{questionId}
DELETE /api/courses/{courseId}/questions/{questionId}
POST   /api/courses/{courseId}/questions          // thêm câu thủ công
```

### 11. Live monitor: `progressPercent` luôn bằng 0

**File:** `Aives.Application/Exams/Services/ExamSessionService.cs` → `GetSessionMonitorAsync`: `AnsweredQuestions = 0 // TODO`. Cần đếm từ bảng `answers`.

### 12. Không có code nào tạo `Result`

Luồng thi vấn đáp chưa có, nên `GET /api/results/session/{id}` sẽ luôn rỗng. FE đang dùng mock. Cần chốt contract cho luồng viva (xem mục "API còn thiếu" bên dưới).

### 13. Quy tắc review và publish

**File:** `Aives.Application/Exams/Services/ResultService.cs`

- `PublishResultAsync` cho publish thẳng từ `PENDING_REVIEW`, bỏ qua bước review. Cần xác nhận đây có phải là chủ ý không.
- `finalScore` của Result không tự tính từ `finalScore` từng câu. FE nên tự cộng, hay BE tính? Thang điểm Result (0–100) và thang điểm câu (`maxScore`) quan hệ thế nào?
- `ResultService` đang `using Microsoft.EntityFrameworkCore` trong tầng Application, không đúng Clean Architecture (lỗi nhỏ).

### 14. Thời gian ca thi

`CreateSessionAsync` so `StartTime` với `DateTime.UtcNow`. FE sẽ luôn gửi ISO UTC có `Z` (ví dụ `2026-10-15T07:30:00Z`). Đề nghị BE cũng trả về giờ UTC có `Z`, để FE không hiển thị lệch 7 tiếng.

### 15. Logout chưa thu hồi token (cập nhật 2026-10-12, `dev` @ `0b7a3a5`)

**Đã chạy thử thật:** sau khi `POST /api/auth/logout` trả 200, gọi `GET /api/auth/me` bằng token cũ vẫn trả **200**.

- `AuthService.LogoutAsync` chỉ ghi log.
- `JwtBlacklistService` có đăng ký (`AddSingleton`) nhưng **không chỗ nào gọi** `AddToBlacklistAsync` hay `IsTokenBlacklistedAsync`.
- Blacklist đang lưu trong RAM (biến `static`), nên sẽ mất khi restart server.

**Đề xuất:** trong `Logout`, đọc token từ header rồi gọi `AddToBlacklistAsync(token, exp)`. Trong `AddJwtBearer`, thêm `Events.OnTokenValidated` để gọi `IsTokenBlacklistedAsync`, nếu token đã bị thu hồi thì `context.Fail(...)`. Nếu nhóm chấp nhận token còn hiệu lực tới khi hết hạn (như ghi chú trong tài liệu API Authentication), hãy xoá phần code blacklist không dùng.

FE đã xoá token phía client khi đăng xuất, nên người dùng không bị ảnh hưởng trên giao diện.

---

## 📋 API còn thiếu cho các bước tiếp theo

**Teacher**

- `GET /api/teacher/courses` (mục 4)
- Ngân hàng câu hỏi (mục 10), sửa đề (mục 7)
- `POST /api/results/{resultId}/regrade`: yêu cầu AI chấm lại (UC-07 trong SRS)

**Student**

- `GET /api/student/enrollments`: danh sách ca thi đã enroll
- `GET /api/student/results` và `GET /api/student/results/{id}`: chỉ trả kết quả `PUBLISHED`. Nếu chưa publish thì chỉ trả trạng thái, không kèm điểm.

**Phòng thi vấn đáp** (FE gọi qua BE, không gọi trực tiếp AI, theo BR-14), đề xuất:

```http
POST /api/viva/{enrollmentId}/start   → { question, audioUrl, questionIndex, totalQuestions, timeRemainingSeconds }
POST /api/viva/{enrollmentId}/answer  (multipart: audio | text) → { transcript, next: { question, audioUrl, isFollowUp } | null, finished }
POST /api/viva/{enrollmentId}/finish
GET  /api/viva/{enrollmentId}/state   → resume khi reload trang
```

Cần chốt: định dạng audio (Chrome `audio/webm`, Safari `audio/mp4`), giới hạn dung lượng, và fallback trả lời bằng chữ khi STT lỗi.

**Admin**

- Xem danh sách exam và result toàn hệ thống (mục 6.3 tài liệu Overview).

---

## Ghi chú khác

- `Aives.slnx` cần **.NET SDK ≥ 9.0.200**. Máy dùng SDK 8 sẽ báo `error MSB4068: The element <Solution> is unrecognized`. Nên ghi rõ phiên bản SDK trong README (hoặc thêm `global.json`).
- Tài liệu `Api_Teacher_Exam.md` và `SWAGGER_GUIDE.md` đang lệch nhau (login bằng `username` hay `email`, `token` hay `accessToken`, base URL `5001`/`5116`/`7223`). Code hiện tại khớp `SWAGGER_GUIDE.md`. Đề nghị cập nhật `Api_Teacher_Exam.md` cho khớp.
- Đề nghị thống nhất **ExamCode và SessionCode**: sinh viên nhập mã `ESS-…` (SessionCode), nhưng tài liệu Overview/SRS gọi là "Exam Code". FE sẽ hiển thị là "Mã tham gia".
