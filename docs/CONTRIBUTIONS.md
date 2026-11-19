# AIVES — Báo cáo tổng hợp đóng góp thành viên

> Cơ sở đối chiếu: `task AIVES v2.0 (3).xlsx` (Sheet1 – 66 task / Sheet2 – phạm vi),
> `Task-SWD-version-1.xlsx` (Sheet1 – sprint 1–3 / Sheet2 – phân công theo vai trò),
> và lịch sử Git thực tế của repository (`git log --all`, 288 commit).
> Thống kê dưới đây là số commit **không tính merge commit**, tính trên toàn bộ nhánh.

| # | Thành viên | Vai trò | Commit | Dòng code thay đổi | Phạm vi công việc chính |
|---|---|---|---|---|---|
| 1 | **Võ Trần Ngọc Hữu** | AI1 / Backend Core | **64** | ~27.3K | STT, AI Service, Question Bank, Queue, Desktop App |
| 2 | **Nguyễn Văn Bắc** | BE3 | **29** | ~59.7K | Exam, Session, Slot, Audit Log, Neon, R2 |
| 3 | **Nguyễn Đỗ Bảo Hưng** | BE1 | **30** | ~14.4K | Auth/RBAC, Course/Document, Monitoring, Retake |
| 4 | **Nguyễn Thị Thu Huyền** | Frontend | **23** | ~22.7K | Toàn bộ giao diện Web (STAFF/Admin/Student/Viva) |
| 5 | **Nguyễn Lê Ngọc Trí** | AI2 | **23** | ~70.6K | AI Evaluation, Rubric, Test suite, Monitoring results |
| 6 | **Alex-Broly (Thịnh)** | BE2 | **27** | ~45.2K | Cloudflare R2, Chunked upload, Nginx, Load test, Admin API |

---

## 1. Hữu — AI1 (Backend Core / AI Pipeline / Desktop)

**Phạm vi theo task:** 1.14 Faster-Whisper STT API · 1.15 STT Error Handling ·
1.26 Sprint 1 AI Pipeline Verification · 2.1 Markdown Processing · 2.2 AI Question Generation ·
2.11 AI Service Base Setup

**Đóng góp chính (64 commit):**

| Nhóm | Chi tiết |
|---|---|
| **AI Service nền tảng** | Khởi tạo FastAPI + router + prompt template (`feat(ai-service): initialize base project setup`), cấu trúc service, Pydantic schema, pluggable AI client |
| **Markdown processing** | `MarkdownProcessor` với pipeline 5 bước, endpoint `POST /ai/markdown/process`, tích hợp vào toàn bộ AI service |
| **Sinh câu hỏi** | Contract sinh câu hỏi có validate, prompt outcome-aligned, endpoint sinh theo lô (batch) cho Question Bank, lưu topic + model answer |
| **STT / Faster-Whisper** | `feat(stt): transcribe private R2 audio with Faster-Whisper`, xử lý audio rỗng / không có tiếng nói / lỗi inference, kết nối R2 transcription và lưu kết quả pipeline |
| **Question Bank CRUD** | Course-scoped CRUD, chặn sửa/xoá câu hỏi đã dùng, lưu atomically, timeout HTTP cho generation |
| **Queue & Grading** | Hangfire queue chấm cả đề (`feat(queue): add a Hangfire queue for whole-exam grading`), enqueue khi sinh viên nộp bài, job chấm từng đề, test burst 50 đề & 100 submission đồng thời |
| **Desktop App (Flutter)** | Cấu hình build Windows, giao diện thi tiếng Anh, backup audio bền vững vào AppData, chế độ fullscreen + nút Quit có guard, CI đóng gói GitHub Releases |
| **Docs** | Tài liệu protocol nộp bài & hành vi queue, release notes (EN), hướng dẫn tải desktop release |

---

## 2. Bắc — BE3 (Exam / Session / Audit / Data Platform)

**Phạm vi theo task:** 1.2 Answer & Result Contract · 1.11 Answer, Result & Audit Database ·
1.11 State Machine & Business Rules · 1.19 Transcript & AI Score Persistence ·
1.20 Result Status Update · 1.22 ERD & State Diagram

**Đóng góp chính (29 commit):**

| Nhóm | Chi tiết |
|---|---|
| **Exam & Session** | `Exam` CRUD, `ExamSession`, Session Code, thời gian, sĩ số, Enrollment, chống đăng ký trùng |
| **Refactor module hóa** | Tách "đề thi gốc" và "quản lý slot ca thi" thành 2 module độc lập (`feat: decouple master exam papers and session slot management into distinct modules`), refactor theo period + quick slot splitter + phân cấp lọc |
| **Slot & phân bổ** | Phân bổ slot thi, hỗ trợ nhiều đề trong một ca + phân phối ngẫu nhiên, `SessionDetailDialog` |
| **Answer / Result / Audit** | `Result` entity + trạng thái, `feat(audit-log)`, lưu transcript + AI score, chỉ chuyển `PENDING_REVIEW` khi các Answer bắt buộc đã xử lý xong |
| **Hạ tầng dữ liệu** | Set up Neon + EF scaffold, `setup_cloudflare r2`, `change_neon`, chuyển TEACHER → STAFF |
| **Viva flow** | `VivaSessionService`, `teacher_flow`, `record_fe` |
| **Bảo mật repo** | `security: remove sensitive appsettings from git tracking` (3 commit) — dọn secrets khỏi git tracking |

---

## 3. Hưng — BE1 (Auth / RBAC / Course / Monitoring)

**Phạm vi theo task:** 1.1 API Contract · 1.6 Authentication, Role Migration & RBAC ·
1.7 Course & Document APIs · 1.21 C0 & C1 Architecture Diagrams · 1.23 Swagger Documentation ·
2.14 RBAC & Ownership Testing · 2.15 Authentication Security Testing

**Đóng góp chính (30 commit):**

| Nhóm | Chi tiết |
|---|---|
| **Auth & RBAC** | Login/register, JWT + password hashing, đổi TEACHER → STAFF, thêm EXAMINER, cập nhật seed + policy + ownership |
| **API Contract** | `API Contract Standardization` — chuẩn hóa endpoint, request/response, HTTP status, error format |
| **Course / Document / Question** | Admin course / admin user / teacher document, `expected_keywords`, trạng thái duyệt `PENDING_REVIEW → APPROVED / REJECTED` |
| **Exam & Session** | Exam CRUD, ExamSession management, Session Code, Student Enrollment; luồng chọn period → session |
| **Exam Period & Retake** | Thêm kỳ thi cho examiner, `feat: add exam retake flow` |
| **Monitoring & Load test** | Tích hợp Prometheus + Grafana + cAdvisor (`feat(monitoring): integrate Prometheus Grafana and cAdvisor`) |
| **Tài liệu** | Cập nhật README, tài liệu API, viết hướng dẫn chấm điểm MidTerm Week 6 |

---

## 4. Huyền — Frontend (toàn bộ giao diện Web)

**Phạm vi theo task:** 2.1 Staff Portal · 2.2 Student Viva Room · 2.3 Remove Client-side STT ·
2.4 Processing Status UI · 2.5 Examiner Portal & Review UI · 2.6 Student Result UI ·
2.13 Audio Playback UI · 2.20 Frontend Integration Testing · 2.27 Frontend Security Testing ·
2.31 English Slide Content · 2.34 Demo Script

**Đóng góp chính (23 commit):**

| Nhóm | Chi tiết |
|---|---|
| **Nền tảng FE** | `feat(web): landing page, auth UI on shadcn/ui, feature-based structure`, app shell theo role, auth validation chặt hơn, setup dự án React + TypeScript |
| **Admin** | Trang quản lý user, admin statistics page, exams & results session browser, tìm course theo code/tên/staff, thiết kế list phẳng edge-to-edge |
| **Staff / Teacher** | Trang teacher exams & exam sessions, bảng session dạng flat, sửa/xoá session + thêm/xoá học sinh, đổi trạng thái đề thi |
| **Viva Room** | Microphone + MediaRecorder, timer đếm ngược + auto-submit, fallback trả lời bằng chữ, `fix(web): don't call typed answers transcribed when the browser has no speech-to-text` (bỏ Web Speech API khỏi luồng thi chính) |
| **Examiner & Review** | Chấm học sinh lần lượt trong review dialog, TTS đọc câu hỏi, duyệt/từ chối câu hỏi trong question bank |
| **Thiết kế (UI/UX)** | Redesign landing hero + how-it-works, sign-in panel & sign-up panel với robot artwork, giao diện tiếng Anh, dọn dead code |
| **Tài liệu** | Viết lại README (project overview, architecture, setup guide) |

---

## 5. Trí — AI2 (AI Evaluation / Test / Monitoring results)

**Phạm vi theo task:** 1.3 AI Evaluation Contract · 1.16 AI Question Generation & Follow-up APIs ·
1.17 AI Evaluation & Response Validation · 2.17 AI Evaluation Testing · 2.24 AI Evaluation Contract Testing ·
2.33 AI Slide Content

**Đóng góp chính (23 commit):**

| Nhóm | Chi tiết |
|---|---|
| **AI Evaluation** | Scoring provider + retry settings, validate output rubric trước khi trả về, LLM response contract cho scoring & follow-up, typed pipeline error |
| **Interview & Follow-up** | Route interview flow qua contract đã validate, align follow-up service + prompt với field của contract |
| **Gemini client** | Gọi `generateContent` với strict JSON output, retry lỗi provider tạm thời, fix deploy chia sẻ internal service token giữa backend và AI |
| **Gemini Interactions + STT/TTS** | `feat(ai-service): add Gemini Interactions client, faster-whisper STT, edge-tts TTS` |
| **Test suite** | Fixture provider xác định + contract test, test pipeline evaluation/question/follow-up, end-to-end + live Gemini suite, config pytest loại trừ test provider live |
| **Feedback** | Scoring feedback theo ngôn ngữ câu trả lời (en/vi), cập nhật rubric |
| **Monitoring / Load test** | 81 file kết quả monitoring + load test local Member 3 |
| **Tài liệu** | `docs(api): document every AI service endpoint`, docker compose cho local |

---

## 6. Thịnh — BE2 (Cloudflare R2 / Upload / Nginx / Admin API)

**Phạm vi theo task:** 1.4 Queue Job Contract · 1.5 STT Response Contract ·
1.12 Cloudflare R2 Integration · 1.13 Hangfire, Retry & Duplicate Prevention ·
1.18 AI Worker Orchestration & Authentication · 1.25 Sprint 1 Storage Verification ·
2.11 Retry Answer API & Integration · 2.12 Audio Playback Authorization ·
2.26 Load Testing · 2.29 Deployment Documentation

**Đóng góp chính (27 commit):**

| Nhóm | Chi tiết |
|---|---|
| **Cloudflare R2** | `setup_cloudflare r2`, `feature: cloudflare upload`, `upload tao c up lên cloudflare`, cấu hình S3 SDK + env var + presigned GET URL (tối đa 3600s) |
| **Chunked upload** | `chunking file audio luu local & r2` — upload theo chunk, lưu local fallback + đẩy R2, quản lý `received_chunk_indexes` |
| **Admin API** | `feature: admin quan ly user crs teacher`, `feature: upload md, admin, teacher, student course crud`, `feature: staff crud api`, `admin, user, result`, `admin exam/admin rp sumarry`, `feature: result, gen quest` |
| **Retry & Queue** | Queue job contract (jobId, answerId, resultId, objectKey, attempt), Hangfire retry tối đa 3 lần tổng cộng, chống duplicate job, timeout, retry Answer API |
| **Nginx & Load balancing** | `feat(nginx): Round Robin load balancing + k6 verification + Grafana dashboard` + tài liệu hướng dẫn chi tiết Nginx & Load Balancing |
| **Load test** | 20 test, `update 20 test`, `make_baseline.ps1`, `test dev` |
| **Tài liệu** | `guide deploy Backend, Frontend, Neon, R2, Hangfire, AI Service` (task 2.29), tài liệu API guide |

---

## Bảng đối chiếu Task ↔ Thành viên (Sprint 1 + Sprint 2, file `task AIVES v2.0 (3).xlsx`)

| Sprint | Hữu | Bắc | Hưng | Huyền | Trí | Thịnh |
|---|---|---|---|---|---|---|
| **Sprint 1** (1.1–1.26) | 1.14, 1.15, 1.26 | 1.2, 1.11, 1.12, 1.19, 1.20, 1.22 | 1.1, 1.6, 1.7, 1.8, 1.9, 1.21, 1.23, 1.24 | — | 1.3, 1.16, 1.17 | 1.4, 1.5, 1.13, 1.18, 1.25 |
| **Sprint 2** (2.1–2.34) | — | 2.7, 2.8, 2.9, 2.10, 2.19, 2.25, 2.30 | 2.14, 2.15, 2.21, 2.28, 2.32 | 2.1–2.6, 2.13, 2.20, 2.27, 2.31, 2.34 | 2.17, 2.24, 2.33 | 2.11, 2.12, 2.16, 2.18, 2.22, 2.23, 2.26, 2.29 |

Tất cả 66 task trong 2 sprint đều đã có người phụ trách và đều đạt trạng thái **Complete / 100%**.

---

## Kết quả tổng thể

- **288 commit** trên toàn bộ nhánh, **30+ nhánh tính năng** đã merge về `dev`
- Backend build sạch: **0 error** (17 warning `CS8602` trong repository layer)
- Frontend build production thành công: `tsc -b && vite build` → 2212 modules
- AI Service khởi động OK với Gemini (AI + STT) và Edge-TTS
- Database Neon: 21 bảng, kết nối thành công, 11 user, dữ liệu thật
- 2 Hangfire server hoạt động song song: `aives-api-*` (5 worker: `default`/`answers`) + `aives-grading-*` (1 worker: `grading`)
- Hệ thống chạy đồng thời 3 service: Frontend `:5173` · Backend `:5116` · AI Service `:8000`
- Tài liệu kiến trúc đầy đủ: C0/C1 Container, 3 mức ERD, 4 sequence diagram, swimlane, bản EN song song
