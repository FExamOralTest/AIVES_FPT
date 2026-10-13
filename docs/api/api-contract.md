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

## Backend question bank and exam selection

These routes use the backend base URL and a Bearer JWT with role `TEACHER`.
The caller must be the teacher currently assigned to the course. A teacher from
another course receives `403`, including when listing, searching, suggesting,
creating, updating, deleting questions or creating an exam. A missing course or
a question outside the route's course returns `404`.

### GET /api/courses/{courseId}/questions

Query parameters: `search` (optional, at most 1000 characters), `page` (default 1,
range 1–21474836), `pageSize` (default 20, range 1–100). Search is a literal,
case-insensitive substring match on question text or topic. Results are ordered
by creation time descending, then question ID descending. All counts refer to
the filtered course. An empty or out-of-range page has an empty `items` array.

**Response 200:**
```json
{
  "items": [{
    "questionId": 42,
    "courseId": 7,
    "documentId": 3,
    "questionText": "Explain microservices.",
    "topic": "Architecture",
    "sampleAnswer": "Services can be deployed independently.",
    "level": 2,
    "maxScore": 10,
    "isFollowUpAllowed": true,
    "createdAt": "2026-10-13T00:00:00Z",
    "isInUse": false
  }],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

### Question CRUD

| Method | Route | Success |
| --- | --- | --- |
| GET | `/api/courses/{courseId}/questions/{questionId}` | `200`, one question |
| POST | `/api/courses/{courseId}/questions` | `201`, one question and a Location header |
| PUT | `/api/courses/{courseId}/questions/{questionId}` | `200`, updated question |
| DELETE | `/api/courses/{courseId}/questions/{questionId}` | `204`, no body |

POST and PUT share this body:
```json
{
  "questionText": "Explain microservices.",
  "topic": "Architecture",
  "sampleAnswer": "Services can be deployed independently.",
  "level": 2,
  "maxScore": 10,
  "isFollowUpAllowed": true
}
```

`questionText` is required and nonblank (at most 10000 characters); `topic` and
`sampleAnswer` are optional (at most 1000 and 10000 characters). Text is trimmed.
`level` defaults to 1 and accepts 1–3; `maxScore` defaults to 1 and accepts finite
values 0.01–100; `isFollowUpAllowed` defaults to true. PUT replaces these editable
fields; course, document provenance and creation time remain fixed. A manually
created question has `documentId: null`. The question shape above also applies
to the existing generated-question response.

`isInUse` is true when any exam or answer references the question. PUT and DELETE
then return `409` without changing the question, exam or answer. This includes
draft exams. Create a new question to revise used content. Concurrent conflicts
return `409`; reload data before retrying. Invalid bodies or query parameters
return `400`; missing/invalid authentication returns `401`.

### POST /api/exams/suggest-questions

Replaces `/api/exams/ai-suggest-questions`, which is removed. This endpoint uses
keyword ranking and does not call an AI provider. The real AI generation endpoint
remains `/api/courses/{courseId}/documents/generate-questions`.

**Request:**
```json
{ "courseId": 7, "focusTopic": "microservices architecture", "maxSuggestions": 10 }
```

`courseId` must be positive; `focusTopic` is optional (at most 1000 characters),
and `maxSuggestions` defaults to 10 (range 1–100). Rank the entire course bank by
the number of distinct, case-insensitive keyword substrings in question text,
then maximum score, creation time and question ID descending. With no topic,
rank by maximum score, creation time and ID. Zero matches may still return
questions, and an empty bank returns an empty list.

**Response 200:**
```json
{
  "suggestedQuestions": [{
    "questionId": 42,
    "questionText": "Explain microservices.",
    "questionScore": 10,
    "maxScore": 10,
    "isFollowUpAllowed": true
  }],
  "selectionMethod": "keyword",
  "rationale": "Ranked by keyword matches in question text for: microservices architecture."
}
```

Clients must replace `aiRationale` with `rationale` and use the new route.

### POST /api/exams

**Request:**
```json
{
  "title": "Architecture viva",
  "courseId": 7,
  "durationMinutes": 30,
  "maxQuestions": 10,
  "questions": [{ "questionId": 42, "questionScore": 10 }]
}
```

Returns `201` with `ExamDto` and a Location header. A nonblank title is required
(at most 255 characters); duration is 1–480 and `maxQuestions` is 1–200. Question
IDs must be positive, distinct, exist and belong to the course. Scores must be
finite and within 0.01–100; selected count cannot exceed `maxQuestions`. Selected
bank questions must have nonblank text, level 1–3 and a valid maximum score.
Invalid selection returns `400` before any exam is saved. Missing or empty
`questions` creates an empty draft. Validation, exam creation and all question
assignments run in one transaction, so persistence failures leave no partial
exam. Concurrent conflicts return `409`.

`GET /api/exams/{examId}` requires both ownership and current course assignment.
`GET /api/exams/my` includes only owned exams in currently assigned courses,
with their complete question text. Reassignment revokes the previous teacher's
access to questions and exams.
