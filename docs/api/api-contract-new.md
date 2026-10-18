# AIVES API Contract Standardization

**Version:** 1.0  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Quy ước chung

### 1.1 Base URL

- **Backend (ASP.NET Core):** `http://localhost:5000` (development)
- **FastAPI (AI Service):** `http://localhost:8000` (development)

### 1.2 Authentication

- **Backend:** JWT Bearer Token
- **FastAPI:** Internal secret key via header `X-AI-Service-Key`

### 1.3 JSON Conventions

- **Naming:** `camelCase` for all fields
- **Date Format:** ISO 8601 (`2026-10-16T14:30:00Z`)
- **Null Handling:** Nullable fields marked with `?` in schemas

### 1.4 Error Response Format

> **Current Implementation:** Anonymous object `{ "message": "..." }`  
> **Recommendation:** Adopt Problem Details (RFC 7807) for better standardization

```json
// Current (used throughout codebase)
{ "message": "Error description here" }

// Proposed (Problem Details)
{
  "type": "https://api.aives.edu.vn/errors/validation-error",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed",
  "errors": [
    { "field": "email", "message": "Invalid email format" }
  ]
}
```

### 1.5 HTTP Status Conventions

| Status | Usage |
|--------|-------|
| `200 OK` | Success with response body |
| `201 Created` | Resource created successfully |
| `202 Accepted` | Async job accepted (audio upload) |
| `204 No Content` | Success, no response needed |
| `400 Bad Request` | Invalid request body |
| `401 Unauthorized` | Missing/invalid JWT token |
| `403 Forbidden` | Valid token but insufficient permissions |
| `404 Not Found` | Resource doesn't exist |
| `409 Conflict` | Duplicate/state conflict |
| `413 Payload Too Large` | Audio file exceeds limit |
| `422 Unprocessable Entity` | Validation errors |
| `429 Too Many Requests` | Rate limit exceeded |
| `500 Internal Server Error` | Unexpected errors |
| `502 Bad Gateway` | Upstream service error |
| `503 Service Unavailable` | Temporary service down |
| `504 Gateway Timeout` | Upstream timeout |

---

## 2. Role-Based Access Control

### 2.1 Roles

| Role | Description | Permissions |
|------|-------------|-------------|
| `ADMIN` | System Administrator | Full access to all resources |
| `STAFF` | Teacher/Lecturer | Manage courses, exams, sessions; view results |
| `EXAMINER` | Examiner | Review and publish results |
| `STUDENT` | Student | Enroll, take viva, view own results |

### 2.2 Authorization Rules

- **STAFF** can only access courses assigned to them
- **STUDENT** can only access their own data
- **STUDENT** cannot view results until `PUBLISHED`
- **EXAMINER** can review and publish results
- **AI Score** cannot override `FinalScore`

---

## 3. API Endpoints

### 3.1 Authentication & User Management

#### POST /api/auth/register

- **Auth:** Public
- **Request:**
```json
{
  "username": "string",
  "password": "string",
  "email": "string",
  "fullName": "string"
}
```
- **Response:** `201 Created`
```json
{
  "userId": 1,
  "username": "string",
  "email": "string",
  "fullName": "string",
  "role": "STUDENT"
}
```
- **Errors:** `400` (validation), `409` (duplicate username/email)

#### POST /api/auth/login

- **Auth:** Public (Rate Limited)
- **Request:**
```json
{
  "username": "string",
  "password": "string"
}
```
- **Response:** `200 OK`
```json
{
  "token": "eyJhbGc...",
  "expiresAt": "2026-10-16T15:30:00Z",
  "user": { "userId": 1, "username": "string", "role": "STUDENT" }
}
```
- **Errors:** `400`, `401` (invalid credentials)

#### GET /api/auth/me

- **Auth:** JWT (any role)
- **Response:** `200 OK`
```json
{
  "userId": 1,
  "username": "string",
  "email": "string",
  "fullName": "string",
  "role": "STUDENT"
}
```

#### POST /api/auth/admin/create-user

- **Auth:** ADMIN only
- **Request:**
```json
{
  "username": "string",
  "password": "string",
  "email": "string",
  "fullName": "string",
  "role": "STAFF"
}
```
- **Response:** `201 Created` - UserDto
- **Errors:** `400`, `403` (not admin), `409` (duplicate)

---

### 3.2 Course Management

#### GET /api/admin/courses

- **Auth:** ADMIN
- **Response:** `200 OK` - `IEnumerable<CourseDto>`

#### GET /api/admin/courses/{id}

- **Auth:** ADMIN
- **Response:** `200 OK` - CourseDto
- **Errors:** `404` (not found)

#### POST /api/admin/courses

- **Auth:** ADMIN
- **Request:**
```json
{
  "courseName": "string",
  "courseCode": "string",
  "description": "string"
}
```
- **Response:** `201 Created` - CourseDto

#### PUT /api/admin/courses/{id}/assign-teacher

- **Auth:** ADMIN
- **Request:**
```json
{ "teacherId": 1 }
```
- **Response:** `200 OK` - CourseDto
- **Errors:** `404` (course/teacher not found)

#### GET /api/teacher/courses

- **Auth:** STAFF, ADMIN
- **Response:** `200 OK` - Courses assigned to current user

---

### 3.3 Document Management

#### POST /api/documents/upload

- **Auth:** STAFF, ADMIN
- **Content-Type:** `multipart/form-data`
- **Form Fields:**
  - `courseId`: int (required)
  - `docType`: int (0=CONTENT, 1=LEARNING_OUTCOMES)
  - `file`: binary (markdown)
  - `title`: string (optional)
- **Response:** `201 Created` - MarkdownDocumentDto
- **Errors:** `400`, `404` (course not found)

#### GET /api/documents/course/{courseId}

- **Auth:** STAFF, ADMIN
- **Response:** `200 OK` - List<MarkdownDocumentDto>

#### DELETE /api/documents/{id}

- **Auth:** STAFF, ADMIN (owner only)
- **Response:** `204 No Content`
- **Errors:** `404`, `403` (not owner)

---

### 3.4 Question Bank

#### GET /api/courses/{courseId}/questions

- **Auth:** STAFF, ADMIN
- **Query Params:**
  - `search` (optional, max 1000 chars)
  - `page` (default 1, range 1-21474836)
  - `pageSize` (default 20, range 1-100)
- **Response:** `200 OK`
```json
{
  "items": [...],
  "totalCount": 100,
  "page": 1,
  "pageSize": 20,
  "totalPages": 5
}
```

#### POST /api/courses/{courseId}/questions

- **Auth:** STAFF, ADMIN (course owner only)
- **Request:**
```json
{
  "questionText": "string (required, max 10000)",
  "topic": "string (optional, max 1000)",
  "sampleAnswer": "string (optional, max 10000)",
  "level": 1,
  "maxScore": 10.0,
  "isFollowUpAllowed": true
}
```
- **Response:** `201 Created` - QuestionBankDto
- **Errors:** `400`, `403`, `404`

#### PUT /api/courses/{courseId}/questions/{questionId}

- **Auth:** STAFF, ADMIN (owner only)
- **Errors:** `409` (question in use)

#### DELETE /api/courses/{courseId}/questions/{questionId}

- **Auth:** STAFF, ADMIN (owner only)
- **Response:** `204 No Content`
- **Errors:** `409` (question in use)

---

### 3.5 Exam Management

#### POST /api/exams

- **Auth:** STAFF, ADMIN
- **Request:**
```json
{
  "title": "string (required, max 255)",
  "courseId": 1,
  "durationMinutes": 30,
  "maxQuestions": 10,
  "questions": [{ "questionId": 1, "questionScore": 10 }]
}
```
- **Response:** `201 Created` - ExamDto
- **Errors:** `400`, `404`

#### GET /api/exams/{examId}

- **Auth:** STAFF, ADMIN (owner + course assigned)
- **Response:** `200 OK` - ExamDto

#### GET /api/exams/my

- **Auth:** STAFF, ADMIN
- **Response:** `200 OK` - List<ExamDto> (owned exams in assigned courses)

#### POST /api/exams/suggest-questions

- **Auth:** STAFF, ADMIN
- **Request:**
```json
{
  "courseId": 1,
  "focusTopic": "microservices",
  "maxSuggestions": 10
}
```
- **Response:** `200 OK`
```json
{
  "suggestedQuestions": [...],
  "selectionMethod": "keyword",
  "rationale": "Ranked by keyword matches..."
}
```

---

### 3.6 Exam Session Management

#### POST /api/exam-sessions

- **Auth:** STAFF, ADMIN
- **Request:**
```json
{
  "examId": 1,
  "sessionName": "string",
  "room": "string",
  "startTime": "2026-10-20T09:00:00Z",
  "endTime": "2026-10-20T10:00:00Z",
  "maxCapacity": 50
}
```
- **Response:** `201 Created` - ExamSessionDto
- **Errors:** `400`, `404`

#### GET /api/exam-sessions/{sessionId}

- **Auth:** STAFF, ADMIN
- **Response:** `200 OK` - ExamSessionDto

#### GET /api/exam-sessions/teacher/{teacherId}

- **Auth:** STAFF, ADMIN
- **Response:** `200 OK` - List<ExamSessionSummaryDto>

#### PUT /api/exam-sessions/{sessionId}/status

- **Auth:** STAFF, ADMIN
- **Request:**
```json
{ "status": "OPEN" }
```
- **Response:** `200 OK` - ExamSessionDto

---

### 3.7 Enrollment

#### POST /api/enroll/session

- **Auth:** STUDENT
- **Request:**
```json
{ "sessionCode": "ABC123" }
```
- **Response:** `200 OK` - EnrollSessionResponse
- **Errors:** `400`, `404`, `409` (duplicate)

#### GET /api/student/enrollments

- **Auth:** STUDENT
- **Response:** `200 OK` - List<StudentSessionEnrollmentDto>

---

### 3.8 Viva Session (Student)

#### POST /api/student/viva-sessions/{sessionId}/start

- **Auth:** STUDENT (enrolled)
- **Response:** `200 OK` - StartVivaSessionResponse

#### GET /api/student/viva-sessions/{sessionId}/current-question

- **Auth:** STUDENT (in session)
- **Response:** `200 OK` - VivaQuestionDto

#### POST /api/student/viva-sessions/{sessionId}/submit-answer

- **Auth:** STUDENT (in session)
- **Content-Type:** `multipart/form-data`
- **Form Fields:**
  - `file`: binary (audio)
  - `questionId`: int
- **Response:** `202 Accepted` - SubmitVivaAnswerResponse
  - Backend queues Hangfire job
  - Returns answerId and processing status

#### POST /api/student/viva-sessions/{sessionId}/finish

- **Auth:** STUDENT (in session)
- **Response:** `200 OK` - FinishVivaSessionResponse

---

### 3.9 Processing Status & Retry

#### GET /api/student/answers/{answerId}/status

- **Auth:** STUDENT (owner only)
- **Response:** `200 OK`
```json
{
  "answerId": 1,
  "status": "EVALUATED",
  "transcript": "...",
  "aiScore": 8.5,
  "aiFeedback": "...",
  "error": null
}
```

#### POST /api/student/answers/{answerId}/retry

- **Auth:** STUDENT (owner only)
- **Precondition:** Answer status is `ERROR`
- **Response:** `202 Accepted`
- **Errors:** `400` (not in ERROR state), `404`, `429` (retry limit exceeded)

---

### 3.10 Results & Review

#### GET /api/results/pending

- **Auth:** EXAMINER, ADMIN
- **Response:** `200 OK` - List<ResultForReviewDto>

#### GET /api/results/session/{sessionId}

- **Auth:** EXAMINER, ADMIN
- **Response:** `200 OK` - List<ResultForReviewDto>

#### GET /api/results/{resultId}

- **Auth:** EXAMINER, ADMIN
- **Response:** `200 OK` - ResultForReviewDto

#### PUT /api/results/{resultId}/review

- **Auth:** EXAMINER, ADMIN
- **Request:**
```json
{
  "finalScore": 8.0,
  "finalFeedback": "Good answer...",
  "answerReviews": [
    { "answerId": 1, "finalScore": 8.0, "teacherComment": "..." }
  ]
}
```
- **Response:** `200 OK` - ResultForReviewDto
- **Precondition:** Result status is `PENDING_REVIEW` or `REVIEWED`
- **Errors:** `400`, `409`

#### POST /api/results/{resultId}/publish

- **Auth:** EXAMINER, ADMIN
- **Response:** `200 OK` - ResultForReviewDto
- **Precondition:** Result status is `REVIEWED` or `PENDING_REVIEW`
- **Errors:** `400`, `409`

---

### 3.11 Student Result View

#### GET /api/student/results/{sessionId}

- **Auth:** STUDENT (owner only)
- **Response:** `200 OK` - StudentResultDto
- **Postcondition:** Returns only if Result status is `PUBLISHED`
- **Errors:** `404` (not found), `403` (not published yet)

---

### 3.12 Audio Playback Authorization

#### GET /api/answers/{answerId}/audio-url

- **Auth:** STUDENT (owner), EXAMINER, ADMIN
- **Response:** `200 OK`
```json
{
  "audioUrl": "https://r2.aives.edu.vn/presigned/abc123?expires=3600",
  "expiresAt": "2026-10-16T15:30:00Z"
}
```
- **Preconditions:**
  - Bucket is private
  - URL TTL ≤ 3600 seconds
  - Ownership validated

---

## 4. Backend ↔ FastAPI Contracts

> **See:** `docs/backend-ai-contract.md` for detailed contracts

### 4.1 STT (Speech-to-Text)

**Endpoint:** `POST /ai/speech/transcribe`

- **Input:** multipart/form-data
  - `audio_file`: binary (WAV/WebM/MP3)
  - `exam_session_id`: UUID
  - `language`: BCP-47 (default `vi-VN`)
- **Output:**
```json
{
  "success": true,
  "transcript": "...",
  "confidence": 0.95,
  "language_detected": "vi-VN"
}
```
- **Errors:** `400`, `422`, `500`, `503`

### 4.2 AI Evaluation

**Endpoint:** `POST /ai/evaluate/session`

- **Input:**
```json
{
  "exam_session_id": "uuid",
  "student_id": "uuid",
  "course_name": "string",
  "documents": [...],
  "question_answers": [
    {
      "question_id": "uuid",
      "question_text": "string",
      "student_answer": "string",
      "is_follow_up": false,
      "rubric": [...]
    }
  ],
  "max_score_per_question": 10.0
}
```
- **Output:**
```json
{
  "success": true,
  "exam_session_id": "uuid",
  "total_score": 7.5,
  "max_total_score": 10.0,
  "percentage": 75.0,
  "overall_feedback": "...",
  "question_scores": [...],
  "study_suggestions": [...]
}
```

### 4.3 Follow-up Question Generation

**Endpoint:** `POST /ai/interview/follow-up`

- **Input:**
```json
{
  "exam_session_id": "uuid",
  "course_name": "string",
  "documents": [...],
  "original_question": "string",
  "student_answer": "string",
  "question_index": 1
}
```
- **Output:**
```json
{
  "success": true,
  "follow_up_question": "...",
  "reason": "...",
  "should_follow_up": true
}
```

---

## 5. State Machines

### 5.1 Answer State

```
PROCESSING → EVALUATED
         ↘ ERROR
```

- `PROCESSING`: Audio uploaded, STT running
- `EVALUATED`: STT complete, AI evaluation done
- `ERROR`: Processing failed (retry available)

### 5.2 Result State

```
PENDING_REVIEW → REVIEWED → PUBLISHED
```

- `PENDING_REVIEW`: All required answers processed
- `REVIEWED`: Examiner reviewed
- `PUBLISHED`: Student can view

---

## 6. Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| JSON Fields | camelCase | `userId`, `courseName` |
| Route Parameters | kebab-case | `/exam-sessions/{session-id}` |
| Query Parameters | camelCase | `?pageSize=20` |
| Enums | UPPER_SNAKE | `PENDING_REVIEW`, `PUBLISHED` |

---

## 7. Security Rules

1. **No Secrets in Responses:** JWT tokens, API keys, passwords never in response
2. **Private R2 Bucket:** Audio stored in private bucket, presigned URLs with TTL ≤ 3600s
3. **Ownership Validation:** All endpoints validate resource ownership
4. **Role Checks:** EXAMINER/ADMIN required for result operations
5. **No Client-side STT:** Frontend must call Backend, Backend calls FastAPI

---

## 8. Appendix: HTTP Status Quick Reference

| Code | Name | Use Case |
|------|------|----------|
| 200 | OK | Success |
| 201 | Created | Resource created |
| 202 | Accepted | Async job accepted |
| 204 | No Content | Delete success |
| 400 | Bad Request | Invalid input |
| 401 | Unauthorized | No/invalid token |
| 403 | Forbidden | No permission |
| 404 | Not Found | Resource missing |
| 409 | Conflict | Duplicate/state conflict |
| 413 | Payload Too Large | File too big |
| 422 | Unprocessable Entity | Validation failed |
| 429 | Too Many Requests | Rate limit |
| 500 | Internal Server Error | Unexpected |
| 502 | Bad Gateway | Upstream error |
| 503 | Service Unavailable | Service down |
| 504 | Gateway Timeout | Upstream timeout |
