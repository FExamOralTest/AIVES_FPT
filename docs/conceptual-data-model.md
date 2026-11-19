# AIVES V2.0 — Minimal Conceptual Examination Data Model

> Streamlined 7-entity examination data model showing strictly **Entity / Table Names** and **Business Relationships** following the exact criteria of **C0** and **C1**.

---

## 1. Overview of Core Entities

| Entity / Table | Conceptual Role | Plain English Description |
| :--- | :--- | :--- |
| **Course** | Academic Subject | Root course defining materials, questions, and exam papers (e.g. `PRN222`, `SWD392`). |
| **QuestionBank** | Question Catalog | Approved oral viva questions and rubric grading guidelines. |
| **Exam** | Exam Paper | Curated set of questions selected for a specific test paper. |
| **ExamSession** | Exam Schedule / Slot | Specific viva session and room where students sit for the oral exam. |
| **User** | System Actor | Account representing Student (candidate) or Examiner (reviewer). |
| **Result** | Exam Attempt Outcome | Overall student exam score and review status (`PENDING_REVIEW` / `PUBLISHED`). |
| **Answer** | Oral Voice Response | Student voice recording, transcription text, and question-level evaluation. |

---

## 2. Interactive HTML Viewer

The interactive HTML view with responsive, dynamically rendered diagrams and tab switching is available at:
👉 **[`docs/conceptual-data-model.html`](file:///d:/FPT/GitHub/AIVES_FPT/docs/conceptual-data-model.html)**

---

## 3. Streamlined Diagrams

### View 1: Entity Graph (Flowchart Style — Like C0 & C1)

```mermaid
flowchart TD
    classDef entity fill:#eff6ff,stroke:#2563eb,stroke-width:1.5px,color:#1e3a8a,font-weight:700,font-size:13px;

    COURSE[Course]:::entity
    QB[QuestionBank]:::entity
    EXAM[Exam]:::entity
    SESSION[ExamSession]:::entity
    USER[User]:::entity
    RESULT[Result]:::entity
    ANSWER[Answer]:::entity

    %% 1. Preparation
    COURSE -->|defines| QB
    COURSE -->|creates| EXAM
    EXAM -->|includes| QB

    %% 2. Scheduling
    EXAM -->|held in| SESSION
    USER -->|attends| SESSION

    %% 3. Examination & Evaluation
    SESSION -->|records| RESULT
    USER -->|takes| RESULT
    RESULT -->|contains| ANSWER
    ANSWER -->|answers| QB
```

---

### View 2: Conceptual ER Diagram (Crow's Feet Cardinality)

```mermaid
erDiagram
    Course ||--o{ QuestionBank : "defines"
    Course ||--o{ Exam : "creates"
    Exam }o--o{ QuestionBank : "includes"
    Exam ||--o{ ExamSession : "held in"
    User ||--o{ ExamSession : "attends"
    ExamSession ||--o{ Result : "records"
    User ||--o{ Result : "takes"
    Result ||--o{ Answer : "contains"
    Answer }o--|| QuestionBank : "answers"
```

---

### View 3: PlantUML Source Code

```plantuml
@startuml Conceptual_Examination_Data_Model
skinparam shadowing false
skinparam DefaultFontName "Segoe UI", Arial, sans-serif
skinparam DefaultFontSize 11
skinparam ArrowColor #334155
skinparam ArrowThickness 1.2

skinparam class {
    BackgroundColor #eff6ff
    BorderColor #2563eb
    FontColor #1e3a8a
    FontSize 12
    FontStyle bold
}

hide circle
hide empty members

title CONCEPTUAL EXAMINATION DATA MODEL — AIVES (MINIMAL)

class "Course" as Course
class "QuestionBank" as Question
class "Exam" as Exam
class "ExamSession" as Session
class "User" as User
class "Result" as Result
class "Answer" as Answer

' 1. Academic Preparation
Course "1" --> "0..*" Question : defines
Course "1" --> "0..*" Exam : creates
Exam "1" --> "1..*" Question : includes

' 2. Exam Scheduling
Exam "1" --> "1..*" Session : held in
User "1" --> "0..*" Session : attends

' 3. Result & Evaluation
Session "1" --> "0..*" Result : records
User "1" --> "0..*" Result : takes
Result "1" --> "1..*" Answer : contains
Answer "0..*" --> "1" Question : answers

@enduml
```
