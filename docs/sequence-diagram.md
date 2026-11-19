# AIVES v2.0 — Complete Sequence Diagrams (5 End-to-End Flows)

## Sequence 1 — Upload Markdown Materials & AI Question/Rubric Generation

```mermaid
sequenceDiagram
    autonumber
    actor Staff as Staff / Teacher
    participant Web as React Web App
    participant API as ASP.NET Core API
    participant AIS as Python AI Service
    participant GEM as Google Gemini API
    participant DB as PostgreSQL 16

    Staff->>Web: Upload CONTENT.md & LEARNING_OUTCOMES.md
    Web->>API: POST /api/courses/{id}/documents [HTTPS + JWT]
    API->>DB: Save markdown_documents [TCP:5432]
    API-->>Web: 201 Created

    Staff->>Web: Click "Generate AI Questions & Rubrics"
    Web->>API: POST /api/questions/generate [HTTPS + JWT]
    API->>AIS: POST /api/v1/generate-questions [HTTP + X-Internal-Token]
    AIS->>GEM: Prompt with Course Content & Outcomes [HTTPS]
    GEM-->>AIS: Structured Questions + 3-5 Rubric Criteria
    AIS-->>API: Generated Question Drafts (JSON)
    API->>DB: Insert question_bank (PENDING_REVIEW) + question_rubric_criteria
    API-->>Web: Display Draft Questions & Rubrics

    Staff->>Web: Review, Edit & Approve Questions
    Web->>API: PATCH /api/questions/{id}/approve [HTTPS + JWT]
    API->>DB: Update approval_status = APPROVED + Insert audit_logs
    API-->>Web: Question Ready for Exams
```

---

## Sequence 2 — Exam Paper, ExamPeriod & Multi-Slot Session Scheduling

```mermaid
sequenceDiagram
    autonumber
    actor Staff as Staff / Teacher
    participant Web as React Web App
    participant API as ASP.NET Core API
    participant DB as PostgreSQL 16

    Staff->>Web: Create Exam Paper from Approved Questions
    Web->>API: POST /api/exams [HTTPS + JWT]
    API->>DB: Insert exams + exam_questions (Validate APPROVED)
    API-->>Web: Exam Created (DRAFT)

    Staff->>Web: Create ExamPeriod & Slot Templates
    Web->>API: POST /api/exam-periods [HTTPS + JWT]
    API->>DB: Insert exam_periods + exam_slot_templates
    API-->>Web: ExamPeriod Created

    Staff->>Web: Schedule ExamSession, Slots & Assign Examiner
    Web->>API: POST /api/exam-sessions [HTTPS + JWT]
    API->>DB: Insert exam_sessions + exam_session_slots + exam_session_exams
    API-->>Web: Session Scheduled (NOT_STARTED)

    Staff->>Web: Enroll Students into Session & Slot
    Web->>API: POST /api/exam-sessions/{id}/enrollments [HTTPS + JWT]
    API->>DB: Insert session_enrollments (Check Slot Capacity)
    API-->>Web: Students Whitelisted for Viva
```

---

## Sequence 3 — Live Viva Exam, Chunked Audio Upload to R2 & Queue Dispatch

```mermaid
sequenceDiagram
    autonumber
    actor Student
    participant Web as React Web App
    participant API as ASP.NET Core API
    participant R2 as Cloudflare R2
    participant MQ as Hangfire Queue
    participant DB as PostgreSQL 16

    Student->>Web: Click "Join Viva Session"
    Web->>API: POST /api/viva-sessions/{id}/start [HTTPS + JWT]
    API->>DB: Verify session_enrollments & Create Result (ResultId)
    API-->>Web: Return ResultId + Randomized Questions

    loop For Each Question (Or Early Finish)
        Web->>Student: Play Question Audio (TTS)
        Student->>Web: Speak Answer (MediaRecorder 4s Chunks)
        Web->>API: POST /api/media/chunks [Multipart Audio]
        Student->>Web: Click "Confirm Answer"
        Web->>API: POST /api/viva-sessions/submit-answer [ResultId, QuestionId]
        API->>R2: Merge Chunks & Upload .webm [S3 PutObject]
        R2-->>API: Confirm ObjectKey (viva/{resultId}/q{id}.webm)
        API->>DB: Upsert answers (Status = PROCESSING, ObjectKey)
        API-->>Web: 202 Accepted (Move to Next Question)
    end

    Student->>Web: Click "Finish Exam"
    Web->>API: POST /api/viva-sessions/{id}/finish [HTTPS + JWT]
    API->>DB: Update Result (Status = SUBMITTED, AiStatus = PENDING)
    API->>MQ: Enqueue SessionGradingService.GradeSessionAsync(ResultId)
    API-->>Web: 200 OK (Exam Submitted — Grading in Background)
```

---

## Sequence 4 — Async Hangfire Queue Worker, Whisper STT & Gemini Rubric Grading

```mermaid
sequenceDiagram
    autonumber
    participant MQ as Hangfire Worker (Backend)
    participant DB as PostgreSQL 16
    participant AIS as Python AI Service
    participant R2 as Cloudflare R2
    participant GEM as Google Gemini API

    MQ->>DB: Dequeue Job & Load Result + Answers + Rubric Criteria [TCP:5432]

    loop For Each Answer in Result (Or Batch Session Grade)
        MQ->>AIS: POST /api/v1/grade-session [HTTP:8000 + X-Internal-Token]
        AIS->>R2: Download Audio (.webm) via ObjectKey [S3 GetObject]
        R2-->>AIS: Audio Stream
        AIS->>AIS: Transcribe Audio via Faster-Whisper (STT)
        AIS->>GEM: Send Transcript + Question + Rubric Criteria [HTTPS]
        GEM-->>AIS: Criterion Scores + Strengths/Weaknesses (JSON)
        AIS-->>MQ: Return Transcript, AiScore & Rubric Breakdown
    end

    alt Evaluation Succeeded
        MQ->>DB: Update answers (Status = EVALUATED, Transcript, AiScore, JSONB)
        MQ->>DB: Update results (AiStatus = SUCCEEDED, Status = PENDING_REVIEW)
    else Transient Error / Timeout (Attempt < 3)
        MQ->>DB: Increment attempt_count & Re-schedule Job in Hangfire
    else Permanent Failure (Attempt == 3)
        MQ->>DB: Update answers (Status = ERROR) & results (AiStatus = FAILED)
    end
```

---

## Sequence 5 — Examiner Audio Review, Score Override, Retake Grant & Grade Publication

```mermaid
sequenceDiagram
    autonumber
    actor Examiner
    actor Student
    participant Web as React Web App
    participant API as ASP.NET Core API
    participant R2 as Cloudflare R2
    participant DB as PostgreSQL 16

    Examiner->>Web: Open Submission Review Detail
    Web->>API: GET /api/results/{resultId} [HTTPS + JWT]
    API->>DB: Load Result, Answers & Rubric JSONB
    API->>R2: Generate 15-min Presigned GET URL
    API-->>Web: Return AI Breakdown + Presigned Audio URL
    Web->>R2: Stream Student Audio Playback [HTTPS]

    opt Examiner Overrides AI Score (BR-14)
        Examiner->>Web: Enter New FinalScore + Mandatory Reason
        Web->>API: PATCH /api/results/{resultId}/override [HTTPS + JWT]
        API->>DB: Update final_score, status = REVIEWED + Insert audit_logs
    end

    opt Examiner Grants Exam Retake
        Examiner->>Web: Click "Grant Retake" with Reason
        Web->>API: POST /api/results/{resultId}/retake [HTTPS + JWT]
        API->>DB: Insert exam_retakes + Insert audit_logs
    end

    Examiner->>Web: Click "Publish Official Result" (BR-15)
    Web->>API: POST /api/results/{resultId}/publish [HTTPS + JWT]
    API->>DB: Lock Result (status = PUBLISHED) + Insert audit_logs
    API-->>Web: Result Published

    Student->>Web: Open "My Exam Results"
    Web->>API: GET /api/student/results [HTTPS + JWT]
    API->>DB: Query results WHERE status = PUBLISHED
    API-->>Web: Display Final Score, Transcript & Rubric Feedback
```


---

## Sequence 6 — Student Pre-Exam Authentication & Slot Verification

```mermaid
sequenceDiagram
    autonumber
    actor Student
    participant Web as React Web App
    participant API as ASP.NET Core API
    participant DB as PostgreSQL 16

    Student->>Web: Enter Credentials & Sign In
    Web->>API: POST /api/auth/login [HTTPS]
    API->>DB: Validate user (STUDENT) & Fetch active enrollments
    API-->>Web: 200 OK + JWT Bearer Token (Claims: UserId, Role)

    Student->>Web: Navigate to Assigned Viva Session
    Web->>API: GET /api/viva-sessions/{sessionId}/check-eligibility [HTTPS + JWT]
    API->>DB: Query session_enrollments & exam_session_slots
    alt Eligible & Slot Window Active
        API-->>Web: 200 OK (SlotId, StartTime, EndTime, ExaminerName)
        Web->>Student: Unlock Microphone Testing & "Start Exam" Button
    else Slot Window Not Reached
        API-->>Web: 403 Forbidden ("Session slot opens at {StartTime}")
        Web->>Student: Display Countdown Timer
    else Not Enrolled in Session
        API-->>Web: 403 Forbidden ("Student is not enrolled in this viva session")
        Web->>Student: Display Contact Staff Error
    end
```

---

## Sequence 7 — End-to-End Retake Workflow (Grant, Slot Re-allocation & Re-grading)

```mermaid
sequenceDiagram
    autonumber
    actor Examiner
    actor Student
    participant Web as React Web App
    participant API as ASP.NET Core API
    participant MQ as Hangfire Worker
    participant AIS as Python AI Service
    participant DB as PostgreSQL 16

    Examiner->>Web: Open Result Detail & Click "Grant Retake"
    Web->>API: POST /api/results/{resultId}/retake [HTTPS + JWT]
    API->>DB: Insert exam_retakes (Reason, GrantedBy) + Insert audit_logs
    API->>DB: Allocate New Slot in exam_session_slots & session_enrollments
    API-->>Web: 201 Created (Retake Authorized)

    Student->>Web: Open Viva Dashboard (Notified of Retake)
    Web->>API: GET /api/student/viva-sessions [HTTPS + JWT]
    API-->>Web: Return Active Retake Session Slot
    Student->>Web: Join Retake Slot & Speak New Answers
    Web->>API: POST /api/viva-sessions/{newSessionId}/finish [HTTPS + JWT]
    API->>DB: Insert new Result (IsRetake = true, Status = SUBMITTED)
    API->>MQ: Enqueue SessionGradingService(NewResultId)

    MQ->>AIS: POST /api/v1/grade-session [HTTP:8000 + X-Internal-Token]
    AIS-->>MQ: Return New AI Scores & Transcript
    MQ->>DB: Update new Result (AiStatus = SUCCEEDED, Status = PENDING_REVIEW)
    Examiner->>Web: Audit Retake Scores vs Original Attempt
    Examiner->>Web: Click "Publish Final Retake Result"
    Web->>API: POST /api/results/{newResultId}/publish [HTTPS + JWT]
    API->>DB: Lock Result (PUBLISHED) + Insert audit_logs
    API-->>Web: Retake Result Published
```
