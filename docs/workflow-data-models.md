# AIVES v2.0 — Static Workflow Data Models (WF-01, WF-02, WF-03)

> **Static Modeling Standards**:
> - **Nodes (Entities)**: Static entity names consistent across the system.
> - **Associations (Undirected Lines)**: In static structural modeling, connections use **undirected association lines (`---`) without arrowheads**, labeled with `Verb + Target Entity + Scope`.

---

## 1. WF-01: Prepare and Schedule an Examination

```mermaid
flowchart TD
    classDef entity fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:700,font-size:14px;

    COURSE["Course"]:::entity
    QB["QuestionBank"]:::entity
    RUBRIC["QuestionRubricCriterion"]:::entity
    EXAM["Exam"]:::entity
    SESSION["ExamSession"]:::entity
    ENROLL["SessionEnrollment"]:::entity
    USER["User"]:::entity

    COURSE ---|"defines course questions"| QB
    QB ---|"specifies rubric criteria"| RUBRIC
    COURSE ---|"provides exam papers"| EXAM
    EXAM ---|"schedules paper sessions"| SESSION
    SESSION ---|"allocates session enrollments"| ENROLL
    USER ---|"registers student enrollments"| ENROLL
```

---

## 2. WF-02: Complete a Scheduled Viva

```mermaid
flowchart TD
    classDef entity fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:700,font-size:14px;

    USER["User"]:::entity
    SESSION["ExamSession"]:::entity
    EXAM["Exam"]:::entity
    QB["QuestionBank"]:::entity
    RESULT["Result"]:::entity
    ANSWER["Answer"]:::entity

    USER ---|"initiates exam results"| RESULT
    SESSION ---|"records session results"| RESULT
    EXAM ---|"evaluates paper results"| RESULT
    RESULT ---|"contains spoken answers"| ANSWER
    QB ---|"grades question answers"| ANSWER
```

---

## 3. WF-03: Review and Publish an Official Result

```mermaid
flowchart TD
    classDef entity fill:#eff6ff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a,font-weight:700,font-size:14px;

    USER["User"]:::entity
    RESULT["Result"]:::entity
    ANSWER["Answer"]:::entity
    RETAKE["ExamRetake"]:::entity
    AUDIT["AuditLog"]:::entity

    USER ---|"publishes examiner results"| RESULT
    RESULT ---|"contains graded answers"| ANSWER
    RESULT ---|"grants retake authorizations"| RETAKE
    USER ---|"records audit logs"| AUDIT
```
