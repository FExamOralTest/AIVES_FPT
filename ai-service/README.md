# AIVES AI Service

FastAPI microservice cung cấp AI cho hệ thống thi vấn đáp AIVES.

## 📐 Kiến trúc

```
Frontend → Backend (ASP.NET Core) → AI Service (FastAPI)
                                        ├── /ai/interview/question     (AI 1)
                                        ├── /ai/interview/follow-up    (AI 1)
                                        ├── /ai/speech/transcribe      (AI 2 - STT)
                                        ├── /ai/speech/synthesize      (AI 2 - TTS)
                                        ├── /ai/evaluate/answer        (AI 1)
                                        └── /ai/evaluate/session       (AI 1)
```

> **BR-14**: Frontend KHÔNG gọi trực tiếp AI Service. Mọi request đi qua Backend.

## 📁 Cấu trúc

```
ai-service/
├── main.py                    # Root entry point (uvicorn)
├── requirements.txt
├── Dockerfile
├── .env.example
└── app/
    ├── main.py                # FastAPI app factory
    ├── core/
    │   ├── config.py          # Settings (pydantic-settings)
    │   └── ai_client.py       # Pluggable AI client (Mock/Gemini/OpenAI)
    ├── models/
    │   ├── request.py         # Pydantic request schemas
    │   └── response.py        # Pydantic response schemas
    ├── prompts/
    │   ├── question_prompt.txt   # Prompt tạo câu hỏi vấn đáp
    │   ├── followup_prompt.txt   # Prompt tạo câu hỏi đào sâu
    │   └── scoring_prompt.txt    # Prompt chấm điểm câu trả lời
    ├── services/
    │   ├── question_service.py   # Logic tạo câu hỏi
    │   ├── followup_service.py   # Logic tạo câu hỏi follow-up
    │   ├── interview_service.py  # Orchestrator phỏng vấn
    │   ├── scoring_service.py    # Logic chấm điểm (single + session)
    │   ├── evaluation_service.py # Wrapper evaluation cho API layer
    │   ├── speech_service.py     # STT (Mock/Google/Whisper)
    │   └── tts_service.py        # TTS (Mock/Google/OpenAI)
    └── api/
        ├── interview.py          # Router: /ai/interview/*
        ├── speech.py             # Router: /ai/speech/*
        └── evaluation.py         # Router: /ai/evaluate/*
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
```

### 4. Chạy server

```bash
uvicorn app.main:app --reload --port 8000
```

Hoặc:
```bash
python main.py
```

### 5. Mở API Docs

- Swagger UI: http://localhost:8000/docs
- ReDoc: http://localhost:8000/redoc
- Health check: http://localhost:8000/health

## 🔧 Cấu hình AI Provider

Chỉnh `AI_PROVIDER` trong `.env`:

| Giá trị | Mô tả | Cần |
|---------|-------|-----|
| `mock` | Mock response (mặc định, không cần API key) | - |
| `gemini` | Google Gemini API | `GEMINI_API_KEY` |
| `openai` | OpenAI Chat API | `OPENAI_API_KEY` |

Tương tự cho STT (`STT_PROVIDER`) và TTS (`TTS_PROVIDER`).

## 🐳 Docker

```bash
docker build -t aives-ai-service .
docker run -p 8000:8000 --env-file .env aives-ai-service
```

## 📡 API Endpoints

### Interview (AI 1)

| Method | Path | Mô tả |
|--------|------|-------|
| POST | `/ai/interview/question` | Tạo câu hỏi vấn đáp |
| POST | `/ai/interview/follow-up` | Tạo câu hỏi đào sâu |

### Speech (AI 2)

| Method | Path | Mô tả |
|--------|------|-------|
| POST | `/ai/speech/transcribe` | STT - Chuyển giọng nói thành văn bản |
| POST | `/ai/speech/synthesize` | TTS - Chuyển văn bản thành giọng nói |

### Evaluation (AI 1)

| Method | Path | Mô tả |
|--------|------|-------|
| POST | `/ai/evaluate/answer` | Chấm điểm 1 câu Q&A |
| POST | `/ai/evaluate/session` | Chấm điểm toàn bộ session |

### Health

| Method | Path | Mô tả |
|--------|------|-------|
| GET | `/health` | Kiểm tra trạng thái service |
