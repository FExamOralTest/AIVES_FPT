# AIVES AI Service — Hướng dẫn tích hợp & Test chi tiết

> Base URL: `http://localhost:8000` (local) hoặc URL triển khai
> Prefix: `/ai`
> Auth: Header `X-AI-Service-Key` (shared secret giữa Backend .NET ↔ AI Service)
> Health endpoint `/health` **không** cần key.

---

## 1. Tổng quan kiến trúc

```
┌─────────────┐      HTTP/JSON      ┌─────────────────┐      HTTP/JSON      ┌─────────────────┐
│  Frontend   │ ──────────────────► │   Backend       │ ──────────────────► │   AI Service    │
│  (React)    │                     │  (ASP.NET Core) │                     │   (FastAPI)     │
└─────────────┘                     └─────────────────┘                     └─────────────────┘
      ▲                                    │                                     ▼
      │                                    ▼                                     AI Models
      │                            Business Logic                         (Gemini, Whisper, Edge TTS)
      │                            Auth/Authorization
      └──────────────────────────────────────────────────────────────────────────────┘
                               Flow: FE → BE → AI Service → BE → FE

**Quy tắc (BR-14):** Frontend **KH�ONG** gọi trực tiếp AI Service. Mọi request đều đi qua Backend (ASP.NET Core).
Backend gửi header `X-AI-Service-Key` để xác thực giữa backend .NET ↔ AI service.
Endpoint `/health` luôn bypass kiểm tra auth.
```

---

## 2. Chi tiết 7 Endpoint (đã refactor)

### 2.1 POST /ai/interview/question

**Mục đích:** Tạo câu hỏi vấn đáp tiếp theo cho sinh viên.

**K khi nào gọi:** Khi bắt đầu phiên thi, hoặc sau khi sinh viên trả lời xong 1 câu (kể cả follow-up).

**Request:**
```json
{
  "exam_session_id": "uuid",
  "exam_id": "uuid",
  "course_name": "Software Architecture",
  "documents": [
    {
      "document_id": "uuid",
      "title": "Chapter 1",
      "content": "Microservices architecture divides a system into small services that communicate via APIs.",
      "document_type": "course_content"
    },
    {
      "document_id": "uuid",
      "title": "Learning Outcomes",
      "content": "1. Explain microservices concepts...\n2. Design microservices...",
      "document_type": "learning_outcomes"
    }
  ],
  "question_index": 1,
  "total_questions": 5,
  "already_asked_topics": ["microservices definition", "service communication"]
}
```

| Field | Required | Mô tả |
|-------|----------|-------|
| `exam_session_id` | ✅ | UUID phiên thi |
| `exam_id` | ✅ | UUID bài thi |
| `course_name` | ✅ | Tên môn học |
| `documents` | ✅ | Mảng document (course_content + learning_outcomes) |
| `question_index` | ✅ | Số thứ tự câu hỏi (1-based) |
| `total_questions` | ✅ | Tổng số câu hỏi trong bài thi |
| `already_asked_topics` | ❌ | Danh sách chủ đề đã hỏi (tránh lặp) |

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "question": "Trong kiến trúc Microservices, việc phân tách một hệ thống nguyên khối thành các dịch vụ nhỏ giao tiếp qua API mang lại những ưu điểm gì về quản trị và phát triển, đồng thời thách thức lớn nhất của kiến trúc này đối với sự ổn định của hệ thống?",
  "question_type": "analysis",
  "difficulty": "medium",
  "expected_keywords": ["service decomposition", "independent deployment", "fault isolation", "distributed systems complexity"],
  "topic": "Microservices architecture challenges"
}
```

| Field | Mô tả |
|-------|-------|
| `question` | Câu hỏi tiếng Việt |
| `question_type` | `conceptual` / `application` / `analysis` |
| `difficulty` | `easy` / `medium` / `hard` |
| `expected_keywords` | Từ khóa kỳ vọng trong câu trả lời tốt |
| `topic` | Chủ đề liên quan |

**cURL test:**
```bash
curl -X POST "http://localhost:8000/ai/interview/question" \
  -H "Content-Type: application/json" \
  -H "X-AI-Service-Key: aives-dev-internal-key" \
  -d '{
    "exam_session_id": "s1",
    "exam_id": "e1",
    "course_name": "Software Architecture",
    "documents": [{"document_id":"d1","title":"Ch1","content":"Microservices are small services...","document_type":"course_content"}],
    "question_index": 1,
    "total_questions": 5,
    "already_asked_topics": []
  }'
```

**Lưu ý:** Endpoint `/ai/generate-questions` là alias **deprecated**,delegate sang `QuestionBankService.generate`, đánh dấu trong OpenAPI/docs + log warning. Chỉ dùng cho backward compatibility.

---

### 2.2 POST /ai/interview/follow-up

**Mục đích:** Phân tích câu trả lời của sinh viên, quyết định có cần câu hỏi follow-up hay không.

**K khi nào gọi:** Sau khi sinh viên trả lời xong 1 câu hỏi chính (không phải follow-up).

**Request:**
```json
{
  "exam_session_id": "s1",
  "course_name": "Software Architecture",
  "documents": [...],
  "original_question": "What is microservices?",
  "student_answer": "It is a way to build apps.",
  "question_index": 1
}
```

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "follow_up_question": "Bạn có thể giải thích chi tiết hơn về cách các dịch vụ trong hệ thống microservices giao tiếp với nhau về ưu điểm của cách tiếp cận này so với kiến trúc monolithic truyền thống không?",
  "reason": "Câu trả lời của sinh viên quá sải sędzi, chưa thể hiện được sự hiểu biết về cách thức giao tiếp giữa các dịch vụ.",
  "should_follow_up": true
}
```

| Field | Mô tả |
|-------|-------|
| `follow_up_question` | Câu hỏi đào sâu (rỗng nếu `should_follow_up=false`) |
| `reason` | Lý do tại sao cần/không cần follow-up (cho Teacher xem) |
| `should_follow_up` | `true` = cần follow-up, `false` = câu trả lời đủ tốt |

---

### 2.3 POST /ai/speech/transcribe (STT)

**Mục đích:** Chuyển giọng nói của sinh viên thành văn bản.

**K khi nào gọi:** Sau khi sinh viên ghi âm xong câu trả lời, Frontend gửi file audio lên Backend → Backend gọi AI Service.

**Request (multipart/form-data):**

| Field | Type | Required | Mô tả |
|-------|------|----------|-------|
| `audio_file` | File | ✅ | File audio (WAV/WebM/MP3), khuyến nghị 16kHz mono |
| `exam_session_id` | Form | ✅ | UUID phiên thi |
| `language` | Form | ❌ | Mã ngôn ngữ BCP-47 (mặc định `vi-VN`) |

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "transcript": "Microservices là một kiểu kiến trúc nơi ứng dụng được chia thành các dịch vụ nhỏ độc lập.",
  "confidence": 0.95,
  "language_detected": "vi-VN"
}
```

| Field | Mô tả |
|-------|-------|
| `transcript` | Văn bản đã chuyển đổi |
| `confidence` | Độ tin cậy 0–1 (null nếu provider không hỗ trợ) |
| `language_detected` | Ngôn ngữ thực tế phát hiện |

**cURL test:**
```bash
curl -X POST "http://localhost:8000/ai/speech/transcribe" \
  -F "audio_file=@answer.wav" \
  -F "exam_session_id=s1" \
  -F "language=vi-VN"
```

**Lưu ý:** File test sóng vuông sẽ trả về transcript rỗng. Cần file có giọng nói thật để test thực tế.

---

### 2.4 POST /ai/speech/synthesize (TTS)

**Mục đích:** Chuyển văn bản câu hỏi thành audio để phát cho sinh viên nghe.

**K khi nào gọi:** Sau khi tạo câu hỏi (question hoặc follow-up), trước khi gửi về Frontend phát.

**Request:**
```json
{
  "text": "Trong kiến trúc Microservices, việc phân tách một hệ thống...",
  "language": "vi-VN",
  "voice": "vi-VN-HoaiMyNeural"
}
```

| Field | Required | Mô tả |
|-------|----------|-------|
| `text` | ✅ | Văn bản cần chuyển thành giọng nói |
| `language` | ❌ | Mã ngôn ngữ (mặc định `vi-VN`) |
| `voice` | ❌ | Tên giọng (edge-tts: `vi-VN-HoaiMyNeural`, `vi-VN-NamMinhNeural`...) |

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "audio_base64": "SUQzAwAAAAA...",
  "audio_format": "mp3",
  "duration_seconds": 3.2
}
```

**Xử lý audio:** Decode base64 → lưu file `.mp3` → Frontend phát bằng `<audio>` tag.

**cURL test:**
```bash
curl -X POST "http://localhost:8000/ai/speech/synthesize" \
  -H "Content-Type: application/json" \
  -H "X-AI-Service-Key: aives-dev-internal-key" \
  -d '{"text":"Xin chào sinh viên","language":"vi-VN"}' \
  -o response.json

# Decode base64 to mp3
python -c "
import base64, json
data = json.load(open('response.json'))
open('output.mp3', 'wb').write(base64.b64decode(data['audio_base64']))
"
# Mở test.mp3 để nghe
```

---

### 2.5 POST /ai/evaluate/answer

**Mục đích:** Chấm điểm 1 cặp câu hỏi - câu trả lời.

**K khi nào gọi:** Sau mỗi câu trả lời (có thể gọi real-time hoặc batch ở cuối).

**Request:**
```json
{
  "exam_session_id": "s1",
  "course_name": "Software Architecture",
  "documents": [...],
  "question_answer": {
    "question_id": "q1",
    "question_text": "What is microservices?",
    "student_answer": "Microservices is an architectural style where an application is composed of small, independent services that communicate over APIs. Each service owns its own data and can be deployed independently.",
    "is_follow_up": false
  },
  "max_score": 10.0
}
```

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "question_id": "q1",
  "score": 8.5,
  "max_score": 10.0,
  "feedback": "The answer provides a solid and accurate definition of microservices, demonstrating a good grasp of the core concepts such as independence and API communication.",
  "strengths": [
    "Accurately identified the core definition of microservices.",
    "Correctly highlighted the importance of API communication and independent deployment."
  ],
  "weaknesses": [
    "Could benefit from concrete examples or real-world scenarios."
  ],
  "missing_content": [
    "Real-world deployment scenarios",
    "Fault tolerance mechanisms"
  ]
}
```

**Lưu ý:** `feedback`, `strengths`, `weaknesses`, `missing_content` sẽ bằng **cùng ngôn ngữ** với `student_answer` (tự động detect EN/VI).

---

### 2.6 POST /ai/evaluate/session

**Mục đích:** Chấm điểm toàn bộ phiên thi một lần.

**K khi nào gọi:** Khi phiên thi kết thúc (sinh viên bấm "Kết thúc" hoặc hết giờ).

**Request:**
```json
{
  "exam_session_id": "s1",
  "student_id": "st1",
  "course_name": "Software Architecture",
  "documents": [...],
  "question_answers": [
    {
      "question_id": "q1",
      "question_text": "What is microservices?",
      "student_answer": "Microservices is an architectural style...",
      "is_follow_up": false
    },
    {
      "question_id": "q2",
      "question_text": "How do services communicate?",
      "student_answer": "Services communicate via REST APIs or message queues...",
      "is_follow_up": false
    }
  ],
  "max_score_per_question": 10.0
}
```

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "exam_session_id": "s1",
  "total_score": 17.0,
  "max_total_score": 20.0,
  "percentage": 85.0,
  "overall_feedback": "The student demonstrates a strong understanding of microservices architecture, covering both conceptual and practical aspects.",
  "question_scores": [
    {
      "question_id": "q1",
      "question_text": "What is microservices?",
      "score": 8.5,
      "feedback": "..."
    },
    {
      "question_id": "q2",
      "question_text": "How do services communicate?",
      "score": 8.5,
      "feedback": "..."
    }
  ]
}
```

**Lưu ý quan trọng:** Kết quả luôn ở trạng thái **`PendingReview`**. Teacher phải duyệt (Approve) mới chuyển sang `Published` để sinh viên thấy.

---

### 2.7 GET /health

**Mục đích:** Kiểm tra trạng thái service.

**K khi nào gọi:** Luôn gọi trước khi dùng service khác.

**Response 200:**
```json
{
  "Status": "Healthy",
  "DatabaseConnected": true,
  "UserCount": 15,
  "CourseCount": 8,
  "ServerTime": "2026-10-14T03:00:00Z"
}
```

**Auth:** **Bypass** - không cần `X-AI-Service-Key`.

---

## 3. Xử lý lỗi chuẩn

| Status Code | Ý nghĩa | Xử lý |
|-------------|---------|-------|
| `200` | Thành công | Parse response bình thường |
| `400` | Request không hợp lệ | Kiểm tra field required, format |
| `401` | Thiếu/mã `X-AI-Service-Key` sai (trừ `/health`) | Tr gửi lại kèm header hợp lệ |
| `422` | Validation lỗi | Đọc `detail` để biết field sai |
| `500` | Lỗi nội bộ AI service | Log error, retry sau (nếu 503) |
| `503` | AI provider quá tải | Exponential backoff retry |

**Response lỗi format:**
```json
{
  "success": false,
  "error": "ValidationError",
  "detail": "Field 'exam_session_id' is required"
}
```

---

## 4. Hướng dẫn tích hợp vào Backend (ASP.NET Core)

### 4.1 Cấu hình HttpClient (Program.cs)

```csharp
builder.Services.AddHttpClient<IAIQuestionService, AIQuestionService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"] ?? "http://localhost:8000");
    var timeoutSeconds = builder.Configuration.GetValue<double>("AI:QuestionGenerationTimeoutSeconds", 300);
    if (!double.IsFinite(timeoutSeconds) || timeoutSeconds <= 0 || timeoutSeconds > 3600)
        throw new InvalidOperationException("AI:QuestionGenerationTimeoutSeconds must be between 0 and 3600.");
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    var apiKey = builder.Configuration["AI:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-AI-Service-Key", apiKey);
    }
});

builder.Services.AddHttpClient<Aives.Application.AI.Interfaces.IAiServiceClient, AiServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI:BaseUrl"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromMinutes(5);
    var apiKey = builder.Configuration["AI:ApiKey"];
    if (!string.IsNullOrEmpty(apiKey))
    {
        client.DefaultRequestHeaders.Add("X-AI-Service-Key", apiKey);
    }
});
```

### 4.2 Cấu hình appsettings.json

```json
{
  "AI": {
    "BaseUrl": "http://localhost:8000",
    "ApiKey": "aives-dev-internal-key",
    "QuestionGenerationTimeoutSeconds": 300
  }
}
```

### 4.3 AiServiceClient (implementation)

`AiServiceClient` đã được cập nhật để dùng HttpClient có `X-AI-Service-Key` header mặc định (cấu hình ở trên). Không cần thêm header thủ công trong từng method.

---

## 5. Hướng dẫn test

### 5.1 Swagger UI (dễ nhất)

Mở `http://localhost:8000/docs` → Try it out từng endpoint. Nhớ thêm header `X-AI-Service-Key` khi cần.

### 5.2 Script tự động (test_smoke.py)

```bash
cd ai-service
python test_smoke.py
```

Kết quả: 6/6 endpoint 200 (kèm header `X-AI-Service-Key`).

### 5.3 pytest

```bash
cd ai-service
python -m pytest tests/ -v
```

Kết quả: 7/7 passed (kèm mock env trong conftest.py).

### 5.4 Test Gemini live (có điều kiện)

```bash
cd ai-service
GEMINI_API_KEY=your-key python -m pytest tests/test_gemini_live.py -v
```

Bỏ qua nếu `GEMINI_API_KEY` chưa được set.

### 5.5 Test auth 401/200

```bash
# Không có key → 401
curl -X POST "http://localhost:8000/ai/interview/question" \
  -H "Content-Type: application/json" \
  -d '{"exam_session_id":"s1","exam_id":"e1","course_name":"Test","documents":[{"document_id":"d1","title":"Ch1","content":"Test","document_type":"course_content"}],"question_index":1,"total_questions":5,"already_asked_topics":[]}'

# Có key → 200
curl -X POST "http://localhost:8000/ai/interview/question" \
  -H "Content-Type: application/json" \
  -H "X-AI-Service-Key: aives-dev-internal-key" \
  -d '{"exam_session_id":"s1","exam_id":"e1","course_name":"Test","documents":[{"document_id":"d1","title":"Ch1","content":"Test","document_type":"course_content"}],"question_index":1,"total_questions":5,"already_asked_topics":[]}'
```

### 5.6 Test health bypass

```bash
curl -X GET "http://localhost:8000/health"
# Luôn 200 dù không có key
```

---

## 6. Cấu hình Environment Variables (.env.example)

```bash
# Service
AI_SERVICE_PORT=8000
AI_SERVICE_ENV=development
AI_SERVICE_HOST=0.0.0.0

# AI Provider
AI_PROVIDER=gemini
GEMINI_API_KEY=your-gemini-api-key
GEMINI_API_BASE_URL=https://generativelanguage.googleapis.com/v1beta
GEMINI_API_REVISION=2026-05-20
AI_SERVICE_API_KEY=aives-dev-internal-key   # key giữa backend .NET <-> AI service

# STT
STT_PROVIDER=faster_whisper
FASTER_WHISPER_MODEL=base  # base / small / medium / large

# TTS
TTS_PROVIDER=edge_tts

# Backend
BACKEND_URL=http://localhost:5000
```

**.env** nằm trong **.gitignore** (không commit).

*Lưu ý:*
- Nếu `AI_SERVICE_API_KEY` rỗng → enforcement tắt (chỉ dùng dev/test).
- `/health` luôn bypass kiểm tra auth.
- `GEMINI_API_BASE_URL` và `GEMINI_API_REVISION` dùng để cấu hình endpoint và revision cho Gemini Interactions API.

---

## 7. Checklist trước khi demo

- [ ] 7/7 endpoint trả 200 kèm header `X-AI-Service-Key` (trừ `/health`)
- [ ] `/health` trả 200 mà không có key
- [ ] TTS decode base64 → nghe được tiếng Việt
- [ ] STT transcribe đúng giọng nói tiếng Việt (file thật)
- [ ] Scoring trả điểm + feedback hợp lệ (EN/VI đúng ngôn ngữ)
- [ ] Question/follow-up liên quan đến nội dung .md
- [ ] pytest 7/7 pass (với mock env)
- [ ] Backend AiServiceClient gọi được 7 method kèm header
- [ ] `.env` không nằm trong git history

---

## 8. Troubleshooting thường gặp

| Vấn đề | Nguyên nhân | Giải pháp |
|--------|-------------|-----------|
| `401 Unauthorized` | Thiếu header `X-AI-Service-Key` hoặc key sai | Thêm header `X-AI-Service-Key: aives-dev-internal-key` hoặc set `AI_SERVICE_API_KEY=""` để tắt enforcement (dev) |
| `429 Too Many Requests` | Gemini rate limit | Đợi quota reset, hoặc giảm trequency request |
| `503 Service Unavailable` | AI provider quá tải | Retry với exponential backoff (client đã có retry logic) |
| `500` trong pytest | `.env` chưa set đúng `AI_PROVIDER=gemini`/`AI_SERVICE_API_KEY` | Dùng conftest.py force mock hoặc set env thật |
| Gemini `Bad Request` | Provider free model không ổn lòng | Chển sang `google/gemini-3.8-flash` hoặc giảm context size |
| Feedback vẫn tiếng Việt dù answer tiếng Anh | Prompt chưa load mới | Restart server/service sau khi sửa |
| TTS base64 decode lỗi | `audio_format` không khớp | Kiểm tra response `audio_format` field |

---

## 9. File liên quan

| File | Mô tả |
|------|-------|
| `docs/api/api-contract.md` | Schema request/response chi tiết |
| `docs/ai/ai-service.md` | Kiến trúc, cách chạy, cấu trúc thư mục |
| `ai-service/app/prompts/` | Prompt templates (question, followup, scoring) |
| `ai-service/tests/test_ai_service.py` | Test cases pytest |
| `ai-service/tests/test_service_auth.py` | Test auth 401/200 |
| `ai-service/tests/test_gemini_live.py` | Test Gemini live (có điều kiện) |
| `ai-service/test_smoke.py` | Script test nhanh 7 endpoint |
| `ai-service/conftest.py` | Force mock env vars cho test |

---
*Cập nhật lần cuối: 2026-10-14*