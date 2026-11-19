#!/usr/bin/env python3
"""Generate the Markdown companion page for each sequence diagram."""
import pathlib

OUT = pathlib.Path(__file__).parent

DIAGRAMS = [
    dict(slug="seq-01-viva-flow", lang="vi",
         title="SEQ 1 – Luồng thi Viva",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         image="SEQ_01_VIVA_FLOW.svg",
         puml="seq_01_viva_flow.puml",
         hint="**3 giai đoạn**: bắt đầu ca thi → ghi âm & nộp câu trả lời (STT + sinh follow-up) → kết thúc & xếp hàng chấm.",
         summary=(
             "Sơ đồ mô tả trọn vẹn vòng đời một lượt thi Viva của Sinh viên: từ lúc mở phòng thi, "
             "ghi âm và nộp từng câu trả lời, cho tới khi kết thúc ca thi và bài được đẩy vào hàng đợi chấm AI.\n\n"
             "Điểm quan trọng được thể hiện: file âm thanh được upload theo **luồng chunked** "
             "(`/api/audio/chunked/init` → `chunk` → `complete`) cho bản ghi dài hoặc mạng yếu; "
             "Backend gọi AI Service để **STT** và **sinh câu hỏi follow-up**, nhưng nếu AI lỗi thì hệ thống "
             "**fallback sang TextAnswer** thay vì mất bài. Mỗi câu trả lời được lưu với `Status = PROCESSING`, "
             "còn toàn bộ bài sẽ sang `PENDING_REVIEW` khi sinh viên kết thúc ca thi."),
         table_title="Các participant & trách nhiệm",
         thead="| Participant | Trách nhiệm |",
         rows=[("Sinh viên (Student)", "Bắt đầu ca thi, ghi âm, nộp câu trả lời, kết thúc bài"),
               ("Web SPA (VivaRoomPage)", "Ghi âm WAV/webm, upload chunked, hiển thị câu hỏi & feedback"),
               ("VivaSessionController", "Nhận request, phân quyền STUDENT, trả lỗi 400/403/404/409"),
               ("ChunkedAudioController", "Init / chunk / complete / abort — upload audio dài theo phần"),
               ("VivaSessionService", "Nghiệp vụ: STT, follow-up, lưu Answer, cập nhật Enrollment"),
               ("Cloudflare R2", "Lưu file âm thanh, trả presigned URL"),
               ("AI Service (FastAPI)", "`/ai/speech/transcribe-object` và `/ai/interview/follow-up`"),
               ("PostgreSQL (Neon)", "Session, Enrollment, ExamQuestions, Answer, Result, AuditLog")]),
    dict(slug="seq-02-grading", lang="vi",
         title="SEQ 2 – Chấm điểm AI",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         image="SEQ_02_GRADING.svg",
         puml="seq_02_grading.puml",
         hint="**Hangfire worker** giữ 1 khóa phân tán toàn hệ thống nên chấm lần lượt từng bài. Hàng đợi lưu trong schema `hangfire` của PostgreSQL; lỗi retryable được thử lại 30s → 120s → 300s (tối đa 3 lần).",
         summary=(
             "Sơ đồ mô tả luồng chấm điểm tự động chạy **ngoài request HTTP**, trong worker Hangfire.\n\n"
             "Điểm nhấn thiết kế: `SessionGradingLock` giữ **một khóa phân tán toàn hệ thống** "
             "(`aives:session-grading`, timeout 12 giờ) nên các bài được chấm **lần lượt**, "
             "tránh việc gọi LLM dồn dập. Hệ thống gom đủ context (Result + Answers + Rubric + Documents) "
             "rồi gửi **một lần** `/ai/evaluate/session` cho toàn bộ bài.\n\n"
             "Điểm được kiểm tra nghiêm ngặt: số phân tích AI trả về phải **khớp đúng** số câu đã nộp, "
             "nếu lệch → `question_ids_mismatch` → thất bại ngay, không retry. Lỗi retryable "
             "(timeout, rate limit) được Hangfire thử lại với backoff 30s → 120s → 300s, tối đa 3 lḀn; "
             "lỗi non-retryable → `Answer.Status = ERROR`, `Result = FAILED` và ghi audit log."),
         table_title="Các participant & trách nhiệm",
         thead="| Participant | Trách nhiệm |",
         rows=[("Hangfire Worker", "Queue `grading`, 1 worker — chấm lần lượt từng bài"),
               ("SessionGradingLock", "Khóa phân tán `aives:session-grading`, timeout 12 giờ"),
               ("SessionGradingJob", "Resolve attempt (1..3), phân loại lỗi retryable / non-retryable"),
               ("SessionGradingService", "Thu thập context, gọi AI, quy đổi điểm theo rubric, lưu AiScore"),
               ("AI Service (FastAPI)", "`/ai/evaluate/session` — chấm theo từng tiêu chí rubric"),
               ("LLM Provider", "Gemini / OpenAI — sinh phân tích rubric cho từng câu"),
               ("PostgreSQL (Neon)", "Result, Answers, Rubric, Documents, AiAnalysis, AuditLog")]),
    dict(slug="seq-03-review-publish", lang="vi",
         title="SEQ 3 – Review, Công bố & Xem kết quả",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         image="SEQ_03_REVIEW_PUBLISH.svg",
         puml="seq_03_review_publish.puml",
         hint="Sinh viên **chỉ thấy điểm khi `Status = PUBLISHED`** — trước đó API trả 404. Điểm AI không bao giờ bị ghi đè bởi điểm chính thức.",
         summary=(
             "Sơ đồ mô tả vòng đời kết quả từ lúc AI chấm xong đến khi sinh viên nhìn thấy điểm chính thức.\n\n"
             "Vòng đời `Result`: **`PENDING_REVIEW` → `REVIEWED` → `PUBLISHED`**. "
             "Khảo thí nghe lại bằng chứng qua presigned URL của Cloudflare R2, chấm điểm chính thức rồi mới publish.\n\n"
             "Hai quy tắc nghiệp vụ được thể hiện rõ:\n"
             "- **BR-15**: review chỉ ghi `final_score` + `teacher_comment`, **không ghi đè** `ai_score` / `ai_feedback` — "
             "điểm AI và điểm người được giữ riêng để đối chiếu.\n"
             "- **BR-11**: sinh viên chỉ đọc được điểm khi `Status = PUBLISHED`; trước đó API trả 404 nên không rò rỉ thông tin.\n\n"
             "Phần cuối mô tả xử lý sự cố: **chấm lại 1 câu** (`RetryAnswerJob`) và **cấp lại thi** (retake) — "
             "bài cũ được chuyển `CANCELLED` nhưng vẫn giữ lịch sử để đối soát."),
         table_title="Các participant & trách nhiệm",
         thead="| Participant | Trách nhiệm |",
         rows=[("Khảo thí (Examiner)", "Mở danh sách pending, nghe audio, chấm điểm chính thức, publish"),
               ("ResultsController", "`GET /api/results`, `PUT /{id}/review`, `POST /{id}/publish`"),
               ("ResultService", "Kiểm tra trạng thái, ghi FinalScore / TeacherNotes, chuyển sang PUBLISHED"),
               ("AnswerAudioController", "Sinh presigned URL để nghe lại bằng chứng"),
               ("Cloudflare R2", "Phát lại file âm thanh qua presigned URL"),
               ("Sinh viên (Student)", "Xem điểm chính thức sau khi publish; xem lịch sử bài thi bù"),
               ("PostgreSQL (Neon)", "Result (PENDING_REVIEW → REVIEWED → PUBLISHED), Answers, AuditLog")]),
    dict(slug="seq-04-admin-prep", lang="vi",
         title="SEQ 4 – Chuẩn bị & Tổ chức thi",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         image="SEQ_04_ADMIN_PREP.svg",
         puml="seq_04_admin_prep.puml",
         hint="Chuỗi chuẩn bị: **tạo tài khoản → tạo môn học → upload .md → AI sinh câu hỏi → duyệt → lập lịch ca thi → ghi danh**.",
         summary=(
             "Sơ đồ mô tả chuỗi chuẩn bị nghiệp vụ trước khi kỳ thi diễn ra, chia vai trò rõ giữa Quản trị và Giảng viên.\n\n"
             "Điểm kiểm soát chất lượng quan trọng: **tài liệu .md phải hợp lệ** (không rỗng, đúng kích thước, "
             "đúng định dạng) thì mới được lưu; **câu hỏi do AI sinh ra luôn ở trạng thái `PENDING_REVIEW`** "
             "và bắt buộc giảng viên approve mới được dùng dựng đề — AI không bao giờ tự đưa vào đề thi.\n\n"
             "Khi tạo ca thi, hệ thống kiểm tra **slot trùng giờ / ngoài khung ca thi** và sinh `SessionCode` "
             "theo định dạng `ESS-XXXXXXXX`; giảng viên mở ca bằng cách chuyển sang `IN_PROGRESS` rồi theo dõi "
             "tiến độ ghi danh qua endpoint monitor."),
         table_title="Các participant & trách nhiệm",
         thead="| Participant | Trách nhiệm |",
         rows=[("Quản trị (Admin)", "Tạo tài khoản & gán vai trò, tạo môn học, phân công giảng viên"),
               ("Giang viên (Staff)", "Upload tài liệu .md, duyệt câu hỏi AI, lập lịch ca thi, theo dõi ghi danh"),
               ("AuthController", "`POST /api/auth/admin/create-user` — kiểm tra trùng email/username"),
               ("AdminCourseController", "Tạo Course và gán giảng viên phụ trách"),
               ("DocumentsController", "Upload .md hợp lệ, sinh câu hỏi AI"),
               ("QuestionBankController", "Duyệt / từ chối câu hỏi AI (PENDING_REVIEW → APPROVED)"),
               ("ExamSessionsController", "Tạo ca thi (SessionCode ESS-XXXXXXXX), mở ca (IN_PROGRESS), monitor"),
               ("AI Service (FastAPI)", "`/ai/questions/generate` — sinh bộ câu hỏi từ tài liệu"),
               ("PostgreSQL (Neon)", "Users, Courses, MarkdownDocuments, QuestionBanks, ExamSessions, Enrollment")]),
    dict(slug="seq-01-viva-flow_en", lang="en",
         title="SEQ 1-EN – Viva Exam Flow",
         subtitle="Member 5 · Sequence Diagram · English version",
         image="SEQ_01_VIVA_FLOW_EN.svg",
         puml="seq_01_viva_flow_en.puml",
         hint="**Three phases**: start the session → record & submit the answer (STT + follow-up generation) → finish and queue for grading.",
         summary=(
             "The diagram covers the full life of one Viva attempt: opening the room, recording and submitting "
             "each answer, finishing the session, and queueing the submission for AI grading.\n\n"
             "Key points shown: the audio file is uploaded through the **chunked path** "
             "(`/api/audio/chunked/init` → `chunk` → `complete`) for long recordings or a weak link; "
             "the Backend calls the AI Service for **STT** and **follow-up generation**, and if the AI fails "
             "the system **falls back to TextAnswer** instead of losing the submission. Each answer is stored "
             "with `Status = PROCESSING`, while the whole session moves to `PENDING_REVIEW` when the student finishes."),
         table_title="Participants & responsibilities",
         thead="| Participant | Responsibility |",
         rows=[("Student", "Starts the session, records, submits the answer, finishes the exam"),
               ("Web SPA (VivaRoomPage)", "Records WAV/webm, chunked upload, renders questions & feedback"),
               ("VivaSessionController", "Accepts requests, enforces the STUDENT role, returns 400/403/404/409"),
               ("ChunkedAudioController", "Init / chunk / complete / abort — chunked upload of long recordings"),
               ("VivaSessionService", "Business logic: STT, follow-up, persists Answer, updates Enrollment"),
               ("Cloudflare R2", "Stores the audio file, returns a presigned URL"),
               ("AI Service (FastAPI)", "`/ai/speech/transcribe-object` and `/ai/interview/follow-up`"),
               ("PostgreSQL (Neon)", "Session, Enrollment, ExamQuestions, Answer, Result, AuditLog")]),
    dict(slug="seq-02-grading_en", lang="en",
         title="SEQ 2-EN – AI Grading",
         subtitle="Member 5 · Sequence Diagram · English version",
         image="SEQ_02_GRADING_EN.svg",
         puml="seq_02_grading_en.puml",
         hint="The **Hangfire worker** holds one system-wide distributed lock, so exams are graded one at a time. The queue lives in the PostgreSQL schema `hangfire`; retryable errors back off 30s → 120s → 300s (max 3 attempts).",
         summary=(
             "The diagram shows the automatic grading flow that runs **outside the HTTP request**, inside the "
             "Hangfire worker.\n\n"
             "Design highlight: `SessionGradingLock` holds **one system-wide distributed lock** "
             "(`aives:session-grading`, 12-hour timeout) so submissions are graded **one at a time**, "
             "avoiding a burst of concurrent LLM calls. The system gathers the full context "
             "(Result + Answers + Rubric + Documents) and issues **a single** `/ai/evaluate/session` call "
             "for the whole submission.\n\n"
             "Strict validation: the number of AI analyses returned must **exactly match** the number of "
             "submitted answers, otherwise `question_ids_mismatch` fails the job immediately with no retry. "
             "Retryable errors (timeout, rate limit) are retried by Hangfire with a 30s → 120s → 300s backoff, "
             "up to 3 attempts; non-retryable errors set `Answer.Status = ERROR`, `Result = FAILED` and write "
             "an audit log."),
         table_title="Participants & responsibilities",
         thead="| Participant | Responsibility |",
         rows=[("Hangfire Worker", "The `grading` queue, one worker — grades one exam at a time"),
               ("SessionGradingLock", "Distributed lock `aives:session-grading`, 12-hour timeout"),
               ("SessionGradingJob", "Resolves the attempt (1..3), classifies retryable / non-retryable errors"),
               ("SessionGradingService", "Collects the context, calls the AI, scales the rubric score, persists AiScore"),
               ("AI Service (FastAPI)", "`/ai/evaluate/session` — grades against each rubric criterion"),
               ("LLM Provider", "Gemini / OpenAI — produces the per-criterion rubric analysis"),
               ("PostgreSQL (Neon)", "Result, Answers, Rubric, Documents, AiAnalysis, AuditLog")]),
    dict(slug="seq-03-review-publish_en", lang="en",
         title="SEQ 3-EN – Review, Publish & Result View",
         subtitle="Member 5 · Sequence Diagram · English version",
         image="SEQ_03_REVIEW_PUBLISH_EN.svg",
         puml="seq_03_review_publish_en.puml",
         hint="A student **only sees the score once `Status = PUBLISHED`** — before that the API returns 404. The AI score is never overwritten by the official score.",
         summary=(
             "The diagram covers the result lifecycle from the moment AI grading finishes until the student "
             "sees the official score.\n\n"
             "The `Result` lifecycle is **`PENDING_REVIEW` → `REVIEWED` → `PUBLISHED`**. The examiner replays "
             "the evidence through a Cloudflare R2 presigned URL, enters the official score, and only then publishes.\n\n"
             "Two business rules are made explicit:\n"
             "- **BR-15**: a review only writes `final_score` + `teacher_comment` and **never overwrites** "
             "`ai_score` / `ai_feedback` — the AI score and the human score stay separate for reconciliation.\n"
             "- **BR-11**: a student can only read the score when `Status = PUBLISHED`; before that the API "
             "returns 404 so nothing leaks.\n\n"
             "The closing section covers incident handling: **re-grading one answer** (`RetryAnswerJob`) and "
             "**granting a retake** — the old submission moves to `CANCELLED` but its history is preserved."),
         table_title="Participants & responsibilities",
         thead="| Participant | Responsibility |",
         rows=[("Examiner", "Opens the pending list, listens to the audio, grades officially, publishes"),
               ("ResultsController", "`GET /api/results`, `PUT /{id}/review`, `POST /{id}/publish`"),
               ("ResultService", "Validates the state, writes FinalScore / TeacherNotes, moves to PUBLISHED"),
               ("AnswerAudioController", "Issues a presigned URL to replay the evidence"),
               ("Cloudflare R2", "Serves the audio file back through the presigned URL"),
               ("Student", "Views the official score after publication; takes the retake"),
               ("PostgreSQL (Neon)", "Result (PENDING_REVIEW → REVIEWED → PUBLISHED), Answers, AuditLog")]),
    dict(slug="seq-04-admin-prep_en", lang="en",
         title="SEQ 4-EN – Preparation & Exam Scheduling",
         subtitle="Member 5 · Sequence Diagram · English version",
         image="SEQ_04_ADMIN_PREP_EN.svg",
         puml="seq_04_admin_prep_en.puml",
         hint="Preparation chain: **create account → create course → upload .md → AI generates questions → approve → schedule session → enrol**.",
         summary=(
             "The diagram describes the business preparation chain before an exam session, with the split "
             "between Admin and Staff made explicit.\n\n"
             "The key quality gates: the **.md material must be valid** (not empty, right size, right format) "
             "before it is stored; and **every AI-generated question starts as `PENDING_REVIEW`** and must be "
             "approved by staff before it can be used to build a paper — the AI never places a question in an "
             "exam on its own.\n\n"
             "When the session is created the system validates the **slot for overlap / outside the exam "
             "period** and generates a `SessionCode` in the `ESS-XXXXXXXX` format; the teacher then opens the "
             "session by moving it to `IN_PROGRESS` and tracks enrolment through the monitor endpoint."),
         table_title="Participants & responsibilities",
         thead="| Participant | Responsibility |",
         rows=[("Admin", "Creates accounts & roles, creates the course, allocates the teacher"),
               ("Staff", "Uploads the .md material, approves AI questions, schedules the session, monitors enrolment"),
               ("AuthController", "`POST /api/auth/admin/create-user` — duplicate email/username check"),
               ("AdminCourseController", "Creates the Course and assigns the responsible teacher"),
               ("DocumentsController", "Validates the uploaded .md, triggers AI question generation"),
               ("QuestionBankController", "Approves / rejects AI questions (PENDING_REVIEW → APPROVED)"),
               ("ExamSessionsController", "Creates the session (SessionCode ESS-XXXXXXXX), opens it (IN_PROGRESS), monitors"),
               ("AI Service (FastAPI)", "`/ai/questions/generate` — generates the question set from the material"),
               ("PostgreSQL (Neon)", "Users, Courses, MarkdownDocuments, QuestionBanks, ExamSessions, Enrollment")]),
]

TPL = """# {title}

> {subtitle}

{hint}

![{title}](./{image})

## Tóm tắt luồng

{summary}

## {table_title}

{thead}
{rows}

---

* Nguồn PlantUML: [`{puml}`](./{puml}) · SVG: [`{image}`](./{image}) · PNG: [`{image_png}`](./{image_png})*
* [Danh mục Sequence Diagram](./sequence-index{lang_suffix}.md) · [Tất cả trên 1 trang](../all-diagrams{all_suffix}.md)
"""

TPL_EN = """# {title}

> {subtitle}

{hint}

![{title}](./{image})

## Flow summary

{summary}

## {table_title}

{thead}
{rows}

---

* PlantUML source: [`{puml}`](./{puml}) · SVG: [`{image}`](./{image}) · PNG: [`{image_png}`](./{image_png})*
* [Sequence Diagram index](./sequence-index{lang_suffix}.md) · [All on one page](../all-diagrams{all_suffix}.md)
"""


def build():
    for d in DIAGRAMS:
        rows = "\n".join(f"| {a} | {b} |" for a, b in d["rows"])
        tpl = TPL if d["lang"] == "vi" else TPL_EN
        md = tpl.format(title=d["title"], subtitle=d["subtitle"], hint=d["hint"],
                        image=d["image"], image_png=d["image"].replace(".svg", ".png"),
                        summary=d["summary"], table_title=d["table_title"],
                        thead=d["thead"], rows=rows, puml=d["puml"],
                        lang_suffix="" if d["lang"] == "vi" else "-en",
                        all_suffix="" if d["lang"] == "vi" else "-en")
        (OUT / f"{d['slug']}.md").write_text(md, encoding="utf-8")
        print("wrote", d["slug"] + ".md")


if __name__ == "__main__":
    build()
