# AIVES v2.0 — Complete 21-Entity ERD Diagrams

## 1. Full 21-Entity Architecture Map (Synced with `AivesDbContext.cs`)

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

## 2. Core 12-Entity High-Level ERD

```mermaid
flowchart LR
    U["User"]:::core -->|"teaches"| C["Course"]:::core
    C -->|"owns"| QB["QuestionBank"]:::core
    QB -->|"graded by"| QRC["QuestionRubricCriterion"]:::added

    C -->|"defines"| EXM["Exam"]:::core
    C -->|"opens"| EP["ExamPeriod"]:::added
    EP -->|"schedules"| ES["ExamSession"]:::core
    ES -->|"divides into"| ESS["ExamSessionSlot"]:::added

    U -->|"takes"| RES["Result"]:::core
    ES -->|"hosts"| RES
    EXM -->|"assigned to"| RES

    RES -->|"contains"| ANS["Answer"]:::core
    QB -->|"asked in"| ANS
    RES -->|"authorizes"| RET["ExamRetake"]:::added
    U -->|"recorded in"| AUD["AuditLog"]:::added

    classDef core fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:bold
    classDef added fill:#dcfce7,stroke:#15803d,stroke-width:2px,color:#14532d,font-weight:bold
```

---

## 3. Formal Crow's Foot ER Diagram (Full 21 Entities)

```mermaid
erDiagram
    USERS ||--o{ COURSES : "teaches"
    USERS ||--o{ ENROLLMENTS : "enrolls"
    COURSES ||--o{ ENROLLMENTS : "has"
    COURSES ||--o{ MARKDOWN_DOCUMENTS : "contains"
    COURSES ||--o{ QUESTION_BANK : "owns"
    QUESTION_BANK ||--|{ QUESTION_RUBRIC_CRITERIA : "defines"
    COURSES ||--o{ EXAMS : "has"
    EXAMS ||--|{ EXAM_QUESTIONS : "includes"
    QUESTION_BANK ||--o{ EXAM_QUESTIONS : "used_in"
    COURSES ||--o{ EXAM_PERIODS : "schedules"
    EXAM_PERIODS ||--o{ EXAM_SESSIONS : "contains"
    EXAM_SESSIONS ||--o{ EXAM_SESSION_SLOTS : "divides_into"
    EXAM_SESSIONS ||--o{ SESSION_ENROLLMENTS : "admits"
    EXAM_SESSION_SLOTS ||--o{ SESSION_ENROLLMENTS : "assigns"
    EXAM_SESSIONS ||--o{ RESULTS : "produces"
    EXAMS ||--o{ RESULTS : "evaluated_by"
    USERS ||--o{ RESULTS : "attempts"
    RESULTS ||--|{ ANSWERS : "contains"
    QUESTION_BANK ||--o{ ANSWERS : "answered_in"
    RESULTS ||--o{ MEDIA_UPLOADS : "stores_audio"
    RESULTS ||--o{ EXAM_RETAKES : "grants"
    USERS ||--o{ AUDIT_LOGS : "triggers"

    USERS {
        int user_id PK
        varchar username UK
        user_role role
    }
    COURSES {
        int course_id PK
        varchar course_code UK
        int teacher_id FK
    }
    QUESTION_BANK {
        int question_id PK
        int course_id FK
        varchar approval_status
    }
    QUESTION_RUBRIC_CRITERIA {
        int criterion_id PK
        int question_id FK
        decimal max_points
    }
    EXAMS {
        int exam_id PK
        int course_id FK
        exam_status status
    }
    EXAM_PERIODS {
        int period_id PK
        int course_id FK
        exam_period_status status
    }
    EXAM_SESSIONS {
        int session_id PK
        int period_id FK
        int examiner_id FK
        session_status status
    }
    EXAM_SESSION_SLOTS {
        int slot_id PK
        int session_id FK
        int capacity
    }
    RESULTS {
        int result_id PK
        int session_id FK
        decimal final_score
        submission_status status
    }
    ANSWERS {
        int answer_id PK
        int result_id FK
        int question_id FK
        decimal ai_score
    }
```
