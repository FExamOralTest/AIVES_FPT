# Nội dung nộp giảng viên — AIVES (copy-paste vào form)

> Nguồn đối chiếu: `task AIVES v2.0 (3).xlsx` (66 task, Sprint 1–2) +
> `Task-SWD-version-1.xlsx` (Sprint 1–3) + lịch sử Git thực tế (288 commit).
> % đề xuất mỗi mục cộng lại = 100%. Có thể chỉnh ±2% cho tròn số thực tế của nhóm.

---

## EVALUATION 1 – Requirement Analysis & Modeling

### 1. Võ Trần Ngọc Hữu — 15%
- Research ASR (Faster-Whisper) và TTS cho bài thi vấn đáp; chốt STT Response Contract (transcript, language, duration, error format).
- Thiết kế AI Service base (FastAPI, router, prompt template, Pydantic schema) và pipeline MarkdownProcessor 5 bước tạo context cho AI.
- Xây dựng contract sinh câu hỏi theo Learning Outcomes và AI Evaluation Contract (AiScore, confidence, feedback, rubric results).

### 2. Nguyễn Văn Bắc — 22%
- Chốt Answer & Result Contract, trạng thái xử lý / review / publish và các business rules liên quan.
- Thiết kế migration Answer / Result / Audit Log (ObjectKey, jobId, AiScore / FinalScore, người review-publish, timestamp).
- Vẽ ERD + state diagram (Answer PROCESSING → EVALUATED / ERROR; Result PENDING_REVIEW → REVIEWED → PUBLISHED); viết tài liệu BR-01 – BR-17.
- Tham gia System & Database Design, Base Project Setup (Neon PostgreSQL, EF scaffold).

### 3. Nguyễn Đỗ Bảo Hưng — 22%
- Chuẩn hóa API Contract (endpoint, request/response, HTTP status, error format) cho giao tiếp Backend ↔ FastAPI.
- Thiết kế Authentication, Role Migration & RBAC (JWT, đổi TEACHER → STAFF, thêm EXAMINER, policy, ownership, seed).
- Cập nhật C0 Context & C1 Container (Admin / STAFF / EXAMINER / Student, React, ASP.NET API, Neon, R2, Hangfire, FastAPI, AI Provider) + Swagger.
- Phân tích User Requirements, Use Case Diagram và business process thi vấn đáp AI.

### 4. Nguyễn Thị Thu Huyền — 10%
- Tham gia phân tích yêu cầu phía người dùng (3 actors: Admin / Teacher / Student) và luồng thi Viva.
- Thiết kế Basic UI: Login, Admin / Teacher Dashboard, Course Management, Upload .md, Create Exam, Student nhập Exam Code.

### 5. Nguyễn Lê Ngọc Trí — 16%
- Research rubric chấm điểm và AI scoring theo Learning Outcomes; chốt AI Evaluation Contract và LLM Response Contract (scoring / follow-up).
- Thiết kế luồng AI Interview Engine (AI hỏi → Student trả lời → AI phân tích → câu hỏi tiếp theo) và AI Feedback (điểm mạnh / yếu / nội dung thiếu).
- Research STT / TTS (Faster-Whisper, Edge-TTS) phục vụ transcript và đọc câu hỏi cho Student.

### 6. Thịnh — 15%
- Chốt Queue Job Contract (jobId, answerId, resultId, objectKey, attempt, trạng thái job) và STT Response Contract.
- Thiết kế Cloudflare R2 Integration (private bucket, S3 SDK, environment variables, presigned GET URL tối đa 3600s) và cơ chế Hangfire retry / chống duplicate job.
- Tham gia System & Database Design, Base Project Setup (Backend, Frontend, Neon, AI Service, Git, Environment).

**Tổng Eval 1: 15 + 22 + 22 + 10 + 16 + 15 = 100%**

---

## EVALUATION 2 – Software Architecture & Design

### 1. Võ Trần Ngọc Hữu — 20%
- Triển khai Faster-Whisper STT API đọc audio private từ R2; xử lý file rỗng, sai định dạng, audio không có tiếng nói, transcript rỗng, timeout, lỗi inference.
- Làm Question Bank CRUD theo course, sinh câu hỏi theo lô từ nội dung .md, lưu topic + model answer, chặn sửa / xóa câu hỏi đã sử dụng.
- Dùng background job (Hangfire) chấm cả đề: enqueue khi sinh viên nộp bài, job chấm từng đề, API trạng thái xử lý; test burst 50 đề và 100 submission đồng thời.
- Kết nối R2 transcription, lưu transcript + kết quả pipeline (language, duration, AiScore, feedback) vào Database.

### 2. Nguyễn Văn Bắc — 19%
- Làm Exam / ExamSession / Session Code / thời gian / sĩ số / Enrollment, chống đăng ký trùng; refactor tách module đề thi gốc ↔ quản lý slot ca thi; phân bổ slot + phân phối đề ngẫu nhiên.
- Làm Examiner Review / Publish API, Student Result API, Review & Audit API; Result chỉ chuyển PENDING_REVIEW khi các Answer bắt buộc đã xử lý xong; AiScore không ghi đè FinalScore.
- Setup Neon + EF scaffold + Cloudflare R2 (change_neon); dọn toàn bộ sensitive appsettings khỏi git tracking; làm VivaSessionService + teacher flow.

### 3. Nguyễn Đỗ Bảo Hưng — 16%
- Làm login / register, JWT, password hashing, phân quyền Admin / Teacher / Student; Exam CRUD, ExamSession management, Session Code, Student Enrollment.
- Làm Teacher Review & Publish Final Results; thêm Exam Period cho examiner; luồng thi lại (retake); expected keywords + duyệt câu hỏi (APPROVED / REJECTED).
- Tích hợp Prometheus + Grafana + cAdvisor phục vụ giám sát hệ thống; kiểm thử RBAC & ownership, authentication security (token hết hạn, 401 / 403).

### 4. Nguyễn Thị Thu Huyền — 14%
- Làm Staff Portal, Student Viva Room (microphone, MediaRecorder, timer, auto-submit, fallback trả lời bằng chữ); loại bỏ Web Speech API khỏi luồng thi chính.
- Làm Processing Status UI (PROCESSING / EVALUATED / ERROR), Examiner Portal & Review UI, Student Result UI, Audio Playback bằng presigned URL.
- Redesign landing / auth / admin list; kiểm thử tích hợp Frontend (Staff Portal, Viva Room, Examiner Portal, Audio, Result) và Frontend security testing.

### 5. Nguyễn Lê Ngọc Trí — 15%
- Làm AI service Grading: scoring provider + retry, validate rubric scoring output, gọi Gemini generateContent với strict JSON, feedback theo ngôn ngữ câu trả lời (en / vi); connect Backend ↔ AI qua internal service token.
- Làm question generation + follow-up qua contract đã validate; align prompt với field contract; retry lỗi provider tạm thời.
- Viết test suite AI (contract test, E2E, live Gemini), tài liệu toàn bộ endpoint AI service; chạy load test local + thu thập monitoring results; docker compose local.

### 6. Thịnh — 16%
- Làm chunked upload audio (lưu local + đẩy R2, theo dõi received_chunk_indexes); Retry Answer API + enqueue lại Hangfire; kiểm soát quyền nghe audio (presigned URL có thời hạn, bucket private).
- Làm Admin / Course / Document / User / Result CRUD API, upload .md, staff CRUD; dựng Nginx Round Robin load balancing + kiểm chứng k6 + Grafana dashboard.
- Kiểm thử STT, queue & retry (tối đa 3 lần), Answer / Result, E2E storage (submit → R2 → ObjectKey → presigned URL) và E2E AI pipeline; load test 20–50 submit đồng thời.

**Tổng Eval 2: 20 + 19 + 16 + 14 + 15 + 16 = 100%**

---

## FINAL PRESENTATION – Product Demo

### 1. Võ Trần Ngọc Hữu — 20%
- Làm desktop app (Flutter Windows) cho dự án: giao diện thi tiếng Anh, chế độ fullscreen, backup audio bền vững vào AppData trước khi nộp, CI đóng gói GitHub Releases.
- Demo pipeline AI hoàn chỉnh R2 → STT → Evaluation → Database (transcript + điểm + feedback được lưu đúng contract).
- Viết tài liệu student submission protocol + queue behavior; chuẩn bị transcript demo cho kịch bản thuyết trình.

### 2. Nguyễn Văn Bắc — 15%
- Demo luồng Admin → STAFF → Course → Exam → Session → Enrollment và phân quyền tương ứng.
- Demo Result end-to-end: Answer hoàn tất → chờ review → Examiner review → publish → Student xem kết quả; audit log đầy đủ người review / publish / thời gian / điểm trước-sau.
- Chuẩn bị Exam Manager + SessionDetailDialog phục vụ demo phân ca thi, nhiều đề trong một session.

### 3. Nguyễn Đỗ Bảo Hưng — 14%
- Demo phân quyền 4 vai trò (Admin / STAFF / EXAMINER / STUDENT) + ownership (STAFF không chạm course người khác, Student không xem dữ liệu người khác).
- Chuẩn bị slide kiến trúc C0, C1, ERD, R2, Queue, AI Worker, Data Flow; kiểm thử Backend end-to-end phục vụ demo.
- Demo Exam Period, luồng retake và duyệt câu hỏi trong Question Bank.

### 4. Nguyễn Thị Thu Huyền — 22%
- Demo toàn bộ giao diện: Staff Portal, Student Viva Room (mic + timer + auto-submit + TTS đọc câu hỏi), Examiner Portal (nghe audio, xem transcript, AI score, feedback, rubric), Student Result (chỉ hiện sau publish).
- Viết nội dung slide tiếng Anh (problem context, workflow, RBAC, tổng quan hệ thống) và chuẩn bị demo script theo 4 vai trò Admin / STAFF / EXAMINER / Student.
- Viết lại README (project overview, architecture, setup guide) phục vụ giảng viên chạy thử.

### 5. Nguyễn Lê Ngọc Trí — 13%
- Demo AI chấm điểm + feedback theo rubric (điểm từng câu, điểm tổng đề xuất, điểm mạnh / yếu) và sinh câu hỏi / follow-up từ nội dung môn học.
- Chuẩn bị slide AI (Faster-Whisper, AI evaluation, rubric, AI score suggestion); kiểm thử AI evaluation contract (transcript + question + rubric → response + điểm đúng contract).

### 6. Thịnh — 16%
- Demo submit audio → Backend → R2 → ObjectKey → cấp presigned URL → nghe lại có kiểm soát quyền; demo retry khi Answer lỗi.
- Chạy load test 20–50 sinh viên submit đồng thời (đo thời gian upload / xử lý, theo dõi lỗi và tài nguyên); demo Nginx load balancing.
- Viết tài liệu deploy (Backend, Frontend, Neon, R2, Hangfire, AI Service) và API guide phục vụ giảng viên dựng + chấm demo.

**Tổng Final: 20 + 15 + 14 + 22 + 13 + 16 = 100%**

---

## PHÂN BỔ 4 NỘI DUNG TEMPLATE → NGƯỜI PHỤ TRÁCH

| # | Nội dung trong template | Người phụ trách | Người hỗ trợ | Minh chứng trong repo |
|---|---|---|---|---|
| 1 | **Làm phần Research cho dự án, ASR, Rubric chấm điểm cho bài thi** | **Trí** (rubric + AI scoring research, 16% Eval 1) | Hữu (STT/TTS research), Hưng (business process) | `ai-service/app/services/` (scoring, feedback), `docs/ai/`, `docs/api/` |
| 2 | **Làm AI service: Grading, generate ra nguồn câu hỏi và connect với back-end** | **Trí** (Grading + scoring provider) | Hữu (question generation, connect Backend ↔ AI) | `ai-service/app/` + `backend/Aives.Infrastructure/Services/AiServiceClient.cs` |
| 3 | **Sử dụng background job để xử lý nhiều bài làm sinh viên với worker lưu local và gửi về server** | **Hữu** (Hangfire queue chấm cả đề, enqueue khi nộp bài) | Thịnh (chunked upload lưu local → R2), Bắc (Result state) | `backend/Aives.Infrastructure/Jobs/` (grading, answers), `ai-service` audio local cache |
| 4 | **Làm desktop app cho dự án** | **Hữu** (Flutter Windows app, fullscreen, AppData backup) | Thịnh (upload/recovery), Huyền (UI tham chiếu) | `frontend/aives-desktop/` |

### Ghi chú bổ sung cho từng nội dung (dùng để trả lời phỏng vấn giảng viên)

**1. Research (ASR, Rubric, nguồn câu hỏi)**
- ASR: Faster-Whisper trong `ai-service` (model `base`/`small`, ngôn ngữ tự nhận diện, VAD filter, timestamp) → trả `transcript`, `language`, `duration`.
- Rubric: mỗi Question có `rubric` (tiêu chí + điểm) và `expected_keywords`; AI chấm theo từng câu rồi scale theo `exam percentage` của câu đó cho tổng đề.
- Nguồn câu hỏi: Markdown `.md` của course → `MarkdownProcessor` 5 bước → context + Learning Outcomes → Gemini sinh câu hỏi có validate JSON, retry lỗi provider.
- Tài liệu: `docs/ai/`, `docs/api/`, `ai-service/README.md`.

**2. AI service (Grading + generate câu hỏi + connect back-end)**
- Grading: `scoring provider` + retry settings → validate rubric output → `Gemini generateContent` strict JSON → trả AiScore / confidence / feedback / rubric results.
- Generate: endpoint sinh câu hỏi theo lô cho Question Bank, lưu topic + model answer, chặn sửa/xóa câu hỏi đã dùng.
- Connect back-end: header `X-Internal-Token` (internal service token), base URL `http://ai-service:8000` trong Docker, timeout 300s; AI service không lộ key ra frontend.
- Test: contract test + E2E + live Gemini suite (`ai-service/tests/`).

**3. Background job (worker lưu local + gửi về server)**
- Hangfire 2 server: `aives-api-*` (5 worker: `default`, `answers`) + `aives-grading-*` (1 worker: `grading`); schema `hangfire`, `QueuePollInterval=2s`, `InvisibilityTimeout=10m`.
- Retry: 30s → 120s → 300s, tối đa 3 lần tổng cộng (BR-17); chống duplicate job bằng metadata `answerId` + ownership.
- Worker lưu local: audio chunk lưu local (`received_chunk_indexes`) rồi đẩy lên Cloudflare R2 → lưu `ObjectKey` trong DB → cấp presigned URL ≤ 3600s.
- Kiểm chứng: test burst 50 đề, 100 submission đồng thời, load test 20–50 student.

**4. Desktop app**
- Flutter Windows target, giao diện thi tiếng Anh, chế độ fullscreen + nút Quit có guard.
- Backup audio bền vững vào AppData trước khi nộp, có cơ chế phục hồi khi mất mạng.
- CI validate & package Windows builds → GitHub Releases.

---

## TỰ KIỂM TRA TRƯỚC KHI NỘP

- [ ] Mỗi mục cộng lại đúng 100% (Eval 1: 15+22+22+10+16+15 · Eval 2: 20+19+16+14+15+16 · Final: 20+15+14+22+13+16)
- [ ] Tên thành viên viết đúng như trong danh sách nhóm (Hữu, Bắc, Hưng, Huyền, Trí, Thịnh)
- [ ] Mỗi gạch đầu dòng có commit thực tế đối chiếu được trong `git log`
- [ ] Demo chạy được: Frontend `:5173` · Backend `:5116` · AI Service `:8000` · Neon PostgreSQL
- [ ] Tài liệu đính kèm: `docs/CONTRIBUTIONS.md`, `docs/architecture/` (C0/C1, ERD 3 mức, 4 sequence, swimlane, bản EN)

