# Teacher Review API - Task 3.4 (BE 1)

## Mục lục
1. [Tổng quan](#tổng-quan)
2. [Business Rules](#business-rules)
3. [Database Schema](#database-schema)
4. [API Endpoints](#api-endpoints)
5. [Request/Response Examples](#requestresponse-examples)
6. [Error Handling](#error-handling)
7. [Workflow](#workflow)
8. [Test Cases](#test-cases)

---

## Tổng quan

Task 3.4 triển khai Teacher Review API để:
- Teacher xem và review kết quả bài thi AI đã chấm
- Teacher approve kết quả (REVIEWED)
- Teacher publish kết quả cho Student (PUBLISHED)
- Student xem kết quả đã publish

---

## Business Rules

| BR | Mô tả |
|----|-------|
| BR-08 | AI tạo Result ở trạng thái PENDING_REVIEW |
| BR-09 | Result được gửi cho Teacher review |
| BR-10 | Teacher approve và publish Final Result |
| BR-11 | Final Result được giao cho Student |
| BR-13 | Xác thực JWT và phân quyền theo role |
| BR-15 | Lưu câu hỏi, câu trả lời, transcript, điểm và feedback để đối soát |

---

## Database Schema

### Results Table
```sql
results (
    result_id       SERIAL PRIMARY KEY,
    session_id      INT NOT NULL,
    student_id      INT NOT NULL,
    reviewed_by     INT NULL,           -- Teacher ID
    ai_score        FLOAT NULL,
    ai_feedback     TEXT NULL,
    final_score     FLOAT NULL,
    final_feedback  TEXT NULL,
    status          submission_status,   -- PENDING_REVIEW, REVIEWED, PUBLISHED
    submitted_at    TIMESTAMP,
    reviewed_at     TIMESTAMP,
    published_at    TIMESTAMP
)
```

### Answers Table
```sql
answers (
    answer_id       SERIAL PRIMARY KEY,
    result_id       INT NOT NULL,
    question_id     INT NOT NULL,
    student_answer  TEXT NULL,
    transcript      TEXT NULL,           -- BR-15
    audio_url       TEXT NULL,           -- BR-15
    is_follow_up    BOOLEAN DEFAULT FALSE,
    ai_score        FLOAT NULL,           -- BR-15
    ai_feedback     TEXT NULL,           -- BR-15
    final_score     FLOAT NULL,           -- Teacher chỉnh sửa
    teacher_comment TEXT NULL,            -- Teacher feedback từng câu
    answered_at     TIMESTAMP
)
```

### Submission Status Enum
```sql
submission_status: PENDING_REVIEW, SUBMITTED, REVIEWED, PUBLISHED, CANCELLED
```

---

## API Endpoints

### Teacher Review API (Role: TEACHER)

#### 1. GET `/api/results/pending`
Lấy tất cả kết quả PENDING_REVIEW của tất cả sessions do Teacher quản lý.

**Authorization:** Bearer Token (Role: TEACHER)

**Response 200:**
```json
[
  {
    "resultId": 1,
    "sessionId": 1,
    "studentId": 200,
    "studentName": "Nguyễn Văn A",
    "studentEmail": "student@example.com",
    "sessionName": "Ca thi sáng - 12/10/2024",
    "examTitle": "Bài kiểm tra AI 1",
    "aiScore": 75.5,
    "aiFeedback": "Câu trả lời tốt, cần cải thiện phần giải thích",
    "finalScore": null,
    "finalFeedback": null,
    "status": "PENDING_REVIEW",
    "submittedAt": "2024-10-12T08:00:00Z",
    "reviewedAt": null,
    "answers": [...]
  }
]
```

---

#### 2. GET `/api/results/session/{sessionId}`
Lấy danh sách kết quả của một Exam Session cụ thể.

**Authorization:** Bearer Token (Role: TEACHER)

**Parameters:**
- `sessionId` (path): ID của Exam Session

**Response Codes:**
- 200: Success
- 401: Unauthorized
- 403: Forbidden (không phải session của teacher)
- 404: Session not found

---

#### 3. GET `/api/results/{resultId}`
Lấy chi tiết một Result để review, bao gồm transcript, audio URL, AI score, AI feedback.

**Authorization:** Bearer Token (Role: TEACHER)

**Parameters:**
- `resultId` (path): ID của Result

**Response 200:**
```json
{
  "resultId": 1,
  "sessionId": 1,
  "studentId": 200,
  "studentName": "Nguyễn Văn A",
  "studentEmail": "student@example.com",
  "sessionName": "Ca thi sáng - 12/10/2024",
  "examTitle": "Bài kiểm tra AI 1",
  "aiScore": 75.5,
  "aiFeedback": "Câu trả lời tốt",
  "finalScore": null,
  "finalFeedback": null,
  "status": "PENDING_REVIEW",
  "submittedAt": "2024-10-12T08:00:00Z",
  "reviewedAt": null,
  "answers": [
    {
      "answerId": 1,
      "questionId": 1,
      "questionText": "Giải thích khái niệm Machine Learning?",
      "studentAnswer": "Machine Learning là...",
      "transcript": "Machine Learning là một nhánh của AI...",
      "audioUrl": "https://storage.example.com/audio/answer1.mp3",
      "isFollowUp": false,
      "aiScore": 8.0,
      "aiFeedback": "Câu trả lời đúng",
      "finalScore": null,
      "teacherComment": null
    },
    {
      "answerId": 2,
      "questionId": 2,
      "questionText": "Follow-up: Cho ví dụ cụ thể?",
      "studentAnswer": "Ví dụ như...",
      "transcript": "Ví dụ như supervised learning...",
      "audioUrl": "https://storage.example.com/audio/answer2.mp3",
      "isFollowUp": true,
      "aiScore": 7.5,
      "aiFeedback": "Ví dụ chưa rõ ràng",
      "finalScore": null,
      "teacherComment": null
    }
  ]
}
```

---

#### 4. PUT `/api/results/{resultId}/review`
Review và lưu điểm cuối cùng, feedback; cập nhật status = REVIEWED.

**Authorization:** Bearer Token (Role: TEACHER)

**Parameters:**
- `resultId` (path): ID của Result

**Request Body:**
```json
{
  "finalScore": 85.0,
  "finalFeedback": "Kết quả tốt, đạt yêu cầu",
  "answerReviews": [
    {
      "answerId": 1,
      "finalScore": 8.5,
      "teacherComment": "Câu trả lời chi tiết và chính xác"
    },
    {
      "answerId": 2,
      "finalScore": 8.0,
      "teacherComment": "Cần bổ sung thêm ví dụ thực tế"
    }
  ]
}
```

**Validation:**
- `finalScore`: 0-100 (optional)
- `answerReviews[].finalScore`: 0-100 (optional)
- `answerReviews[].answerId`: required, must exist

**Response Codes:**
- 200: Review thành công, trả về Result đã cập nhật
- 400: Bad request (validation error)
- 401: Unauthorized
- 403: Forbidden
- 404: Result not found
- 409: Conflict (result không ở trạng thái PENDING_REVIEW)

**Business Rules:**
- Chỉ review được kết quả ở trạng thái PENDING_REVIEW
- Không ghi đè ai_score/ai_feedback
- Cập nhật reviewed_by và reviewed_at (UTC)
- BR-15: Lưu đầy đủ dữ liệu để đối soát

---

#### 5. POST `/api/results/{resultId}/publish`
Publish kết quả đã approve (REVIEWED) → PUBLISHED.

**Authorization:** Bearer Token (Role: TEACHER)

**Parameters:**
- `resultId` (path): ID của Result

**Request Body:** (empty hoặc `{}`)

**Response Codes:**
- 200: Publish thành công
- 400: Bad request (chưa được review)
- 401: Unauthorized
- 403: Forbidden
- 404: Result not found
- 409: Conflict (đã publish rồi)

**Business Rules:**
- Chỉ publish được kết quả ở trạng thái REVIEWED
- Không publish kết quả chưa được approve
- Cập nhật status = PUBLISHED
- Cập nhật published_at theo UTC
- Nếu không có final_score, sử dụng ai_score

---

### Student Results API (Role: STUDENT)

#### 6. GET `/api/student/results`
Lấy danh sách kết quả đã publish của chính mình.

**Authorization:** Bearer Token (Role: STUDENT)

**Response 200:**
```json
[
  {
    "resultId": 1,
    "sessionId": 1,
    "studentId": 200,
    "studentName": "Nguyễn Văn A",
    "sessionName": "Ca thi sáng - 12/10/2024",
    "examTitle": "Bài kiểm tra AI 1",
    "aiScore": 75.5,
    "finalScore": 85.0,
    "finalFeedback": "Kết quả tốt",
    "status": "PUBLISHED",
    "submittedAt": "2024-10-12T08:00:00Z",
    "reviewedAt": "2024-10-12T10:00:00Z",
    "publishedAt": "2024-10-12T10:30:00Z",
    "answers": [...]
  }
]
```

**Business Rules:**
- BR-11: Student chỉ được xem Result thuộc chính mình
- BR-11: Chỉ xem kết quả có status = PUBLISHED

---

## Request/Response Examples

### Example 1: Teacher Review và Publish Flow

**Step 1: Get Pending Results**
```
GET /api/results/pending
Authorization: Bearer <teacher_jwt_token>
```

**Response:**
```json
[
  {
    "resultId": 1,
    "studentName": "Nguyễn Văn A",
    "aiScore": 75.5,
    "status": "PENDING_REVIEW"
  }
]
```

**Step 2: Review Result**
```
PUT /api/results/1/review
Authorization: Bearer <teacher_jwt_token>
Content-Type: application/json

{
  "finalScore": 85.0,
  "finalFeedback": "Kết quả tốt, đạt yêu cầu",
  "answerReviews": [
    {"answerId": 1, "finalScore": 8.5, "teacherComment": "Câu trả lời chi tiết"},
    {"answerId": 2, "finalScore": 8.0, "teacherComment": "Cần bổ sung ví dụ"}
  ]
}
```

**Response:**
```json
{
  "resultId": 1,
  "status": "REVIEWED",
  "finalScore": 85.0,
  "finalFeedback": "Kết quả tốt, đạt yêu cầu",
  "reviewedAt": "2024-10-12T10:00:00Z"
}
```

**Step 3: Publish Result**
```
POST /api/results/1/publish
Authorization: Bearer <teacher_jwt_token>
```

**Response:**
```json
{
  "resultId": 1,
  "status": "PUBLISHED",
  "publishedAt": "2024-10-12T10:30:00Z"
}
```

### Example 2: Student Get Published Results
```
GET /api/student/results
Authorization: Bearer <student_jwt_token>
```

**Response:**
```json
[
  {
    "resultId": 1,
    "sessionName": "Ca thi sáng - 12/10/2024",
    "examTitle": "Bài kiểm tra AI 1",
    "finalScore": 85.0,
    "finalFeedback": "Kết quả tốt, đạt yêu cầu",
    "status": "PUBLISHED",
    "publishedAt": "2024-10-12T10:30:00Z"
  }
]
```

---

## Error Handling

| HTTP Code | Mô tả | Khi nào |
|-----------|-------|---------|
| 400 | Bad Request | Validation error, invalid state transition |
| 401 | Unauthorized | Invalid/missing JWT token |
| 403 | Forbidden | Không có quyền truy cập resource |
| 404 | Not Found | Result/Session không tồn tại |
| 409 | Conflict | Trạng thái không hợp lệ (đã publish, không phải PENDING_REVIEW) |

**Error Response Format:**
```json
{
  "message": "Mô tả lỗi"
}
```

---

## Workflow

### Review và Publish Flow
```
┌─────────────────┐     ┌─────────────┐     ┌────────────┐
│ PENDING_REVIEW  │────▶│  REVIEWED   │────▶│ PUBLISHED │
│  (AI đã chấm)  │     │ (Teacher    │     │ (Teacher   │
│                 │     │  approve)   │     │  publish)  │
└─────────────────┘     └─────────────┘     └────────────┘
        │                       │                    │
        │ PUT /review           │ POST /publish      │
        │ reviewed_by           │ published_at       │
        │ reviewed_at (UTC)     │ (UTC)              │
        ▼                      ▼                    ▼
   Chỉ review            Không publish         Kết quả đến
   PENDING_REVIEW        khi chưa REVIEWED     Student
```

---

## Test Cases

### 1. Danh sách/chi tiết pending review
- [x] GET /api/results/pending - trả về danh sách PENDING_REVIEW
- [x] GET /api/results/session/{id} - trả về results của session
- [x] GET /api/results/{id} - trả về chi tiết với transcript, audio, AI score

### 2. Review/Approve thành công
- [x] PUT /api/results/{id}/review với PENDING_REVIEW → REVIEWED
- [x] Cập nhật reviewed_by và reviewed_at (UTC)
- [x] Cập nhật answerReviews (final_score, teacher_comment)
- [x] Không ghi đè ai_score/ai_feedback

### 3. Publish trước approve bị từ chối
- [x] POST /api/results/{id}/publish với PENDING_REVIEW → 400/409
- [x] POST /api/results/{id}/publish với PUBLISHED → 409 Conflict

### 4. Student không đọc được kết quả chưa publish
- [x] GET /api/student/results - chỉ trả về PUBLISHED
- [x] Student không thể xem kết quả của người khác

### 5. Role validation
- [x] Không có role TEACHER → 403 Forbidden
- [x] Không có JWT → 401 Unauthorized

### 6. Timestamp chính xác
- [x] reviewed_at theo UTC
- [x] published_at theo UTC

---

## Files Changed

### Modified Files
1. `Aives.Application/Exams/Interfaces/IResultRepository.cs` - Thêm method mới
2. `Aives.Application/Exams/Interfaces/IResultService.cs` - Thêm method mới
3. `Aives.Application/Exams/Services/ResultService.cs` - Cập nhật logic, fix workflow
4. `Aives.Infrastructure/Repositories/ResultRepository.cs` - Cập nhật query methods
5. `Aives.Api/Controllers/ResultsController.cs` - Thêm endpoints, error handling

### New Files
1. `Aives.Tests/Aives.Tests.csproj` - Test project
2. `Aives.Tests/ResultServiceTests.cs` - Unit tests cho ResultService
3. `Aives.Tests/ResultsControllerTests.cs` - Integration tests cho Controller

### Updated Files
1. `Aives.slnx` - Thêm test project

---

## Migration

**Không cần migration** - Sử dụng các trường đã có trong database schema:
- `results.reviewed_by`
- `results.reviewed_at`
- `results.published_at`
- `answers.final_score`
- `answers.teacher_comment`

---

## Notes

1. **BR-15 Compliance:** Dữ liệu AI (ai_score, ai_feedback) được preserve khi Teacher review. Teacher chỉ cập nhật final_score, final_feedback, teacher_comment.

2. **Authorization:** Teacher ID được lấy từ JWT claims (ClaimTypes.NameIdentifier), không nhận từ client.

3. **Permission Check:** Kiểm tra `Session.ScheduledBy == teacherId` để đảm bảo Teacher chỉ access session của mình.

4. **State Transitions:**
   - PENDING_REVIEW → REVIEWED (qua /review)
   - REVIEWED → PUBLISHED (qua /publish)
   - Không cho phép chuyển ngược hoặc bỏ qua bước
