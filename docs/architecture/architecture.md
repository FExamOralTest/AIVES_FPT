# AIVES v2.0 — System Architecture & Master Diagrams

> **Version:** 2.0 (Audited & Synchronized with Codebase)  
> **Scope:** C0 Context, C1 Multi-Container (with Hangfire Queue & Protocols), Full 21-Entity ERD, Entity Lifecycle State Machines, and 5 End-to-End Sequence Flows.

---

## 1. C0 — System Context Diagram (Level 0)

High-level system boundary showing the **4 User Roles** (`Admin`, `Staff / Teacher`, `Examiner`, `Student`), the **AIVES v2.0 Platform**, and its **2 External Cloud Dependencies** (`Cloudflare R2` and `Google Gemini API`).

```mermaid
flowchart LR
    subgraph ACTORS["👥 System Actors (4 Roles)"]
        direction TB
        AD["Admin<br/>(System & Accounts)"]:::actor
        STF["Staff / Teacher<br/>(Courses, Questions & Slots)"]:::actor
        ST["Student<br/>(Takes Oral Viva Exam)"]:::actor
        EX["Examiner<br/>(Reviews & Publishes Grades)"]:::actor
    end

    SYS["🎓 AIVES v2.0 Platform<br/>AI-Based Interactive Viva Examination System"]:::system

    subgraph EXT["☁️ External Cloud Services"]
        direction TB
        R2[("Cloudflare R2<br/>Audio Object Storage")]:::external
        GEM["Google Gemini API<br/>LLM Rubric Grading & TTS"]:::external
    end

    AD -->|"Manage Users & Courses [HTTPS]"| SYS
    STF -->|"Upload .md, Approve Questions, Schedule Sessions [HTTPS]"| SYS
    ST -->|"Speak Oral Answers & View Final Grades [HTTPS]"| SYS
    EX -->|"Audit Audio, Override Scores & Publish Results [HTTPS]"| SYS

    SYS -->|"Upload & Stream Audio (.webm) [S3 API / Presigned URL]"| R2
    SYS -->|"Generate Questions, TTS & Grade Transcripts [HTTPS / REST]"| GEM

    classDef actor fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef system fill:#1e40af,stroke:#1e3a8a,stroke-width:3px,color:#ffffff,font-weight:bold
    classDef external fill:#fffbeb,stroke:#d97706,stroke-width:2px,color:#92400e,font-weight:bold
```

---

## 2. C1 — Multi-Container Architecture (Queue, Backend–AI & Protocols)

Detailed multi-container deployment inside the AIVES v2.0 boundary, explicitly modeling the **Hangfire Message Queue & Worker**, **Backend–AI internal protocol (`HTTP:8000 / X-Internal-Token`)**, **Cloudflare R2 (`S3 API / Presigned URL`)**, and **Neon PostgreSQL 16 (`TCP:5432`)**.

```mermaid
flowchart TB
    USERS["👥 Users<br/>(Admin / Staff / Examiner / Student)"]:::actor

    subgraph BOUNDARY["🏢 AIVES v2.0 Multi-Container Boundary (Docker Compose)"]
        direction TB
        SPA["💻 Web Application<br/>(React 19 + TypeScript + Vite)<br/>Port: 5173 / 80"]:::container
        API["⚙️ Backend API Server<br/>(ASP.NET Core 8 Web API)<br/>Port: 5068 / 8080"]:::container
        MQ[["📨 Hangfire Message Queue & Worker<br/>(Async SessionGrading / AnswerOrchestrator)<br/>Max 3 Retries"]]:::queue
        AIS["🤖 AI Microservice<br/>(Python 3.11 + FastAPI + Faster-Whisper)<br/>Port: 8000"]:::ai
        DB[("🗄️ Neon PostgreSQL 16<br/>(22 Domain Tables + Hangfire Schema)<br/>Port: 5432")]:::db
    end

    R2[("☁️ Cloudflare R2<br/>(S3 Audio Storage)")]:::external
    GEM["✨ Google Gemini API<br/>(LLM Scoring & Question Gen)"]:::external

    USERS -->|"1. Browser UI [HTTPS]"| SPA
    SPA -->|"2. REST API Calls [HTTPS / JSON + JWT Bearer]"| API
    SPA -.->|"3. Stream Recording Playback [HTTPS Presigned GET URL]"| R2

    API -->|"4. Read / Write Entities [TCP:5432 / EF Core 8]"| DB
    API -->|"5. Upload Merged Audio [HTTPS / S3 PutObject]"| R2
    API -->|"6. Enqueue Grading Job [Hangfire PostgreSQL Queue]"| MQ
    API -->|"7. Sync Question Gen & Follow-up [HTTP:8000 / X-Internal-Token]"| AIS

    MQ -->|"8. Trigger Async Session Grading [HTTP:8000 / X-Internal-Token]"| AIS
    MQ -->|"9. Save AI Scores & Status [TCP:5432 / EF Core 8]"| DB

    AIS -->|"10. Download Audio for Whisper STT [HTTPS / S3 GetObject]"| R2
    AIS -->|"11. Rubric Prompt & Structured Output [HTTPS / Gemini REST API]"| GEM

    classDef actor fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef container fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef queue fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#581c87,font-weight:bold
    classDef ai fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#14532d,font-weight:bold
    classDef db fill:#e0f2fe,stroke:#0369a1,stroke-width:2px,color:#0c4a6e,font-weight:bold
    classDef external fill:#fffbeb,stroke:#d97706,stroke-width:2px,color:#92400e,font-weight:bold
```

---

## 3. Complete 21-Entity Database Architecture (ERD)

Covers all 21 `DbSet<T>` entities in `AivesDbContext.cs`, including `ExamPeriod`, `ExamSlotTemplate`, `ExamSlotTemplateExam`, `ExamSessionExam`, `ExamSessionSlot`, `ExamSessionSlotExam`, `QuestionRubricCriterion`, `MediaUpload`, `ExamRetake`, and `AuditLog`.

```mermaid
flowchart TB
    subgraph D1["📚 1. Identity, Course & Question Bank"]
        direction LR
        U["User (users)"]
        C["Course (courses)"]
        ENR["Enrollment (enrollments)"]
        MD["MarkdownDocument (markdown_documents)"]
        QB["QuestionBank (question_bank)"]
        QRC["QuestionRubricCriterion (question_rubric_criteria)"]
    end

    subgraph D2["🗓️ 2. Exam Paper, Period & Multi-Slot Scheduling"]
        direction LR
        EXM["Exam (exams)"]
        EQ["ExamQuestion (exam_questions)"]
        EP["ExamPeriod (exam_periods)"]
        EST["ExamSlotTemplate (exam_slot_templates)"]
        ESTE["ExamSlotTemplateExam (exam_slot_template_exams)"]
        ES["ExamSession (exam_sessions)"]
        ESE["ExamSessionExam (exam_session_exams)"]
        ESS["ExamSessionSlot (exam_session_slots)"]
        ESSE["ExamSessionSlotExam (exam_session_slot_exams)"]
        SE["SessionEnrollment (session_enrollments)"]
    end

    subgraph D3["🎙️ 3. Live Viva Execution, Audio Chunks & Grading"]
        direction LR
        RES["Result (results)"]
        ANS["Answer (answers)"]
        MU["MediaUpload (media_uploads)"]
    end

    subgraph D4["🛡️ 4. Retake & Audit Governance"]
        direction LR
        RET["ExamRetake (exam_retakes)"]
        AUD["AuditLog (audit_logs)"]
    end

    U -->|"teaches"| C
    U -->|"registers"| ENR
    C -->|"has"| ENR
    C -->|"stores"| MD
    C -->|"owns"| QB
    QB -->|"defines 3-5"| QRC

    C -->|"has"| EXM
    C -->|"opens"| EP
    EXM -->|"includes"| EQ
    QB -->|"selected in"| EQ
    EP -->|"defines"| EST
    EST -->|"links"| ESTE
    EXM -->|"assigned to"| ESTE
    EP -->|"schedules"| ES
    ES -->|"links"| ESE
    EXM -->|"pooled in"| ESE
    ES -->|"divides into"| ESS
    ESS -->|"links"| ESSE
    EXM -->|"pooled in"| ESSE
    ES -->|"admits"| SE
    ESS -->|"assigns slot"| SE
    U -->|"enrolled in"| SE

    ES -->|"produces"| RES
    ESS -->|"tracks slot"| RES
    EXM -->|"randomized for"| RES
    U -->|"takes"| RES
    RES -->|"contains"| ANS
    QB -->|"answered by"| ANS
    RES -->|"uploads chunks"| MU
    U -->|"uploads"| MU
    RES -->|"grants retake"| RET
    ES -->|"retake in"| RET
    U -->|"granted to"| RET
    U -->|"logged in"| AUD
```

---

## 4. Entity Lifecycles & State Machine Diagrams

### 4.1 Result & AI Evaluation Lifecycle (`SubmissionStatus` + `AiEvaluationStatus`)

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

### 4.2 Answer & Hangfire Queue Retry Lifecycle (`AnswerStatus`)

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

### 4.3 ExamPeriod, Exam & ExamSession Lifecycles

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

## 5. End-to-End Sequence Diagrams

See [docs/sequence-diagram.html](file:///d:/FPT/GitHub/AIVES_FPT/docs/sequence-diagram.html) and [docs/sequence-diagram.md](file:///d:/FPT/GitHub/AIVES_FPT/docs/sequence-diagram.md) for all 5 complete interactive sequence diagrams:
1. **Seq 1**: Upload Markdown Materials & AI Question/Rubric Generation
2. **Seq 2**: Exam Paper, ExamPeriod & Multi-Slot Session Scheduling
3. **Seq 3**: Live Viva Exam, Chunked Audio Upload to R2 & Queue Dispatch
4. **Seq 4**: Async Hangfire Queue Worker, Whisper STT & Gemini Rubric Grading
5. **Seq 5**: Examiner Audio Review, Score Override, Retake Grant & Grade Publication
