# AIVES API Contract Gaps Analysis

**Version:** 1.0  
**Last Updated:** 2026-10-16  
**Owner:** Hưng (BE1)

---

## 1. Executive Summary

This document identifies gaps between the current implementation and the standardized API contract defined in `api-contract.md` and `backend-ai-contract.md`.

---

## 2. Missing Endpoints

### 2.1 Audio Playback Authorization

| Gap | Description | Severity |
|-----|-------------|----------|
| Missing Endpoint | No `/api/answers/{answerId}/audio-url` endpoint exists | HIGH |

**Current Implementation:**
- Audio URLs are stored directly in Answer.AudioUrl as local paths
- No presigned URL generation for R2

**Required Implementation:**
```csharp
// New endpoint needed in AnswersController or dedicated AudioController
[HttpGet("answers/{answerId}/audio-url")]
[Authorize]
public async Task<IActionResult> GetAudioUrl(int answerId)
{
    // Validate ownership
    // Generate presigned URL from R2
    // Return with TTL ≤ 3600 seconds
}
```

**Files to Create:**
- New controller: `AudioPlaybackController.cs`
- New service: `IAudioPlaybackService`

---

### 2.2 Answer Status Endpoint

| Gap | Description | Severity |
|-----|-------------|----------|
| Missing Endpoint | No `/api/student/answers/{answerId}/status` endpoint | MEDIUM |

**Current Implementation:**
- Status embedded in SubmitVivaAnswerResponse
- No way to poll for status after submission

**Required Implementation:**
```csharp
[HttpGet("student/answers/{answerId}/status")]
[Authorize(Roles = "STUDENT")]
public async Task<IActionResult> GetAnswerStatus(int answerId)
{
    // Return: answerId, status, transcript, aiScore, aiFeedback, error
}
```

---

### 2.3 Retry Answer Endpoint

| Gap | Description | Severity |
|-----|-------------|----------|
| Missing Endpoint | No `/api/student/answers/{answerId}/retry` endpoint | MEDIUM |

**Required Implementation:**
```csharp
[HttpPost("student/answers/{answerId}/retry")]
[Authorize(Roles = "STUDENT")]
public async Task<IActionResult> RetryAnswer(int answerId)
{
    // Only allow if Answer.Status == ERROR
    // Check retry limit
    // Re-queue Hangfire job
    // Return 202 Accepted
}
```

---

## 3. Inconsistent Error Handling

### 3.1 Error Response Format

| Issue | Current | Required |
|-------|---------|----------|
| No Standard | `{ "message": "..." }` | Problem Details (RFC 7807) |

**Current:** Anonymous objects with `message` field  
**Gap:** Not using Problem Details standard  
**Recommendation:** Create global exception handler

**Files to Modify:**
- `Program.cs` - Add exception handling middleware
- Create `ProblemDetailsFactory` custom implementation

---

### 3.2 HTTP Status Inconsistencies

| Endpoint | Current Status | Expected Status | Issue |
|----------|----------------|-----------------|-------|
| POST /api/exam-sessions | 200 OK | 201 Created | Should return 201 for resource creation |
| POST /api/documents/upload | 200 OK | 201 Created | Should return 201 |
| POST /api/exams | 200 OK | 201 Created | Should return 201 |
| DELETE /api/exam-sessions/{id} | 200 OK | 204 No Content | Should return 204 |

**Files to Modify:**
- ExamSessionsController.cs
- DocumentsController.cs
- ExamsController.cs

---

### 3.3 Missing ProducesResponseType Attributes

| Controller | Issue |
|------------|-------|
| VivaSessionController | Missing 202 Accepted for async operations |
| ResultsController | Missing proper response type documentation |
| ExamSessionsController | Missing 409 Conflict for duplicates |

---

## 4. Authentication & Authorization Gaps

### 4.1 Role Implementation

| Gap | Description | Severity |
|-----|-------------|----------|
| Role Enum | TEACHER still in codebase | MEDIUM |
| Role Migration | Need to migrate TEACHER → STAFF | HIGH |

**Current:**
- Role enum has: ADMIN, TEACHER, EXAMINER, STUDENT

**Required:**
- Role enum should be: ADMIN, STAFF, EXAMINER, STUDENT

**Files to Modify:**
- `Aives.Domain/Enums/UserRole.cs`

---

### 4.2 Missing Authorization Checks

| Endpoint | Gap | Severity |
|----------|-----|----------|
| GET /api/results/pending | Should filter by teacher/course assignment | MEDIUM |
| GET /api/exam-sessions/teacher/{teacherId} | Should validate teacher owns session | MEDIUM |
| POST /api/exams | Should validate course assignment | MEDIUM |

---

## 5. FastAPI Contract Gaps

### 5.1 Missing Service Authentication

| Gap | Description | Severity |
|-----|-------------|----------|
| No Service Auth | FastAPI endpoints not validating X-AI-Service-Key | HIGH |

**Required:**
- Add middleware to validate `X-AI-Service-Key` header on all FastAPI endpoints

---

### 5.2 STT Response Contract

| Field | Current | Required | Gap |
|-------|---------|----------|-----|
| duration | Missing | Required | MEDIUM |

**Files to Modify:**
- Faster-Whisper integration
- Response DTO: `SpeechToTextResponse`

---

### 5.3 Evaluation Response Contract

| Field | Current | Required | Gap |
|-------|---------|----------|-----|
| criterion_analyses | Optional | Required when rubric provided | MEDIUM |
| sentence_feedback | Optional | Required | LOW |

**Gap:** Backend must validate these fields when rubric is provided

---

## 6. State Machine Gaps

### 6.1 Answer State

| Current | Required | Gap |
|---------|----------|-----|
| No explicit state | PROCESSING → EVALUATED/ERROR | Need to implement |

**Required:**
- Add Answer.Status field (AnswerStatus enum)
- Update state transitions in service

---

### 6.2 Result State

| Current | Required | Gap |
|---------|----------|-----|
| Implemented | PENDING_REVIEW → REVIEWED → PUBLISHED | Need verification |

**Files to Verify:**
- ResultService.cs
- ResultRepository.cs

---

## 7. Data Validation Gaps

### 7.1 Input Validation

| Field | Current | Required | Gap |
|-------|---------|----------|-----|
| questionText | Max 10000 | Validate in DTO | MEDIUM |
| maxScore | No range check | 0.01-100 | MEDIUM |
| level | No range check | 1-3 | LOW |

---

### 7.2 Duplicate Prevention

| Endpoint | Gap | Severity |
|----------|-----|----------|
| POST /api/enroll/session | Check duplicate enrollment | MEDIUM |
| POST /api/exam-sessions | Check duplicate session code | MEDIUM |
| POST /api/auth/register | Check duplicate username/email | LOW |

---

## 8. Storage Gaps

### 8.1 R2 Integration

| Gap | Description | Severity |
|-----|-------------|----------|
| No Presigned URLs | Currently using local file storage | HIGH |
| No R2 Client | Need to implement R2 S3 client | HIGH |

**Required Implementation:**
- Install AWS S3 SDK (compatible with R2)
- Configure R2 credentials in appsettings
- Implement presigned URL generation

---

### 8.2 Audio Storage

| Current | Required | Gap |
|---------|----------|-----|
| Local `/uploads/audio/` | R2 private bucket | HIGH |

---

## 9. Queue & Retry Gaps

### 9.1 Hangfire Integration

| Gap | Description | Severity |
|-----|-------------|----------|
| No Hangfire Setup | Need to configure Hangfire | HIGH |
| No Job Metadata | Need to define job structure | MEDIUM |

**Required:**
- Configure Hangfire in Program.cs
- Create job classes for STT and Evaluation
- Implement retry policy (max 3 attempts)

---

### 9.2 Job Status Tracking

| Gap | Description | Severity |
|-----|-------------|----------|
| No Job ID | Need to track Hangfire job ID | MEDIUM |

---

## 10. Monitoring Gaps

### 10.1 Missing Health Checks

| Endpoint | Status |
|----------|--------|
| GET /health | Not implemented |
| GET /health/ai | Not implemented |

**Required:**
- Add health check endpoints
- Monitor AI service connectivity

---

## 11. Migration Plan

### Phase 1: Critical (Sprint 1)

1. Implement R2 storage with presigned URLs
2. Add Answer status endpoint
3. Add Retry answer endpoint
4. Implement Hangfire queue
5. Migrate TEACHER → STAFF role

### Phase 2: Important (Sprint 1-2)

1. Standardize error response format
2. Fix HTTP status inconsistencies
3. Add proper authorization checks
4. Implement service authentication for FastAPI

### Phase 3: Nice to Have (Sprint 2+)

1. Add health check endpoints
2. Implement comprehensive logging
3. Add metrics and monitoring

---

## 12. Breaking Changes

| Change | Impact | Migration Path |
|--------|--------|----------------|
| Role TEACHER → STAFF | Frontend, seeds | Update role references |
| Error format change | Frontend | Update error handling |
| HTTP status changes | Frontend, tests | Update assertions |
| Presigned URLs | Frontend | Update audio playback |

---

## 13. Questions for Team

1. Should we adopt Problem Details (RFC 7807) or keep current format?
2. What is the maximum audio file size limit? (Currently not defined)
3. Should we implement rate limiting? At what level?
4. What is the retry limit for failed answers? (Suggest 3)
5. Should we keep local file storage as fallback for development?

---

## 14. Appendix: File Changes Summary

### New Files to Create

| File | Purpose |
|------|---------|
| `AudioPlaybackController.cs` | Audio presigned URL endpoint |
| `IAudioPlaybackService.cs` | Audio URL generation interface |
| `AudioPlaybackService.cs` | R2 presigned URL implementation |

### Files to Modify

| File | Changes |
|------|---------|
| `Program.cs` | Add exception handling, Hangfire |
| `UserRole.cs` | Migrate TEACHAR → STAFF |
| `ExamSessionsController.cs` | Fix HTTP status |
| `DocumentsController.cs` | Fix HTTP status |
| `ExamsController.cs` | Fix HTTP status |
| `VivaSessionController.cs` | Add 202 responses |
| `SpeechToTextResponse.cs` | Add duration field |

### Files to Verify

| File | Purpose |
|------|---------|
| `ResultService.cs` | State machine implementation |
| `ResultRepository.cs` | Query implementation |
| `AiServiceClient.cs` | FastAPI contract compliance |
