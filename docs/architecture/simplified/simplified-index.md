# AIVES – Bộ Diagram Rút Gọn (cho thuyết trình)

**Phần công việc:** Thành viên 5 – Tài liệu đồ thị
**Mục đích:** 5 sơ đồ **gọn, dễ nhìn, nhét vừa 1 slide** — bổ sung cho bộ swimlane chi tiết (dùng để tham khảo & trả lời câu hỏi sâu).

> 📘 **Bản chi tiết (giữ nguyên, dùng để tham khảo):** [`../swimlane-index.md`](../swimlane-index.md) · [`../swimlane-index-en.md`](../swimlane-index-en.md)

---

## 5 sơ đồ

| # | Sơ đồ | Kích thước | Lane / Node | Tài liệu | Ảnh |
|---|---|---|---|---|---|
| **DIAGRAM 1** | **Use Case Diagram** | 1819x1553 | 4 actor · 13 use case · 5 nhóm | [MD](./diagram-01-usecase.md) | `DIAGRAM_01_USECASE.svg/.png` |
| **DIAGRAM 2** | **Swimlane rút gọn** *(1 activity = 1 luồng)* | 1929x1570 | 5 lane · 13 hộp · 3 nhánh lỗi | [MD](./diagram-02-swimlane.md) | `DIAGRAM_02_SWIMLANE.svg/.png` |
| **DIAGRAM 3** | **System Context Diagram** | 1016x663 | 4 actor · 4 năng lực · 3 hệ thống ngoài | [MD](./diagram-03-context.md) | `DIAGRAM_03_CONTEXT.svg/.png` |
| **DIAGRAM 4** | **Swimlane 10 Activity** *(cô đọng nhất)* | 2347x1517 | 5 lane · **10 activity** · 3 nhánh lỗi | [MD](./diagram-04-swimlane-10.md) | `DIAGRAM_04_SWIMLANE_10.svg/.png` |
| **DIAGRAM 5** | **Sequence Diagram rút gọn** *(thứ tự gọi)* | 940x1508 | 6 lifeline · **3 giai đoạn** · trắng + xanh nhạt · PNG 2.5× | [MD](./diagram-05-sequence.md) | `DIAGRAM_05_SEQUENCE.svg/.png` |

---
## Bản tiếng Anh 🇬🇧

| # | Diagram | Size | Tài liệu | Ảnh |
|---|---|---|---|---|
| **DIAGRAM 1-EN** | Use Case Diagram | 1848x1615 | [MD](./diagram-01-usecase_en.md) | `DIAGRAM_01_USECASE_EN.svg/.png` |
| **DIAGRAM 2-EN** | Simplified Swimlane | 1855x1594 | [MD](./diagram-02-swimlane_en.md) | `DIAGRAM_02_SWIMLANE_EN.svg/.png` |
| **DIAGRAM 3-EN** | System Context Diagram | 1230x641 | [MD](./diagram-03-context_en.md) | `DIAGRAM_03_CONTEXT_EN.svg/.png` |
| **DIAGRAM 4-EN** | Swimlane 10 Activity | 2699x1517 | [MD](./diagram-04-swimlane-10_en.md) | `DIAGRAM_04_SWIMLANE_10_EN.svg/.png` |
| **DIAGRAM 5-EN** | Simplified Sequence Diagram | 1416x1597 | [MD](./diagram-05-sequence_en.md) | `DIAGRAM_05_SEQUENCE_EN.svg/.png` |

---

## 📄 Xem tất cả trong 1 trang

Từ bản **ngắn gọn** (để thuyết trình) đến bản **đầy đủ** (để tham khảo):
→ [`../all-diagrams.md`](../all-diagrams.md) · [HTML](../all-diagrams.html)

---

## Thứ tự trình bày đề xuất

| Slide | Nội dung | Sơ đồ |
|---|---|---|
| 1 | **Bối cảnh** – ai dùng hệ thống, hệ thống dùng gì | DIAGRAM 3 (Context) |
| 2 | **Hệ thống làm gì** – 13 chức năng & ai dùng | DIAGRAM 1 (Use Case) |
| 3 | **Luồng đi qua ai** – 5 vai trò, 3 nhánh lỗi | DIAGRAM 2 (Swimlane rút gọn) |
| 4 | *Cô đọng nhất* – 10 activity, vừa 1 slide | DIAGRAM 4 (Swimlane 10 Activity) |
| 5 | **Thứ tự gọi thực tế** giữa các thành phần | DIAGRAM 5 (Sequence rút gọn) |
| 6+ | *Chi tiết* (khi GV hỏi sâu) | SWL-1 / SWL-2A / SWL-2B · SEQ-1…SEQ-4 |

---

## Điểm khác biệt so với bộ chi tiết

| | Bộ chi tiết | Bộ rút gọn |
|---|---|---|
| Số hộp trên sơ đồ | ~40 / diagram | **13 / diagram** |
| Chi tiết API, retry, audit | Có | Chỉ 1 dòng ghi chú |
| Số nhánh lỗi | 8 | **3** (đúng 3 nhánh đề bài yêu cầu) |
| Kích thước | rất rộng (6411px) | **vừa 1 slide** |
| Dùng để | trả lời câu hỏi sâu | **giới thiệu tổng quan** |

---

## Traceability: sơ đồ rút gọn ↔ SRS

| SRS requirement | Sơ đồ | Nơi thể hiện |
|---|---|---|
| FR-ADM-01/02, BR-03 | DIAGRAM 1, 2 | UC-01, UC-02 |
| FR-STF-01…07, BR-07/08/10/11 | DIAGRAM 1, 2 | UC-03…UC-06 |
| FR-STU-01…06, BR-11 | DIAGRAM 1, 2 | UC-07, nhánh lỗi điều kiện bắt đầu |
| FR-AI-01/02, BR-13 | DIAGRAM 1, 2, 3 | UC-08, năng lực "chấm tự động theo rubric" |
| FR-STU-06, BR-15 | DIAGRAM 2 | Nhánh lỗi chấm AI lỗi |
| FR-EXA-01…04, BR-09/10 | DIAGRAM 1, 2, 3 | UC-09, UC-10, nhánh lỗi chưa review |
| FR-EXA-05/06, BR-16/17 | DIAGRAM 1 | UC-10 (chấm lại / cấp lại thi) |
| FR-STU-07, BR-18 | DIAGRAM 1, 2 | UC-11 |
| FR-DATA-01/02 | DIAGRAM 1, 3 | UC-13, R2 |

---

## Cách render lại

```powershell
cd docs\architecture\simplified
java -jar ..\..\..\ops\tools\plantuml.jar -tsvg -charset UTF-8 diagram_*.puml
java -jar ..\..\..\ops\tools\plantuml.jar -tpng -charset UTF-8 diagram_*.puml
```

> Các file `.puml` **tự chứa** (không `!include` thư viện C4 online) → render được cả khi không có mạng.

---

[[🏠 Về `docs/architecture/architecture.md`]](../architecture.md) · [[Bộ swimlane chi tiết →](../swimlane-index.md)] · [[🔄 Bộ sequence chi tiết →](../sequence/sequence-index.md)]
