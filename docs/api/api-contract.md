# AIVES AI Service - API Contract

Base URL: `http://localhost:8000`
Prefix: `/ai`
Auth: Internal secret key shared with Backend (`AI_SERVICE_SECRET_KEY`)

## Endpoints

### POST /ai/interview/question
Generate the next viva interview question.

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
      "content": "...",
      "document_type": "course_content | learning_outcomes"
    }
  ],
  "question_index": 1,
  "total_questions": 5,
  "already_asked_topics": ["topic1"]
}
```

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "question": "...",
  "question_type": "conceptual | application | analysis",
  "difficulty": "easy | medium | hard",
  "expected_keywords": ["..."],
  "topic": "..."
}
```

---

### POST /ai/interview/follow-up
Analyse student answer and decide if a follow-up question is needed.

**Request:**
```json
{
  "exam_session_id": "uuid",
  "course_name": "Software Architecture",
  "documents": [...],
  "original_question": "...",
  "student_answer": "...",
  "question_index": 1
}
```

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "follow_up_question": "...",
  "reason": "...",
  "should_follow_up": true
}
```

---

### POST /ai/speech/transcribe
Speech-to-Text (multipart/form-data).

**Form fields:**
- `audio_file`: audio file (WAV / WebM / MP3)
- `exam_session_id`: UUID
- `language`: BCP-47 code (default `vi-VN`)

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "transcript": "...",
  "confidence": 0.95,
  "language_detected": "vi-VN"
}
```

---

### POST /ai/speech/synthesize
Text-to-Speech.

**Request:**
```json
{
  "text": "Xin chào",
  "language": "vi-VN",
  "voice": "vi-VN-HoaiMyNeural"
}
```

**Response 200:**
```json
{
  "success": true,
  "message": null,
  "audio_base64": "...",
  "audio_format": "mp3",
  "duration_seconds": null
}
```

---

### POST /ai/evaluate/answer
Score a single Q&A pair.

**Request:**
```json
{
  "exam_session_id": "uuid",
  "course_name": "Software Architecture",
  "documents": [...],
  "question_answer": {
    "question_id": "uuid",
    "question_text": "...",
    "student_answer": "...",
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
  "question_id": "uuid",
  "score": 7.5,
  "max_score": 10.0,
  "feedback": "...",
  "strengths": ["..."],
  "weaknesses": ["..."],
  "missing_content": ["..."]
}
```

---

### POST /ai/evaluate/session
Evaluate a full exam session.

**Request:**
```json
{
  "exam_session_id": "uuid",
  "student_id": "uuid",
  "course_name": "Software Architecture",
  "documents": [...],
  "question_answers": [
    {
      "question_id": "uuid",
      "question_text": "...",
      "student_answer": "...",
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
  "exam_session_id": "uuid",
  "total_score": 7.5,
  "max_total_score": 10.0,
  "percentage": 75.0,
  "overall_feedback": "...",
  "question_scores": [
    {
      "question_id": "uuid",
      "question_text": "...",
      "score": 7.5,
      "feedback": "..."
    }
  ]
}
```

---

## Error Responses

All endpoints return standard error format:

```json
{
  "success": false,
  "error": "ErrorType",
  "detail": "Human readable message"
}
```

Status codes: 400 (bad request), 422 (validation), 500 (internal), 503 (AI provider unavailable).
