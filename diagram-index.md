# AIVES – Tổng hợp bộ sơ đồ (diagram cho thành viên 5)

Có **2 bộ sơ đồ**, dùng song song: bộ **rút gọn** để thuyết trình, bộ **chi tiết** để tham khảo.

---

## ⚡ Bộ rút gọn — dùng để nhét vào slide

4 sơ đồ, mỗi sơ đồ **vừa 1 slide**, đọc rõ khi chiếu.

| # | Sơ đồ | Nội dung | Tài liệu | Ảnh |
|---|---|---|---|---|
| **DIAGRAM 1** | **Use Case Diagram** | 4 actor · 13 use case trong 5 nhóm | [MD](./docs/architecture/simplified/diagram-01-usecase.md) | `DIAGRAM_01_USECASE.svg/.png` |
| **DIAGRAM 2** | **Swimlane rút gọn** *(1 activity = 1 luồng)* | 5 lane · 13 hộp · 3 nhánh lỗi | [MD](./docs/architecture/simplified/diagram-02-swimlane.md) | `DIAGRAM_02_SWIMLANE.svg/.png` |
| **DIAGRAM 3** | **System Context Diagram** | 4 actor · 4 năng lực · 3 hệ thống bên ngoài | [MD](./docs/architecture/simplified/diagram-03-context.md) | `DIAGRAM_03_CONTEXT.svg/.png` |
| **DIAGRAM 4** | **Swimlane 10 Activity** *(cô đọng nhất)* | 5 lane · **10 activity** · 3 nhánh lỗi | [MD](./docs/architecture/simplified/diagram-04-swimlane-10.md) | `DIAGRAM_04_SWIMLANE_10.svg/.png` |

**Bản tiếng Anh:** [DIAGRAM 1-EN](./docs/architecture/simplified/diagram-01-usecase_en.md) · [DIAGRAM 2-EN](./docs/architecture/simplified/diagram-02-swimlane_en.md) · [DIAGRAM 3-EN](./docs/architecture/simplified/diagram-03-context_en.md) · [DIAGRAM 4-EN](./docs/architecture/simplified/diagram-04-swimlane-10_en.md)

👉 **[Mở bộ rút gọn (HTML)](./docs/architecture/simplified/simplified-index.html)**

### Thứ tự trình bày đề xuất

| Slide | Nội dung | Sơ đồ |
|---|---|---|
| 1 | Bối cảnh – ai dùng hệ thống, hệ thống dùng gì | DIAGRAM 3 (Context) |
| 2 | Hệ thống làm gì – 13 chức năng & ai dùng | DIAGRAM 1 (Use Case) |
| 3 | Luồng đi qua ai – 5 vai trò, 3 nhánh lỗi | DIAGRAM 2 (Swimlane rút gọn) |
| 4 | Cô đọng nhất – 10 activity, vừa 1 slide | DIAGRAM 4 (Swimlane 10 Activity) |

---

## 📘 Bộ chi tiết — dùng để tham khảo & trả lời câu hỏi sâu

4 swimlane diagram đầy đủ (6 lane, ~40 hộp, 8 nhánh lỗi) + C0/C1 + sequence + state machine.

| # | Sơ đồ | Lane | Tài liệu |
|---|---|---|---|
| **SWL-1** | Chuẩn bị & Tổ chức thi | ADMIN · STAFF · AI Service · Backend/DB | [MD](./docs/architecture/swimlane-01-preparation-scheduling.md) |
| **SWL-2** | Làm bài, Chấm & Công bố *(đầy đủ)* | STUDENT · Client · Backend · AI · Hangfire · EXAMINER | [MD](./docs/architecture/swimlane-02-exam-grading-publish.md) |
| **SWL-2A** | Làm bài & Chấm AI | STUDENT · Client · Backend · AI · Hangfire | [MD](./docs/architecture/swimlane-02a-exam-delivery-grading.md) |
| **SWL-2B** | Review, Công bố & Xử lý sự cố | Backend · EXAMINER · STUDENT | [MD](./docs/architecture/swimlane-02b-review-publish-incident.md) |

**Bản tiếng Anh:** [SWL-1-EN](./docs/architecture/swimlane-01-preparation-scheduling_en.md) · [SWL-2-EN](./docs/architecture/swimlane-02-exam-grading-publish_en.md) · [SWL-2A-EN](./docs/architecture/swimlane-02a-exam-delivery-grading_en.md) · [SWL-2B-EN](./docs/architecture/swimlane-02b-review-publish-incident_en.md)

👉 **[Mở bộ chi tiết (HTML)](./docs/architecture/swimlane-index.html)**

---

## Cả 2 bộ đều dùng chung nguyên tắc

| Nguyên tắc | Thực hiện |
|---|---|
| **Lane = đơn vị giữ thông tin trách nhiệm** | Mỗi lane một actor/service rõ ràng |
| **Mọi nhánh lỗi quay về đúng lane xử lý** | Không nhánh nào "chết" giữa luồng |
| **AI không bao giờ công bố điểm** | `PUBLISHED` chỉ nằm trong tay Khảo thí |
| **Nguồn đối chiếu** | SRS 07/11/2026 + mã nguồn `backend/Aives.Api` (21 controller) + `ai-service/app` |

---

## Tài liệu kiến trúc khác

| Tài liệu | Nội dung |
|---|---|
| [`docs/architecture/architecture.md`](./docs/architecture/architecture.md) | C0 Context · C1 Container · Sequence · State Machine · Traceability Matrix |
| [`docs/architecture/system_overview_and_flows.md`](./docs/architecture/system_overview_and_flows.md) | Tổng quan hệ thống & luồng nghiệp vụ |
| [`docs/architecture/c0_context.puml`](./docs/architecture/c0_context.puml) · [`c1_container.puml`](./docs/architecture/c1_container.puml) | Nguồn PlantUML C4 |
