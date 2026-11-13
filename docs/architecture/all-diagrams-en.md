# AIVES – All Diagrams on One Page

**Part of work:** Member 5 – Diagram documentation
**Combined HTML page:** [`all-diagrams-en.html`](./all-diagrams-en.html) · [VI](./all-diagrams.html)

> This page collects **every** AIVES diagram, ordered from the **simplified** set (drop into slides) down to the **full detailed** set (for reference).

---

## 🗂️ Overview

```
AIVES – All Diagrams on One Page
│
├─ ⚡ SIMPLIFIED  (for presenting — fits one slide)
│   ├─ DIAGRAM 1 · Use Case Diagram
│   ├─ DIAGRAM 2 · Simplified Swimlane (one box = one flow)
│   ├─ DIAGRAM 3 · System Context Diagram
│   ├─ DIAGRAM 4 · Swimlane 10 Activity (most compact swimlane)
│   └─ DIAGRAM 5 · Simplified Sequence Diagram (the real call order)
│
├─ 📚 DETAILED  (reference — 6 lanes, ~40 boxes, 8 error branches)
│   ├─ SWL-1  · Preparation & Exam Scheduling
│   ├─ SWL-2  · Exam Delivery, Grading & Publication (full, 6 lanes)
│   ├─ SWL-2A · Exam Delivery & AI Grading
│   └─ SWL-2B · Review, Publication & Incident Handling
│
├─ ⚠️ 7 error branches summary
├─ 🎯 Suggested presentation order
└─ 📁 Source files & how to use them
```

---

## ⚡ Simplified set — drop into slides

| # | Diagram | Content | Size | Doc |
|---|---|---|---|---|
| **DIAGRAM 1** | **Use Case Diagram** | 4 actors · 13 use cases · 5 business groups | 1819×1553 | [MD](./simplified/diagram-01-usecase_en.md) |
| **DIAGRAM 2** | **Simplified Swimlane** | 5 lanes · 13 boxes (**one box = one flow**) · 3 error branches | 1929×1570 | [MD](./simplified/diagram-02-swimlane_en.md) |
| **DIAGRAM 3** | **System Context Diagram** | 4 actors · 4 capabilities · 3 external systems | 1016×663 | [MD](./simplified/diagram-03-context_en.md) |
| **DIAGRAM 4** | **Swimlane 10 Activity** *(most compact swimlane)* | 5 lanes · **10 activities** · 3 error branches | 2699×1517 | [MD](./simplified/diagram-04-swimlane-10_en.md) |
| **DIAGRAM 5** | **Simplified Sequence Diagram** *(call order)* | 6 lifelines · **3 phases** · white + light-blue | 944×1508 | [MD](./simplified/diagram-05-sequence_en.md) |

## 📚 Detailed set — kept 100% unchanged

| # | Diagram | Lanes | Size | Doc |
|---|---|---|---|---|
| **SWL-1** | Preparation & Exam Scheduling | ADMIN · STAFF · AI Service · Backend/DB | 2084×3030 | [MD](./swimlane-01-preparation-scheduling_en.md) |
| **SWL-2** | Exam Delivery, Grading & Publication *(full)* | STUDENT · Client · Backend · AI · Hangfire · EXAMINER | 6411×4578 | [MD](./swimlane-02-exam-grading-publish_en.md) |
| **SWL-2A** | Exam Delivery & AI Grading | STUDENT · Client · Backend · AI · Hangfire | 5230×3047 | [MD](./swimlane-02a-exam-delivery-grading_en.md) |
| **SWL-2B** | Review, Publication & Incident Handling | Backend · EXAMINER · STUDENT | 2699×1737 | [MD](./swimlane-02b-review-publish-incident_en.md) |

---

## ⚠️ 7 error branches summary

| Error branch | SRS | Handling | Responsible lane | Appears in |
|---|---|---|---|---|
| **Start preconditions not met** | BR-11 | 400/403, message shown, retry | Client → STUDENT | DIAGRAM 2 · SWL-2 |
| **STT failure (submit error)** | FR-AI-03 | EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR, re-answer or text fallback | Client → STUDENT | DIAGRAM 2 · SWL-2A |
| **Grading error (retryable)** | BR-15 | Hangfire retries up to 3 times, backoff 30s/120s/300s | Hangfire Worker | DIAGRAM 2 · SWL-2A |
| **Grading error (non-retryable) / attempts exhausted** | FR-AI-02 | FAILED + Answer.Status = ERROR + audit log | Hangfire → Backend | DIAGRAM 2 · SWL-2A |
| **Publish before review / score missing** | BR-10 | API 409, review again then publish | EXAMINER | DIAGRAM 2 · SWL-2B |
| **Request a re-check (retry answer)** | FR-EXA-05 | `POST /api/answers/{id}/retry` → RetryAnswerJob | EXAMINER / ADMIN | SWL-2B |
| **Incident requiring a new attempt (retake)** | FR-EXA-06, BR-16/17 | Old Result → CANCELLED, new session, history preserved, audit log | EXAMINER / ADMIN | SWL-2B |

> The 3 error branches **required by the assignment** (submit error, grading error, re-check request) are retained in **DIAGRAM 2**.

---

## 🎯 Suggested presentation order

| Slide | Content | Diagram |
|---|---|---|
| 1 | Context – who uses the system, what the system uses | **DIAGRAM 3** |
| 2 | What the system does – 13 capabilities & who uses them | **DIAGRAM 1** |
| 3 | Who the flow passes through – 5 roles, 3 error branches | **DIAGRAM 2** |
| 4 | Most compact – 10 activities, fits one slide | **DIAGRAM 4** |
| 5 | The real call order between the components | **DIAGRAM 5** |
| 6+ | Details (when asked deeper) | SWL-1 / SWL-2A / SWL-2B · SEQ-1…SEQ-4 |

---

## 📁 Source files & how to use them

| File | Content | Used for |
|---|---|---|
| `docs/architecture/simplified/diagram_0*.puml` | PlantUML source (self-contained, renders offline) | Re-rendering the simplified set |
| `docs/architecture/swimlane_0*.puml` | PlantUML source of the detailed swimlanes | Re-rendering the detailed set |
| `docs/architecture/*/DIAGRAM_*.svg` | Vector image, infinitely scalable | **Dropping into PowerPoint / slides** |
| `docs/architecture/*/SWIMLANE_*.svg` | Vector image of the detailed swimlanes | Printing or zoomed viewing |
| `docs/architecture/*/DIAGRAM_*.png` | Full-resolution raster (rendered from the SVG) | Documents that need a raster image |
| `docs/architecture/*.md`, `simplified/*.md` | Markdown documentation for every diagram | Reading / printing the explanation |
| `docs/architecture/architecture.md` | C4 context + C1 container + sequence + state machine | The overall architecture document |

### Re-render

```powershell
cd docs\architecture\simplified
java -jar ..\..\..\ops\tools\plantuml.jar -tsvg -charset UTF-8 diagram_*.puml
python ..\..\..\ops\_svg2png.py simplified
```

---

## 🔗 Other pages

| Page | Content |
|---|---|
| [`all-diagrams-en.html`](./all-diagrams-en.html) | **This page** – all diagrams on one page (EN) |
| [`all-diagrams.html`](./all-diagrams.html) | All diagrams on one page (VI) |
| [`swimlane-index-en.html`](./swimlane-index-en.html) | Separate index of the detailed set |
| [`simplified/simplified-index-en.html`](./simplified/simplified-index-en.html) | Separate index of the simplified set |
| [`architecture.md`](./architecture.md) | Overall C4 architecture document |

---

[[🏠 Back to `docs/architecture/architecture.md`]](./architecture.md) · [[Detailed set →](./swimlane-index-en.md)] · [[Simplified set →](./simplified/simplified-index-en.md)]
