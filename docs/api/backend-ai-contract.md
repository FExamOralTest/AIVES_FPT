# AIVES Backend ↔ FastAPI AI Service Contract

**Version:** 1.0  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Overview

This document defines the contract between **ASP.NET Core Backend** and **FastAPI AI Service**.

- **FastAPI Base URL:** `http://localhost:8000`
- **Auth:** Internal secret key via header `X-AI-Service-Key`
- **Timeout:** 60 seconds per request (configurable)
- **Retry Policy:** Up to 3 retries for transient errors

---

## 2. Authentication

### 2.1 Service-to-Service Auth

All Backend → FastAPI requests must include:

```
Header: X-AI-Service-Key
Value: <configured-secret-key>
```

> **Note:** JWT tokens are NOT used for AI service calls. Only the internal secret key.

### 2.2 Error Response for Auth Failure

```json
{
  "success": false,
  "error": "unauthorized",
  "message": "Invalid or missing service key"
}
```

**Status:** `401 Unauthorized`

---

## 3. Speech-to-Text (STT)

### 3.1 Endpoint

```
POST /ai/speech/transcribe
Content-Type: multipart/form-data
```

### 3.2 Request

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `audio_file` | binary | Yes | Audio file (WAV, WebM, MP3) |
| `exam_session_id` | string (UUID) | Yes | Exam session identifier |
| `language` | string (BCP-47) | No | Language code, default `vi-VN` |

### 3.3 Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "message": null,
  "transcript": "Student's answer transcribed text...",
  "confidence": 0.95,
  "language_detected": "vi-VN"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `transcript` | string | Transcribed text |
| `confidence` | float | Confidence score (0-1) |
| `language_detected` | string | BCP-47 language code |

### 3.4 Error Responses

**Status: `400 Bad Request`**
```json
{
  "success": false,
  "error": "invalid_audio_format",
  "message": "Audio format not supported. Use WAV, WebM, or MP3."
}
```

**Status: `413 Payload Too Large`**
```json
{
  "success": false,
  "error": "audio_too_large",
  "message": "Audio file exceeds maximum size of 50MB."
}
```

**Status: `422 Unprocessable Entity`**
```json
{
  "success": false,
  "error": "validation_error",
  "message": "Missing required field: exam_session_id"
}
```

**Status: `500 Internal Server Error`**
```json
{
  "success": false,
  "error": "transcription_failed",
  "message": "Failed to transcribe audio. Please try again."
}
```

**Status: `503 Service Unavailable`**
```json
{
  "success": false,
  "error": "stt_service_unavailable",
  "message": "STT service is temporarily unavailable. Retry later."
}
```

### 3.5 Backend Processing Notes

- Backend downloads audio from R2 before sending to STT
- Transcript is stored in Answer.Transcript
- Language detection stored in Answer.Language
- Duration calculated from audio metadata

---

## 4. Text-to-Speech (TTS)

### 4.1 Endpoint

```
POST /ai/speech/synthesize
Content-Type: application/json
```

### 4.2 Request

```json
{
  "text": "Xin chào sinh viên",
  "language": "vi-VN",
  "voice": "vi-VN-HoaiMyNeural"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `text` | string | Yes | Text to synthesize |
| `language` | string | No | BCP-47 code, default `vi-VN` |
| `voice` | string | No | Voice ID, default system voice |

### 4.3 Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "message": null,
  "audio_base64": "UklGRi...",
  "audio_format": "mp3",
  "duration_seconds": 2.5
}
```

### 4.4 Error Responses

Same format as STT errors.

---

## 5. AI Evaluation

### 5.1 Endpoint

```
POST /ai/evaluate/session
Content-Type: application/json
```

### 5.2 Request

```json
{
  "exam_session_id": "550e8400-e29b-41d4-a716-446655440000",
  "student_id": "660e8400-e29b-41d4-a716-446655440001",
  "course_name": "Software Architecture",
  "documents": [
    {
      "document_id": "770e8400-e29b-41d4-a716-446655440002",
      "title": "Chapter 1",
      "content": "...",
      "document_type": "course_content"
    }
  ],
  "question_answers": [
    {
      "question_id": "880e8400-e29b-41d4-a716-446655440003",
      "question_text": "Explain microservices architecture",
      "student_answer": "Microservices are small services...",
      "is_follow_up": false,
      "sample_answer": "Microservices are independently deployable services...",
      "learning_outcome": "LO1: Explain microservices",
      "topic": "Architecture",
      "rubric": [
        {
          "criterion_id": "c1",
          "description": "Define microservices",
          "max_score": 4
        }
      ]
    }
  ],
  "max_score_per_question": 10.0
}
```

### 5.3 Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "message": null,
  "exam_session_id": "550e8400-e29b-41d4-a716-446655440000",
  "total_score": 7.5,
  "max_total_score": 10.0,
  "percentage": 75.0,
  "overall_feedback": "Good understanding of microservices...",
  "question_scores": [
    {
      "question_id": "880e8400-e29b-41d4-a716-446655440003",
      "question_text": "Explain microservices architecture",
      "score": 7.5,
      "feedback": "You explained the concept well...",
      "sentence_feedback": [
        {
          "sentence": "Microservices are small services that work together",
          "judgment": "correct",
          "comment": "Accurate definition"
        }
      ],
      "criterion_analyses": [
        {
          "criterion_id": "c1",
          "judgment": "partial",
          "explanation": "Definition is present but incomplete",
          "errors": [],
          "omissions": ["deployment aspect"],
          "evidence_quotes": ["Microservices are small services"]
        }
      ],
      "improvements": ["Include deployment aspect"]
    }
  ],
  "study_suggestions": [
    "LO1: Explain microservices — Criterion c1 needs more detail on deployment"
  ]
}
```

### 5.4 Field Definitions

| Field | Type | Description |
|-------|------|-------------|
| `total_score` | float | Sum of all question scores |
| `max_total_score` | float | Maximum possible score |
| `percentage` | float | (total_score / max_total_score) * 100 |
| `overall_feedback` | string | Summary feedback |
| `question_scores` | array | Per-question evaluation |
| `sentence_feedback[].judgment` | enum | `correct`, `error`, `omission`, `irrelevant` |
| `criterion_analyses[].judgment` | enum | `met`, `partial`, `not_met` |
| `study_suggestions` | array | Learning outcome suggestions |

### 5.5 Error Responses

**Status: `400 Bad Request`**
```json
{
  "success": false,
  "error": "invalid_request",
  "message": "Missing required field: exam_session_id"
}
```

**Status: `422 Unprocessable Entity`**
```json
{
  "success": false,
  "error": "validation_error",
  "message": "Invalid rubric format: criterion_id must be unique"
}
```

**Status: `500 Internal Server Error`**
```json
{
  "success": false,
  "error": "evaluation_failed",
  "message": "AI evaluation failed. Please try again."
}
```

**Status: `503 Service Unavailable`**
```json
{
  "success": false,
  "error": "ai_provider_unavailable",
  "message": "AI provider is temporarily unavailable"
}
```

### 5.6 Validation Contract

Backend validates AI response:

1. **Required Fields:** `success`, `exam_session_id`, `total_score`, `question_scores`
2. **Score Range:** Each score must be 0 ≤ score ≤ max_score
3. **JSON Structure:** All nested objects must match schema
4. **Evidence Quotes:** Must be exact substrings from transcript

> **See:** `Aives.Application.AI.EvaluationContract.cs`

---

## 6. Follow-up Question Generation

### 6.1 Endpoint

```
POST /ai/interview/follow-up
Content-Type: application/json
```

### 6.2 Request

```json
{
  "exam_session_id": "550e8400-e29b-41d4-a716-446655440000",
  "course_name": "Software Architecture",
  "documents": [...],
  "original_question": "Explain microservices",
  "student_answer": "Microservices are small services...",
  "question_index": 1
}
```

### 6.3 Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "message": null,
  "follow_up_question": "How do microservices communicate with each other?",
  "reason": "Student mentioned services but didn't cover communication patterns",
  "should_follow_up": true
}
```

| Field | Type | Description |
|-------|------|-------------|
| `follow_up_question` | string | Generated follow-up question |
| `reason` | string | Why follow-up is needed |
| `should_follow_up` | boolean | Whether to ask follow-up |

### 6.4 Error Responses

Same format as other AI endpoints.

---

## 7. Question Generation

### 7.1 Endpoint

```
POST /ai/interview/question
Content-Type: application/json
```

### 7.2 Request

```json
{
  "exam_session_id": "550e8400-e29b-41d4-a716-446655440000",
  "exam_id": "660e8400-e29b-41d4-a716-446655440001",
  "course_name": "Software Architecture",
  "documents": [...],
  "question_index": 1,
  "total_questions": 5,
  "already_asked_topics": ["microservices"]
}
```

### 7.3 Success Response

**Status:** `200 OK`

```json
{
  "success": true,
  "message": null,
  "question": "What are the advantages of microservices over monolithic architecture?",
  "question_type": "application",
  "difficulty": "medium",
  "expected_keywords": ["independent deployment", "scalability", "fault isolation"],
  "topic": "Architecture Comparison"
}
```

---

## 8. Timeout & Retry Policy

### 8.1 Timeout Configuration

| Operation | Timeout |
|-----------|---------|
| STT | 60 seconds |
| Evaluation | 120 seconds |
| Follow-up | 30 seconds |
| Question Generation | 30 seconds |

### 8.2 Retry Strategy

- **Max Retries:** 3 attempts total
- **Retry Delay:** Exponential backoff (2s, 4s, 8s)
- **Retryable Errors:**
  - `503 Service Unavailable`
  - `504 Gateway Timeout`
  - Connection errors
- **Non-Retryable:**
  - `400 Bad Request`
  - `401 Unauthorized`
  - `422 Unprocessable Entity`
  - `500 Internal Server Error` (may be retryable if transient)

### 8.3 Error Logging

All errors logged with:
- Request ID / Correlation ID
- Endpoint called
- Request payload (sanitized)
- Response/error details
- Timestamp
- Stack trace (for 500 errors only)

---

## 9. Data Flow

### 9.1 Audio Processing Flow

```
Student → Frontend → Backend (upload) → R2 (storage)
                                        ↓
                              Hangfire Job Queue
                                        ↓
                              AI1 (Faster-Whisper)
                                        ↓
                              Backend (save transcript)
                                        ↓
                              AI2 (Evaluation)
                                        ↓
                              Backend (save scores)
```

### 9.2 Contract Points

| Step | Backend → AI | AI → Backend |
|------|--------------|--------------|
| 1 | POST /ai/speech/transcribe | Transcript, confidence |
| 2 | POST /ai/evaluate/session | Scores, feedback |
| 3 | POST /ai/interview/follow-up | Follow-up question |

### 9.3 No Direct Access

- **Frontend MUST NOT call FastAPI directly**
- **Frontend MUST NOT call R2 directly**
- All requests go through Backend

---

## 10. Security

### 10.1 Secrets

- Never log service key
- Never include in error messages
- Rotate periodically

### 10.2 Input Validation

- Sanitize all user input before sending to AI
- Validate file types and sizes
- Limit request payload size

### 10.3 Output Validation

- Validate AI response structure
- Check score ranges
- Sanitize feedback before storing

---

## 11. Monitoring & Health

### 11.1 Health Check

```
GET /health
```

Response: `200 OK` if service is healthy

### 11.2 Metrics to Track

- Request latency (p50, p95, p99)
- Error rate by type
- Success rate
- Queue depth (for async jobs)

---

## 12. Appendix: Error Codes

| Error Code | HTTP Status | Description | Retry? |
|------------|-------------|-------------|--------|
| `invalid_audio_format` | 400 | Unsupported audio format | No |
| `audio_too_large` | 413 | File exceeds limit | No |
| `transcription_failed` | 500 | STT processing error | Yes |
| `evaluation_failed` | 500 | AI evaluation error | Yes |
| `invalid_request` | 400 | Malformed request | No |
| `validation_error` | 422 | Request validation failed | No |
| `unauthorized` | 401 | Invalid service key | No |
| `ai_provider_unavailable` | 503 | External AI down | Yes |
| `timeout` | 504 | Request timeout | Yes |
| `connection_error` | 503 | Cannot reach service | Yes |
