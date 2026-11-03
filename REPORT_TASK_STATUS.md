# AIVES – Báo cáo tiến độ theo Task (Sprint 1–3)

Ngày lập: 2026-10-18
Phạm vi kiểm tra: `backend/` (ASP.NET Core 8), `ai-service/` (FastAPI), `frontend/aives-web` (React 19 + Vite), `docs/`, hạ tầng (Docker/Neon).
Căn cứ đối chiếu: `docs/documentation/Task-SWD - Sheet1.csv`.

> Lưu ý: File tracker CSV hiện đánh dấu **toàn bộ 33 task = "Complete"**. Kết quả đối chiếu với mã nguồn thực tế cho thấy **tracker chưa phản ánh đúng**: chỉ **20/33 task hoàn thành**, **11 task hoàn thành một phần**, **2 task chưa làm**.

## 1. Tổng hợp nhanh

| Trạng thái | Số lượng | Task |
|---|---|---|
| ✅ Hoàn thành | 20 | 1.1, 1.2, 1.3, 1.5, 1.6, 1.7, 1.8, 1.10, 1.11, 2.1, 2.2, 2.3, 2.6, 2.8, 3.1, 3.2, 3.3, 3.4, 3.6, 3.7 |
| ⚠️ Hoàn thành một phần | 11 | 1.4, 1.9, 2.4, 2.5, 2.7, 2.9, 2.10, 3.8, 3.10, 3.11, 3.12 |
| ❌ Chưa làm | 2 | 3.5, 3.9 |
| **Tổng** | **33** | |

**Số task chưa hoàn thành hoàn toàn (cần làm tiếp): 13 task** (11 partial + 2 chưa làm).

### Điểm nghẽn nghiêm trọng (blocker) cần xử lý trước demo
1. **Exam History API (3.5) chưa tồn tại** – không có API lịch sử bài thi/kết quả theo Course/Exam.
2. **Testing (3.9) gần như chưa làm** – backend chỉ có 2 unit test + 1 integration test (và project test **không nằm trong** `Aives.slnx`), **không có test controller/auth/E2E, không có test frontend**.
3. **TTS chưa nối vào luồng thi (2.4/2.10)** – AI Service đã có `/ai/speech/synthesize`, nhưng backend **không gọi** và frontend **không phát** câu hỏi cho Student → yêu cầu "AI đọc câu hỏi" chưa đạt end-to-end.
4. **Migration chưa chạy được trên DB trắng** – chỉ có các migration bổ sung, **thiếu migration khởi tạo**; runtime không gọi `Migrate()` (chỉ `EnsureCreatedAsync` khi `Docker:InitializeDatabase=true`).
5. **Exam status không đổi được** – `ExamStatus` chỉ luôn ở `DRAFT`, không có endpoint đổi trạng thái đề thi (1.9).
6. **Full E2E và Deployment thật chưa được xác nhận** – chỉ có Docker Compose môi trường dev với AI provider = `mock`.

---

## 2. Chi tiết theo từng task

### Sprint 1 – System Design, Authentication & Exam Management

| # | Task | Trạng thái | Bằng chứng / Ghi chú |
|---|---|---|---|
| 1.1 | Introduction & Overall Description | ✅ | `system_overview_and_flows.md`, `docs/architecture/architecture.md` (C0/C1, business process, BR-14/15). |
| 1.2 | User Requirements (actors, UC diagram, business rules) | ✅ | `docs/architecture/architecture.md` (UML/use-case), SRS CSV business rules. **Lệch thiết kế:** thực tế có **4 actor** (ADMIN, STAFF, EXAMINER, STUDENT), không phải 3 như đề bài – "Teacher" bị tách thành STAFF + EXAMINER. |
| 1.3 | Functional & Non-Functional Requirements | ✅ | Đầy đủ trong `architecture.md` + `docs/api/*`. |
| 1.4 | System & Database Design (ERD + Data Dictionary) | ⚠️ **Partial** | Có `docs/documentation/erd.drawio.png` + C4 PlantUML. **Thiếu Data Dictionary dạng văn bản.** Không có entity `QuestionScore`/`FinalResult` riêng – thay bằng `exam_questions.question_score`, `results.final_score/final_feedback`. |
| 1.5 | Base Project Setup | ✅ | `docker-compose.yml` (5 service), Neon config (`docs/database/database.md`, `neon.ts`), git repo, `.env.example`. |
| 1.6 | Authentication & Authorization | ✅ | `AuthController`, `AuthService`, `JwtService` (HS256 + blacklist), `PasswordHasher` (BCrypt 12), role guard `Program.cs`. |
| 1.7 | User & Course Management | ✅ | `AdminUserController` (CRUD đầy đủ), `AdminCourseController` + `PUT /assign-teacher`. |
| 1.8 | Markdown Document Management (2 file .md) | ✅ | `DocumentsController` + `TeacherDocumentController`; ràng buộc đúng **1 CONTENT + 1 LEARNING_OUTCOMES**; validate `.md`, ≤5MB. |
| 1.9 | Exam Management (Exam Code, config, status) | ⚠️ **Partial** | Tạo/sửa/xoá đề, sinh `EX-XXXXXXXX`, `DurationMinutes`/`MaxQuestions` OK (`ExamService.cs`). **Thiếu:** endpoint chuyển trạng thái đề (`ExamStatus` luôn `DRAFT`). |
| 1.10 | Basic UI | ✅ | Login/Register, Admin Dashboard + Users/Courses, Teacher (STAFF) Dashboard + Courses/Questions/Exams/Sessions, Student nhập Exam Code. (`src/features/*`) |
| 1.11 | AI Service Base Setup | ✅ | `ai-service/app/main.py`, routers, prompts, `ai_client.py`, Dockerfile. **Thiếu:** `AI_SERVICE_API_KEY` khai báo trong `.env` nhưng **không có auth middleware** trong `config.py`. |

### Sprint 2 – AI Viva Interview & Speech Processing

| # | Task | Trạng thái | Bằng chứng / Ghi chú |
|---|---|---|---|
| 2.1 | Markdown Processing | ✅ | `markdown_processor.py` (579 dòng), `/ai/markdown/process`. |
| 2.2 | AI Question Generation | ✅ | `question_service.py`, `question_bank_service.py`, `/ai/interview/question`, `/ai/questions/generate`. ⚠️ Endpoint phụ `/ai/interview/generate-questions` là **stub hardcode**, không gọi AI (`app/api/interview.py`). |
| 2.3 | Speech-to-Text | ✅ | `speech_service.py` – nhiều engine; đang chạy `faster_whisper` (local). |
| 2.4 | Text-to-Speech | ⚠️ **Partial** | `tts_service.py` + `/ai/speech/synthesize` (engine `edge_tts`) đã xong ở AI Service, **nhưng backend không gọi** và frontend không phát audio → câu hỏi chưa được "đọc" cho Student. |
| 2.5 | AI Interview Engine (hỏi→đáp→phân tích→câu tiếp) | ⚠️ **Partial** | `interview_service.py` là **stateless**, không có session state (Redis nêu ở docstring "future sprint"); vòng lặp do backend tự ghép. |
| 2.6 | Follow-up Question | ✅ | `followup_service.py`, `/ai/interview/follow-up`, prompt + validate chặt. |
| 2.7 | Exam Session API (Start/Submit/Next/End) | ⚠️ **Partial** | `VivaSessionController` đủ start/submit-answer/finish + lưu Answer/Transcript. **"Next Question" không phải endpoint riêng** (trả trong response submit); **không có API thi viết** (chỉ vấn đáp AI). |
| 2.8 | AI Integration API | ✅ | `AiServiceClient` gọi đúng path `/ai/*` (lỗi "path mismatch" trong AUDIT cũ **đã được sửa**), timeout + fallback. |
| 2.9 | Viva Exam UI | ⚠️ **Partial** | `VivaRoomPage.tsx` (audio recorder, waveform, follow-up badge, progress). **Thiếu:** đếm ngược thời gian thi (chỉ hiện thời lượng tĩnh); transcript dùng **Web Speech API của trình duyệt** (không ổn định trên mọi browser). |
| 2.10 | Viva Flow Integration | ⚠️ **Partial / chưa verify** | Luồng Exam Code→Session→Question→Voice→STT→Answer→Follow-up→End đã ghép. **Thiếu TTS**, và chưa có bằng chứng chạy E2E trên DB/AI thật. |

### Sprint 3 – AI Scoring, Teacher Review, Result & Deployment

| # | Task | Trạng thái | Bằng chứng / Ghi chú |
|---|---|---|---|
| 3.1 | AI Evaluation & Scoring | ✅ | `scoring_service.py`, `evaluation_service.py`, prompt trọng số 40/30/20/10, `/ai/evaluate/*`. |
| 3.2 | AI Feedback & Answer Analysis | ✅ | `feedback_analysis_service.py`, `answer_evidence.py` (425 dòng, chống prompt-injection), rubric-grounded. |
| 3.3 | Result API | ✅ | `ResultService`, `Result`/`Answer` entity, status `PENDING_REVIEW/REVIEWED/PUBLISHED/CANCELLED`. Schema khác thiết kế ban đầu (không tách bảng `QuestionScore`/`FinalResult`) nhưng đủ chức năng. |
| 3.4 | Teacher Review API (review/approve/publish) | ✅ | `ResultsController` (`/review`, `/publish`), giữ nguyên điểm AI (BR-15). **Lệch:** role thẩm định là **EXAMINER**, không phải TEACHER. |
| 3.5 | Exam History | ❌ **Chưa làm** | Không có API lịch sử bài thi/kết quả theo Course/Exam/Student. `GetStudentPublishedResultsAsync` đã viết nhưng **không expose**; `GetSessionEnrollmentsAsync` cũng không có controller. |
| 3.6 | Teacher Review UI | ✅ | `TeacherResultsPage.tsx` + `ResultReviewModal.tsx` (xem AI score, nghe audio, sửa điểm, publish). |
| 3.7 | Student Result UI | ✅ | `StudentResultsPage.tsx` – chỉ hiện chi tiết sau khi `isPublished`. |
| 3.8 | Full System Integration | ⚠️ **Chưa verify** | Đã ghép module nhưng **không có bằng chứng chạy E2E** Admin→Course→…→Final Result trên Neon + AI thật; Docker dùng `AI_PROVIDER=mock`. |
| 3.9 | Testing | ❌ **Gần như chưa làm** | Chỉ `Aives.Tests/ExamSessionServiceTests.cs` (2 test) + `CloudflareR2IntegrationTests.cs`. **Không có test** Auth/Authorize/User/Course/Upload/Exam/Session/STT/Scoring/Review/Result cho backend, **không có test frontend**. `Aives.Tests` **không được tham chiếu trong `Aives.slnx`**. |
| 3.10 | Security & Error Handling | ⚠️ **Partial** | JWT + blacklist, RBAC, validation, rate-limit login, error mapping OK; secret chỉ ở file local đã gitignore (`appsettings.Development.json`, `.env`) – **không** commit. **Thiếu:** `AI:ApiKey` cấu hình nhưng không gửi; AI service không xác thực nội bộ. |
| 3.11 | Deployment | ⚠️ **Partial** | Có Docker Compose dev + Dockerfile 3 service + nginx. **Thiếu:** deploy cloud/CI thật (không có vercel/render/fly/GitHub Actions); **migration chưa chạy được trên DB trắng** (thiếu initial migration, runtime không `Migrate()`). |
| 3.12 | Documentation & Demo | ⚠️ **Partial** | Có API docs (`docs/api/*`, `API_GUIDE.md`), diagram C0/C1, ERD ảnh. **Thiếu:** **User Guide**, **Data Dictionary văn bản**, slide/demo. Tài liệu lệch thực tế (`API_GUIDE.md` ghi `POST /api/seed` không tồn tại; `API_TEACHER_REVIEW.md` nhắc test file không tồn tại; role TEACHER cũ). |

---

## 3. Danh sách việc cần làm để hoàn tất (đề xuất ưu tiên)

**P1 – Bắt buộc trước khi demo/đánh giá**
1. **3.5 Exam History API**: bổ sung endpoint danh sách bài thi/kết quả theo Course/Exam/Student; expose `ResultService.GetStudentPublishedResultsAsync` (`GET /api/student/results`) và `ExamSessionService.GetSessionEnrollmentsAsync`.
2. **2.4 + 2.10 TTS vào luồng thi**: backend gọi `/ai/speech/synthesize`, trả audio về FE và phát câu hỏi cho Student (hoặc nút "Nghe câu hỏi").
3. **3.9 Testing**: 
   - Đưa `Aives.Tests` vào `Aives.slnx`.
   - Viết integration test (WebApplicationFactory) cho Auth/RBAC, Exam, Document, Viva Session, Result review/publish.
   - Thêm test tối thiểu cho frontend (Vitest) các luồng chính.
4. **3.11 Migration**: tạo **initial migration** đầy đủ + bật `Database.Migrate()` (hoặc quy trình migrate rõ ràng) để dựng DB trắng.
5. **3.8 E2E thật**: chạy trọn luồng 1 lần trên Neon + AI provider thật (không mock) và ghi lại biên bản test.

**P2 – Hoàn thiện chức năng**
6. **1.9 Exam status lifecycle**: endpoint đổi trạng thái đề (`DRAFT→SCHEDULED→OPEN→CLOSED/CANCELLED`).
7. **2.9 Viva UI**: đếm ngược thời gian thi thật; dự phòng transcript khi trình duyệt không hỗ trợ Web Speech.
8. **2.5 Interview engine**: cân nhắc lưu trạng thái phiên (Redis/DB) để engine chủ động hỏi–đáp.
9. **3.10 Security**: gửi `X-AI-Service-Key` giữa backend ↔ AI service; middleware xác thực phía FastAPI.
10. **3.12 Docs**: viết **User Guide**, **Data Dictionary**, cập nhật tài liệu khớp thực tế (role STAFF/EXAMINER, bỏ `POST /api/seed` sai).

**P3 – Nên có**
11. **1.4 Data Dictionary** văn bản cho 12 thực thể (User, Course, MarkdownDocument, Exam, ExamSession, Question, Answer, Transcript, Result, …).
12. Admin dashboard "Bài thi & kết quả" (`/api/admin/exams`, `/api/admin/results`) – hiện là placeholder `ADMIN/reports`.
13. Dọn stub `/ai/interview/generate-questions` (hardcode) hoặc chuyển hướng về `/ai/questions/generate`.
14. Dọn endpoint/hàm không dùng: `examsApi.updateQuestions`, `vivaApi.getCurrentQuestion`, `teacherResultsApi.get`.

---

## 4. Ghi chú về tính đúng đắn của AUDIT_REPORT.md cũ (Oct 12)

Một số kết luận trong `AUDIT_REPORT.md` đã **lỗi thời** và cần cập nhật:
- "Endpoint path mismatch" C# ↔ FastAPI → **đã sửa** (`AiServiceClient` gọi đúng `/ai/*`).
- "Thiếu endpoint nộp câu trả lời của sinh viên" → **đã có** `VivaSessionController` (start/submit-answer/finish).
- "Chưa gọi `/ai/evaluate/*`" → **đã gọi** trong `VivaSessionController.finish`.
- Cảnh báo "secret bị commit" → **không đúng**: `appsettings.Development.json` và `.env` đều đã gitignore.
