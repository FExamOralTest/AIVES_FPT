# AIVES AI Service

FastAPI microservice cung cấp AI cho hệ thống thi vấn đáp AIVE.

## Kiến trúc

```
Frontend → Backend (ASP.NET Core) → AI Service (FastAPI)
```
**BR-14**: Frontend KH�ONG gọi trực tiếp AI Service. Mọi request đi qua Backend (ASP.NET Core), kèm header `X-AI-Service-Key` cho xác thực giữa backend .NET ↔ AI Service.

## Endpoints

| Method | Path | Mô tả | Auth |
|--------|------|-------|------|
| POST | `/ai/interview/question` | Tạo câu hỏi vấn đáp | `X-AI-Service-Key` |
| POST | `/ai/interview/follow-up` | Tạo câu hỏi follow-up | `X-AI-Service-Key` |
| POST | `/ai/speech/transcribe` | Speech-to-Text | `X-AI-Service-Key` |
| POST | `/ai/speech/synthesize` | Text-to-Speech | `X-AI-Service-Key` |
| POST | `/ai/evaluate/answer` | Chấm điểm 1 câu | `X-AI-Service-Key` |
| POST | `/ai/evaluate/session` | Chấm điểm cả buổi thi | `X-AI-Service-Key` |
| GET | `/health` | Kiểm tra trạng thái service | **Bypass** (không cần key) |

Xem chi tiết request/response tại [api-contract.md](api-contract.md).

---

## Cấu hình Provider

| Component | Provider | Cost | Env var |
|-----------|----------|------|---------|
| AI (LLM) | Gemini 3.1 Flash Lite | Free tier | `AI_PROVIDER=gemini` |
| STT | Faster-Whisper (local) | 0 | `STT_PROVIDER=faster_whisper` |
| TTS | Edge TTS (Microsoft) | 0 | `TTS_PROVIDER=edge_tts` |

Biến quan trọng thêm:
- `AI_SERVICE_API_KEY` - key giữa backend .NET ↔ AI service. Nếu rỗng → enforcement tắt (dev/test).
- `GEMINI_API_BASE_URL` - base URL Gemini (mặc định: `https://generativelanguage.googleapis.com/v1beta`).
- `GEMINI_API_REVISION` - revision date (mặc định: `2026-05-20`).

## Chạy Local

```bash
cd ai-service
python -m venv venv
venv\Scripts\activate     # Windows
# source venv/bin/activate  # macOS/Linux
pip install -r requirements.txt
cp .env.example .env      # edit các biến ở phần dưới
uvicorn app.main:app --reload --port 8000
```

Hoặc: `python main.py`

Swagger UI: http://localhost:8000/docs

## Test

```bash
python -m pytest tests/ -v
```

Kết quả: 7/7 passed (với mock env trong conftest.py).

Hoặc test auth:
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

## Cấu trúc thư mục

```
ai-service/
├ main.py                    # Root entry point (uvicorn)
├ requirements.txt
├ Dockerfile
├ .env.example
├ app/
│   main.py                  # FastAPI app factory
│   core/
│   │   ├── config.py        # Settings (pydantic-settings, Literal providers)
│   │   ├── ai_client.py     # Pluggable AI client (Mock/Gemini/OpenAI)
│   │   └── security.py      # verify_ai_service_key (X-AI-Service-Key)
│   ├── models/
│   │   ├── request.py       # Pydantic request schemas
│   │   └── response.py      # Pydantic response schemas
│   ├── prompts/
│   │   ├── question_prompt.txt
│   │   ├── followup_prompt.txt
│   │   └── scoring_prompt.txt
│   ├── services/
│   │   ├── question_service.py    # Logic tạo câu hỏi (delegate từ QuestionBankService)
│   │   ├── followup_service.py
│   │   ├── interview_service.py   # Orchestrator phỏng vấn
│   │   ├── scoring_service.py     # Logic chấm điểm (single + session)
│   │   ├── evaluation_service.py  # Wrapper evaluation cho API layer
│   │   ├── speech_service.py      # STT (Mock/Google/Whisper)
│   │   └── tts_service.py         # TTS (Mock/Google/OpenAI)
│   └── api/
│       ├── interview.py          # Router: /ai/interview/* (có generate-questions legacy)
│       ├── speech.py             # Router: /ai/speech/*
│       ├── evaluation.py         # Router: /ai/evaluate/*
│       ├── markdown.py
│       └── questions.py           # Router: /ai/questions/* (import get_question_bank_service)
│   └── security.py              # verify_ai_service_key (X-AI-Service-Key)
├── tests/
│   ├── test_ai_service.py
│   ├── test_service_auth.py     # Test auth 401/200
│   └── test_gemini_live.py      # Test Gemini live (có điều kiện)
│   └── conftest.py              # Force mock env vars
└── tests/
    └── test_ai_service.py
```

## Biến môi trường (.env.example)

```
AI_PROVIDER=gemini
GEMINI_API_KEY=your-gemini-api-key
GEMINI_API_BASE_URL=https://generativelanguage.googleapis.com/v1beta
GEMINI_API_REVISION=2026-05-20
AI_SERVICE_API_KEY=aives-dev-internal-key   # key giữa backend .NET <-> AI service
STT_PROVIDER=faster_whisper
TTS_PROVIDER=edge_tts
```

**.env** nằm trong **.gitignore** (không commit).

*Lưu ý:*
- Nếu `AI_SERVICE_API_KEY` rỗng → enforcement tắt (chỉ dùng dev/test).
- `/health` luôn bypass kiểm tra auth.
- `GEMINI_API_BASE_URL` và `GEMINI_API_REVISION` dùng để cấu hình endpoint và revision cho Gemini Interactions API.

---

*Cập nhật lần cuối: 2026-10-14*