# AIVES v2.0 — Entity Lifecycle & State Machine Diagrams

## 1. Result & AI Grading Lifecycle (`SubmissionStatus` + `AiEvaluationStatus`)

```mermaid
flowchart LR
    START(( )) -->|"Student Starts Viva"| SUB["SUBMITTED<br/>(AiStatus: PENDING)"]
    SUB -->|"Hangfire Worker Grades"| PR["PENDING_REVIEW<br/>(AiStatus: SUCCEEDED)"]
    SUB -->|"AI Worker Fails (3x)"| FAIL["PENDING_REVIEW<br/>(AiStatus: FAILED)"]
    FAIL -->|"Manual Re-queue"| SUB
    PR -->|"Examiner Audits / Overrides"| REV["REVIEWED<br/>(AuditLog Recorded)"]
    PR -->|"Examiner Direct Publish"| PUB["PUBLISHED<br/>(Visible to Student)"]
    REV -->|"Examiner Publishes"| PUB
    SUB -->|"Session Aborted"| CAN["CANCELLED"]
    PR -->|"Invalid Attempt"| CAN
    PUB --> ENDNode((( )))
    CAN --> ENDNode
```

---

## 2. Answer & Hangfire Queue Retry Lifecycle (`AnswerStatus`)

```mermaid
flowchart LR
    START(( )) -->|"Audio Merged & Uploaded to R2"| PROC["PROCESSING<br/>(AttemptCount: 0..3)"]
    PROC -->|"Faster-Whisper STT + Gemini Rubric OK"| EVAL["EVALUATED<br/>(AiScore & JSONB Saved)"]
    PROC -->|"Timeout / API Error (Attempt < 3)"| RETRY["Hangfire Auto-Retry<br/>(AttemptCount++)"]
    RETRY -->|"Re-dispatch Job"| PROC
    PROC -->|"AttemptCount >= 3 Failed"| ERR["ERROR<br/>(ErrorMessage Logged)"]
    ERR -->|"Examiner / Admin Clicks Retry"| PROC
    EVAL --> ENDNode((( )))
```

---

## 3. ExamPeriod, Exam & ExamSession Lifecycles

```mermaid
flowchart TB
    subgraph EP["🗓️ A. ExamPeriod Lifecycle (ExamPeriodStatus)"]
        direction LR
        EP_D["DRAFT"] -->|"Staff Activates"| EP_A["ACTIVE"]
        EP_A -->|"Period Ends"| EP_C["COMPLETED"]
        EP_D -->|"Cancel"| EP_X["CANCELLED"]
        EP_A -->|"Cancel"| EP_X
    end

    subgraph EX["📄 B. Exam Paper Lifecycle (ExamStatus)"]
        direction LR
        EX_D["DRAFT"] -->|"Assign to Period/Slot"| EX_S["SCHEDULED"]
        EX_S -->|"Window Opens"| EX_O["OPEN"]
        EX_O -->|"Window Closes"| EX_CL["CLOSED"]
        EX_D -->|"Cancel"| EX_X["CANCELLED"]
        EX_S -->|"Cancel"| EX_X
    end

    subgraph ES["🎙️ C. ExamSession Lifecycle (SessionStatus)"]
        direction LR
        ES_N["NOT_STARTED"] -->|"StartTime Reached / First Student Joins"| ES_I["IN_PROGRESS"]
        ES_I -->|"EndTime Reached / All Finished"| ES_C["COMPLETED"]
        ES_N -->|"Staff Cancels"| ES_X["CANCELLED"]
        ES_I -->|"Emergency Cancel"| ES_X
    end
```

---

## 4. Question Approval & Exam Retake Lifecycles

```mermaid
flowchart TB
    subgraph QA["❓ A. Question Bank Approval Lifecycle (QuestionApprovalStatus)"]
        direction LR
        Q_P["PENDING_REVIEW"] -->|"Staff Approves"| Q_A["APPROVED"]
        Q_P -->|"Staff Rejects"| Q_R["REJECTED"]
        Q_R -->|"Staff Revises"| Q_A
    end

    subgraph RT["🔁 B. Exam Retake Lifecycle (exam_retakes)"]
        direction LR
        R_O["Original Result"] -->|"Examiner Grants Retake"| R_G["RETAKE GRANTED"]
        R_G -->|"Student Re-enters Session"| R_I["NEW RESULT: SUBMITTED"]
        R_I -->|"AI Grades & Examiner Publishes"| R_P["PUBLISHED"]
    end
```

