#!/usr/bin/env python3
"""Generate standalone HTML pages for each sequence diagram (VI + EN separated).

Mirrors docs/architecture/_build_swimlane_html.py so the sequence set can be
presented on its own, exactly like the swimlane and ERD sets.
"""
import pathlib

OUT = pathlib.Path(__file__).parent

TEMPLATE = """<!DOCTYPE html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>
  :root {{ color-scheme: light; }}
  * {{ box-sizing: border-box; }}
  body {{ margin:0; font-family:"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
         background:#f6f7f9; color:#1f2933; }}
  header {{ background:#0f3d5c; color:#fff; padding:20px 28px; }}
  header h1 {{ margin:0 0 6px; font-size:22px; }}
  header p {{ margin:0; font-size:13px; opacity:.85; }}
  nav {{ padding:10px 28px; background:#e8eef3; font-size:13px; border-bottom:1px solid #d3dce3; }}
  nav a {{ color:#0f3d5c; margin-right:16px; text-decoration:none; font-weight:600; }}
  nav a:hover {{ text-decoration:underline; }}
  main {{ padding:24px 28px 60px; }}
  .card {{ background:#fff; border:1px solid #dfe5ea; border-radius:8px; padding:20px; overflow:auto; }}
  .card h2 {{ margin-top:0; font-size:16px; color:#0f3d5c; }}
  .diag {{ display:block; max-width:none; height:auto; }}
  table {{ border-collapse:collapse; width:100%; margin-top:8px; font-size:13.5px; }}
  th,td {{ border:1px solid #dfe5ea; padding:8px 10px; text-align:left; vertical-align:top; }}
  th {{ background:#eef3f7; color:#0f3d5c; }}
  code {{ background:#eef3f7; padding:1px 5px; border-radius:4px; font-size:12.5px; }}
  .hint {{ background:#fff8e1; border-left:4px solid #B8860B; padding:12px 16px; margin:0 0 18px;
           border-radius:0 6px 6px 0; font-size:13.5px; }}
  .zoom {{ position:fixed; inset:0; background:rgba(10,20,30,.93); display:none; z-index:99;
           align-items:center; justify-content:center; padding:20px; }}
  .zoom.on {{ display:flex; }}
  .zoom img {{ max-width:100%; max-height:100%; background:#fff; border-radius:6px; }}
  .zoom .close {{ position:absolute; top:14px; right:20px; color:#fff; font-size:32px;
                  cursor:pointer; line-height:1; }}
  footer {{ padding:20px 28px; font-size:12px; color:#5b6b7a; }}
</style>
</head>
<body>
<header>
  <h1>{title}</h1>
  <p>{subtitle}</p>
</header>
<nav>
  <a href="{prev}">{prev_label}</a>
  <a href="{next}">{next_label}</a>
  <a href="./sequence-index{lang_suffix}.html">Index</a>
  <a href="../all-diagrams{all_suffix}.html">&#128196; All on one page</a>
</nav>
<main>
  <div class="hint">{hint}</div>
  <div class="card">
    <h2>{diagram_title}</h2>
    <img class="diag" id="diag" src="./{image_svg}" alt="{diagram_title}">
    <p><a href="./{image_png}" download>Download PNG</a> &middot; <a href="./{image_svg}" download>Download SVG</a> &middot; <a href="./{puml}" target="_blank">PlantUML source</a> &middot; <a href="./{md_file}">Markdown</a></p>
  </div>
  <div class="card" style="margin-top:20px;">
    <h2>{table_title}</h2>
    <table>
      <thead><tr>{thead}</tr></thead>
      <tbody>
{rows}
      </tbody>
    </table>
  </div>
</main>
<div class="zoom" id="zoom">
  <span class="close" id="zoomClose">&times;</span>
  <img id="zoomImg" src="./{image_png}" alt="{diagram_title}">
</div>
<footer>AIVES &ndash; Member 5 deliverable &middot; sequence diagram &middot; generated page</footer>
<script>
  const z = document.getElementById('zoom');
  document.getElementById('diag').addEventListener('click', () => z.classList.add('on'));
  z.addEventListener('click', () => z.classList.remove('on'));
  document.getElementById('zoomClose').addEventListener('click', () => z.classList.remove('on'));
  document.addEventListener('keydown', e => {{ if (e.key === 'Escape') z.classList.remove('on'); }});
</script>
</body>
</html>
"""

INDEX_TPL = """<!DOCTYPE html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>
  * {{ box-sizing: border-box; }}
  body {{ margin:0; font-family:"Segoe UI", Roboto, Arial, sans-serif; background:#f6f7f9; color:#1f2933; }}
  header {{ background:#0f3d5c; color:#fff; padding:28px; }}
  header h1 {{ margin:0 0 8px; font-size:26px; }}
  header p {{ margin:0; opacity:.85; font-size:14px; }}
  .switch {{ padding:12px 28px; background:#e8eef3; font-size:14px; border-bottom:1px solid #d3dce3; }}
  .switch a {{ color:#0f3d5c; font-weight:600; text-decoration:none; margin-right:18px; }}
  main {{ padding:24px 28px 60px; max-width:1200px; }}
  .grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(320px,1fr)); gap:18px; }}
  .card {{ background:#fff; border:1px solid #dfe5ea; border-radius:8px; padding:18px; display:flex; flex-direction:column; }}
  .card h2 {{ margin:0 0 6px; font-size:17px; color:#0f3d5c; }}
  .card .meta {{ font-size:12.5px; color:#5b6b7a; margin-bottom:12px; }}
  .card .thumb {{ width:100%; height:240px; overflow:hidden; border:1px solid #eceff2; border-radius:6px;
                   background:#fbfcfd; display:flex; align-items:flex-start; justify-content:center; }}
  .card .thumb img {{ width:100%; height:240px; object-fit:contain; display:block; }}
  .card .links {{ margin-top:12px; font-size:13px; }}
  .card .links a {{ color:#0f3d5c; margin-right:12px; font-weight:600; text-decoration:none; }}
  .card .links a:hover {{ text-decoration:underline; }}
  h3.sec {{ margin:32px 0 14px; font-size:18px; color:#0f3d5c; border-bottom:2px solid #dbe3ea; padding-bottom:8px; }}
  table {{ border-collapse:collapse; width:100%; font-size:13.5px; background:#fff; }}
  th,td {{ border:1px solid #dfe5ea; padding:8px 10px; text-align:left; vertical-align:top; }}
  th {{ background:#eef3f7; color:#0f3d5c; }}
  code {{ background:#eef3f7; padding:1px 5px; border-radius:4px; font-size:12.5px; }}
  footer {{ padding:20px 28px; font-size:12px; color:#5b6b7a; }}
</style>
</head>
<body>
<header>
  <h1>{title}</h1>
  <p>{subtitle}</p>
</header>
<div class="switch">
  <a href="./sequence-index{lang_suffix}.html">{switch_label}</a>
  <a href="./sequence-index{lang_suffix}.md">Markdown index</a>
  <a href="../swimlane-index{lang_suffix}.html">&#127912; Swimlane set</a>
  <a href="../erd/erd-index{lang_suffix}.html">&#128202; ERD set</a>
  <a href="../all-diagrams{all_suffix}.html">&#128196; All on one page</a>
</div>
<main>
  <h3 class="sec">{sec_diagrams}</h3>
  <div class="grid">
{cards}
  </div>
  <h3 class="sec">{sec_actors}</h3>
  <table>
    <thead><tr><th>{th_participant}</th><th>{th_role}</th></tr></thead>
    <tbody>
{participants}
    </tbody>
  </table>
  <h3 class="sec">{sec_errors}</h3>
  <table>
    <thead><tr><th>{th_error}</th><th>{th_trigger}</th><th>{th_handling}</th></tr></thead>
    <tbody>
{errors}
    </tbody>
  </table>
</main>
<footer>AIVES &ndash; Member 5 deliverable &middot; sequence diagrams</footer>
</body>
</html>
"""

DIAGRAMS = [
    dict(slug="seq-01-viva-flow", lang="vi",
         title="SEQ 1 – Luồng thi Viva",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         hint="<strong>3 giai đoạn</strong>: bắt đầu ca thi → ghi âm & nộp câu trả lời (STT + sinh follow-up) → kết thúc & xếp hàng chấm.",
         diagram_title="Luồng thi Viva (Viva Exam Flow)",
         table_title="Các participant & trách nhiệm",
         image_svg="SEQ_01_VIVA_FLOW.svg", image_png="SEQ_01_VIVA_FLOW.png",
         puml="seq_01_viva_flow.puml", md_file="seq-01-viva-flow.md",
         prev="sequence-index.html", prev_label="← Index", next="seq-02-grading.html", next_label="SEQ 2 →",
         thead="<th>Participant</th><th>Trách nhiệm</th>",
         rows=[("Sinh viên (Student)", "Bắt đầu ca thi, ghi âm, nộp câu trả lời, kết thúc bài"),
               ("Web SPA (VivaRoomPage)", "Ghi âm WAV/webm, upload chunked, hiển thị câu hỏi & feedback"),
               ("VivaSessionController", "Nhận request, phân quyền STUDENT, trả lỗi 400/403/404/409"),
               ("ChunkedAudioController", "Init / chunk / complete / abort — upload audio dài theo phần"),
               ("VivaSessionService", "Nghiệp vụ: STT, follow-up, lưu Answer, cập nhật Enrollment"),
               ("Cloudflare R2", "Lưu file âm thanh, trả presigned URL"),
               ("AI Service (FastAPI)", "<code>/ai/speech/transcribe-object</code> và <code>/ai/interview/follow-up</code>"),
               ("PostgreSQL (Neon)", "Session, Enrollment, ExamQuestions, Answer, Result, AuditLog")]),
    dict(slug="seq-02-grading", lang="vi",
         title="SEQ 2 – Chấm điểm AI",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         hint="<strong>Hangfire worker</strong> giữ 1 khóa phân tán toàn hệ thống nên chấm lần lượt từng bài. Hàng đợi lưu trong schema <code>hangfire</code> của PostgreSQL; lỗi retryable được thử lại 30s → 120s → 300s (tối đa 3 lần).",
         diagram_title="Chấm điểm AI (AI Grading & Queue)",
         table_title="Các participant & trách nhiệm",
         image_svg="SEQ_02_GRADING.svg", image_png="SEQ_02_GRADING.png",
         puml="seq_02_grading.puml", md_file="seq-02-grading.md",
         prev="seq-01-viva-flow.html", prev_label="← SEQ 1", next="seq-03-review-publish.html", next_label="SEQ 3 →",
         thead="<th>Participant</th><th>Trách nhiệm</th>",
         rows=[("Hangfire Worker", "Queue <code>grading</code>, 1 worker — chấm lần lượt từng bài"),
               ("SessionGradingLock", "Khóa phân tán <code>aives:session-grading</code>, timeout 12 giờ"),
               ("SessionGradingJob", "Resolve attempt (1..3), phân loại lỗi retryable / non-retryable"),
               ("SessionGradingService", "Thu thập context, gọi AI, quy đổi điểm theo rubric, lưu AiScore"),
               ("AI Service (FastAPI)", "<code>/ai/evaluate/session</code> — chấm theo từng tiêu chí rubric"),
               ("LLM Provider", "Gemini / OpenAI — sinh phân tích rubric cho từng câu"),
               ("PostgreSQL (Neon)", "Result, Answers, Rubric, Documents, AiAnalysis, AuditLog")]),
    dict(slug="seq-03-review-publish", lang="vi",
         title="SEQ 3 – Review, Công bố & Xem kết quả",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         hint="Sinh viên <strong>chỉ thấy điểm khi <code>Status = PUBLISHED</code></strong> — trước đó API trả 404. Điểm AI không bao giờ bị ghi đè bởi điểm chính thức.",
         diagram_title="Review, Công bố & Xem kết quả (Review, Publish & Result View)",
         table_title="Các participant & trách nhiệm",
         image_svg="SEQ_03_REVIEW_PUBLISH.svg", image_png="SEQ_03_REVIEW_PUBLISH.png",
         puml="seq_03_review_publish.puml", md_file="seq-03-review-publish.md",
         prev="seq-02-grading.html", prev_label="← SEQ 2", next="seq-04-admin-prep.html", next_label="SEQ 4 →",
         thead="<th>Participant</th><th>Trách nhiệm</th>",
         rows=[("Khảo thí (Examiner)", "Mở danh sách pending, nghe audio, chấm điểm chính thức, publish"),
               ("ResultsController", "<code>GET /api/results</code>, <code>PUT /{id}/review</code>, <code>POST /{id}/publish</code>"),
               ("ResultService", "Kiểm tra trạng thái, ghi FinalScore / TeacherNotes, chuyển sang PUBLISHED"),
               ("AnswerAudioController", "Sinh presigned URL để nghe lại bằng chứng"),
               ("Cloudflare R2", "Phát lại file âm thanh qua presigned URL"),
               ("Sinh viên (Student)", "Xem điểm chính thức sau khi publish; xem lịch sử bài thi bù"),
               ("PostgreSQL (Neon)", "Result (PENDING_REVIEW → REVIEWED → PUBLISHED), Answers, AuditLog")]),
    dict(slug="seq-04-admin-prep", lang="vi",
         title="SEQ 4 – Chuẩn bị & Tổ chức thi",
         subtitle="Thành viên 5 · Sequence Diagram · Bản tiếng Việt",
         hint="Chuỗi chuẩn bị: <strong>tạo tài khoản → tạo môn học → upload .md → AI sinh câu hỏi → duyệt → lập lịch ca thi → ghi danh</strong>.",
         diagram_title="Chuẩn bị & Tổ chức thi (Preparation & Exam Scheduling)",
         table_title="Các participant & trách nhiệm",
         image_svg="SEQ_04_ADMIN_PREP.svg", image_png="SEQ_04_ADMIN_PREP.png",
         puml="seq_04_admin_prep.puml", md_file="seq-04-admin-prep.md",
         prev="seq-03-review-publish.html", prev_label="← SEQ 3", next="sequence-index.html", next_label="Index →",
         thead="<th>Participant</th><th>Trách nhiệm</th>",
         rows=[("Quản trị (Admin)", "Tạo tài khoản & gán vai trò, tạo môn học, phân công giảng viên"),
               ("Giang viên (Staff)", "Upload tài liệu .md, duyệt câu hỏi AI, lập lịch ca thi, theo dõi ghi danh"),
               ("AuthController", "<code>POST /api/auth/admin/create-user</code> — kiểm tra trùng email/username"),
               ("AdminCourseController", "Tạo Course và gán giảng viên phụ trách"),
               ("DocumentsController", "Upload .md hợp lệ, sinh câu hỏi AI"),
               ("QuestionBankController", "Duyệt / từ chối câu hỏi AI (PENDING_REVIEW → APPROVED)"),
               ("ExamSessionsController", "Tạo ca thi (SessionCode ESS-XXXXXXXX), mở ca (IN_PROGRESS), monitor"),
               ("AI Service (FastAPI)", "<code>/ai/questions/generate</code> — sinh bộ câu hỏi từ tài liệu"),
               ("PostgreSQL (Neon)", "Users, Courses, MarkdownDocuments, QuestionBanks, ExamSessions, Enrollment")]),
    dict(slug="seq-01-viva-flow_en", lang="en",
         title="SEQ 1-EN – Viva Exam Flow",
         subtitle="Member 5 · Sequence Diagram · English version",
         hint="<strong>Three phases</strong>: start the session → record &amp; submit the answer (STT + follow-up generation) → finish and queue for grading.",
         diagram_title="Viva Exam Flow",
         table_title="Participants & responsibilities",
         image_svg="SEQ_01_VIVA_FLOW_EN.svg", image_png="SEQ_01_VIVA_FLOW_EN.png",
         puml="seq_01_viva_flow_en.puml", md_file="seq-01-viva-flow_en.md",
         prev="sequence-index-en.html", prev_label="← Index", next="seq-02-grading_en.html", next_label="SEQ 2-EN →",
         thead="<th>Participant</th><th>Responsibility</th>",
         rows=[("Student", "Starts the session, records, submits the answer, finishes the exam"),
               ("Web SPA (VivaRoomPage)", "Records WAV/webm, chunked upload, renders questions &amp; feedback"),
               ("VivaSessionController", "Accepts requests, enforces the STUDENT role, returns 400/403/404/409"),
               ("ChunkedAudioController", "Init / chunk / complete / abort — chunked upload of long recordings"),
               ("VivaSessionService", "Business logic: STT, follow-up, persists Answer, updates Enrollment"),
               ("Cloudflare R2", "Stores the audio file, returns a presigned URL"),
               ("AI Service (FastAPI)", "<code>/ai/speech/transcribe-object</code> and <code>/ai/interview/follow-up</code>"),
               ("PostgreSQL (Neon)", "Session, Enrollment, ExamQuestions, Answer, Result, AuditLog")]),
    dict(slug="seq-02-grading_en", lang="en",
         title="SEQ 2-EN – AI Grading",
         subtitle="Member 5 · Sequence Diagram · English version",
         hint="The <strong>Hangfire worker</strong> holds one system-wide distributed lock, so exams are graded one at a time. The queue lives in the PostgreSQL schema <code>hangfire</code>; retryable errors back off 30s → 120s → 300s (max 3 attempts).",
         diagram_title="AI Grading & Queue",
         table_title="Participants & responsibilities",
         image_svg="SEQ_02_GRADING_EN.svg", image_png="SEQ_02_GRADING_EN.png",
         puml="seq_02_grading_en.puml", md_file="seq-02-grading_en.md",
         prev="seq-01-viva-flow_en.html", prev_label="← SEQ 1-EN", next="seq-03-review-publish_en.html", next_label="SEQ 3-EN →",
         thead="<th>Participant</th><th>Responsibility</th>",
         rows=[("Hangfire Worker", "The <code>grading</code> queue, one worker — grades one exam at a time"),
               ("SessionGradingLock", "Distributed lock <code>aives:session-grading</code>, 12-hour timeout"),
               ("SessionGradingJob", "Resolves the attempt (1..3), classifies retryable / non-retryable errors"),
               ("SessionGradingService", "Collects the context, calls the AI, scales the rubric score, persists AiScore"),
               ("AI Service (FastAPI)", "<code>/ai/evaluate/session</code> — grades against each rubric criterion"),
               ("LLM Provider", "Gemini / OpenAI — produces the per-criterion rubric analysis"),
               ("PostgreSQL (Neon)", "Result, Answers, Rubric, Documents, AiAnalysis, AuditLog")]),
    dict(slug="seq-03-review-publish_en", lang="en",
         title="SEQ 3-EN – Review, Publish & Result View",
         subtitle="Member 5 · Sequence Diagram · English version",
         hint="A student <strong>only sees the score once <code>Status = PUBLISHED</code></strong> — before that the API returns 404. The AI score is never overwritten by the official score.",
         diagram_title="Review, Publish & Result View",
         table_title="Participants & responsibilities",
         image_svg="SEQ_03_REVIEW_PUBLISH_EN.svg", image_png="SEQ_03_REVIEW_PUBLISH_EN.png",
         puml="seq_03_review_publish_en.puml", md_file="seq-03-review-publish_en.md",
         prev="seq-02-grading_en.html", prev_label="← SEQ 2-EN", next="seq-04-admin-prep_en.html", next_label="SEQ 4-EN →",
         thead="<th>Participant</th><th>Responsibility</th>",
         rows=[("Examiner", "Opens the pending list, listens to the audio, grades officially, publishes"),
               ("ResultsController", "<code>GET /api/results</code>, <code>PUT /{id}/review</code>, <code>POST /{id}/publish</code>"),
               ("ResultService", "Validates the state, writes FinalScore / TeacherNotes, moves to PUBLISHED"),
               ("AnswerAudioController", "Issues a presigned URL to replay the evidence"),
               ("Cloudflare R2", "Serves the audio file back through the presigned URL"),
               ("Student", "Views the official score after publication; takes the retake"),
               ("PostgreSQL (Neon)", "Result (PENDING_REVIEW → REVIEWED → PUBLISHED), Answers, AuditLog")]),
    dict(slug="seq-04-admin-prep_en", lang="en",
         title="SEQ 4-EN – Preparation & Exam Scheduling",
         subtitle="Member 5 · Sequence Diagram · English version",
         hint="Preparation chain: <strong>create account → create course → upload .md → AI generates questions → approve → schedule session → enrol</strong>.",
         diagram_title="Preparation & Exam Scheduling",
         table_title="Participants & responsibilities",
         image_svg="SEQ_04_ADMIN_PREP_EN.svg", image_png="SEQ_04_ADMIN_PREP_EN.png",
         puml="seq_04_admin_prep_en.puml", md_file="seq-04-admin-prep_en.md",
         prev="seq-03-review-publish_en.html", prev_label="← SEQ 3-EN", next="sequence-index-en.html", next_label="Index →",
         thead="<th>Participant</th><th>Responsibility</th>",
         rows=[("Admin", "Creates accounts & roles, creates the course, allocates the teacher"),
               ("Staff", "Uploads the .md material, approves AI questions, schedules the session, monitors enrolment"),
               ("AuthController", "<code>POST /api/auth/admin/create-user</code> — duplicate email/username check"),
               ("AdminCourseController", "Creates the Course and assigns the responsible teacher"),
               ("DocumentsController", "Validates the uploaded .md, triggers AI question generation"),
               ("QuestionBankController", "Approves / rejects AI questions (PENDING_REVIEW → APPROVED)"),
               ("ExamSessionsController", "Creates the session (SessionCode ESS-XXXXXXXX), opens it (IN_PROGRESS), monitors"),
               ("AI Service (FastAPI)", "<code>/ai/questions/generate</code> — generates the question set from the material"),
               ("PostgreSQL (Neon)", "Users, Courses, MarkdownDocuments, QuestionBanks, ExamSessions, Enrollment")]),
]

VI_CARDS = ["seq-01-viva-flow", "seq-02-grading", "seq-03-review-publish", "seq-04-admin-prep"]
EN_CARDS = ["seq-01-viva-flow_en", "seq-02-grading_en", "seq-03-review-publish_en", "seq-04-admin-prep_en"]

META = {
    "seq-01-viva-flow": ("SEQ 1 · Luồng thi Viva", "3 giai đoạn · 8 participant · 2271×2858"),
    "seq-02-grading": ("SEQ 2 · Chấm điểm AI", "5 giai đoạn · 7 participant · 2263×2021"),
    "seq-03-review-publish": ("SEQ 3 · Review, Công bố & Xem kết quả", "6 giai đoạn · 7 participant · 2198×3287"),
    "seq-04-admin-prep": ("SEQ 4 · Chuẩn bị & Tổ chức thi", "7 giai đoạn · 9 participant · 1966×2627"),
    "seq-01-viva-flow_en": ("SEQ 1-EN · Viva Exam Flow", "3 phases · 8 participants · 2293×2837"),
    "seq-02-grading_en": ("SEQ 2-EN · AI Grading", "5 phases · 7 participants · 2320×2021"),
    "seq-03-review-publish_en": ("SEQ 3-EN · Review, Publish & Result View", "6 phases · 7 participants · 2159×3285"),
    "seq-04-admin-prep_en": ("SEQ 4-EN · Preparation & Exam Scheduling", "7 phases · 9 participants · 1959×2606"),
}
IMAGES = {d["slug"]: d["image_svg"] for d in DIAGRAMS}

PARTICIPANTS_VI = [
    ("Sinh viên (Student)", "Tham gia thi, ghi âm, nộp bài, xem điểm sau khi công bố"),
    ("Giang vien (Staff)", "Upload tai lieu, duyet cau hoi AI, lap lich ca thi, ghi danh"),
    ("Khao thi (Examiner)", "Review bang chung, cham diem chinh thuc, cong bo, xu ly su co"),
    ("Quan tri (Admin)", "Quan ly tai khoan, mon hoc, phan cong giang vien"),
    ("Web SPA", "Giao dien phia web / Windows client"),
    ("Backend API (ASP.NET Core)", "Controller + Service + Hangfire worker + audit log"),
    ("AI Service (FastAPI)", "Sinh cau hoi, STT, sinh follow-up, cham rubric theo LLM"),
    ("Cloudflare R2", "Luu tru & phat file am thanh qua presigned URL"),
    ("PostgreSQL (Neon)", "Co so du lieu duy nhat cua he thong"),
]
PARTICIPANTS_EN = [
    ("Student", "Takes the exam, records, submits, views the score after publication"),
    ("Staff", "Uploads material, approves AI questions, schedules the session, enrols students"),
    ("Examiner", "Reviews evidence, grades officially, publishes, handles incidents"),
    ("Admin", "Manages accounts, courses, teacher allocation"),
    ("Web SPA", "The web / Windows client front end"),
    ("Backend API (ASP.NET Core)", "Controller + Service + Hangfire worker + audit log"),
    ("AI Service (FastAPI)", "Question generation, STT, follow-up generation, LLM rubric grading"),
    ("Cloudflare R2", "Stores & serves the audio files through presigned URLs"),
    ("PostgreSQL (Neon)", "The single system of record"),
]

ERRORS_VI = [
    ("Chua du dieu kien bat dau ca thi", "BR-11", "400/403 + thong bao, sinh vien thu lai"),
    ("STT that bai", "FR-AI-03", "EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR → fallback sang TextAnswer"),
    ("Sinh follow-up that bai", "FR-AI-02", "Bo qua follow-up, van luu Answer vao DB"),
    ("AI cham diem that bai (retryable)", "BR-15", "Hangfire thu lai 30s → 120s → 300s, toi da 3 lan"),
    ("AI cham diem that bai (non-retryable)", "FR-AI-02", "Answer.Status = ERROR, Result = FAILED, ghi audit log"),
    ("AI tra so phan tich lech so cau hoi", "BR-15", "question_ids_mismatch → that bai ngay, khong retry"),
    ("Cong bo khi chua review", "BR-10", "409 Conflict, khao thi review lai truoc"),
    ("Ye cau cham lai 1 cau / cap lai thi", "FR-EXA-05/06", "RetryAnswerJob hoac tao Session moi, giu lich su"),
]
ERRORS_EN = [
    ("Start preconditions not met", "BR-11", "400/403 + message, the student retries"),
    ("STT failure", "FR-AI-03", "EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR → fall back to TextAnswer"),
    ("Follow-up generation failure", "FR-AI-02", "Skip the follow-up, still persist the Answer"),
    ("AI grading failed (retryable)", "BR-15", "Hangfire retries 30s → 120s → 300s, up to 3 attempts"),
    ("AI grading failed (non-retryable)", "FR-AI-02", "Answer.Status = ERROR, Result = FAILED, audit log written"),
    ("AI returned the wrong number of analyses", "BR-15", "question_ids_mismatch → fails immediately, no retry"),
    ("Publish before review", "BR-10", "409 Conflict, the examiner reviews again first"),
    ("Re-grade one answer / grant a retake", "FR-EXA-05/06", "RetryAnswerJob or a new Session, history preserved"),
]


def build_diagram_pages():
    for d in DIAGRAMS:
        rows = "\n".join(
            f"        <tr><td><strong>{a}</strong></td><td>{b}</td></tr>"
            for a, b in d["rows"]
        )
        html = TEMPLATE.format(lang=d["lang"], title=d["title"], subtitle=d["subtitle"],
                               hint=d["hint"], diagram_title=d["diagram_title"],
                               table_title=d["table_title"],
                               image_svg=d["image_svg"], image_png=d["image_png"],
                               puml=d["puml"], md_file=d["md_file"],
                               prev=d["prev"], prev_label=d["prev_label"],
                               next=d["next"], next_label=d["next_label"],
                               thead=d["thead"], rows=rows,
                               lang_suffix="" if d["lang"] == "vi" else "-en",
                               all_suffix="" if d["lang"] == "vi" else "-en")
        (OUT / f"{d['slug']}.html").write_text(html, encoding="utf-8")
        print("wrote", d["slug"] + ".html")


def build_index(lang):
    vi = lang == "vi"
    open_label = "Mở trang" if vi else "Open page"
    if lang == "vi":
        cards = VI_CARDS
        title = "AIVES – Danh mục Sequence Diagram"
        subtitle = "Thành viên 5 · Bản tiếng Việt"
        sec_diagrams = "Các sơ đồ"
        sec_actors = "Tất cả participant trong hệ thống"
        sec_errors = "Tổng hợp các nhánh lỗi"
        th_participant, th_role = "Participant", "Vai trò"
        th_error, th_trigger, th_handling = "Nhánh lỗi", "Điều kiện", "Xử lý"
        participants = PARTICIPANTS_VI
        errors = ERRORS_VI
        switch = "English version →"
        lang_suffix = "-en"
        all_suffix = ""
        switch_en = "English version →"
    else:
        cards = EN_CARDS
        title = "AIVES – Sequence Diagram Index"
        subtitle = "Member 5 · English version"
        sec_diagrams = "Diagrams"
        sec_actors = "Every participant in the system"
        sec_errors = "Error branches summary"
        th_participant, th_role = "Participant", "Role"
        th_error, th_trigger, th_handling = "Error branch", "Condition", "Handling"
        participants = PARTICIPANTS_EN
        errors = ERRORS_EN
        switch = "Vietnamese version →"
        lang_suffix = ""
        all_suffix = "-en"
        switch_en = "Vietnamese version →"

    card_html = []
    for slug in cards:
        name, meta = META[slug]
        card_html.append(f"""    <div class="card">
      <h2>{name}</h2>
      <div class="meta">{meta}</div>
      <div class="thumb"><img src="./{IMAGES[slug]}" alt="{name}"></div>
      <div class="links">
        <a href="./{slug}.html">{open_label}</a>
        <a href="./{slug}.md">Markdown</a>
        <a href="./{IMAGES[slug]}">SVG</a>
      </div>
    </div>""")
    part_html = "\n".join(
        f"      <tr><td><strong>{a}</strong></td><td>{b}</td></tr>"
        for a, b in participants
    )
    err_html = "\n".join(
        f"      <tr><td><strong>{a}</strong></td><td><code>{b}</code></td><td>{c}</td></tr>"
        for a, b, c in errors
    )
    html = INDEX_TPL.format(lang=lang, title=title, subtitle=subtitle,
                            switch_label=switch, lang_suffix=lang_suffix,
                            all_suffix=all_suffix,
                            sec_diagrams=sec_diagrams, sec_actors=sec_actors,
                            sec_errors=sec_errors,
                            th_participant=th_participant, th_role=th_role,
                            th_error=th_error, th_trigger=th_trigger,
                            th_handling=th_handling,
                            cards="\n".join(card_html), participants=part_html,
                            errors=err_html)
    name = "sequence-index.html" if lang == "vi" else "sequence-index-en.html"
    (OUT / name).write_text(html, encoding="utf-8")
    print("wrote", name)


if __name__ == "__main__":
    build_diagram_pages()
    build_index("vi")
    build_index("en")
