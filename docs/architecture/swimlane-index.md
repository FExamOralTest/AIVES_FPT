# AIVES – Danh mục Swimlane Diagram (Swimlane Index)

**Phần công việc:** Thành viên 5 – Swimlane Diagram
**Nguồn đối chiếu:** `AIVES_Software_Requirements_Specification.docx` (07/11/2026) + mã nguồn thực tế `backend/Aives.Api`, `backend/Aives.Application`, `backend/Aives.Infrastructure`, `ai-service/app`.
**Quy ước chung:** Mỗi **lane** = một đơn vị giữ *thông tin trách nhiệm* (actor/service nào chịu trách nhiệm cho bước xử lý đó). Mỗi hộp là một hành động; **mọi nhánh lỗi đều quay về đúng lane chịu trách nhiệm xử lý**.

---

## ⚡ Bộ diagram rút gọn (dùng để thuyết trình)

> 📄 **Xem tất cả trong 1 trang:** [`all-diagrams.md`](./all-diagrams.md) · [HTML](./all-diagrams.html)

Bộ trên đây **chi tiết đúng 100%** nhưng rất rộng → nên dùng cho tham khảo & trả lời câu hỏi sâu.
Khi **nhét ảnh vào slide**, hãy dùng bộ 3 sơ đồ rút gọn:

| # | Sơ đồ | Kích thước | Tài liệu |
|---|---|---|---|
| **DIAGRAM 1** | Use Case Diagram (4 actor · 13 use case) | 1847×1614 | [MD](./simplified/diagram-01-usecase.md) |
| **DIAGRAM 2** | Swimlane rút gọn (1 activity = 1 luồng) | 1854×1593 | [MD](./simplified/diagram-02-swimlane.md) |
| **DIAGRAM 3** | System Context Diagram | 1229×640 | [MD](./simplified/diagram-03-context.md) |

👉 **[Mở bộ diagram rút gọn](./simplified/simplified-index.md)**

---

## Yêu cầu được giao (SV5)

> *"Vẽ hai luồng: chuẩn bị và tổ chức thi; làm bài, chấm và công bố. Chia lane theo thông tin trách nhiệm. Có các nhánh lỗi: nộp bài lỗi, chấm lỗi và yêu cầu xem lại."*
> Sản phẩm bàn giao: *"Hai Swimlane Diagram, kèm giải thích các điểm chuyển trách nhiệm."*

---

## Danh mục 🇻🇳 Tiếng Việt

| # | Sơ đồ | Lane | Tài liệu | Ảnh | Kích thước |
|---|---|---|---|---|---|
| **SWL-1** | Chuẩn bị & Tổ chức thi | ADMIN · STAFF · AI Service · Backend/DB | [MD](./swimlane-01-preparation-scheduling.md) | `SWIMLANE_01_PREPARATION.svg/.png` | 2084x3030 |
| **SWL-2** | Làm bài, Chấm & Công bố *(đầy đủ)* | STUDENT · Client · Backend · AI · Hangfire · EXAMINER | [MD](./swimlane-02-exam-grading-publish.md) | `SWIMLANE_02_EXAM_GRADING_PUBLISH.svg/.png` | 6411x4578 |
| **SWL-2A** | Làm bài & Chấm AI | STUDENT · Client · Backend · AI · Hangfire | [MD](./swimlane-02a-exam-delivery-grading.md) | `SWIMLANE_02A_EXAM_DELIVERY_GRADING.svg/.png` | 5230x3047 |
| **SWL-2B** | Review, Công bố & Xử lý sự cố | Backend · EXAMINER · STUDENT | [MD](./swimlane-02b-review-publish-incident.md) | `SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT.svg/.png` | 2699x1737 |

## Danh mục 🇬🇧 English

| # | Diagram | Lanes | Doc | Image | Size |
|---|---|---|---|---|---|
| **SWL-1-EN** | Preparation & Exam Scheduling | ADMIN · STAFF · AI Service · Backend/DB | [MD](./swimlane-01-preparation-scheduling_en.md) | `SWIMLANE_01_PREPARATION_EN.svg/.png` | 2334x3000 |
| **SWL-2-EN** | Exam Delivery, Grading & Publication *(full)* | STUDENT · Client · Backend · AI · Hangfire · EXAMINER | [MD](./swimlane-02-exam-grading-publish_en.md) | `SWIMLANE_02_EXAM_GRADING_PUBLISH_EN.svg/.png` | 7042x4578 |
| **SWL-2A-EN** | Exam Delivery & AI Grading | STUDENT · Client · Backend · AI · Hangfire | [MD](./swimlane-02a-exam-delivery-grading_en.md) | `SWIMLANE_02A_EXAM_DELIVERY_GRADING_EN.svg/.png` | 5788x3047 |
| **SWL-2B-EN** | Review, Publication & Incident Handling | Backend · EXAMINER · STUDENT | [MD](./swimlane-02b-review-publish-incident_en.md) | `SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT_EN.svg/.png` | 2874x1737 |

---

## Tổng hợp các nhánh lỗi (theo yêu cầu bàn giao)

| Nhánh lỗi | Sơ đồ | Điều kiện | Xử lý | Lane chịu trách nhiệm |
|---|---|---|---|---|
| Trùng email/username | SWL-1 | BR-03 | API 400, sửa & tạo lại | ADMIN |
| Tài liệu `.md` không hợp lệ | SWL-1 | FR-STF-01 | API 400, sửa & tải lại | STAFF |
| LLM sinh câu hỏi lỗi | SWL-1 | FR-STF-03 | `error_code`, STAFF tạo câu hỏi tay | STAFF |
| Tổng trọng số ≠ 100% | SWL-1 | BR-08 | API 400, chỉnh lại trọng số | STAFF |
| Slot trùng giờ / ngoài khung ca | SWL-1 | BR-10 | API 400, sửa cấu hình | STAFF |
| Trùng ghi danh / kíp đầy | SWL-1 | BR-11 | API 400, sửa danh sách/kíp | STAFF |
| **Chưa đủ điều kiện bắt đầu** | SWL-2A | BR-11 | 400/403, hiển thị thông báo, thử lại | Client → STUDENT |
| **STT thất bại (nộp bài lỗi)** | SWL-2A | FR-AI-03 | `EMPTY_AUDIO`/`TIMEOUT`/`INTERNAL_ERROR`, SV nhập lại hoặc text fallback | Client → STUDENT |
| Không có câu trả lời nào | SWL-2A | FR-STU-04 | `AiEvaluationStatus = FAILED` | STUDENT |
| **Chấm AI lỗi (retryable)** | SWL-2A | BR-15 | Hangfire retry tối đa 3 lḀn, backoff 30s/120s/300s | Hangfire Worker |
| **Chấm AI lỗi (non-retryable) / hết lượt** | SWL-2A | FR-AI-02 | `FAILED` + `Answer.Status = ERROR` + audit log | Hangfire → Backend |
| Publish khi chưa review / thiếu điểm | SWL-2B | BR-10 | API 409, review lại rồi publish | EXAMINER |
| Điểm tổng ngoài khoảng hợp lệ | SWL-2B | NFR-DATA-03 | API 400, chỉnh điểm từng câu | EXAMINER |
| **Yêu cầu xem lại (retry answer)** | SWL-2B | FR-EXA-05 | `POST /api/answers/{id}/retry` → `RetryAnswerJob` | EXAMINER/ADMIN |
| **Sự cố cần thi lại (retake)** | SWL-2B | FR-EXA-06, BR-16/17 | Result cũ → `CANCELLED`, ca thi mới, giữ lịch sử, audit log | EXAMINER/ADMIN |
| Retake chưa bắt đầu / đã dùng / hết hạn | SWL-2B | BR-17 | API 400/403, SV báo GV | STUDENT |

---

## Traceability tổng hợp: Swimlane ↔ SRS

| Yêu cầu SRS | Sơ đồ | Lane |
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

## Phân biệt chức năng hiện có và đề xuất

| Mục | Hiện có (đã kiểm chứng trong mã nguồn) | Đề xuất (chưa có đường dẫn kiểm chứng) |
|---|---|---|
| Chấm lại | `POST /api/answers/{answerId}/retry` + `RetryAnswerJob` | Tự động tính lại tổng điểm `Result.AiScore` sau retry (FR-AI-05) |
| Snapshot ngữ cảnh chấm | Đang ghi `provider`, `model`, `rubric`, câu trả lời vào `AiAnalysis` | Snapshot bản gốc của question/rubric/tài liệu (FR-AI-04) |
| Phân biệt "STT lỗi" vs "SV không trả lời" | Đang ghi lỗi vào `Answer.ErrorMessage` | Trạng thái riêng `STT_FAILED` (FR-AI-03) |
| Điều kiện publish | Đã có kiểm tra REVIEWED + điểm | Cổng publish cấu hình được (FR-EXA-04 – Partial) |
| Sửa kết quả đã công bố | Chưa có API | Quy trình khiếu nại/correction (FR-EXA-07) |

---

## Cách render lại (đã phóng to)

```powershell
python ops\_apply_skin.py    # áp skin (font 22, bold, nét 3.0) + render SVG/PNG
python ops\_svg2png.py       # rasterize SVG -> PNG full-res bằng Edge headless
```

> `ops/tools/plantuml.jar` là công cụ local, đã được thêm vào `.gitignore`.
> Font chữ trong sơ đồ: **22pt bold** (tiêu đề 28pt), đường nối **3.0px**, viền hộp **2.5px** – đủ rõ khi chiếu.
> PlantUML mặc định giới hạn PNG ở 4096px nên script `_svg2png.py` render lại từ SVG để không bị cắt.

---

## Gợi ý chọn ảnh cho slide

1. **Slide 16:9** → dùng **SVG** (vector, zoom không vỡ nét). PowerPoint: *Insert → Pictures → This Device*, chọn file `.svg`.
2. **Cần PNG** → dùng file `.png` đã render ở **độ phân giải gốc** (xem cột "Kích thước"), không bị cắt.
3. **SWL-2 bản đầy đủ rộng 6411–7042px** → nên dùng **SWL-2A + SWL-2B** thay vì 1 slide duy nhất.
4. Bản **EN** dùng cho slide tiếng Anh hoặc bản quốc tế; bản **VI** giữ nguyên thuật ngữ nghiệp vụ tiếng Việt.

---

[[🏠 Về `docs/architecture/architecture.md`]](./architecture.md)
