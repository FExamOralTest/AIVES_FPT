# AIVES AI Service

FastAPI microservice cung cấp AI cho hệ thống thi vấn đáp AIVES.

## Kiến trúc

```
Frontend → Backend (ASP.NET Core) → AI Service (FastAPI)
```

Frontend KHÔNG gọi trực tiếp AI Service (BR-14). Mọi request đi qua Backend.

## Endpoints

| Method | Path | Mô tả |
|--------|------|-------|
| POST | `/ai/interview/question` | Tạo câu hỏi vấn đáp |
| POST | `/ai/interview/follow-up` | Tạo câu hỏi follow-up |
| POST | `/ai/speech/transcribe` | Speech-to-Text |
| POST | `/ai/speech/synthesize` | Text-to-Speech |
| POST | `/ai/evaluate/answer` | Chấm điểm 1 câu |
| POST | `/ai/evaluate/session` | Chấm điểm cả buổi thi |

Xem chi tiết request/response tại [api-contract.md](api-contract.md).

## Cấu hình Provider

| Component | Provider | Cost | Env var |
|-----------|----------|------|---------|
| AI (LLM) | Gemini 3.1 Flash Lite | Free tier | `AI_PROVIDER=gemini` |
| STT | Faster-Whisper (local) | 0 | `STT_PROVIDER=faster_whisper` |
| TTS | Edge TTS (Microsoft) | 0 | `TTS_PROVIDER=edge_tts` |

## Chạy Local

```bash
cd ai-service
python -m venv venv
venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env  # edit GEMINI_API_KEY
uvicorn app.main:app --reload --port 8000
```

Swagger UI: http://localhost:8000/docs

## Test

```bash
pytest tests/ -v
```

## Cấu trúc thư mục

```
ai-service/
├── main.py
├── requirements.txt
├── .env.example
├── app/
│   ├── main.py              # FastAPI app factory
│   ├── core/
│   │   ├── config.py        # Settings (pydantic-settings)
│   │   └── ai_client.py     # Pluggable AI client (Mock/Gemini/OpenAI)
│   ├── models/
│   │   ├── request.py       # Pydantic request schemas
│   │   └── response.py      # Pydantic response schemas
│   ├── prompts/
│   │   ├── question_prompt.txt
│   │   ├── followup_prompt.txt
│   │   └── scoring_prompt.txt
│   ├── services/
│   │   ├── question_service.py
│   │   ├── followup_service.py
│   │   ├── interview_service.py
│   │   ├── scoring_service.py
│   │   ├── evaluation_service.py
│   │   ├── speech_service.py
│   │   └── tts_service.py
│   └── api/
│       ├── interview.py     # Router /ai/interview/*
│       ├── speech.py        # Router /ai/speech/*
│       └── evaluation.py    # Router /ai/evaluate/*
└── tests/
    └── test_ai_service.py
```
