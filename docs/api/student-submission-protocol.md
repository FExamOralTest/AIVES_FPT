# AIVES — Giao thức nộp bài của sinh viên

**Version:** 1.0
**Last updated:** 2026-11-11
**Phạm vi:** Backend `Aives.Api`, queue Hangfire và các call Backend → AI service.

Tài liệu này mô tả **chính xác** điều gì xảy ra khi sinh viên nộp bài lên server:
sinh viên gửi gì, server xác định chủ sở hữu (`studentId` / `resultId` / `answerId`)
như thế nào, job nào được đưa vào queue, retry/failure ra sao, và backend gọi AI
service ở đâu. Mục tiêu là bảo đảm **100 bài nộp đồng thời không bị nhầm sinh viên**.

---

## 1. Xác thực và danh tính

Mọi endpoint nộp bài đều yêu cầu JWT:

```http
Authorization: Bearer <access_token>
```

Backend lấy danh tính từ **token**, không từ body:

| Claim | Dùng làm |
|---|---|
| `ClaimTypes.NameIdentifier` | `userId` — chính là `studentId` khi role là `STUDENT` |
| `ClaimTypes.Role` | `STUDENT` / `STAFF` / `EXAMINER` / `ADMIN` |

> **Quy tắc bất biến:** client **không bao giờ** gửi `studentId`, `resultId` hay
> `jobId` để server tin dùng. Các giá trị này do server suy ra từ token + DB.
> `answerId`, `sessionId`, `questionId`, `uploadId` do client gửi nhưng **luôn được
> đối chiếu quyền sở hữu** trước khi ghi dữ liệu.

Các endpoint nộp bài:

| Nhóm | Endpoint | Role |
|---|---|---|
| Viva trực tiếp | `POST /api/student/viva-sessions/{sessionId}/start` | `STUDENT` |
| Viva trực tiếp | `GET  /api/student/viva-sessions/{sessionId}/current-question` | `STUDENT` |
| Viva trực tiếp | `POST /api/student/viva-sessions/{sessionId}/submit-answer` | `STUDENT` |
| Viva trực tiếp | `POST /api/student/viva-sessions/{sessionId}/finish` | `STUDENT` |
| Upload audio lẻ | `POST /api/answers/{answerId}/audio` | `STUDENT`, `ADMIN` |
| Upload chunked | `POST /api/audio/chunked/init` \| `chunk` \| `complete` | `STUDENT`, `ADMIN` |
| Retry | `POST /api/answers/{answerId}/retry` | `STUDENT`, `STAFF`, `EXAMINER`, `ADMIN` |

---

## 2. Các ID trong hệ thống

| ID | Nguồn | Ý nghĩa | Ràng buộc |
|---|---|---|---|
| `studentId` | JWT (`NameIdentifier`) | Chủ bài nộp | Không nhận từ body |
| `sessionId` | URL / body | Ca thi | Sinh viên phải có enrollment trong ca |
| `resultId` | DB | Bài nộp của 1 sinh viên trong 1 ca | Unique `(StudentId, SessionId)` |
| `questionId` | Body | Câu hỏi trong đề | Phải thuộc đề của ca thi |
| `answerId` | DB, trả về sau khi lưu | Câu trả lời của 1 câu hỏi | Unique `(ResultId, QuestionId)` trong bảng `answers` |
| `jobId` | Hangfire | Job xử lý answer | Lưu vào `answers.job_id` để tra cứu |
| `uploadId` | Client sinh (CUID/GUID) | Phiên upload chunked | Thuộc đúng `(studentId, sessionId, questionId)` |

Luồng suy ra chủ sở hữu ở server:

```
JWT.studentId ─┐
sessionId ─────┼──► results (StudentId, SessionId) ──► resultId
questionId ────┘                                        │
                                                        ▼
                                            answers (ResultId, QuestionId) ──► answerId
```

Vì `resultId` được tra theo **cặp** `(studentId từ token, sessionId)`, hai sinh viên
trong cùng ca không thể ghi đè bài của nhau dù gửi cùng `sessionId`.

---

## 3. Ba đường nộp bài

### 3.1. Đường A — Viva trực tiếp (multipart, STT đồng bộ)

Dùng cho desktop/web làm bài vấn đáp từng câu.

```http
POST /api/student/viva-sessions/{sessionId}/start
```

- Kiểm tra enrollment, ca thi còn mở.
- Trả `currentQuestion`, tiến độ câu hỏi, thời gian còn lại.

```http
POST /api/student/viva-sessions/{sessionId}/submit-answer
Content-Type: multipart/form-data
```

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `QuestionId` | int | ✅ | Câu đang trả lời |
| `AudioFile` | file | Một trong hai | webm/wav/mp3/ogg/aac/flac, tối đa 50 MB |
| `TextAnswer` | string | Một trong hai | Dùng khi không có audio |
| `IsFollowUp` | bool | — | Đánh dấu trả lời câu hỏi phụ |
| `AudioUploadId` | string | — | Phiên chunked đã `COMPLETED` (ưu tiên) |
| `AudioObjectKey` | string | — | Fallback cho client cũ; server vẫn kiểm chứng qua `media_uploads` |

Server xử lý **trong request** (đồng bộ):

1. `result` được tạo nếu chưa có, theo `(studentId từ JWT, sessionId)`.
2. Audio (nếu có) được upload lên R2.
3. Gọi AI STT:
   - audio vừa upload → `POST /ai/speech/transcribe` (multipart)
   - audio đã có trên R2 (chunked) → `POST /ai/speech/transcribe-object`
4. Nếu câu hỏi cho phép, gọi `POST /ai/interview/follow-up` để sinh câu hỏi phụ.
5. Lưu `Answer` với `Transcript`, `ObjectKey`, `AudioUrl`, `Status = PROCESSING`.
6. Trả `answerId`, `transcript`, `shouldFollowUp`, câu hỏi kế tiếp.

Response rút gọn:

```json
{
  "success": true,
  "message": "Đã ghi nhận câu trả lời thành công.",
  "transcript": "Câu trả lời đã nhận dạng...",
  "shouldFollowUp": false,
  "currentQuestionIndex": 2,
  "totalQuestions": 5,
  "isCompleted": false,
  "nextQuestion": { "questionId": 8, "questionText": "...", "isFollowUp": false },
  "answerId": 45
}
```

Kết thúc bài:

```http
POST /api/student/viva-sessions/{sessionId}/finish
```

- Lưu bài, đặt `results.status = PENDING_REVIEW`, `ai_evaluation_status = PENDING`.
- Enqueue **một** job chấm cả bài lên queue `grading` (`SessionGradingJob`).
- Gọi `finish` lần hai khi job đang chờ sẽ **không** tạo job mới và không xoá điểm.
- Response trả `aiEvaluationStatus`, `submittedAt`.

> Đường A **không** tạo job STT riêng cho từng answer: STT đã chạy đồng bộ trong
> `submit-answer`; job bất đồng bộ duy nhất là job chấm cả bài ở bước `finish`.

### 3.2. Đường B — Upload audio lẻ cho Answer đã tồn tại

Dùng khi answer đã được tạo (ví dụ bài làm theo luồng khác) và cần gắn/ghi đè audio,
hoặc khi giáo viên/admin upload hộ.

```http
POST /api/answers/{answerId}/audio
Authorization: Bearer <token>
Content-Type: multipart/form-data
```

| Field | Kiểu | Bắt buộc |
|---|---|---|
| `audioFile` | file | ✅ (≤ 50 MB) |

Server kiểm tra:

- `STUDENT` chỉ upload cho `answer.Result.StudentId == userId`.
- `ADMIN` được phép mọi answer.
- Bài đã `CANCELLED` / `PUBLISHED` → từ chối.
- Answer đã được giáo viên chấm (`EVALUATED` + `FinalScore`) → chỉ `ADMIN` được ghi đè.

Sau khi upload:

1. Upload R2, ghi `answers.audio_url` / `answers.object_key`.
2. `UpdateAudioUrlAsync` đặt `Status = PROCESSING`, **reset `AttemptCount = 0`**,
   xoá `JobId` cũ (audio mới = vòng xử lý mới).
3. Enqueue `AnswerProcessingJob` lên queue `answers` kèm ownership:
   `AnswerId`, `ResultId`, `StudentId`, `SessionId`, `ObjectKey`.
4. Lưu `jobId` vào `answers.job_id`.

Response:

```json
{
  "answerId": 45,
  "audioKey": "exams/3/sessions/300/students/501/q_7_638...webm",
  "jobId": "b1f0c2d3e4f5",
  "status": "PROCESSING",
  "uploadedAt": "2026-11-11T09:30:00Z"
}
```

Nếu một job cho answer đang chờ/đang chạy, enqueue bị dedup và `jobId` trả về rỗng;
client nên coi đây là trạng thái "đã có job" và poll status API.

### 3.3. Đường C — Upload chunked rồi complete / submit

Dùng cho file lớn hoặc mạng không ổn định.

**Bước 1 — init**

```http
POST /api/audio/chunked/init
Content-Type: application/json
```

```json
{
  "uploadId": "up_01J...",
  "questionId": 7,
  "sessionId": 300,
  "fileName": "recording.webm",
  "contentType": "audio/webm",
  "totalSize": 7340032,
  "chunkSize": 5242880,
  "totalChunks": 2
}
```

Response trả `uploadId`, `receivedChunks` (resume), `status`, `localFilePath`.
Nếu đã có phiên dở cho cùng `(studentId, sessionId, questionId)` và cùng kích thước,
server trả lại phiên cũ để resume.

**Bước 2 — chunk** (idempotent, gửi lại được khi rớt mạng)

```http
POST /api/audio/chunked/chunk
Content-Type: multipart/form-data
```

| Field | Kiểu | Bắt buộc |
|---|---|---|
| `uploadId` | string | ✅ |
| `chunkIndex` | int (0-based) | ✅ |
| `chunkData` | file (≤ 10 MB) | ✅ |

**Bước 3 — complete**

```http
POST /api/audio/chunked/complete
Content-Type: application/json

{ "uploadId": "up_01J...", "answerId": 45 }
```

- `answerId` là **tuỳ chọn**.
- Nếu `answerId > 0`, server đối chiếu answer với chính phiên upload:
  `answer.Result.StudentId == upload.StudentId`, `answer.Result.SessionId == upload.SessionId`,
  `answer.QuestionId == upload.QuestionId`. Sai bất kỳ điều kiện nào → `403`/`400`,
  **không** ghi audio và **không** enqueue.
- Nếu hợp lệ: gộp chunk, upload R2, cập nhật answer, enqueue job `answers` kèm ownership,
  trả `jobId`.
- Nếu `answerId` = 0 (answer chưa tồn tại): trả `audioKey` để client gửi kèm
  `AudioUploadId` ở `submit-answer`.

Response complete:

```json
{
  "uploadId": "up_01J...",
  "answerId": 45,
  "audioKey": "exams/3/sessions/300/students/501/q_7.webm",
  "localFilePath": "C:\\audio_review\\501\\Software Architecture\\7\\q_7.webm",
  "jobId": "b1f0c2d3e4f5",
  "status": "COMPLETED",
  "completedAt": "2026-11-11T09:31:10Z",
  "storageFallback": false,
  "storageError": null,
  "storageHint": null
}
```

`storageFallback = true` nghĩa là R2 lỗi và audio chỉ nằm trên đĩa server — client
**phải** cảnh báo người dùng.

`complete` là idempotent: gọi lại khi phiên đã `COMPLETED` trả lại kết quả cũ, không
merge lại và không tạo job thứ hai.

---

## 4. Bảo đảm khi 100 bài nộp đồng thời

### 4.1. Không nhầm sinh viên

1. **Danh tính chỉ từ JWT.** Không endpoint nào nhận `studentId` từ client.
2. **Result tra theo cặp** `(studentId, sessionId)`; `answers` unique theo
   `(resultId, questionId)`.
3. **Metadata job mang ownership.** Mọi call site enqueue đều dùng
   `QueueJobContract.Create(..., studentId, sessionId)`.
4. **Worker đối chiếu lại DB** trước khi chạy pipeline:

   ```
   Answer.Result.StudentId == metadata.StudentId
   Answer.Result.SessionId == metadata.SessionId
   Answer.ResultId        == metadata.ResultId (nếu có)
   ```

   Lệch → job bị **dead-letter**, pipeline không chạy, `answers` của sinh viên đúng
   **không bị sửa**, không tăng `attempt_count`, không `MarkFailed`.
5. **Chunked complete** kiểm tra `answerId` thuộc đúng phiên upload (student/session/question)
   trước khi gắn audio.

Test tương ứng: `backend/Aives.Tests/AnswerQueueConcurrencyTests.cs`
(`D1_OneHundredConcurrentJobs_EachProcessesOnlyItsOwnAnswer`,
`D2_OneMismatchedJob_DoesNotAffectTheOtherNinetyNine`).

### 4.2. Không tạo job trùng

`HangfireQueueService.EnqueueAnswerProcessing` thực hiện "check dedup + enqueue"
trong một **striped lock theo `AnswerId`** (64 stripe). Nhiều request đồng thời cho
cùng answer chỉ tạo đúng **một** job; các request còn lại nhận `jobId` rỗng.

Dedup quét các state `Processing`, `Scheduled` (đang chờ retry), `Enqueued` trên cả
queue `default` và `answers`, match chính xác `AnswerId` (không khớp `77` với `777`).

### 4.3. Queue tách biệt

| Queue | Job | Worker |
|---|---|---|
| `answers` | `AnswerProcessingJob` — STT + evaluation từng câu | Worker pool chung với `default` |
| `grading` | `SessionGradingJob` — chấm cả bài | Một worker riêng, chạy tuần tự |

Tách queue để bài đang chấm cả bài không chặn việc xử lý audio mới.

---

## 5. Trạng thái job, retry và failure

### 5.1. Vòng đời

```text
Pending ──► Processing ──► Completed
                  │
                  ├──► Failed ──► (retry còn ngân sách) ──► Processing
                  │        └──► DeadLetter (hết 3 lần / lỗi không retryable)
                  └──► DeadLetter (ownership sai, audio hỏng, transcript rỗng…)
```

| `QueueJobStatus` | Hangfire state |
|---|---|
| `Pending` | `Enqueued`, `Scheduled`, `Created`, `Waiting` |
| `Processing` | `Processing` |
| `Completed` | `Succeeded` |
| `Failed` | `Failed` (còn retry) |
| `DeadLetter` | `Deleted` |

### 5.2. Retry (BR-17)

- Tối đa **3 lần thử tổng cộng** cho mỗi vòng xử lý.
- Nguồn sự thật là `answers.attempt_count`, **không phải** `metadata.Attempt`
  (Hangfire serialize lại metadata mỗi lần retry).
- Backoff: 30 s → 120 s → 300 s.
- Retry tự động chỉ áp dụng cho `AnswerProcessingJob.RetryableJobException`
  (STT timeout/internal, evaluation timeout/connection/internal/http).
- Lỗi không retryable → `NonRetryableJobException`, vào dead-letter ngay:
  audio hỏng, transcript rỗng, format không hỗ trợ, evaluation response sai contract,
  ownership mismatch, answer không tồn tại.
- Retry thủ công: `POST /api/answers/{answerId}/retry`
  - Chỉ khi answer `ERROR` (hoặc `PROCESSING` quá 30 phút — job orphan).
  - Không reset `attempt_count`; hết ngân sách → `400`.
  - Nếu đã có job đang chờ/chạy → trả `ALREADY_QUEUED`.
  - `ResetForRetryAsync` xoá transcript/điểm AI nhưng giữ `attempt_count` và `job_id`.

### 5.3. Ai được retry

| Role | Phạm vi |
|---|---|
| `STUDENT` | Answer của chính mình |
| `STAFF` | Answer trong session mình tạo (`ScheduledBy`) |
| `EXAMINER` | Toàn hệ thống |
| `ADMIN` | Toàn hệ thống |

---

## 6. API tra cứu trạng thái (chỉ đọc)

Client nên poll các API này thay vì đoán trạng thái từ DB.

### 6.1. `GET /api/answers/{answerId}/processing-status`

Quyền: student sở hữu answer, staff của session, examiner/admin.

```json
{
  "answerId": 45,
  "resultId": 12,
  "questionId": 7,
  "studentId": 501,
  "sessionId": 300,
  "answerStatus": "ERROR",
  "jobStatus": "DeadLetter",
  "hangfireState": "Deleted",
  "jobId": "b1f0c2d3e4f5",
  "attemptCount": 3,
  "maxAttempts": 3,
  "canRetry": false,
  "error": "STT error [AudioCorrupted]: Audio file is corrupted.",
  "answeredAt": "2026-11-11T09:30:00Z",
  "jobCreatedAt": "2026-11-11T09:30:01Z",
  "jobStateChangedAt": "2026-11-11T09:32:01Z"
}
```

- `jobStatus` lấy từ Hangfire nếu có `jobId`; nếu job đã hết hạn khỏi storage thì suy
  ra từ DB (`answers.status` + `attempt_count`).
- `canRetry` = answer `ERROR` + còn ngân sách + không có job đang chờ/chạy.

### 6.2. `GET /api/results/{resultId}/grading-status`

Quyền như trên. Trả trạng thái chấm cả bài + tóm tắt từng answer.

```json
{
  "resultId": 12,
  "sessionId": 300,
  "studentId": 501,
  "submissionStatus": "PENDING_REVIEW",
  "aiEvaluationStatus": "PENDING",
  "gradingJobId": "grade-8891",
  "gradingJobStatus": "Pending",
  "hangfireState": "Enqueued",
  "answerCount": 5,
  "evaluatedAnswerCount": 3,
  "failedAnswerCount": 1,
  "answers": [
    { "answerId": 45, "questionId": 7, "answerStatus": "ERROR",
      "jobStatus": "DeadLetter", "attemptCount": 3, "canRetry": false,
      "error": "..." }
  ]
}
```

### 6.3. `GET /api/student/viva-sessions/{sessionId}/submission-status`

Chỉ `STUDENT`; `studentId` lấy từ token. Đây là API tổng hợp để màn hình "đã nộp"
poll sau khi bấm **Kết thúc**.

```json
{
  "sessionId": 300,
  "studentId": 501,
  "resultId": 12,
  "submissionStatus": "PENDING_REVIEW",
  "aiEvaluationStatus": "PENDING",
  "message": "Bài thi đang chờ chấm. Hệ thống chấm lần lượt từng bài để tránh quá tải.",
  "grading": { "...": "cùng cấu trúc ResultGradingStatusDto" }
}
```

Không tìm thấy result của sinh viên trong ca → `404`.

**Gợi ý polling:** 2–5 s/lần trong 2 phút đầu; sau đó giãn ra 10–30 s. Dừng khi
`gradingJobStatus` là `Completed`, `Failed` hoặc `DeadLetter`.

---

## 7. Backend gọi AI service như thế nào

### 7.1. Cấu hình

| Key (`appsettings` section `AI`) | Ý nghĩa |
|---|---|
| `AI:BaseUrl` | Base URL FastAPI, mặc định `http://localhost:8000` |
| `AI:TimeoutSeconds` | Timeout HttpClient, mặc định 300 s |
| `AI:ApiKey` | Shared secret gửi qua header `X-Internal-Token` |

Mọi request Backend → AI đều tự động gắn:

```http
X-Internal-Token: <AI:ApiKey>
Content-Type: application/json  (hoặc multipart/form-data với STT file)
```

AI service từ chối request thiếu/sai token. Token **không** commit vào git.

### 7.2. Các endpoint thực tế

| Endpoint | Gọi từ | Khi nào | Request |
|---|---|---|---|
| `POST /ai/speech/transcribe` | `VivaSessionService.SubmitAnswerAsync` → `AiServiceClient.TranscribeAudioAsync` | Đường A, audio vừa upload | multipart: `audio_file`, `exam_session_id`, `language` (mặc định `vi-VN`) |
| `POST /ai/speech/transcribe-object` | `AnswerOrchestrator.TranscribeFromR2Async` | Job `answers` (đường B/C) và audio đã có trên R2 | JSON: `{ "answerId": 45, "objectKey": "exams/..." }` |
| `POST /ai/evaluate/session` | `AnswerOrchestrator.EvaluateAnswerAsync`, `SessionGradingService.GradeAsync` | Chấm từng answer hoặc chấm cả bài | JSON `EvaluateSessionRequest` (session, student, course, documents, question_answers, rubric…) |
| `POST /ai/interview/follow-up` | `VivaSessionService.SubmitAnswerAsync` → `GenerateFollowUpAsync` | Đường A, sau STT, nếu câu hỏi cho phép | JSON `FollowUpRequest` (session, course, original question, student answer, index) |
| `POST /ai/speech/synthesize` | `AiServiceClient.SynthesizeSpeechAsync` | Chưa có call site production (dự phòng TTS) | JSON: `{ "text", "language", "voice" }` |

Timeout nghiệp vụ cho từng lần gọi STT/evaluation trong `AnswerOrchestrator` là
**2 phút** (`AiCallTimeout`); `HttpClient` có timeout cấu hình riêng.

### 7.3. Hành vi lỗi

`AiServiceClient` map lỗi HTTP/response về `SttErrorCode` / `EvaluationErrorCode`:

| Tình huống | Retry |
|---|---|
| `TIMEOUT`, `INTERNAL_ERROR`, `connection_error`, `http_error`, `internal_error` | ✅ |
| `AUDIO_CORRUPTED`, `EMPTY_TRANSCRIPT`, `UNSUPPORTED_FORMAT`, `INVALID_RESPONSE`, `AUTH_FAILED` | ❌ dead-letter ngay |
| Evaluation response sai contract (`invalid_response`) | ❌ |

Chi tiết taxonomy: [`queue-and-stt-contract.md`](queue-and-stt-contract.md) mục 2.

---

## 8. Checklist client

1. Luôn gửi JWT; không gửi `studentId`/`resultId` trong body.
2. Dùng `answerId` server trả về để poll `processing-status`; không tự đoán.
3. Với file lớn, dùng chunked upload và gửi `AudioUploadId` khi submit.
4. Coi `jobId` rỗng là "đã có job" (dedup), không phải lỗi.
5. Hiển thị cảnh báo khi `storageFallback = true`.
6. Poll `submission-status` sau `finish`; dừng khi trạng thái là terminal.
7. Không retry khi `canRetry = false` (đã hết 3 lần thử).

---

## 9. Test tham chiếu

| File | Phạm vi |
|---|---|
| `backend/Aives.Tests/AnswerQueueConcurrencyTests.cs` | 100 job đồng thời, ownership mismatch, dedup, queue `answers`, job state |
| `backend/Aives.Tests/HangfireQueueRetryTests.cs` | retry pipeline thật, non-retryable dead-letter, worker queue |
| `backend/Aives.Tests/QueueJobContractTests.cs` | validate metadata, mapping state, retry budget |
| `backend/Aives.Tests/SttContractTests.cs` | taxonomy STT và quyết định retry |
