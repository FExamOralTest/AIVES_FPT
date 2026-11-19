# AIVES v2.0 — System Architecture & Master Diagrams

> **Version:** 2.0 (Audited & Synchronized with Codebase)  
> **Scope:** C0 Context, C1 Multi-Container (with Hangfire Queue & Protocols), Full 21-Entity ERD, Entity Lifecycle State Machines, 7 End-to-End Sequence Flows, and Traceability Matrix.

> ### 📌 Các bộ sơ đồ & Tài liệu liên quan
>
> | Bộ sơ đồ | Dùng để | Liên kết |
> |---|---|---|
> | **🌟 Master Architecture Dashboard** | Xem & xuất ảnh UHD 4x/8x/SVG toàn bộ sơ đồ | [`aives-diagrams.html`](../aives-diagrams.html) |
> | **1. C0 & C1 Diagrams (Left-to-Right)** | Kiến trúc Multi-Container & Protocol | [`c0-c1-diagram.html`](../c0-c1-diagram.html) · [`c0-c1-diagram.md`](../c0-c1-diagram.md) |
> | **2. Complete 21-Entity ERD** | CSDL 21 bảng đồng bộ `AivesDbContext.cs` | [`erd-diagram.html`](../erd-diagram.html) · [`erd-diagram.md`](../erd-diagram.md) |
> | **3. Entity State Lifecycles** | Vòng đời Result, Answer Queue Retry, ExamPeriod, Session | [`lifecycle-diagram.html`](../lifecycle-diagram.html) · [`lifecycle-diagram.md`](../lifecycle-diagram.md) |
> | **4. End-to-End Sequence Diagrams** | 7 luồng tương tác khép kín từ AI gen đến Retake | [`sequence-diagram.html`](../sequence-diagram.html) · [`sequence-diagram.md`](../sequence-diagram.md) |
> | **5. Static Workflow Data Models** | Mô hình dữ liệu tĩnh (WF-01, WF-02, WF-03) | [`workflow-data-models.html`](../workflow-data-models.html) · [`workflow-data-models.md`](../workflow-data-models.md) |

---

## 1. C0 — System Context Diagram (Level 0, Left-to-Right)

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
flowchart LR
    subgraph ROLES["👥 1. Actors (Who)"]
        direction TB
        AD["Admin"]:::actor
        STF["Staff / Teacher"]:::actor
        STU["Student"]:::actor
        EXM["Examiner"]:::actor
    end

    subgraph BOUNDARY["🏢 AIVES v2.0 Multi-Container System Boundary"]
        direction LR

        subgraph SPA["💻 2. Web App Container (React 19 SPA :5173) — Actor Workspaces"]
            direction TB
            W_AD["Admin Workspace<br/>(Users, Roles & Courses)"]:::portal
            W_STF["Staff Workspace<br/>(Upload .md, Approve Bank, Schedule Slots)"]:::portal
            W_STU["Student Viva Workspace<br/>(Mic Chunk Recording & Published Grades)"]:::portal
            W_EXM["Examiner Review Workspace<br/>(R2 Audio Player, Score Override, Publish)"]:::portal
        end

        subgraph CORE["⚙️ 3. Backend & Queue Containers (.NET 8)"]
            direction TB
            API["Backend API Server<br/>(ASP.NET Core 8 :5068)<br/>RBAC, Chunk Merge, R2 Upload"]:::container
            MQ[["Hangfire Message Queue & Worker<br/>(Async SessionGradingService)<br/>Background Dispatch · Max 3 Retries"]]:::queue
            DB[("Neon PostgreSQL 16 (:5432)<br/>22 Domain Tables + Hangfire Jobs")]:::db
        end

        subgraph AIFRAME["🤖 4. AI Microservice Frame (Python FastAPI :8000)"]
            direction TB
            AI_GEN["Question & Rubric Generator<br/>(Parses Course .md → 3-5 Criteria)"]:::ai
            AI_STT["Faster-Whisper STT Engine<br/>(Converts R2 .webm Audio → Transcript)"]:::ai
            AI_GRD["Rubric Grading & Follow-up Engine<br/>(Scores Transcript per Criterion)"]:::ai
        end
    end

    subgraph EXT["☁️ 5. External Cloud Services"]
        direction TB
        R2[("Cloudflare R2<br/>(S3 Audio Storage)")]:::external
        GEM["Google Gemini API<br/>(LLM & Structured JSON)"]:::external
    end

    AD -->|"HTTPS"| W_AD
    STF -->|"HTTPS"| W_STF
    STU -->|"HTTPS"| W_STU
    EXM -->|"HTTPS"| W_EXM

    W_AD -->|"REST JSON + JWT"| API
    W_STF -->|"REST JSON + JWT"| API
    W_STU -->|"Multipart Audio + JWT"| API
    W_EXM -->|"REST JSON + JWT"| API
    W_EXM -.->|"Stream Audio [Presigned GET URL]"| R2

    API -->|"Read / Write [TCP:5432 / EF Core]"| DB
    API -->|"Enqueue ResultId [Hangfire Queue]"| MQ
    MQ -->|"Save AI Scores [TCP:5432]"| DB
    API -->|"Upload Merged .webm [S3 PutObject]"| R2

    API -->|"Sync Gen Questions [HTTP:8000 + X-Internal-Token]"| AI_GEN
    MQ -->|"Async Grade Session [HTTP:8000 + X-Internal-Token]"| AI_STT
    AI_STT -->|"Pass Transcript"| AI_GRD

    AI_STT -->|"Download Audio [S3 GetObject]"| R2
    AI_GEN -->|"Prompt Syllabus [HTTPS REST]"| GEM
    AI_GRD -->|"Evaluate Rubric [HTTPS REST]"| GEM

    classDef actor fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef portal fill:#f8fafc,stroke:#3b82f6,stroke-width:1.5px,color:#1e3a8a,font-weight:bold
    classDef container fill:#dbeafe,stroke:#1d4ed8,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef queue fill:#f3e8ff,stroke:#7e22ce,stroke-width:2px,color:#581c87,font-weight:bold
    classDef ai fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#14532d,font-weight:bold
    classDef db fill:#e0f2fe,stroke:#0369a1,stroke-width:2px,color:#0c4a6e,font-weight:bold
    classDef external fill:#fffbeb,stroke:#d97706,stroke-width:2px,color:#92400e,font-weight:bold
```

> **Topology multi-container (Docker Compose):**
>
> | Service | Cổng trong container | Cổng host | Ghi chú |
> |---|---|---|---|
> | `frontend` | `:80` (Nginx) | `:5173` | Reverse proxy `/api` và `/uploads` → `http://backend:8080` |
> | `backend` | `:8080` | `:5068` | ASP.NET Core 8 + Hangfire Worker (`SessionGradingService`) |
> | `ai-service` | `:8000` | `:8000` | FastAPI / uvicorn, chỉ nội bộ qua Docker DNS với header `X-Internal-Token` |
> | `postgres` | `:5432` | — | PostgreSQL 16 (profile: local-db, mặc định kết nối Neon Cloud) |
>
> Internal DNS: `backend` → `http://ai-service:8000`, `ai-service` → `http://backend:8080`.  
> Bản PlantUML C1 chi tiết: [`c1_container.puml`](./c1_container.puml).

---

## 3. Complete 21-Entity Database Architecture (ERD)

Covers all 21 `DbSet<T>` entities in `AivesDbContext.cs`, organized into 4 logical domain containers: `Course Curriculum & Questions`, `Exam Paper & Scheduling`, `Execution & Audio`, and `Governance & Audit`.

```mermaid
flowchart LR
    classDef core fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:700;
    classDef added fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#14532d,font-weight:700;

    subgraph D1["📚 1. Course Curriculum & AI Questions"]
        direction TB
        COURSE["Course"]:::core
        MD["MarkdownDocument"]:::core
        QB["QuestionBank"]:::core
        QRC["QuestionRubricCriterion"]:::added

        COURSE -->|"stores"| MD
        COURSE -->|"owns"| QB
        QB -->|"defines 3-5"| QRC
    end

    subgraph D2["🗓️ 2. Exam Paper, Period & Scheduling"]
        direction TB
        EXAM["Exam"]:::core
        EQ["ExamQuestion"]:::core
        EP["ExamPeriod"]:::added
        EST["ExamSlotTemplate"]:::added
        ESTE["ExamSlotTemplateExam"]:::added
        SESSION["ExamSession"]:::core
        ESE["ExamSessionExam"]:::added
        ESS["ExamSessionSlot"]:::added
        ESSE["ExamSessionSlotExam"]:::added

        COURSE -->|"creates"| EXAM
        EXAM -->|"includes"| EQ
        COURSE -->|"opens"| EP
        EP -->|"defines"| EST
        EST -->|"links"| ESTE
        EXAM -->|"assigned to"| ESTE
        EP -->|"schedules"| SESSION
        SESSION -->|"links"| ESE
        EXAM -->|"pools"| ESE
        SESSION -->|"divides"| ESS
        ESS -->|"links"| ESSE
        EXAM -->|"pools"| ESSE
    end

    subgraph D3["🎙️ 3. Execution, Audio Chunks & Grading"]
        direction TB
        USER["User"]:::core
        ENROLL["Enrollment"]:::core
        SENROLL["SessionEnrollment"]:::core
        RESULT["Result"]:::core
        ANSWER["Answer"]:::core
        MEDIA["MediaUpload"]:::added

        USER -->|"enrolled"| ENROLL
        COURSE -->|"tracks"| ENROLL
        SESSION -->|"admits"| SENROLL
        ESS -->|"assigns"| SENROLL
        USER -->|"registers"| SENROLL
        SESSION -->|"records"| RESULT
        ESS -->|"tracks"| RESULT
        EXAM -->|"evaluates"| RESULT
        USER -->|"submits"| RESULT
        RESULT -->|"contains"| ANSWER
        QB -->|"grades"| ANSWER
        RESULT -->|"uploads"| MEDIA
        USER -->|"records"| MEDIA
    end

    subgraph D4["🛡️ 4. Governance & Audit"]
        direction TB
        RETAKE["ExamRetake"]:::added
        AUDIT["AuditLog"]:::added

        RESULT -->|"authorizes"| RETAKE
        RETAKE -->|"retake in"| SESSION
        USER -->|"granted to"| RETAKE
        USER -->|"audited in"| AUDIT
    end
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

Chi tiết toàn bộ 7 luồng tương tác với animation và xuất ảnh độ phân giải cao tại [`docs/sequence-diagram.html`](../sequence-diagram.html) và [`docs/sequence-diagram.md`](../sequence-diagram.md):
1. **Seq 1**: Upload Course Markdown & AI Question/Rubric Generation
2. **Seq 2**: Exam Paper, ExamPeriod & Multi-Slot Session Scheduling
3. **Seq 3**: Live Viva Exam, Chunked Audio Upload to R2 & Queue Dispatch
4. **Seq 4**: Async Hangfire Queue Worker, Whisper STT & Gemini Rubric Grading
5. **Seq 5**: Examiner Audio Review, Score Override & Grade Publication
6. **Seq 6**: Student Pre-Exam Authentication & Slot Eligibility Verification
7. **Seq 7**: End-to-End Retake Authorization, Slot Re-allocation & Re-grading

---

## 6. Bảng Truy vết Nghiệp vụ (Traceability Matrix)

| Business Rule | Sơ đồ liên quan | UI Component | API Route | Controller & Service | Entity & Table DB | Trạng thái kiểm chứng |
|---|---|---|---|---|---|---|
| **BR-01**: Admin quản lý tài khoản & phân quyền (RBAC) | C0, C1 | `UsersPage.tsx`, `UserFormDialog.tsx` | `GET /api/admin/users`<br>`POST /api/auth/admin/create-user`<br>`DELETE /api/admin/users/{id}` | `AdminUserController`, `AuthController`, `UserService` | `Users` (`users`) | Đã kiểm thử CRUD, xóa an toàn |
| **BR-02**: Admin phân công giảng viên phụ trách môn | C0, C1 | `AdminCoursesPage.tsx` | `POST /api/admin/courses`<br>`PUT /api/admin/courses/{id}`<br>`PUT /api/admin/courses/{id}/assign-teacher` | `AdminCourseController`, `CourseService` | `Courses` (`courses`) | Đã kiểm thử tạo, sửa tên & gán GV |
| **BR-03**: Giảng viên upload tài liệu chuẩn (.md) | C1, Sequence | `TeacherCoursesPage.tsx` | `POST /api/courses/{id}/documents`<br>`GET /api/courses/{id}/documents/{docId}` | `TeacherDocumentController`, `DocumentsController`, `DocumentService` | `MarkdownDocuments` (`markdown_documents`) | Upload Multipart, lưu nội dung vào DB & xem lại |
| **BR-04**: AI sinh câu hỏi từ tài liệu chuẩn | C1 | `TeacherQuestionsPage.tsx` | `POST /api/courses/{id}/documents/generate-questions` | `DocumentsController`, `DocumentService`, FastAPI AI Service | `QuestionBanks` (`question_banks`) | Hỗ trợ chọn số lượng, level và trọng tâm |
| **BR-05**: Giảng viên quản lý ngân hàng câu hỏi (CRUD) | C1 | `TeacherQuestionsPage.tsx` | `GET /api/courses/{id}/questions`<br>`DELETE /api/courses/{id}/questions/{id}` | `QuestionBankController`, `QuestionBankService` | `QuestionBanks` (`question_banks`) | Tìm kiếm, lọc theo level và xóa câu hỏi |
| **BR-06**: Giảng viên tạo đề thi từ ngân hàng | Sequence | `CreateExamDialog.tsx`, `ExamDetailDialog.tsx` | `POST /api/exams`<br>`POST /api/exams/suggest-questions`<br>`PUT /api/exams/{id}/questions` | `ExamsController`, `ExamService` | `Exams` (`exams`), `ExamQuestions` | AI gợi ý chọn câu, bắt buộc có câu hỏi |
| **BR-07**: Giảng viên lập lịch ca thi & sinh SessionCode | State ExamSession | `SessionsPage.tsx` | `POST /api/exam-sessions`<br>`PATCH /api/exam-sessions/{id}/status` | `ExamSessionsController`, `ExamSessionService` | `ExamSessions` (`exam_sessions`) | Mã `ESS-XXXXXXXX`, chặn tạo ca rỗng |
| **BR-08**: Sinh viên ghi danh bằng SessionCode | Sequence, State | `EnrollPage.tsx`, `StudentSessionsPage.tsx` | `POST /api/student/enrollments`<br>`GET /api/student/enrollments` | `StudentEnrollmentsController`, `EnrollController` | `SessionEnrollments` | Kiểm tra lịch, sĩ số tối đa |
| **BR-09**: Sinh viên trả lời Viva (âm thanh & văn bản) | C1, Sequence | `VivaRoomPage.tsx` | `POST /api/viva/sessions/{id}/start`<br>`POST /api/viva/sessions/{id}/submit-answer` | `VivaSessionController`, `VivaSessionService` | `Answers` (`answers`), File storage audio | Thu âm trực tiếp, STT, AI chấm điểm rubric |
| **BR-10**: Giảng viên phúc khảo & sửa điểm bài thi | Sequence, State Result | `TeacherResultsPage.tsx`, `ResultReviewModal.tsx` | `GET /api/teacher/results/{id}`<br>`PUT /api/teacher/results/{id}/review` | `ResultsController`, `ResultService` | `Results`, `Answers` | Nghe file ghi âm, xem gợi ý rubric AI |
| **BR-11**: Công bố kết quả & sinh viên tra cứu | Sequence, State Result | `StudentResultsPage.tsx` | `POST /api/teacher/results/{id}/publish`<br>`GET /api/student/results/{sessionId}` | `ResultsController`, `StudentResultController`, `ResultService` | `Results` (`results`) | Điểm chính thức chỉ hiển thị khi `PUBLISHED` |
| **BR-14**: Ghi đè điểm AI & bắt buộc ghi nhận AuditLog | C1, Seq 5 | `ResultReviewModal.tsx` | `PATCH /api/results/{id}/override` | `ResultsController`, `ResultService` | `AuditLogs` (`audit_logs`) | Lưu FinalScore, lý do & ActorId |
| **BR-15**: Khóa bài thi khi công bố kết quả | C1, Seq 5 | `ResultReviewModal.tsx` | `POST /api/results/{id}/publish` | `ResultsController`, `ResultService` | `Results` (`results`) | Không thể sửa sau khi đã công bố |
| **BR-17**: Hangfire Queue Retry tối đa 3 lần | C1, Seq 4 | Background | `POST /api/v1/grade-session` | `SessionGradingService`, AI Microservice | `Answers` (`answers`) | Tự động retry theo backoff 30s-120s-300s |
