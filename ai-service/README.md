# AIVES AI Service

FastAPI microservice cung cấp AI cho hệ thống thi vấn đáp AIVES.

## 📐 Kiến trúc

```
Frontend → Backend (ASP.NET Core) → AI Service (FastAPI)
                                      ├── /ai/interview/question      (AI 1, deprecated alias)
                                      ├── /ai/interview/follow-up     (AI 1)
                                      ├── /ai/speech/transcribe       (AI 2 - STT)
                                      ├── /ai/speech/synthesize        (AI 2 - TTS)
                                      ├── /ai/evaluate/answer         (AI 1)
                                      └── /ai/evaluate/session        (AI 1)
```

**BR-14**: Frontend KH�ONG gọi trực tiếp AI Service. Mọi request đi qua Backend (ASP.NET Core), bao gồm header `X-AI-Service-Key` để xác thực giữa backend .NET ↔ AI service.

## 📁 Cấu trúc

```
ai-service/
├ main.py                    # Root entry point (uvicorn)
├ requirements.txt
├ Dockerfile
├ .env.example
└ app/
    main.py                  # FastAPI app factory
    core/
        config.py              # Settings (pydantic-settings, Literal providers)
        ai_client.py           # Pluggable AI client (Mock/Gemini/OpenAI), Gemini Interactions + retry
    models/
        request.py             # Pydantic request schemas
        response.py            # Pydantic response schemas
    prompts/
        question_prompt.txt    # Prompt tạo câu hỏi vấn đáp
        followup_prompt.txt    # Prompt tạo câu hỏi đào sâu
        scoring_prompt.txt     # Prompt chấm điểm câu trả lời
    services/
        question_service.py    # Logic tạo câu hỏi (delegate từ QuestionBankService)
        followup_service.py    # Logic tạo câu hỏi follow-up
        interview_service.py   # Orchestrator phỏng vấn
        scoring_service.py     # Logic chấm điểm (single + session)
        evaluation_service.py  # Wrapper evaluation cho API layer
        speech_service.py      # STT (Mock/Google/Whisper)
        tts_service.py         # TTS (Mock/Google/OpenAI)
    security.py                # verify_ai_service_key (X-AI-Service-Key)
    api/
        deps.py                # DI cho service layer
        errors.py              # Error mapping sang HTTP exception
        interview.py           # Router: /ai/interview/* (có generate-questions legacy)
        evaluation.py
        speech.py
        markdown.py
        questions.py
```

## 🚀 Chạy Local

### 1. Tạo virtual environment

```bash
cd ai-service
python -m venv venv
venv\Scripts\activate     # Windows
# source venv/bin/activate  # macOS/Linux
```

### 2. Cài dependencies

```bash
pip install -r requirements.txt
```

### 3. Cấu hình .env

```bash
copy .env.example .env
# Chỉnh sửa .env theo nhu cầu
# Các biến quan trọng:
#   AI_PROVIDER=gemini           # provider: mock / gemini / openai
#   GEMINI_API_KEY=...         # Gemini API key (chỉ cần cho gemini)
#   GEMINI_API_BASE_URL=https://generativelanguage.googleapis.com/v1beta
#   GEMINI_API_REVISION=2026-05-20
#   AI_SERVICE_API_KEY=aives-dev-internal-key  # Key giữa backend .NET <-> AI service
#   STT_PROVIDER=faster_whisper
#   TTS_PROVIDER=edge_tts
#   AI_PROVIDER=mock           # dùng cho test thiếu key
```

### 4. Chạy server

```bash
uvicorn app.main:app --reload --port 8000
# hoặc:
python main.py
```

### 5. Mở API Docs

- Swagger UI: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc
- Health check: http://localhost:8000/health (không cần key)

## 🔧 Cấu hình AI Provider

Chỉnh `AI_PROVIDER` trong `.env`:

| Giá trị | Mô tả | Cần |
|---------|-------|-----|
| `mock` | Mock response (mặc định, không cần API key) | - |
| `gemini` | Google Gemini API | `GEMINI_API_KEY` |
| `openai` | OpenAI Chat API | `OPENAI_API_KEY` |

Tương tự cho STT (`STT_PROVIDER`) và TTS (`TTS_PROVIDER`).

Để nhận dạng giọng nói bằng key Gemini hiện có, đặt `STT_PROVIDER=gemini`.
Provider này nhận WAV, WebM, Ogg, MP3 và MP4, tối đa 14 MB mỗi lần gửi;
`GEMINI_STT_MODEL` mặc định dùng model của `GEMINI_MODEL`.
Đọc câu hỏi tiếng Việt không cần key riêng: đặt `TTS_PROVIDER=edge_tts`.

Khi backend có API mới, cần build và khởi động lại backend; chạy trực tiếp DLL cũ
có thể trả HTTP 404 dù controller đã có trong mã nguồn. Từ repo root trên PowerShell,
dùng `./scripts/run-backend.ps1` sau khi cấu hình `ConnectionStrings__DefaultConnection`,
`ASPNETCORE_ENVIRONMENT`, `Jwt__Key` và `ASPNETCORE_URLS` cho môi trường cần chạy.
Script tự build mã nguồn hiện tại và giữ cấu hình từ terminal; dùng database development
cho kiểm tra thông thường. Đặt `AI__BaseUrl=http://localhost:8000` để kết nối AI service.

Biến quan trọng mới:
- `AI_SERVICE_API_KEY` - key riêng giữa backend .NET ↔ AI service. Nếu rỗng → enforcement tắt (dev/test).
- `GEMINI_API_BASE_URL` - base URL cho Gemini API (mặc định: `https://generativelanguage.googleapis.com/v1beta`).
- `GEMINI_API_REVISION` - revision date (mặc định: `2026-05-20`).

## 🐳 Docker

```bash
docker build -t aives-ai-service .
docker run -p 8000:8000 --env-file .env aives-ai-service
```

## 📡 API Endpoints

### Interview (AI 1)

| Method | Path | Mô tả | Auth |
|--------|------|-------|------|
| POST | `/ai/interview/question` | Tạo câu hỏi vấn đáp | `X-AI-Service-Key` |
| POST | `/ai/interview/follow-up` | Tạo câu hỏi đào sâu | `X-AI-Service-Key` |
| POST | `/ai/generate-questions` | **Deprecated** → delegate sang QuestionBankService | `X-AI-Service-Key` |
| GET | `/ai/generate-questions` | Alias deprecated, trỏ vào `generate_questions_legacy` | `X-AI-Service-Key` |

*Catatan: `/ai/generate-questions` và `/api/ai/generate-questions` là endpoint deprecated, delegate sang `QuestionBankService.generate`, đánh dấu trong OpenAPI/docs + log warning.*

### Speech (AI 2)

| Method | Path | Mô tả | Auth |
|--------|------|-------|------|
| POST | `/ai/speech/transcribe` | STT - Chuyển giọng nói thành văn bản | `X-AI-Service-Key` |
| POST | `/ai/speech/synthesize` | TTS - Chuyển văn bản thành giọng nói | `X-AI-Service-Key` |

### Evaluation (AI 1)

| Method | Path | Mô tả | Auth |
|--------|------|-------|------|
| POST | `/ai/evaluate/answer` | Chấm điểm 1 câu Q&A | `X-AI-Service-Key` |
| POST | `/ai/evaluate/session` | Chấm điểm toàn bộ session | `X-AI-Service-Key` |

### Health

| Method | Path | Mô tả | Auth |
|--------|------|-------|------|
| GET | `/health` | Kiểm tra trạng thái service | **Bypass** (không cần key) |

## ⚙� Error Handling

- `401 Unauthorized` - Khi thiếu/mã `X-AI-Service-Key` không hợp lệ (trừ `/health`).
- `422` - Context question không hợp lệ.
- `502` - Lỗi từ provider AI (httpx.HTTPError).
- `500` - Lỗi nội bộ/server.

## 🔐 Auth flow

1. **Backend (.NET)** gửi header `X-AI-Service-Key` trên HttpClient khi gọi AI service (FastAPI).
2. **AI Service (FastAPI)** dùng `verify_ai_service_key` dependency kiểm tra header này.
3. Nếu `AI_SERVICE_API_KEY` rỗng trong `.env` → enforcement tắt (chỉ dùng cho dev/test).
4. `/health` luôn bypass kiểm tra auth.

## 📦 Env vars (.env.example)

```
AI_PROVIDER=gemini
GEMINI_API_KEY=your-gemini-key
GEMINI_API_BASE_URL=https://generativelanguage.googleapis.com/v1beta
GEMINI_API_REVISION=2026-05-20
AI_SERVICE_API_KEY=aives-dev-internal-key
STT_PROVIDER=faster_whisper
TTS_PROVIDER=edge_tts
AI_SERVICE_BASE_URL=http://localhost:8000
```

`.env` nằm trong `.gitignore` (không commit).