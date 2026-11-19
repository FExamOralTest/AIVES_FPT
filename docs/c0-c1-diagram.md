# AIVES v2.0 — C0 & C1 Architecture Diagrams (Left-to-Right)

## 1. C0 — System Context Diagram (Left-to-Right)

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

## 2. C1 — Multi-Container Architecture (Left-to-Right: Actor Workspaces → Backend & Queue → AI Processing Frame → Cloud)

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
