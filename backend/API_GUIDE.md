
# AIVES Backend — API Guide

> Base URL: `http://localhost:5116`  
> Swagger UI: `http://localhost:5116/swagger`  
> Tất cả request/response đều dùng **UTF-8 JSON** trừ upload file dùng `multipart/form-data`.

---

## Mục lục

- [Setup & Seed](#setup--seed)
- [Health Check](#health-check)
- [Admin — Quản lý User](#admin--quản-lý-user)
- [Admin — Quản lý Course](#admin--quản-lý-course)
- [Teacher — Upload tài liệu .md](#teacher--upload-tài-liệu-md)
- [Response Schema](#response-schema)
- [Mã lỗi](#mã-lỗi)
- [Luồng test từ đầu](#luồng-test-từ-đầu)

---

## Setup & Seed

### Chạy app
```bash
dotnet run --project backend/Aives.Api --launch-profile http
```

### `POST /api/seed`
> Chỉ hoạt động ở môi trường **Development**. Tạo 1 teacher + 2 courses mẫu. Idempotent.

**Request body** *(tùy chọn)*
```json
{ "password": "Teacher@123" }
```
Bỏ trống body cũng được — password mặc định là `Teacher@123`.

**Response `200 OK`**
```json
{
  "message": "Seed completed successfully.",
  "username": "teacher_test",
  "password": "Teacher@123",
  "courses": [
    "CS101 — Nhập môn Lập trình",
    "SE201 — Kỹ nghệ Phần mềm"
  ]
}
```

---

## Health Check

### `GET /api/health/db`

**Response `200 OK`**
```json
{
  "status": "Healthy",
  "databaseConnected": true,
  "userCount": 3,
  "courseCount": 2,
  "serverTime": "2026-10-04T07:00:00Z"
}
```

---

## Admin — Quản lý User

Base route: `/api/admin/users`

> **Lưu ý bảo mật:** `passwordHash` không bao giờ xuất hiện trong response.  
> `role` nhận các giá trị: `ADMIN` | `TEACHER` | `STUDENT` (uppercase).

---

### `GET /api/admin/users`
Lấy toàn bộ danh sách user.

**Response `200 OK`**
```json
[
  {
    "userId": 1,
    "username": "teacher_test",
    "fullName": "Nguyễn Văn Giảng",
    "email": "teacher_test@aives.edu.vn",
    "role": "TEACHER",
    "createdAt": "2026-10-04T07:00:00Z"
  }
]
```

---

### `GET /api/admin/users/{id}`
Lấy user theo ID.

| Tham số | Kiểu  | Vị trí | Mô tả   |
|---------|-------|--------|---------|
| `id`    | `int` | path   | User ID |

**Response `200 OK`** — xem schema ở trên  
**Response `404 Not Found`**
```json
{ "message": "User 99 not found." }
```

---

### `GET /api/admin/users/role/{role}`
Lọc user theo role.

| Tham số | Kiểu     | Vị trí | Giá trị hợp lệ              |
|---------|----------|--------|-----------------------------|
| `role`  | `string` | path   | `ADMIN`, `TEACHER`, `STUDENT` |

**Response `200 OK`** — mảng UserDto  
**Response `400 Bad Request`** — role không hợp lệ

---

### `POST /api/admin/users`
Tạo user mới.

**Request body**
```json
{
  "username": "teacher_nguyen",
  "fullName": "Nguyễn Văn A",
  "email": "teacher_nguyen@aives.edu.vn",
  "password": "SecurePass@123",
  "role": "TEACHER"
}
```

| Field      | Kiểu     | Bắt buộc | Ghi chú                          |
|------------|----------|----------|----------------------------------|
| `username` | `string` | ✅       | Phải unique                      |
| `fullName` | `string` | ✅       |                                  |
| `email`    | `string` | ❌       | Phải unique nếu có               |
| `password` | `string` | ✅       | Được hash BCrypt trước khi lưu   |
| `role`     | `string` | ✅       | `ADMIN`, `TEACHER`, `STUDENT`    |

**Response `201 Created`** — UserDto của user vừa tạo  
**Response `409 Conflict`** — username hoặc email đã tồn tại
```json
{ "message": "Username 'teacher_nguyen' already exists." }
```

---

### `PUT /api/admin/users/{id}`
Cập nhật thông tin user. Chỉ truyền field cần thay đổi, `null` = giữ nguyên.

**Request body**
```json
{
  "fullName": "Nguyễn Văn A (Updated)",
  "email": "new_email@aives.edu.vn",
  "password": null
}
```

| Field      | Kiểu     | Ghi chú                                    |
|------------|----------|--------------------------------------------|
| `fullName` | `string?` | `null` = không đổi                        |
| `email`    | `string?` | `null` = không đổi; phải unique nếu thay  |
| `password` | `string?` | `null` = không đổi; hash lại nếu có giá trị |

**Response `200 OK`** — UserDto sau khi cập nhật  
**Response `404 Not Found`** — user không tồn tại  
**Response `409 Conflict`** — email đã dùng bởi user khác

---

### `DELETE /api/admin/users/{id}`
Xóa user theo ID.

**Response `204 No Content`** — xóa thành công  
**Response `404 Not Found`** — user không tồn tại

---

## Admin — Quản lý Course

Base route: `/api/admin/courses`

> Chỉ user có `role = ADMIN` mới được **tạo** course (`createdBy` phải là Admin ID).  
> Chỉ user có `role = TEACHER` mới được **assign** vào course.

---

### `GET /api/admin/courses`
Lấy tất cả course kèm thông tin teacher.

**Response `200 OK`**
```json
[
  {
    "courseId": 1,
    "courseCode": "CS101",
    "courseName": "Nhập môn Lập trình",
    "teacherId": 1,
    "teacherFullName": "Nguyễn Văn Giảng",
    "createdBy": 2,
    "createdAt": "2026-10-04T07:00:00Z"
  }
]
```

---

### `GET /api/admin/courses/{id}`
Lấy course theo ID.

**Response `200 OK`** — CourseDto  
**Response `404 Not Found`**

---

### `GET /api/admin/courses/teacher/{teacherId}`
Lấy tất cả course của một teacher.

| Tham số     | Kiểu  | Vị trí | Mô tả      |
|-------------|-------|--------|------------|
| `teacherId` | `int` | path   | Teacher ID |

**Response `200 OK`** — mảng CourseDto

---

### `POST /api/admin/courses`
Tạo course mới.

**Request body**
```json
{
  "courseCode": "AI301",
  "courseName": "Trí tuệ Nhân tạo",
  "createdBy": 2
}
```

| Field        | Kiểu     | Bắt buộc | Ghi chú                               |
|--------------|----------|----------|---------------------------------------|
| `courseCode` | `string` | ✅       | Phải unique, tự động uppercase        |
| `courseName` | `string` | ✅       |                                       |
| `createdBy`  | `int`    | ✅       | Phải là userId có `role = ADMIN`      |

**Response `201 Created`** — CourseDto  
**Response `404 Not Found`** — `createdBy` không tồn tại  
**Response `409 Conflict`** — courseCode đã tồn tại  
**Response `403 Forbidden`** — `createdBy` không phải Admin

---

### `PUT /api/admin/courses/{id}`
Cập nhật tên course.

**Request body**
```json
{
  "courseName": "Nhập môn Lập trình (Nâng cao)"
}
```

**Response `200 OK`** — CourseDto sau khi cập nhật  
**Response `404 Not Found`**

---

### `PUT /api/admin/courses/{id}/assign-teacher`
Phân công teacher phụ trách course.

**Request body**
```json
{
  "teacherId": 1
}
```

| Field       | Kiểu  | Ghi chú                                  |
|-------------|-------|------------------------------------------|
| `teacherId` | `int` | Phải là userId có `role = TEACHER`       |

**Response `200 OK`** — CourseDto với `teacherFullName` được cập nhật  
**Response `400 Bad Request`** — user không phải Teacher  
**Response `404 Not Found`** — course hoặc user không tồn tại

---

### `DELETE /api/admin/courses/{id}`
Xóa course theo ID.

**Response `204 No Content`**  
**Response `404 Not Found`**

---

## Teacher — Upload tài liệu .md

Base route: `/api/documents`

> **Quy tắc upload:**
> - File phải có extension `.md`
> - Nội dung không được rỗng
> - Giới hạn kích thước: **5 MB**
> - Mỗi course chỉ có tối đa **1 file CONTENT** và **1 file LEARNING_OUTCOMES**
> - Teacher phải được **assign vào course** trước khi upload

---

### `GET /api/documents/course/{courseId}`
Lấy tất cả document của một course.

**Response `200 OK`**
```json
[
  {
    "documentId": 1,
    "courseId": 1,
    "courseName": "Nhập môn Lập trình",
    "uploadedBy": 1,
    "fileName": "bai_giang.md",
    "docType": "CONTENT",
    "uploadedAt": "2026-10-04T08:00:00Z"
  }
]
```

---

### `GET /api/documents/{id}`
Lấy document theo ID.

**Response `200 OK`** — DocumentDto  
**Response `404 Not Found`**

---

### `POST /api/documents/upload`
Upload file `.md`. Content-Type: `multipart/form-data`.

| Form field   | Kiểu       | Bắt buộc | Mô tả                                   |
|--------------|------------|----------|-----------------------------------------|
| `courseId`   | `int`      | ✅       | ID của course cần gắn document          |
| `uploadedBy` | `int`      | ✅       | UserId của teacher thực hiện upload     |
| `docType`    | `string`   | ✅       | `CONTENT` hoặc `LEARNING_OUTCOMES`      |
| `file`       | `IFormFile`| ✅       | File `.md`, tối đa 5 MB                 |

**Ví dụ curl (PowerShell)**
```powershell
curl -X POST http://localhost:5116/api/documents/upload `
  -F "courseId=1" `
  -F "uploadedBy=1" `
  -F "docType=CONTENT" `
  -F "file=@C:\path\to\bai_giang.md"
```

**Ví dụ curl (bash/terminal)**
```bash
curl -X POST http://localhost:5116/api/documents/upload \
  -F "courseId=1" \
  -F "uploadedBy=1" \
  -F "docType=CONTENT" \
  -F "file=@/path/to/bai_giang.md"
```

**Response `201 Created`** — DocumentDto
```json
{
  "documentId": 1,
  "courseId": 1,
  "courseName": "Nhập môn Lập trình",
  "uploadedBy": 1,
  "fileName": "bai_giang.md",
  "docType": "CONTENT",
  "uploadedAt": "2026-10-04T08:00:00Z"
}
```

**Các lỗi có thể gặp**

| Status | Nguyên nhân                                              |
|--------|----------------------------------------------------------|
| `400`  | Không có file, file rỗng, extension không phải `.md`, file > 5 MB, tên file chứa path traversal |
| `403`  | `uploadedBy` không phải TEACHER, hoặc teacher không được assign vào course |
| `404`  | `courseId` hoặc `uploadedBy` không tồn tại              |
| `409`  | Course đã có document với `docType` này rồi             |

---

### `DELETE /api/documents/{id}?requestedBy={userId}`
Xóa document. Chỉ teacher đã upload hoặc admin mới được xóa.

| Tham số       | Kiểu  | Vị trí       | Mô tả                        |
|---------------|-------|--------------|------------------------------|
| `id`          | `int` | path         | Document ID cần xóa          |
| `requestedBy` | `int` | query string | UserId của người thực hiện   |

**Ví dụ**
```
DELETE /api/documents/1?requestedBy=1
```

**Response `204 No Content`** — xóa thành công  
**Response `403 Forbidden`** — không có quyền xóa  
**Response `404 Not Found`** — document hoặc user không tồn tại

---

## Response Schema

### UserDto
```json
{
  "userId": 1,
  "username": "string",
  "fullName": "string",
  "email": "string | null",
  "role": "ADMIN | TEACHER | STUDENT",
  "createdAt": "datetime | null"
}
```
> `passwordHash` không bao giờ được trả về.

### CourseDto
```json
{
  "courseId": 1,
  "courseCode": "CS101",
  "courseName": "string",
  "teacherId": 1,
  "teacherFullName": "string | null",
  "createdBy": 2,
  "createdAt": "datetime | null"
}
```

### DocumentDto
```json
{
  "documentId": 1,
  "courseId": 1,
  "courseName": "string",
  "uploadedBy": 1,
  "fileName": "string",
  "docType": "CONTENT | LEARNING_OUTCOMES",
  "uploadedAt": "datetime | null"
}
```

### Error response
```json
{ "message": "Mô tả lỗi cụ thể." }
```

---

## Mã lỗi

| HTTP Status          | Ý nghĩa                                           |
|----------------------|---------------------------------------------------|
| `200 OK`             | Thành công (GET, PUT)                             |
| `201 Created`        | Tạo mới thành công (POST)                         |
| `204 No Content`     | Xóa thành công (DELETE)                           |
| `400 Bad Request`    | Input không hợp lệ (sai format, thiếu field)      |
| `403 Forbidden`      | Không đủ quyền thực hiện thao tác                 |
| `404 Not Found`      | Resource không tồn tại                            |
| `409 Conflict`       | Trùng dữ liệu (username, email, courseCode, docType) |

---

## Luồng test từ đầu

Thứ tự thực hiện để test đầy đủ toàn bộ tính năng:

```
1. POST /api/seed
   → Tạo teacher_test (ghi lại userId) + CS101 (courseId=1), SE201 (courseId=2)

2. POST /api/admin/users   { role: "ADMIN", username: "admin_01", ... }
   → Tạo Admin account (ghi lại adminId)

3. POST /api/admin/users   { role: "TEACHER", username: "teacher_02", ... }
   → Tạo thêm Teacher (ghi lại teacherId)

4. POST /api/admin/courses { courseCode: "DB401", createdBy: <adminId> }
   → Tạo course mới với Admin (ghi lại courseId)

5. PUT /api/admin/courses/<courseId>/assign-teacher  { teacherId: <teacherId> }
   → Phân công teacher_02 phụ trách DB401

6. POST /api/documents/upload (multipart)
      courseId=<courseId>, uploadedBy=<teacherId>, docType=CONTENT, file=bai_giang.md
   → Upload file CONTENT → 201

7. POST /api/documents/upload (multipart)
      courseId=<courseId>, uploadedBy=<teacherId>, docType=LEARNING_OUTCOMES, file=chuan_dau_ra.md
   → Upload file LEARNING_OUTCOMES → 201

8. POST /api/documents/upload (cùng courseId, docType=CONTENT lần 2)
   → 409 Conflict — course đã có CONTENT

9. GET /api/documents/course/<courseId>
   → Xem 2 document vừa upload

10. DELETE /api/documents/1?requestedBy=<teacherId>
    → Xóa document → 204
```

---

## Student — Thi vấn đáp Viva AI & Xem kết quả

### 1. `POST /api/student/viva-sessions/{sessionId}/start`
> [STUDENT] Bắt đầu làm bài thi viva, nhận câu hỏi đầu tiên.

**Response `200 OK`**
```json
{
  "sessionId": 1,
  "sessionName": "Ca thi Vấn đáp SE201 - Ca 1",
  "examTitle": "Thi kết thúc học phần SE201",
  "durationMinutes": 45,
  "totalQuestions": 5,
  "currentQuestionIndex": 0,
  "currentQuestion": {
    "questionId": 12,
    "questionIndex": 0,
    "totalQuestions": 5,
    "questionText": "Trình bày các nguyên lý trong thiết kế hướng đối tượng SOLID.",
    "level": 2,
    "maxScore": 2.0,
    "isFollowUpAllowed": true,
    "isFollowUp": false
  },
  "startedAt": "2026-10-13T08:00:00Z"
}
```

### 2. `GET /api/student/viva-sessions/{sessionId}/current-question`
> [STUDENT] Lấy câu hỏi hiện tại đang cần trả lời.

**Response `200 OK`**
```json
{
  "questionId": 12,
  "questionIndex": 0,
  "totalQuestions": 5,
  "questionText": "Trình bày các nguyên lý trong thiết kế hướng đối tượng SOLID.",
  "level": 2,
  "maxScore": 2.0,
  "isFollowUpAllowed": true,
  "isFollowUp": false
}
```

### 3. `POST /api/student/viva-sessions/{sessionId}/submit-answer` (`multipart/form-data`)
> [STUDENT] Nộp file âm thanh câu trả lời (hoặc text). Backend tự động gửi sang AI Service để Speech-to-Text và kiểm tra câu hỏi phụ (follow-up).

**Form data:**
- `questionId`: `12`
- `audioFile`: `(file .wav / .webm / .mp3)`
- `textAnswer`: *(tùy chọn)*
- `isFollowUp`: `false`

**Response `200 OK` (Khi có câu hỏi phụ)**
```json
{
  "success": true,
  "message": "Cần trả lời thêm câu hỏi phụ từ AI.",
  "transcript": "SOLID là viết tắt của 5 nguyên lý: Single Responsibility, Open-Closed, Liskov Substitution, Interface Segregation và Dependency Inversion.",
  "shouldFollowUp": true,
  "followUpQuestion": "Bạn có thể cho một ví dụ thực tế về việc vi phạm nguyên lý Liskov Substitution (LSP) và cách khắc phục không?",
  "currentQuestionIndex": 0,
  "totalQuestions": 5,
  "isCompleted": false,
  "nextQuestion": {
    "questionId": 12,
    "questionIndex": 0,
    "totalQuestions": 5,
    "questionText": "Bạn có thể cho một ví dụ thực tế về việc vi phạm nguyên lý Liskov Substitution (LSP) và cách khắc phục không?",
    "level": 2,
    "maxScore": 2.0,
    "isFollowUpAllowed": false,
    "isFollowUp": true
  }
}
```

### 4. `POST /api/student/viva-sessions/{sessionId}/finish`
> [STUDENT] Kết thúc ca thi. AI Service đánh giá toàn diện session và tạo Result ở trạng thái `PENDING_REVIEW`.

**Response `200 OK`**
```json
{
  "sessionId": 1,
  "studentId": 5,
  "status": "PENDING_REVIEW",
  "message": "Bài thi đã được nộp thành công và AI đã hoàn thành chấm sơ bộ. Vui lòng đợi giảng viên duyệt và công bố kết quả.",
  "submittedAt": "2026-10-13T08:35:00Z",
  "aiScore": 8.5
}
```

### 5. `GET /api/student/results/{sessionId}`
> [STUDENT] Xem kết quả thi của sinh viên cho ca thi đã hoàn thành.  
> **Lưu ý:** Chỉ trả về điểm số và nhận xét chi tiết sau khi Giảng viên đã duyệt và công bố (`PUBLISHED`).

**Response `200 OK` (Khi chưa công bố)**
```json
{
  "resultId": 10,
  "sessionId": 1,
  "sessionName": "Ca thi Vấn đáp SE201 - Ca 1",
  "examTitle": "Thi kết thúc học phần SE201",
  "status": "PENDING_REVIEW",
  "isPublished": false,
  "message": "Kết quả bài thi đang được giảng viên chấm và duyệt. Vui lòng quay lại sau.",
  "submittedAt": "2026-10-13T08:35:00Z"
}
```

**Response `200 OK` (Khi đã công bố - PUBLISHED)**
```json
{
  "resultId": 10,
  "sessionId": 1,
  "sessionName": "Ca thi Vấn đáp SE201 - Ca 1",
  "examTitle": "Thi kết thúc học phần SE201",
  "status": "PUBLISHED",
  "isPublished": true,
  "finalScore": 9.0,
  "finalFeedback": "Sinh viên nắm rất vững kiến thức và trả lời lưu loát.",
  "submittedAt": "2026-10-13T08:35:00Z",
  "publishedAt": "2026-10-13T09:00:00Z",
  "answers": [
    {
      "answerId": 1,
      "questionId": 12,
      "questionText": "Trình bày các nguyên lý trong thiết kế hướng đối tượng SOLID.",
      "transcript": "SOLID là viết tắt của 5 nguyên lý...",
      "audioUrl": "/uploads/audio/1_5_12_638000000.wav",
      "finalScore": 2.0,
      "teacherComment": "Trả lời đầy đủ và chính xác.",
      "isFollowUp": false
    }
  ]
}
```

---

*Tài liệu này được generate từ source code thực tế — cập nhật khi có thay đổi API.*

