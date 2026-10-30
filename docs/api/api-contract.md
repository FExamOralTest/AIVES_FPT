# AIVES AI Service — API Reference

**Base URL:** `http://localhost:8000` · **Prefix:** `/ai` · **Content-Type:** `application/json`
**Swagger UI:** `/docs` · **OpenAPI:** `/openapi.json`

## Quy ước chung

| Mục | Giá trị |
|---|---|
| Auth nội bộ | Header `X-Internal-Token` = `AI_SERVICE_SECRET_KEY` (backend gửi `AI:ApiKey`) |
| Endpoint yêu cầu auth | Chỉ `POST /ai/speech/transcribe-object`: sai/thiếu token → `401`, chưa cấu hình secret → `503` |
| Đặt tên field | API ngoài dùng `snake_case`; JSON contract nội bộ của LLM dùng `camelCase` |
| Validate request | `422` khi thiếu field hoặc sai kiểu (Pydantic) |
| Lỗi provider | `500` model lỗi · `502` vi phạm contract · `504` timeout |
| Retry | `AI_MAX_RETRIES` = 3, backoff `AI_RETRY_DELAY_SECONDS` = 2s |
| Timeout | `AI_TIMEOUT_SECONDS` = 60s/call · sinh đề `QUESTION_GENERATION_TIMEOUT_SECONDS` = 240s |

## Danh sách endpoint

| # | Method | Path | Mục đích | Backend gọi từ |
|---|---|---|---|---|
| 1 | POST | `/ai/questions/generate` | Sinh bộ đề + rubric | `AIQuestionService` |
| 2 | POST | `/ai/interview/question` | Sinh 1 câu vấn đáp | — (test/Swagger) |
| 3 | POST | `/ai/interview/follow-up` | Quyết định + sinh câu hỏi xoáy | `VivaSessionService` |
| 4 | POST | `/ai/interview/generate-questions` | Sinh nhanh câu hỏi từ markdown | — |
| 5 | POST | `/ai/speech/transcribe` | Speech-to-Text từ file upload | `VivaSessionService` |
| 6 | POST | `/ai/speech/transcribe-object` | Speech-to-Text từ object trên R2 | `AnswerOrchestrator` |
| 7 | POST | `/ai/speech/synthesize` | Text-to-Speech | `AiServiceClient` |
| 8 | POST | `/ai/evaluate/answer` | Chấm 1 câu trả lời | — (test/Swagger) |
| 9 | POST | `/ai/evaluate/session` | Chấm toàn bộ buổi thi | `AnswerOrchestrator` |
| 10 | POST | `/ai/markdown/process` | Chuẩn hoá & kiểm tra markdown | — |
---

## 1. POST /ai/questions/generate

Sinh bộ câu hỏi thi kèm rubric và learning outcome cho ngân hàng đề.

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `documents` | `QuestionBankDocument[]` | ✅ | `document_id` (int > 0), `document_type` (`CONTENT` \| `LEARNING_OUTCOMES`), `content` |
| `question_count` | int | ✅ | ≥ 5 |
| `focus_topics` | string[] | — | Chủ đề ưu tiên |
| `max_score` | number | — | Điểm tối đa mỗi câu, mặc định 10 |

**Response 200**

| Field | Kiểu | Ghi chú |
|---|---|---|
| `questions[]` | array | Danh sách câu hỏi |
| `questions[].question_text` | string | Nội dung câu hỏi |
| `questions[].topic` | string | Chủ đề |
| `questions[].sample_answer` | string | Đáp án mẫu |
| `questions[].learning_outcome` | string | LO mà câu hỏi kiểm tra |
| `questions[].rubric[]` | array | `description`, `max_score`; tổng bằng `max_score` của câu |
| `questions[].max_score` | number | Điểm tối đa |
| `questions[].level` | int | Độ khó 1–3 |
| `questions[].is_follow_up_allowed` | bool | Cho phép hỏi xoáy |
| `questions[].expected_keywords` | string[] | Từ khoá kỳ vọng |

**Lỗi:** `422` request sai · `502` AI trả sai contract · `504` timeout.

---

## 2. POST /ai/interview/question

Sinh một câu hỏi vấn đáp dựa trên tài liệu khoá học.

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `exam_session_id` | string | ✅ | UUID phiên thi |
| `exam_id` | string | ✅ | UUID đề thi |
| `course_name` | string | ✅ | Tên môn học |
| `documents` | `DocumentContext[]` | ✅ | `document_id`, `title`, `content`, `document_type` |
| `question_index` | int | — | Thứ tự câu, mặc định 1 |
| `total_questions` | int | — | Tổng số câu |
| `already_asked_topics` | string[] | — | Tránh trùng chủ đề |
| `already_asked_questions` | string[] | — | Tránh trùng câu hỏi |
| `question_type` | string | — | Ép loại câu hỏi |
| `difficulty` | string | — | `easy` \| `medium` \| `hard` |

**Response 200**

| Field | Kiểu | Ghi chú |
|---|---|---|
| `question` | string | Nội dung câu hỏi |
| `question_type` | string | `conceptual` \| `application` \| `analysis` |
| `difficulty` | string | `easy` \| `medium` \| `hard` |
| `expected_keywords` | string[] | Từ khoá kỳ vọng |
| `topic` | string | Chủ đề |
| `learning_outcome` | string | LO tương ứng |

**Lỗi:** `422` · `500` · `502`.

---

## 3. POST /ai/interview/follow-up

Phân tích câu trả lời và quyết định có hỏi xoáy hay không.

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `exam_session_id` | string | ✅ | UUID phiên thi |
| `course_name` | string | ✅ | Tên môn học |
| `documents` | `DocumentContext[]` | ✅ | Ngữ cảnh khoá học |
| `original_question` | string | ✅ | Câu hỏi gốc |
| `student_answer` | string | ✅ | Transcript câu trả lời |
| `question_index` | int | — | Thứ tự câu hiện tại, mặc định 1 |

**Response 200**

| Field | Kiểu | Ghi chú |
|---|---|---|
| `follow_up_question` | string | Câu hỏi xoáy; rỗng khi `should_follow_up = false` |
| `reason` | string | Lý do |
| `should_follow_up` | bool | Có cần hỏi xoáy không |

**Lỗi:** `422` · `500`.

---

## 4. POST /ai/interview/generate-questions

Sinh nhanh danh sách câu hỏi từ markdown (không lưu DB).

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `markdown_contents` | string[] | — | Nội dung markdown nguồn |
| `question_count` | int | — | Số câu cần sinh |
| `focus_topics` | string[] | — | Chủ đề ưu tiên |

**Response 200:** `success`, `questions[]` với `questionText`, `maxScore`, `level`, `isFollowUpAllowed` (camelCase).

**Lỗi:** `422`.

---

## 5. POST /ai/speech/transcribe

Speech-to-Text từ file upload (`multipart/form-data`).

**Form fields**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `audio_file` | file | ✅ | WAV / WebM / MP3 |
| `exam_session_id` | string | ✅ | UUID phiên thi |
| `language` | string | — | Mặc định `vi-VN` |

**Response 200:** `success`, `transcript`, `duration`, `confidence`, `language_detected`, `error { code, message, retryable }`.

**Lỗi:** `400` audio hỏng/rỗng · `413` quá lớn · `415` định dạng không hỗ trợ · `422` không có giọng nói · `500` lỗi inference · `503` provider/storage không sẵn sàng · `504` timeout.

---

## 6. POST /ai/speech/transcribe-object

Speech-to-Text cho audio đã lưu trên R2 (worker chấm bài gọi).

**Auth:** bắt buộc `X-Internal-Token`.

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `answerId` | int | ✅ | ID câu trả lời |
| `objectKey` | string | ✅ | Key object trong bucket |
| `language` | string | — | Mặc định `vi-VN` |

**Response 200:** `success`, `message`, `answerId`, `transcript`, `language`, `duration`, `error`.

**Lỗi:** `401` token sai/thiếu · `503` chưa cấu hình secret hoặc storage lỗi · `400`/`413`/`415`/`422`/`500`/`504` như endpoint 5.

---

## 7. POST /ai/speech/synthesize

Text-to-Speech, trả audio base64 để frontend phát.

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `text` | string | ✅ | Nội dung cần đọc |
| `language` | string | — | Mặc định `vi-VN` |
| `voice` | string | — | Tên voice của provider |

**Response 200:** `success`, `audio_base64`, `audio_format`, `duration_seconds`.

**Lỗi:** `422` · `500`.

---

## 8. POST /ai/evaluate/answer

Chấm một câu trả lời theo rubric.

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `exam_session_id` | string | ✅ | UUID phiên thi |
| `course_name` | string | ✅ | Tên môn học |
| `documents` | `DocumentContext[]` | ✅ | Ngữ cảnh khoá học |
| `question_answer` | `QuestionAnswerPair` | ✅ | Xem bảng dưới |
| `max_score` | number | — | Điểm tối đa, mặc định 10 |

`QuestionAnswerPair`: `question_id`, `question_text`, `student_answer` (bắt buộc); `is_follow_up`, `sample_answer`, `learning_outcome`, `topic`, `max_score`, `rubric[]` (`criterion_id`, `description`, `max_score`), `expected_keywords`.

**Response 200**

| Field | Kiểu | Ghi chú |
|---|---|---|
| `question_id` | string | Echo lại ID câu hỏi |
| `score` | number | 0 → `max_score` |
| `max_score` | number | Điểm tối đa |
| `feedback` | string | Nhận xét tổng |
| `provider` | string | AI provider đã dùng |
| `model` | string | Model đã dùng |
| `schema_version` | string | Phiên bản audit, hiện là `2.0` |
| `strengths[]` | string[] | Điểm mạnh |
| `weaknesses[]` | string[] | Điểm yếu |
| `missing_content[]` | string[] | Nội dung thiếu |
| `sentence_feedback[]` | array | `sentence`, `judgment`, `comment`; rỗng khi không có rubric |
| `criterion_analyses[]` | array | `criterion_id`, `awarded_score`, `judgment` (`met` \| `partial` \| `not_met`), `explanation`, `errors[]`, `omissions[]`, `evidence_quotes[]` |
| `improvements[]` | string[] | Gợi ý cải thiện |

**Lỗi:** `422` · `500`.

**Cách tính điểm:** Mỗi criterion trả `awarded_score` trong khoảng 0 đến `max_score`; `met` nhận đủ điểm, `not_met` nhận 0, `partial` nhận điểm nằm giữa hai mức. `score` của câu là tổng điểm criterion. Tổng `max_score` trong rubric phải khớp với điểm tối đa của câu. Backend xác thực các analysis và tự tính lại điểm.

---

## 9. POST /ai/evaluate/session

Chấm toàn bộ buổi thi (worker gọi sau khi có transcript).

**Request**

| Field | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `exam_session_id` | string | ✅ | UUID phiên thi |
| `student_id` | string | ✅ | UUID sinh viên |
| `course_name` | string | ✅ | Tên môn học |
| `documents` | `DocumentContext[]` | ✅ | Ngữ cảnh khoá học |
| `question_answers` | `QuestionAnswerPair[]` | ✅ | Danh sách câu hỏi + bài làm |
| `max_score_per_question` | number | — | Điểm tối đa mỗi câu |

**Response 200**

| Field | Kiểu | Ghi chú |
|---|---|---|
| `exam_session_id` | string | Echo lại ID phiên |
| `total_score` | number | Tổng điểm đạt được |
| `max_total_score` | number | Tổng điểm tối đa |
| `percentage` | number | Phần trăm (0–100) |
| `overall_feedback` | string | Nhận xét chung |
| `provider` | string | AI provider đã dùng |
| `model` | string | Model đã dùng |
| `schema_version` | string | Phiên bản audit, hiện là `2.0` |
| `question_scores[]` | array | `question_id`, `question_text`, `student_answer`, `score`, `max_score`, `feedback`, `strengths[]`, `weaknesses[]`, `missing_content[]`, `sentence_feedback[]`, `criterion_analyses[]` (có `awarded_score`), `improvements[]` |
| `study_suggestions[]` | string[] | Gợi ý ôn tập |
| `status` | string | Trạng thái tổng kết |

Backend tính lại điểm từng câu từ `criterion_analyses[].awarded_score`, sau đó tính `total_score`, `max_total_score` và `percentage`; các tổng AI trả về không được dùng làm nguồn dữ liệu chuẩn.

**Lỗi:** `422` · `500`.

---

## 10. POST /ai/markdown/process

Chuẩn hoá markdown và kiểm tra tính hợp lệ trước khi dùng làm ngữ cảnh.

**Request:** `course_name` (✅), `documents` (`DocumentContext[]`, ✅).

**Response 200:** `course_name`, `is_valid`, `validation_errors[]`, `documents[]` (`document_id`, `title`, `document_type`, `raw_char_count`, `clean_char_count`, `heading_count`, `headings[]`, `learning_outcomes[]`, `key_topics[]`, `is_valid`, `validation_errors[]`), `context_string`.

**Lỗi:** `422` · `500`.

---

## Contract nội bộ của LLM

Response của model được validate trước khi trả ra ngoài:

| Quy tắc | Giá trị |
|---|---|
| Điểm | `AiScore` trong `0–100` |
| Độ tin cậy | `confidence` trong `0.0–1.0` |
| Feedback | Đủ `strengths`, `improvements`, `actionableTips` |
| Rubric | Mỗi `criterion_analyses[]` có `criterion_id`, `awarded_score`, `judgment`, `explanation`, `evidence_quotes[]`; tổng điểm câu bằng tổng `awarded_score` |
| Xử lý | Bóc fence ```` ```json ```` → parse → kiểm tra đủ field → kiểm tra range → retry tối đa `AI_MAX_RETRIES` → raise `AIValidationError` / `AITimeoutError` |
