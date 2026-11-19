# AIVES – Swimlane Diagram Index (English)

**Part of work:** Member 5 – Swimlane Diagram
**Source of truth:** `AIVES_Software_Requirements_Specification.docx` (07/11/2026) + actual source code `backend/Aives.Api`, `backend/Aives.Application`, `backend/Aives.Infrastructure`, `ai-service/app`.
**Common convention:** every **lane** = an entity holding *responsibility information* (which actor/service owns the processing step). Every box is an action; **every error branch returns to the lane that owns the handling**.

> 🇻🇳 Vietnamese version: [`swimlane-index.md`](./swimlane-index.md)

---

## ⚡ Simplified diagram set (for presenting)

> 📄 **View everything on one page:** [`all-diagrams-en.md`](./all-diagrams-en.md) · [HTML](./all-diagrams-en.html)

The set above is **100% accurate** but very wide → use it for reference & deep questions.
When **dropping images into slides**, use the 3 simplified diagrams:

| # | Diagram | Size | Doc |
|---|---|---|---|
| **DIAGRAM 1** | Use Case Diagram (4 actors · 13 use cases) | 1847×1614 | [MD](./simplified/diagram-01-usecase_en.md) |
| **DIAGRAM 2** | Simplified Swimlane (one box = one flow) | 1854×1593 | [MD](./simplified/diagram-02-swimlane_en.md) |
| **DIAGRAM 3** | System Context Diagram | 1229×640 | [MD](./simplified/diagram-03-context_en.md) |

👉 **[Open the simplified diagram set](./simplified/simplified-index-en.md)**

---

## Assignment (SV5)

> *"Draw two flows: preparation and exam scheduling; taking the exam, grading and publication. Split lanes by responsibility information. Include error branches: submit error, grading error and re-check request."*
> Deliverable: *"Two Swimlane Diagrams, with an explanation of the responsibility handover points."*

---

## Catalogue 🇬🇧 English

| # | Diagram | Lanes | Doc | Image | Size |
|---|---|---|---|---|---|
| **SWL-1-EN** | Preparation & Exam Scheduling | ADMIN · STAFF · AI Service · Backend/DB | [MD](./swimlane-01-preparation-scheduling_en.md) | `SWIMLANE_01_PREPARATION_EN.svg/.png` | 2334x3000 |
| **SWL-2-EN** | Exam Delivery, Grading & Publication *(full)* | STUDENT · Client · Backend · AI · Hangfire · EXAMINER | [MD](./swimlane-02-exam-grading-publish_en.md) | `SWIMLANE_02_EXAM_GRADING_PUBLISH_EN.svg/.png` | 7042x4578 |
| **SWL-2A-EN** | Exam Delivery & AI Grading | STUDENT · Client · Backend · AI · Hangfire | [MD](./swimlane-02a-exam-delivery-grading_en.md) | `SWIMLANE_02A_EXAM_DELIVERY_GRADING_EN.svg/.png` | 5788x3047 |
| **SWL-2B-EN** | Review, Publication & Incident Handling | Backend · EXAMINER · STUDENT | [MD](./swimlane-02b-review-publish-incident_en.md) | `SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT_EN.svg/.png` | 2874x1737 |

## Catalogue 🇻🇳 Tiếng Việt

| # | Sơ đồ | Lane | Tài liệu | Ảnh | Kích thước |
|---|---|---|---|---|---|
| **SWL-1** | Chuẩn bị & Tổ chức thi | ADMIN · STAFF · AI Service · Backend/DB | [MD](./swimlane-01-preparation-scheduling.md) | `SWIMLANE_01_PREPARATION.svg/.png` | 2084x3030 |
| **SWL-2** | Làm bài, Chấm & Công bố *(đầy đủ)* | STUDENT · Client · Backend · AI · Hangfire · EXAMINER | [MD](./swimlane-02-exam-grading-publish.md) | `SWIMLANE_02_EXAM_GRADING_PUBLISH.svg/.png` | 6411x4578 |
| **SWL-2A** | Làm bài & Chấm AI | STUDENT · Client · Backend · AI · Hangfire | [MD](./swimlane-02a-exam-delivery-grading.md) | `SWIMLANE_02A_EXAM_DELIVERY_GRADING.svg/.png` | 5230x3047 |
| **SWL-2B** | Review, Công bố & Xử lý sự cố | Backend · EXAMINER · STUDENT | [MD](./swimlane-02b-review-publish-incident.md) | `SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT.svg/.png` | 2699x1737 |

---

## Error branches summary

| Error branch | Diagram | Condition | Handling | Responsible lane |
|---|---|---|---|---|
| Duplicate email/username | SWL-1 | BR-03 | API 400, correct and recreate | ADMIN |
| Invalid `.md` document | SWL-1 | FR-STF-01 | API 400, correct and re-upload | STAFF |
| LLM question generation failure | SWL-1 | FR-STF-03 | `error_code`, staff writes the question manually | STAFF |
| Total weight ≠ 100% | SWL-1 | BR-08 | API 400, adjust the weights | STAFF |
| Slot time conflict / outside period | SWL-1 | BR-10 | API 400, fix the configuration | STAFF |
| Duplicate enrollment / slot full | SWL-1 | BR-11 | API 400, fix the list / slots | STAFF |
| **Start preconditions not met** | SWL-2A | BR-11 | 400/403, message shown, retry | Client → STUDENT |
| **STT failure (submit error)** | SWL-2A | FR-AI-03 | `EMPTY_AUDIO`/`TIMEOUT`/`INTERNAL_ERROR`, re-answer or text fallback | Client → STUDENT |
| No answers submitted | SWL-2A | FR-STU-04 | `AiEvaluationStatus = FAILED` | STUDENT |
| **Grading error (retryable)** | SWL-2A | BR-15 | Hangfire retries up to 3 times, backoff 30s/120s/300s | Hangfire Worker |
| **Grading error (non-retryable) / attempts exhausted** | SWL-2A | FR-AI-02 | `FAILED` + `Answer.Status = ERROR` + audit log | Hangfire → Backend |
| Publish before review / score missing | SWL-2B | BR-10 | API 409, review again then publish | EXAMINER |
| Overall score outside the valid range | SWL-2B | NFR-DATA-03 | API 400, correct the per-answer scores | EXAMINER |
| **Request a re-check (retry answer)** | SWL-2B | FR-EXA-05 | `POST /api/answers/{id}/retry` → `RetryAnswerJob` | EXAMINER/ADMIN |
| **Incident requiring a new attempt (retake)** | SWL-2B | FR-EXA-06, BR-16/17 | Old Result → `CANCELLED`, new session, history preserved, audit log | EXAMINER/ADMIN |
| Retake not started / already consumed / expired | SWL-2B | BR-17 | API 400/403, the student contacts the examiner | STUDENT |

---

## Traceability: Swimlane ↔ SRS

| SRS requirement | Diagram | Lane |
|---|---|---|
| FR-ADM-01/02, BR-03 | SWL-1 | ADMIN |
| FR-STF-01/02/03 | SWL-1 | STAFF + AI Service |
| FR-STF-04, BR-07/08 | SWL-1 | STAFF |
| FR-STF-05/06/07, BR-10 | SWL-1 | STAFF |
| FR-STU-01/02, BR-11 | SWL-2A | STUDENT/Backend |
| FR-STU-03/04/08 | SWL-2A | Client |
| FR-STU-05, BR-14 | SWL-2A | AI/Backend |
| FR-STU-06, BR-15 | SWL-2A | Backend/Hangfire |
| FR-AI-01/02, BR-13 | SWL-2A | AI Service |
| FR-EXA-01/02/03, BR-09 | SWL-2B | EXAMINER |
| FR-EXA-04, BR-10 | SWL-2B | EXAMINER |
| FR-EXA-05/06, BR-16/17 | SWL-2B | EXAMINER/ADMIN |
| FR-STU-07, BR-18 | SWL-2B | STUDENT |
| FR-DATA-01/02 | SWL-2A + SWL-2B | Backend |

---

## Existing functionality vs. proposals

| Item | Existing (verified in the source code) | Proposed (no verifiable path yet) |
|---|---|---|
| Re-grading | `POST /api/answers/{answerId}/retry` + `RetryAnswerJob` | Automatically recompute `Result.AiScore` after a retry (FR-AI-05) |
| Grading-context snapshot | Stores `provider`, `model`, `rubric` and answers into `AiAnalysis` | Snapshot of the original question/rubric/document (FR-AI-04) |
| Distinguish "STT failure" vs "student did not answer" | Stored in `Answer.ErrorMessage` | A dedicated `STT_FAILED` state (FR-AI-03) |
| Publish conditions | `REVIEWED` + a score are already checked | A configurable publication gate (FR-EXA-04 – Partial) |
| Correcting a published result | No API yet | An appeal/correction workflow (FR-EXA-07) |

---

## How to re-render (large print applied)

```powershell
python ops\_apply_skin.py    # applies the skin (font 22, bold, 3.0px arrows) + renders SVG/PNG
python ops\_svg2png.py       # rasterises SVG -> full-resolution PNG via headless Edge
```

> `ops/tools/plantuml.jar` is a local tool and is listed in `.gitignore`.
> Diagram text is **22pt bold** (title 28pt), connectors are **3.0px**, box borders **2.5px** – legible when projected.
> PlantUML caps its own PNG output at 4096px, so `_svg2png.py` re-renders from the SVG to avoid truncation.

---

## Which image to use in a slide

1. **16:9 slide** → use **SVG** (vector, no pixelation). PowerPoint: *Insert → Pictures → This Device*, pick the `.svg` file.
2. **If you need PNG** → use the `.png` files rendered at **native resolution** (see the "Size" column); they are not truncated.
3. **The full SWL-2 is 6411–7042px wide** → prefer **SWL-2A + SWL-2B** over one single slide.
4. The **EN** set is for English/international slides; the **VI** set keeps Vietnamese business terminology.

---

[[← Back to the Vietnamese index](./swimlane-index.md)] · [[SWL-1-EN ←](./swimlane-01-preparation-scheduling_en.md)] · [[SWL-2A-EN ←](./swimlane-02a-exam-delivery-grading_en.md)] · [[SWL-2B-EN ←](./swimlane-02b-review-publish-incident_en.md)]
