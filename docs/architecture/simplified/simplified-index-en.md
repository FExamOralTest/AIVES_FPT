# AIVES – Simplified Diagram Set (presentation version)

**Part of work:** Member 5 – Diagram documentation
**Purpose:** 5 **compact, slide-friendly** diagrams that supplement the detailed swimlane set (kept for reference & deep questions).

> 📘 **Detailed set (unchanged, for reference):** [`../swimlane-index-en.md`](../swimlane-index-en.md)

---

## 5 diagrams

| # | Diagram | Size | Actors / Lanes / Nodes | Doc | Image |
|---|---|---|---|---|---|
| **DIAGRAM 1** | **Use Case Diagram** | 1848x1615 | 4 actors · 13 use cases · 5 groups | [MD](./diagram-01-usecase_en.md) | `DIAGRAM_01_USECASE_EN.svg/.png` |
| **DIAGRAM 2** | **Simplified Swimlane** *(one box = one flow)* | 1855x1594 | 5 lanes · 13 boxes · 3 error branches | [MD](./diagram-02-swimlane_en.md) | `DIAGRAM_02_SWIMLANE_EN.svg/.png` |
| **DIAGRAM 3** | **System Context Diagram** | 1230x641 | 4 actors · 4 capabilities · 3 external systems | [MD](./diagram-03-context_en.md) | `DIAGRAM_03_CONTEXT_EN.svg/.png` |
| **DIAGRAM 4** | **Swimlane 10 Activity** *(most compact swimlane)* | 2699x1517 | 5 lanes · **10 activities** · 3 error branches | [MD](./diagram-04-swimlane-10_en.md) | `DIAGRAM_04_SWIMLANE_10_EN.svg/.png` |
| **DIAGRAM 5** | **Simplified Sequence Diagram** *(call order)* | 944x1508 | 6 lifelines · **3 phases** · white + light-blue · PNG 2.5× | [MD](./diagram-05-sequence_en.md) | `DIAGRAM_05_SEQUENCE_EN.svg/.png` |

---
## Vietnamese versions 🇻🇳

| # | Sơ đồ | Tài liệu | Ảnh |
|---|---|---|---|
| **DIAGRAM 1** | Use Case Diagram | [MD](./diagram-01-usecase.md) | `DIAGRAM_01_USECASE.svg/.png` |
| **DIAGRAM 2** | Swimlane rút gọn | [MD](./diagram-02-swimlane.md) | `DIAGRAM_02_SWIMLANE.svg/.png` |
| **DIAGRAM 3** | System Context Diagram | [MD](./diagram-03-context.md) | `DIAGRAM_03_CONTEXT.svg/.png` |
| **DIAGRAM 4** | Swimlane 10 Activity | [MD](./diagram-04-swimlane-10.md) | `DIAGRAM_04_SWIMLANE_10.svg/.png` |
| **DIAGRAM 5** | Sequence Diagram rút gọn | [MD](./diagram-05-sequence.md) | `DIAGRAM_05_SEQUENCE.svg/.png` |

---
## 📄 View everything on one page

From the **simplified** set (for presenting) to the **full** set (for reference):
→ [`../all-diagrams-en.md`](../all-diagrams-en.md) · [HTML](../all-diagrams-en.html)

---

## Suggested presentation order

| Slide | Content | Diagram |
|---|---|---|
| 1 | **Context** – who uses the system, what the system uses | DIAGRAM 3 |
| 2 | **What the system does** – 13 capabilities & who uses them | DIAGRAM 1 |
| 3 | **Who the flow passes through** – 5 roles, 3 error branches | DIAGRAM 2 |
| 4 | *Most compact swimlane* – 10 activities, fits one slide | DIAGRAM 4 |
| 5 | **The real call order** between the components | DIAGRAM 5 |
| 6+ | *Details* (when asked deeper) | SWL-1 / SWL-2A / SWL-2B · SEQ-1…SEQ-4 |

---

## Difference from the detailed set

| | Detailed set | Simplified set |
|---|---|---|
| Boxes per diagram | ~40 | **13** |
| API / retry / audit detail | Yes | One note per box |
| Error branches | 8 | **3** (exactly the 3 the assignment requires) |
| Width | very wide (6411px) | **Fits one slide** |
| Used for | answering deep questions | **the overview slide** |

---

## How to re-render

```powershell
cd docs\architecture\simplified
java -jar ..\..\..\ops\tools\plantuml.jar -tsvg -charset UTF-8 diagram_*.puml
java -jar ..\..\..\ops\tools\plantuml.jar -tpng -charset UTF-8 diagram_*.puml
```

> The `.puml` files are **self-contained** (no online `!include` of the C4 library) → they render even without network access.

---

[[🏠 Back to `docs/architecture/architecture.md`]](../architecture.md) · [[Detailed swimlane set →](../swimlane-index-en.md)] · [[🔄 Detailed sequence set →](../sequence/sequence-index-en.md)]
