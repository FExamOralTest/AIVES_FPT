# AIVES – Toàn bộ Sơ đồ (một trang duy nhất)

**Phần công việc:** Thành viên 5 – Tài liệu đồ thị
**Trang HTML tổng hợp:** [`all-diagrams.html`](./all-diagrams.html) · [EN](./all-diagrams-en.html)

> Trang này gom **toàn bộ** sơ đồ của AIVES theo thứ tự từ **ngắn gọn** (để nhét vào slide) xuống **đầy đủ** (để tham khảo).

---

## 🗂️ Bản đồ tổng thể

```
AIVES – Toàn bộ Sơ đồ
│
├─ ⚡ BỘ RÚT GỌN  (dùng để thuyết trình — vừa 1 slide)
│   ├─ DIAGRAM 1 · Use Case Diagram
│   ├─ DIAGRAM 2 · Swimlane rút gọn (1 activity = 1 luồng)
│   ├─ DIAGRAM 3 · System Context Diagram
│   ├─ DIAGRAM 4 · Swimlane 10 Activity (cô đọng nhất)
│   └─ DIAGRAM 5 · Sequence Diagram rút gọn (thứ tự gọi thực tế)
│
├─ 📚 BỘ CHI TIẾT  (tham khảo — 6 lane, ~40 hộp, 8 nhánh lỗi)
│   ├─ SWL-1  · Chuẩn bị & Tổ chức thi
│   ├─ SWL-2  · Làm bài, Chấm & Công bố (đầy đủ, 6 lane)
│   ├─ SWL-2A · Làm bài & Chấm AI
│   └─ SWL-2B · Review, Công bố & Xử lý sự cố
│
├─ 🔄 BỘ SEQUENCE DIAGRAM  (4 luồng tương tác — tách riêng)
│   ├─ SEQ 1 · Luồng thi Viva
│   ├─ SEQ 2 · Chấm điểm AI
│   ├─ SEQ 3 · Review, Công bố & Xem kết quả
│   └─ SEQ 4 · Chuẩn bị & Tổ chức thi
│
├─ ⚠️ Tổng hợp 7 nhánh lỗi
├─ 🎯 Thứ tự trình bày đề xuất
└─ 📁 File nguồn & cách dùng
```

---

## ⚡ Bộ rút gọn — nhét vào slide

| # | Sơ đồ | Nội dung | Kích thước | Tài liệu |
|---|---|---|---|---|
| **DIAGRAM 1** | **Use Case Diagram** | 4 actor · 13 use case · 5 nhóm nghiệp vụ | 1819×1553 | [MD](./simplified/diagram-01-usecase.md) |
| **DIAGRAM 2** | **Swimlane rút gọn** | 5 lane · 13 hộp (**1 activity = 1 luồng**) · 3 nhánh lỗi | 1929×1570 | [MD](./simplified/diagram-02-swimlane.md) |
| **DIAGRAM 3** | **System Context Diagram** | 4 actor · 4 năng lực · 3 hệ thống ngoài | 1016×663 | [MD](./simplified/diagram-03-context.md) |
| **DIAGRAM 4** | **Swimlane 10 Activity** *(cô đọng nhất)* | 5 lane · **10 activity** · 3 nhánh lỗi | 2347×1517 | [MD](./simplified/diagram-04-swimlane-10.md) |
| **DIAGRAM 5** | **Sequence Diagram rút gọn** *(thứ tự gọi)* | 6 lifeline · **3 giai đoạn** · trắng + xanh nhạt | 940×1508 | [MD](./simplified/diagram-05-sequence.md) |

## 📚 Bộ chi tiết — giữ nguyên 100%

| # | Sơ đồ | Lane | Kích thước | Tài liệu |
|---|---|---|---|---|
| **SWL-1** | Chuẩn bị & Tổ chức thi | ADMIN · STAFF · AI Service · Backend/DB | 2084×3030 | [MD](./swimlane-01-preparation-scheduling.md) |
| **SWL-2** | Làm bài, Chấm & Công bố *(đầy đủ)* | STUDENT · Client · Backend · AI · Hangfire · EXAMINER | 6411×4578 | [MD](./swimlane-02-exam-grading-publish.md) |
| **SWL-2A** | Làm bài & Chấm AI | STUDENT · Client · Backend · AI · Hangfire | 5230×3047 | [MD](./swimlane-02a-exam-delivery-grading.md) |
| **SWL-2B** | Review, Công bố & Xử lý sự cố | Backend · EXAMINER · STUDENT | 2699×1737 | [MD](./swimlane-02b-review-publish-incident.md) |

## 🔄 Bộ Sequence Diagram — tương tác giữa các thành phần

| # | Sơ đồ | Giai đoạn | Kích thước | Tài liệu |
|---|---|---|---|---|
| **SEQ 1** | Luồng thi Viva | 3 giai đoạn · 8 participant | 2271×2858 | [MD](./sequence/seq-01-viva-flow.md) |
| **SEQ 2** | Chấm điểm AI | 5 giai đoạn · 7 participant | 2263×2021 | [MD](./sequence/seq-02-grading.md) |
| **SEQ 3** | Review, Công bố & Xem kết quả | 6 giai đoạn · 7 participant | 2198×3287 | [MD](./sequence/seq-03-review-publish.md) |
| **SEQ 4** | Chuẩn bị & Tổ chức thi | 7 giai đoạn · 9 participant | 1966×2627 | [MD](./sequence/seq-04-admin-prep.md) |

> Bản tiếng Anh: [`sequence/sequence-index-en.md`](./sequence/sequence-index-en.md) — Index riêng: [`sequence/sequence-index.html`](./sequence/sequence-index.html)

---

## ⚠️ Tổng hợp 7 nhánh lỗi

| Nhánh lỗi | SRS | Xử lý | Lane chịu trách nhiệm | Có trong sơ đồ |
|---|---|---|---|---|
| **Chưa đủ điều kiện bắt đầu** | BR-11 | 400/403, hiển thị thông báo, thử lại | Client → STUDENT | DIAGRAM 2 · SWL-2 |
| **STT thất bại (nộp bài lỗi)** | FR-AI-03 | EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR, nhập lại hoặc text fallback | Client → STUDENT | DIAGRAM 2 · SWL-2A |
| **Chấm AI lỗi (retryable)** | BR-15 | Hangfire retry tối đa 3 lḀn, backoff 30s/120s/300s | Hangfire Worker | DIAGRAM 2 · SWL-2A |
| **Chấm AI lỗi (không retry) / hết lượt** | FR-AI-02 | FAILED + Answer.Status = ERROR + audit log | Hangfire → Backend | DIAGRAM 2 · SWL-2A |
| **Publish khi chưa review / thiếu điểm** | BR-10 | API 409, review lại rồi publish | EXAMINER | DIAGRAM 2 · SWL-2B |
| **Yêu cầu xem lại (retry answer)** | FR-EXA-05 | `POST /api/answers/{id}/retry` → RetryAnswerJob | EXAMINER / ADMIN | SWL-2B |
| **Sự cố cần thi lại (retake)** | FR-EXA-06, BR-16/17 | Result cũ → CANCELLED, ca thi mới, giữ lịch sử, audit log | EXAMINER / ADMIN | SWL-2B |

> 3 nhánh lỗi **bắt buộc theo đề bài** (nộp bài lỗi · chấm lỗi · yêu cầu xem lại) đã được giữ trong **DIAGRAM 2**.

---

## 🎯 Thứ tự trình bày đề xuất

| Slide | Nội dung | Sơ đồ |
|---|---|---|
| 1 | Bối cảnh – ai dùng hệ thống, hệ thống dùng gì | **DIAGRAM 3** |
| 2 | Hệ thống làm gì – 13 chức năng & ai dùng | **DIAGRAM 1** |
| 3 | Luồng đi qua ai – 5 vai trò, 3 nhánh lỗi | **DIAGRAM 2** |
| 4 | Cô đọng nhất – 10 activity, vừa 1 slide | **DIAGRAM 4** |
| 5 | Thứ tự gọi thực tế giữa các thành phần | **DIAGRAM 5** |
| 6+ | Chi tiết (khi GV hỏi sâu) | SWL-1 / SWL-2A / SWL-2B · SEQ-1…SEQ-4 |

---

## 📁 File nguồn & cách dùng

| File | Nội dung | Dùng để |
|---|---|---|
| `docs/architecture/simplified/diagram_0*.puml` | PlantUML source (tự chứa, render offline) | Render lại bộ rút gọn |
| `docs/architecture/swimlane_0*.puml` | PlantUML source của swimlane chi tiết | Render lại bộ chi tiết |
| `docs/architecture/*/DIAGRAM_*.svg` | Ảnh vector, co giãn vô hạn | **Nhét vào PowerPoint / slide** |
| `docs/architecture/*/SWIMLANE_*.svg` | Ảnh vector của swimlane chi tiết | In ấn / xem phóng to |
| `docs/architecture/*/DIAGRAM_*.png` | Ảnh raster full-resolution (render từ SVG) | Tài liệu cần ảnh bitmap |
| `docs/architecture/*.md`, `simplified/*.md` | Tài liệu markdown của từng sơ đồ | Đọc / in phần giải thích |
| `docs/architecture/architecture.md` | C4 context + C1 container + sequence + state machine | Tài liệu kiến trúc tổng thể |

### Render lại

```powershell
cd docs\architecture\simplified
java -jar ..\..\..\ops\tools\plantuml.jar -tsvg -charset UTF-8 diagram_*.puml
python ..\..\..\ops\_svg2png.py simplified
```

---

## 🔗 Các trang khác

| Trang | Nội dung |
|---|---|
| [`all-diagrams.html`](./all-diagrams.html) | **Trang này** – toàn bộ sơ đồ trong 1 trang (VI) |
| [`all-diagrams-en.html`](./all-diagrams-en.html) | Toàn bộ sơ đồ trong 1 trang (EN) |
| [`swimlane-index.html`](./swimlane-index.html) | Index riêng của bộ chi tiết |
| [`simplified/simplified-index.html`](./simplified/simplified-index.html) | Index riêng của bộ rút gọn |
| [`erd/erd-index.html`](./erd/erd-index.html) | **Bộ ERD 3 mức** (Conceptual · Logical · Physical) — tách riêng |
| [`sequence/sequence-index.html`](./sequence/sequence-index.html) | **Bộ Sequence Diagram** (4 luồng tương tác) — tách riêng |
| [`architecture.md`](./architecture.md) | Tài liệu kiến trúc C4 tổng thể |

---

[[🏠 Trang chính `docs/architecture/architecture.md`]](./architecture.md) · [[Bộ chi tiết →](./swimlane-index.md)] · [[Bộ rút gọn →](./simplified/simplified-index.md)] · [[Bộ ERD 3 mức →](./erd/erd-index.md)] · [[Bộ Sequence →](./sequence/sequence-index.md)]
