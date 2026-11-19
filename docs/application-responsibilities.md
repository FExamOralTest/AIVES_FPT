# Application Responsibilities — AIVES V2.0

> Responsibility view of the audited **VivaSession Controller**, **VivaSession Service**, **Results Repository**, and **Background AI Jobs** families (Slide 13 layout).

---

## 1. Architecture Diagram

```mermaid
flowchart LR
    classDef ctrl fill:#eff6ff,stroke:#2563eb,stroke-width:1.5px,color:#1e3a8a,font-weight:700;
    classDef srv fill:#ecfdf5,stroke:#059669,stroke-width:1.5px,color:#065f46,font-weight:700;
    classDef repo fill:#fff7ed,stroke:#ea580c,stroke-width:1.5px,color:#c2410c,font-weight:700;
    classDef jobs fill:#faf5ff,stroke:#9333ea,stroke-width:1.5px,color:#6b21a8,font-weight:700;

    C["VivaSession controller family<br/><small style='font-weight:normal;color:#475569'>HTTP boundary<br/>Identity and input</small>"]:::ctrl
    S["VivaSession service family<br/><small style='font-weight:normal;color:#475569'>Start, submit and finish<br/>Domain orchestration</small>"]:::srv
    R["Results repository family<br/><small style='font-weight:normal;color:#475569'>Persist and retrieve<br/>Result and answer data</small>"]:::repo
    J["Answer orchestration and SessionGrading<br/><small style='font-weight:normal;color:#475569'>AI client and background jobs</small>"]:::jobs

    C --> S
    S --> R
    S -->|Async enqueue| J
    J -.->|Update AI scores| R
```

---

## 2. Responsibilities Breakdown

| Component Family | Scope | Responsibilities (Audited) | Underlying Classes |
| :--- | :--- | :--- | :--- |
| **VivaSession controller family** | HTTP Gateway | Validates student JWT authentication, room session codes, and request payloads. | `VivaSessionController`, `ChunkedAudioController` |
| **VivaSession service family** | Domain Orchestration | Coordinates the live viva session lifecycle (`Start`, `Submit`, `Finish`) and dispatches grading jobs. | `VivaSessionService`, `ChunkedAudioUploadService` |
| **Results repository family** | Persistence & Data Access | Persists and retrieves student answers, R2 audio keys, and evaluations. Enforces `BR-11`, `BR-12`, and `BR-15`. | `ResultRepository`, `AnswerRepository` |
| **Answer orchestration & SessionGrading** | Background AI Queue | Hangfire async background job processing. Calls Gemini AI rubric scoring and updates final scores in the repository. | `HangfireQueueService`, `SessionGradingService`, `AiServiceClient` |

---

## 3. Interactive HTML Viewer

The interactive HTML view with responsive slide vector rendering is available at:
👉 **[`docs/application-responsibilities.html`](file:///d:/FPT/GitHub/AIVES_FPT/docs/application-responsibilities.html)**
