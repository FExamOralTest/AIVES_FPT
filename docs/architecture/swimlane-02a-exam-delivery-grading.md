# SWL-2A – Làm bài & Chấm AI (Exam Delivery & AI Grading)

**Phần công việc:** Thành viên 5 – Swimlane Diagram (phần 1/2 của SWL-2)
**Nguồn đối chiếu:** SRS 07/11/2026 + `VivaSessionService`, `SessionGradingJob`, `SessionGradingService`, `ChunkedAudioController`, `ai-service/app/api/speech.py`, `ai-service/app/api/evaluation.py`.
**Phạm vi:** từ `Start` → kiểm tra điều kiện → ghi âm & upload → STT + follow-up → `finish` → enqueue Hangfire → worker chấm AI (kèm nhánh retry) → `AiScore` được ghi. Kết quả vẫn ở `PENDING_REVIEW` — phần review/công bố nằm ở [SWL-2B](./swimlane-02b-review-publish-incident.md).

| | |
|---|---|
| **Nguồn PlantUML** | `swimlane_02a_exam_delivery_grading.puml` |
| **Ảnh** | `SWIMLANE_02A_EXAM_DELIVERY_GRADING.svg` / `.png` |
| **Bản tiếng Anh** | [`swimlane-02a-exam-delivery-grading_en.md`](./swimlane-02a-exam-delivery-grading_en.md) |
| **Bản đầy đủ** | [`swimlane-02-exam-grading-publish.md`](./swimlane-02-exam-grading-publish.md) |

![SWL-2A – Làm bài & Chấm AI](./SWIMLANE_02A_EXAM_DELIVERY_GRADING.svg)

---

## Các lane và thông tin trách nhiệm

| Lane | Giữ thông tin/trách nhiệm gì | Tham chiếu SRS |
|---|---|---|
| **STUDENT** | Quyết định tham gia, trả lời tuần tự | UC-07, FR-STU-01…06 |
| **Client (Web / Windows)** | Thu âm (WAV 16 kHz mono), upload chunked, hiển thị câu hỏi/follow-up, giữ bản nháp cục bộ | FR-STU-03, FR-STU-08, NFR-REL-02 |
| **Backend / DB / Storage** | Kiểm tra luật nghiệp vụ, lưu Answer/Result, audio R2 | FR-DATA-01/02, BR-11/12 |
| **AI Service (FastAPI)** | STT, sinh follow-up, chấm theo rubric | FR-AI-01/02, BR-13 |
| **Hangfire Worker (Queue "grading")** | Chấm cả bài theo hàng đợi, retry có backoff, khóa phân tán 1 job/lần | FR-STU-06, BR-15, NFR-PERF-03 |

---

## Các điểm chuyển trách nhiệm (handover)

1. **STUDENT → Backend (Start)** — `POST /api/student/viva-sessions/{id}/start` kiểm tra đồng thời: có enrollment, ca `IN_PROGRESS`, `now >= session.StartTime`, `now < session.EndTime`, và nằm trong kíp (`BR-11`). Sai → 400/403 và SV phải thử lại.
2. **Client → Backend (Submit)** — Audio đi qua luồng chunked (`init` → `chunk` → `complete`) để giảm rủi ro mất dữ liệu file lớn; Backend **không tin** object key do client gửi mà tra `media_uploads` để đối chiếu `SessionId/StudentId/QuestionId` + trạng thái `COMPLETED`.
3. **Backend → AI Service (STT)** — AI trả transcript; **audio là bằng chứng, rubric chấm trên TEXT** (BR-13). AI **không** chấm phát âm, cảm xúc hay hình ảnh.
4. **AI Service → Backend (Follow-up)** — Nếu AI sinh follow-up, index câu hỏi **không tăng** – SV phải trả lời câu phụ trước khi sang câu chính tiếp theo.
5. **Backend → Hangfire** — `finish` **persist trạng thái nộp bài trước**, rồi mới enqueue. Nếu queue lỗi, bài vẫn còn để nộp lại (thiết kế an toàn khi queue sập).
6. **Hangfire → AI Service → Hangfire** — Lỗi *retryable* được retry với backoff 30s/120s/300s; lỗi *non-retryable* (sai contract, `question_ids_mismatch`) dừng ngay. Hết lượt → `AiEvaluationStatus = FAILED`, `Answer.Status = ERROR`.

---

## Các nhánh lỗi

| Nhánh | Điều kiện | Xử lý | Lane chịu trách nhiệm |
|---|---|---|---|
| Chưa đủ điều kiện bắt đầu | BR-11 | 400/403, Client hiển thị thông báo, SV thử lại | Client → STUDENT |
| STT thất bại | FR-AI-03 (Partial) | `EMPTY_AUDIO` / `TIMEOUT` / `INTERNAL_ERROR`, lưu lỗi vào Answer, SV nhập lại hoặc dùng text fallback | Client → STUDENT |
| Không có câu trả lời nào | FR-STU-04 | `AiEvaluationStatus = FAILED`, SV nộp lại hoặc GV chấm tay | STUDENT |
| Chấm AI lỗi (retryable) | BR-15, NFR-PERF-03 | Hangfire retry tối đa 3 lḀn, backoff 30s/120s/300s | Hangfire Worker |
| Chấm AI lỗi (non-retryable) / hết lượt | FR-AI-02 | `FAILED` + `Answer.Status = ERROR` + audit log, GV chấm tay hoặc retry | Hangfire → Backend |

---

## Traceability

| Yêu cầu SRS | Thể hiện ở đâu |
|---|---|
| FR-STU-01/02, BR-11 | STUDENT/Backend – Start session & nhánh lỗi điều kiện |
| FR-STU-03/04/08 | Client – ghi âm, upload chunked, giữ bản nháp |
| FR-STU-05, BR-14 | AI/Backend – follow-up gộp vào Answer gốc |
| FR-STU-06, BR-15 | Backend/Hangfire – finish → enqueue → retry backoff |
| FR-AI-01/02, BR-13 | AI Service – STT + chấm rubric trên text, validate contract |

---

## Điểm cần nhấn mạnh khi thuyết trình

- **Nộp bài ≠ chấm xong** – `finish` chỉ persist + enqueue; chấm thật diễn ra ở worker, có thể retry nhiều lần.
- **Audio là bằng chứng, rubric chấm trên văn bản** – không có chấm phát âm/cảm xúc/hình ảnh.
- **Follow-up làm đứng index** – SV phải trả lời câu phụ trước khi sang câu chính tiếp theo.
- **Persist trước, enqueue sau** – nếu queue sập thì bài vẫn còn để nộp lại.
- **Phân biệt được lỗi STT với câu trả lời rỗng** là điểm đề xuất (FR-AI-03) – hiện mới ghi vào `Answer.ErrorMessage`.

---

[[← Danh mục Swimlane](./swimlane-index.md)] · [[SWL-1 – Chuẩn bị & Tổ chức thi ←](./swimlane-01-preparation-scheduling.md) · [[SWL-2B – Review & Công bố →](./swimlane-02b-review-publish-incident.md)]
