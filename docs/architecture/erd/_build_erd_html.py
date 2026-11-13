#!/usr/bin/env python3
"""Build the HTML + Markdown pages for the AIVES ERD suite (VI + EN).

Usage:
    python docs/architecture/erd/_build_erd_html.py
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
  body {{ margin:0; font-family: "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
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
  ul {{ font-size:14px; line-height:1.7; }}
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
  <a href="./erd-index{lang_suffix}.html">Index</a>
  <a href="./erd-index{lang_suffix}.md">Markdown</a>
  <a href="./all-erd{lang_suffix}.html">&#128202; All ERDs on one page</a>
  <a href="../sequence/sequence-index{lang_suffix}.html">&#128260; Sequence set</a>
</nav>
<main>
  <div class="card">
    <h2>{diagram_title}</h2>
    <img class="diag" src="./{image_svg}" alt="{diagram_title}">
    <p><a href="./{image_png}" download>Download PNG</a> &middot; <a href="./{image_svg}" download>Download SVG</a> &middot; <a href="./{puml}" target="_blank">PlantUML source</a></p>
  </div>
  <div class="card" style="margin-top:20px;">
    <h2>{section_title}</h2>
    <ul>
{items}
    </ul>
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
<footer>AIVES &ndash; Member 5 deliverable &middot; ERD suite &middot; generated page</footer>
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
  .grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(340px,1fr)); gap:18px; }}
  .card {{ background:#fff; border:1px solid #dfe5ea; border-radius:8px; padding:18px; display:flex; flex-direction:column; }}
  .card h2 {{ margin:0 0 6px; font-size:17px; color:#0f3d5c; }}
  .card .meta {{ font-size:12.5px; color:#5b6b7a; margin-bottom:12px; }}
  .card .thumb {{ width:100%; height:200px; overflow:hidden; border:1px solid #eceff2; border-radius:6px;
                   background:#fbfcfd; display:flex; align-items:flex-start; justify-content:center; }}
  .card .thumb img {{ width:100%; height:200px; object-fit:contain; display:block; }}
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
  <a href="./erd-index{lang_suffix}.html">{switch_label}</a>
  <a href="./erd-index{lang_suffix}.md">Markdown index</a>
  <a href="./all-erd{lang_suffix}.html">&#128202; All ERDs on one page</a>
  <a href="../sequence/sequence-index{lang_suffix}.html">&#128260; Sequence set</a>
</div>
<main>
  <h3 class="sec">{sec_diagrams}</h3>
  <div class="grid">
{cards}
  </div>
  <h3 class="sec">{sec_levels}</h3>
  <table>
    <thead><tr><th>{th_level}</th><th>{th_scope}</th><th>{th_answer}</th></tr></thead>
    <tbody>
{levels}
    </tbody>
  </table>
</main>
<footer>AIVES &ndash; Member 5 deliverable &middot; ERD suite</footer>
</body>
</html>
"""

# ---------------------------------------------------------------- data

DIAGRAMS = [
    dict(slug="erd-01-conceptual", lang="vi", level="Conceptual",
         title="ERD 1 &ndash; Sơ đồ ERD khái niệm",
         subtitle="Thành viên 5 · ERD · Bản tiếng Việt",
         diagram_title="ERD Mức Khái Niệm (Conceptual ERD)",
         section_title="Đặc điểm của mức khái niệm",
         items=[
             "Chỉ mô tả <b>thực thể nghiệp vụ</b> và <b>quan hệ</b> giữa chúng &ndash; không thuộc tính, không khóa, không kiểu dữ liệu.",
             "Phù hợp để thảo luận với người nghiệp vụ (giảng viên, trưởng khoa) và giải thích phạm vi hệ thống.",
             "4 nhóm nghiệp vụ: <b>Danh mục &amp; nội dung</b>, <b>Tổ chức thi</b>, <b>Thi &amp; chấm điểm</b>, <b>Ghi danh &amp; kiểm toán</b>.",
             "<b>20 thực thể</b> nghiệp vụ. Ba bảng nối nhiều–nhiều (<code>session_exams</code>, <code>exam_slot_template_exams</code>, <code>session_slot_exams</code>) được gộp thành một thực thể khái niệm <b>Exam Assignment</b> để giữ sơ đồ dễ đọc.",
         ],
         table_title="Các thực thể nghiệp vụ chính",
         thead="<th>Thực thể</th><th>Ý nghĩa nghiệp vụ</th><th>Thuộc nhóm</th>",
         rows=[
             ("<b>User</b>", "Tài khoản người dùng với vai trò ADMIN / STAFF / EXAMINER / STUDENT", "Danh mục &amp; nội dung"),
             ("<b>Course</b>", "Môn học do giảng viên phụ trách, giảng viên tạo", "Danh mục &amp; nội dung"),
             ("<b>Document</b>", "Tài liệu Markdown (nội dung bài học hoặc learning outcome) nạp lên", "Danh mục &amp; nội dung"),
             ("<b>Question</b>", "Câu hỏi trong ngân hàng câu hỏi, có trạng thái duyệt", "Danh mục &amp; nội dung"),
             ("<b>Rubric</b>", "Tiêu chí chấm điểm của một câu hỏi", "Danh mục &amp; nội dung"),
             ("<b>Exam Paper</b>", "Đề thi chọn từ ngân hàng câu hỏi", "Danh mục &amp; nội dung"),
             ("<b>Exam Question</b>", "Câu hỏi được chọn vào một đề thi kèm điểm", "Danh mục &amp; nội dung"),
             ("<b>Period</b>", "Kỳ thi thuộc một môn học", "Tổ chức thi"),
             ("<b>Session</b>", "Ca thi trong một kỳ thi, gắn mã ghi danh và sức chứa", "Tổ chức thi"),
             ("<b>Slot Template</b>", "Mẫu chia kíp thi theo thời lượng và sức chứa", "Tổ chức thi"),
             ("<b>Session Slot</b>", "Kíp thi cụ thể sinh ra trong một ca thi", "Tổ chức thi"),
             ("<b>Exam Assignment</b>", "Phân công đề thi cho ca thi / kíp thi (gộp 3 bảng nối nhiều–nhiều)", "Tổ chức thi"),
             ("<b>Registration</b>", "Học sinh ghi danh vào ca thi và được phân công kíp / đề", "Tổ chức thi"),
             ("<b>Result</b>", "Bài làm của học sinh trong một ca thi", "Thi &amp; chấm điểm"),
             ("<b>Answer</b>", "Câu trả lời (văn bản hoặc giọng nói) cho một câu hỏi", "Thi &amp; chấm điểm"),
             ("<b>AI Evaluation</b>", "Kết quả chấm gợi ý của AI (điểm, nhận xét, phân tích)", "Thi &amp; chấm điểm"),
             ("<b>Audio Upload</b>", "Tệp âm thanh bài làm, lưu trên Cloudflare R2", "Thi &amp; chấm điểm"),
             ("<b>Retake</b>", "Yêu cầu thi lại khi xảy ra sự cố", "Thi &amp; chấm điểm"),
             ("<b>Course Enrollment</b>", "Học sinh ghi danh học môn", "Ghi danh &amp; kiểm toán"),
             ("<b>Audit Log</b>", "Nhật ký kiểm toán mọi thao tác nhạy cảm", "Ghi danh &amp; kiểm toán"),
         ]),
    dict(slug="erd-02-logical", lang="vi", level="Logical",
         title="ERD 2 &ndash; Sơ đồ ERD logic",
         subtitle="Thành viên 5 · ERD · Bản tiếng Việt",
         diagram_title="ERD Mức Logic (Logical ERD)",
         section_title="Đặc điểm của mức logic",
         items=[
             "Mô tả <b>quan hệ đã chuẩn hóa 3NF</b> với đầy đủ thuộc tính, khóa chính (PK) và khóa ngoại (FK).",
             "Kiểu dữ liệu ở mức <b>trừu tượng</b> (Integer, String, DateTime, Boolean, Text, Decimal) &ndash; không gắn với DBMS cụ thể.",
             "Bảng trung gian cho quan hệ nhiều–nhiều: <code>exam_questions</code>, <code>session_exams</code>, <code>session_slot_exams</code>, <code>exam_slot_template_exams</code>, <code>enrollments</code>, <code>session_enrollments</code>.",
             "Ghi rõ các ràng buộc nghiệp vụ: UNIQUE, CHECK, quy tắc xóa dữ liệu và concurrency token.",
         ],
         table_title="Các quan hệ chính và ràng buộc",
         thead="<th>Quan hệ</th><th>Ý nghĩa</th><th>Ràng buộc chính</th>",
         rows=[
             ("<b>USERS</b>", "Tài khoản người dùng &ndash; vai trò hệ thống", "PK <code>user_id</code> · UQ <code>username</code>, <code>email</code>"),
             ("<b>COURSES</b>", "Môn học", "PK <code>course_id</code> · UQ <code>course_code</code> · FK giảng viên, người tạo"),
             ("<b>ENROLLMENTS</b>", "Ghi danh học môn", "UQ <code>(student_id, course_id)</code>"),
             ("<b>MARKDOWN_DOCUMENTS</b>", "Tài liệu nội dung / learning outcome", "FK <code>course_id</code>, <code>uploaded_by</code>"),
             ("<b>QUESTION_BANK</b>", "Ngân hàng câu hỏi", "UQ trạng thái duyệt · FK tài liệu nguồn (SET NULL)"),
             ("<b>QUESTION_RUBRIC_CRITERIA</b>", "Tiêu chí chấm của câu hỏi", "CHECK <code>0 &lt; max_score &le; 100</code>, <code>0 &le; position &lt; 20</code>"),
             ("<b>EXAMS</b> / <b>EXAM_QUESTIONS</b>", "Đề thi và các câu hỏi được chọn", "PK gộp <code>(exam_id, question_id)</code>"),
             ("<b>EXAM_PERIODS</b> / <b>EXAM_SESSIONS</b>", "Kỳ thi và ca thi", "UQ <code>session_code</code> · theo dõi sức chứa"),
             ("<b>EXAM_SLOT_TEMPLATES</b> / <b>SESSION_SLOTS</b>", "Mẫu kíp thi và kíp thi thực tế", "UQ <code>(session_id, slot_index)</code>, <code>slot_code</code>"),
             ("<b>SESSION_ENROLLMENTS</b>", "Ghi danh ca thi &amp; phân công đề/kíp", "UQ <code>(student_id, session_id)</code> · concurrency token"),
             ("<b>RESULTS</b>", "Kết quả bài làm của học sinh", "UQ <code>(student_id, session_id)</code> · concurrency token <code>status</code>"),
             ("<b>ANSWERS</b>", "Câu trả lời của học sinh", "UQ <code>(result_id, question_id)</code>"),
             ("<b>MEDIA_UPLOADS</b>", "Phiên upload âm thanh theo chunk", "PK <code>upload_id</code> · jsonb chunk indexes"),
             ("<b>EXAM_RETAKES</b>", "Quyền thi lại", "CHECK <code>1 &le; duration_minutes &le; 240</code> · UQ result/session"),
             ("<b>AUDIT_LOGS</b>", "Nhật ký kiểm toán", "jsonb old/new value · FK người thực hiện (SET NULL)"),
         ]),
    dict(slug="erd-03-physical", lang="vi", level="Physical",
         title="ERD 3 &ndash; Sơ đồ ERD vật lý",
         subtitle="Thành viên 5 · ERD · Bản tiếng Việt",
         diagram_title="ERD Mức Vật Lý (Physical ERD) &ndash; PostgreSQL / Neon",
         section_title="Đặc điểm của mức vật lý",
         items=[
             "Ánh xạ <b>1:1</b> với schema thật trên Neon: <b>21 bảng</b> trong schema <code>public</code>.",
             "Hiển thị <b>kiểu dữ liệu PostgreSQL cụ thể</b>: <code>integer</code>, <code>bigint</code>, <code>varchar(n)</code>, <code>text</code>, <code>jsonb</code>, <code>text[]</code>, <code>real</code>, <code>numeric(8,4)</code>, <code>timestamptz</code> và các enum do ứng dụng định nghĩa.",
             "Ghi rõ <b>PK</b>, <b>UNIQUE</b>, <b>FK</b> kèm <b>quy tắc ON DELETE</b> (CASCADE / SET NULL / RESTRICT) và các <b>CHECK constraint</b>.",
             "Bảng được nhóm theo package theo tên bảng thật (<code>public.users</code>, <code>public.results</code>, &hellip;).",
         ],
         table_title="Bảng vật lý và đặc điểm kỹ thuật",
         thead="<th>Bảng</th><th>Số cột</th><th>Khóa chính</th><th>Đặc điểm đáng chú ý</th>",
         rows=[
             ("<code>users</code>", "7", "<code>user_id</code> serial", "Enum <code>user_role</code> · UQ username/email"),
             ("<code>courses</code>", "6", "<code>course_id</code> serial", "UQ <code>course_code</code> · FK SET NULL"),
             ("<code>enrollments</code>", "4", "<code>enrollment_id</code> serial", "UQ <code>(student_id, course_id)</code>"),
             ("<code>markdown_documents</code>", "7", "<code>document_id</code> serial", "Enum <code>doc_type</code> · nội dung <code>text</code>"),
             ("<code>question_bank</code>", "17", "<code>question_id</code> serial", "<code>expected_keywords text[]</code> · <code>approval_status</code>"),
             ("<code>question_rubric_criteria</code>", "5", "<code>criterion_id</code> serial", "2 CHECK constraint về điểm và thứ tự"),
             ("<code>exams</code> / <code>exam_questions</code>", "9 / 3", "<code>exam_id</code> · PK gộp", "Enum <code>exam_status</code> · soft delete <code>is_active</code>"),
             ("<code>exam_periods</code>", "9", "<code>period_id</code> serial", "Enum <code>exam_period_status</code>"),
             ("<code>exam_sessions</code>", "14", "<code>session_id</code> serial", "Enum <code>session_status</code> · UQ <code>session_code</code>"),
             ("<code>session_exams</code> / <code>session_slot_exams</code>", "3 / 3", "PK gộp 2 cột", "Bảng nối nhiều–nhiều"),
             ("<code>exam_slot_templates</code> / <code>exam_slot_template_exams</code>", "7 / 3", "<code>slot_template_id</code> · PK gộp", "FK <code>exam_paper_id</code> RESTRICT"),
             ("<code>session_slots</code>", "11", "<code>slot_id</code> serial", "UQ <code>(session_id, slot_index)</code>, <code>slot_code</code>"),
             ("<code>session_enrollments</code>", "11", "<code>session_enrollment_id</code> serial", "Concurrency token <code>status</code>, <code>started_at</code>"),
             ("<code>results</code>", "19", "<code>result_id</code> serial", "<code>ai_analysis jsonb</code> · concurrency token <code>status</code>"),
             ("<code>answers</code>", "21", "<code>answer_id</code> serial", "<code>ai_analysis jsonb</code> · <code>attempt_count</code> cho retry"),
             ("<code>media_uploads</code>", "21", "<code>upload_id varchar(64)</code>", "<code>received_chunk_indexes jsonb</code> · local-first + R2"),
             ("<code>exam_retakes</code>", "10", "<code>retake_id</code> serial", "CHECK duration · UQ result/session · concurrency <code>revision</code>"),
             ("<code>audit_logs</code>", "11", "<code>log_id bigint</code> identity", "<code>old_value/new_value jsonb</code>"),
         ]),
    dict(slug="erd-01-conceptual_en", lang="en", level="Conceptual",
         title="ERD 1 &ndash; Conceptual ERD",
         subtitle="Member 5 · ERD · English version",
         diagram_title="Conceptual ERD",
         section_title="What the conceptual level captures",
         items=[
             "Describes only <b>business entities</b> and their <b>relationships</b> &ndash; no attributes, no keys, no data types.",
             "Meant for business stakeholders (lecturers, department head) to agree on system scope.",
             "Four business areas: <b>Catalog &amp; Content</b>, <b>Exam Organisation</b>, <b>Taking &amp; Grading</b>, <b>Enrolment &amp; Audit</b>.",
             "<b>20 business entities</b>. The three many-to-many junction tables (<code>session_exams</code>, <code>exam_slot_template_exams</code>, <code>session_slot_exams</code>) are folded into a single conceptual entity, <b>Exam Assignment</b>, to keep the diagram readable.",
         ],
         table_title="Main business entities",
         thead="<th>Entity</th><th>Business meaning</th><th>Area</th>",
         rows=[
             ("<b>User</b>", "Account with role ADMIN / STAFF / EXAMINER / STUDENT", "Catalog &amp; Content"),
             ("<b>Course</b>", "Course owned by a teacher, created by staff", "Catalog &amp; Content"),
             ("<b>Document</b>", "Uploaded Markdown content (lesson or learning outcome)", "Catalog &amp; Content"),
             ("<b>Question</b>", "Question in the question bank, with approval status", "Catalog &amp; Content"),
             ("<b>Rubric</b>", "Scoring criterion of a question", "Catalog &amp; Content"),
             ("<b>Exam Paper</b>", "Exam paper assembled from the question bank", "Catalog &amp; Content"),
             ("<b>Exam Question</b>", "Question selected into an exam paper, with its score", "Catalog &amp; Content"),
             ("<b>Period</b>", "Exam period belonging to a course", "Exam Organisation"),
             ("<b>Session</b>", "Session inside a period, with join code and capacity", "Exam Organisation"),
             ("<b>Slot Template</b>", "Template splitting a paper into timed, capacity-limited slots", "Exam Organisation"),
             ("<b>Session Slot</b>", "Concrete time slot instantiated inside a session", "Exam Organisation"),
             ("<b>Exam Assignment</b>", "Assignment of an exam paper to a session / slot (folds the three junction tables)", "Exam Organisation"),
             ("<b>Registration</b>", "Student registers for a session and gets a slot / paper", "Exam Organisation"),
             ("<b>Result</b>", "Candidate submission within a session", "Taking &amp; Grading"),
             ("<b>Answer</b>", "Answer (text or speech) to one question", "Taking &amp; Grading"),
             ("<b>AI Evaluation</b>", "AI suggested score, feedback and analysis", "Taking &amp; Grading"),
             ("<b>Audio Upload</b>", "Audio file of the submission, stored on Cloudflare R2", "Taking &amp; Grading"),
             ("<b>Retake</b>", "Request for a new attempt after an incident", "Taking &amp; Grading"),
             ("<b>Course Enrollment</b>", "Student enrols in a course", "Enrolment &amp; Audit"),
             ("<b>Audit Log</b>", "Audit trail of every sensitive action", "Enrolment &amp; Audit"),
         ]),
    dict(slug="erd-02-logical_en", lang="en", level="Logical",
         title="ERD 2 &ndash; Logical ERD",
         subtitle="Member 5 · ERD · English version",
         diagram_title="Logical ERD",
         section_title="What the logical level captures",
         items=[
             "Describes <b>3NF-normalised relations</b> with full attributes, primary keys (PK) and foreign keys (FK).",
             "Data types stay <b>abstract</b> (Integer, String, DateTime, Boolean, Text, Decimal) &ndash; independent of any DBMS.",
             "Junction tables for many-to-many links: <code>exam_questions</code>, <code>session_exams</code>, <code>session_slot_exams</code>, <code>exam_slot_template_exams</code>, <code>enrollments</code>, <code>session_enrollments</code>.",
             "Business rules are explicit: UNIQUE, CHECK, delete behaviour and concurrency tokens.",
         ],
         table_title="Main relations and constraints",
         thead="<th>Relation</th><th>Meaning</th><th>Key constraints</th>",
         rows=[
             ("<b>USERS</b>", "User accounts and system roles", "PK <code>user_id</code> · UQ <code>username</code>, <code>email</code>"),
             ("<b>COURSES</b>", "Courses", "PK <code>course_id</code> · UQ <code>course_code</code> · FK teacher, creator"),
             ("<b>ENROLLMENTS</b>", "Course enrolment", "UQ <code>(student_id, course_id)</code>"),
             ("<b>MARKDOWN_DOCUMENTS</b>", "Content / learning outcome documents", "FK <code>course_id</code>, <code>uploaded_by</code>"),
             ("<b>QUESTION_BANK</b>", "Question bank", "Approval status · FK source document (SET NULL)"),
             ("<b>QUESTION_RUBRIC_CRITERIA</b>", "Rubric criteria of a question", "CHECK <code>0 &lt; max_score &le; 100</code>, <code>0 &le; position &lt; 20</code>"),
             ("<b>EXAMS</b> / <b>EXAM_QUESTIONS</b>", "Exam papers and their selected questions", "Composite PK <code>(exam_id, question_id)</code>"),
             ("<b>EXAM_PERIODS</b> / <b>EXAM_SESSIONS</b>", "Exam periods and sessions", "UQ <code>session_code</code> · capacity tracking"),
             ("<b>EXAM_SLOT_TEMPLATES</b> / <b>SESSION_SLOTS</b>", "Slot templates and concrete slots", "UQ <code>(session_id, slot_index)</code>, <code>slot_code</code>"),
             ("<b>SESSION_ENROLLMENTS</b>", "Session registration and paper/slot assignment", "UQ <code>(student_id, session_id)</code> · concurrency token"),
             ("<b>RESULTS</b>", "Result of a candidate in a session", "UQ <code>(student_id, session_id)</code> · concurrency token <code>status</code>"),
             ("<b>ANSWERS</b>", "Answers of the candidate", "UQ <code>(result_id, question_id)</code>"),
             ("<b>MEDIA_UPLOADS</b>", "Chunked audio upload sessions", "PK <code>upload_id</code> · jsonb chunk indexes"),
             ("<b>EXAM_RETAKES</b>", "Grants of a retake", "CHECK <code>1 &le; duration_minutes &le; 240</code> · UQ result/session"),
             ("<b>AUDIT_LOGS</b>", "Audit trail", "jsonb old/new value · FK performer (SET NULL)"),
         ]),
    dict(slug="erd-03-physical_en", lang="en", level="Physical",
         title="ERD 3 &ndash; Physical ERD",
         subtitle="Member 5 · ERD · English version",
         diagram_title="Physical ERD &ndash; PostgreSQL / Neon",
         section_title="What the physical level captures",
         items=[
             "Maps <b>1:1</b> to the live Neon schema: <b>21 tables</b> in schema <code>public</code>.",
             "Shows <b>concrete PostgreSQL types</b>: <code>integer</code>, <code>bigint</code>, <code>varchar(n)</code>, <code>text</code>, <code>jsonb</code>, <code>text[]</code>, <code>real</code>, <code>numeric(8,4)</code>, <code>timestamptz</code> plus the application-defined enums.",
             "Every <b>PK</b>, <b>UNIQUE</b> and <b>FK</b> carries its <b>ON DELETE</b> rule (CASCADE / SET NULL / RESTRICT) and the <b>CHECK constraints</b>.",
             "Tables are grouped in packages named after the real tables (<code>public.users</code>, <code>public.results</code>, &hellip;).",
         ],
         table_title="Physical tables and technical characteristics",
         thead="<th>Table</th><th>Columns</th><th>Primary key</th><th>Notable details</th>",
         rows=[
             ("<code>users</code>", "7", "<code>user_id</code> serial", "Enum <code>user_role</code> · UQ username/email"),
             ("<code>courses</code>", "6", "<code>course_id</code> serial", "UQ <code>course_code</code> · FK SET NULL"),
             ("<code>enrollments</code>", "4", "<code>enrollment_id</code> serial", "UQ <code>(student_id, course_id)</code>"),
             ("<code>markdown_documents</code>", "7", "<code>document_id</code> serial", "Enum <code>doc_type</code> · <code>text</code> content"),
             ("<code>question_bank</code>", "17", "<code>question_id</code> serial", "<code>expected_keywords text[]</code> · <code>approval_status</code>"),
             ("<code>question_rubric_criteria</code>", "5", "<code>criterion_id</code> serial", "2 CHECK constraints on score and position"),
             ("<code>exams</code> / <code>exam_questions</code>", "9 / 3", "<code>exam_id</code> · composite PK", "Enum <code>exam_status</code> · soft delete <code>is_active</code>"),
             ("<code>exam_periods</code>", "9", "<code>period_id</code> serial", "Enum <code>exam_period_status</code>"),
             ("<code>exam_sessions</code>", "14", "<code>session_id</code> serial", "Enum <code>session_status</code> · UQ <code>session_code</code>"),
             ("<code>session_exams</code> / <code>session_slot_exams</code>", "3 / 3", "Composite 2-column PK", "Many-to-many junction tables"),
             ("<code>exam_slot_templates</code> / <code>exam_slot_template_exams</code>", "7 / 3", "<code>slot_template_id</code> · composite PK", "FK <code>exam_paper_id</code> RESTRICT"),
             ("<code>session_slots</code>", "11", "<code>slot_id</code> serial", "UQ <code>(session_id, slot_index)</code>, <code>slot_code</code>"),
             ("<code>session_enrollments</code>", "11", "<code>session_enrollment_id</code> serial", "Concurrency tokens <code>status</code>, <code>started_at</code>"),
             ("<code>results</code>", "19", "<code>result_id</code> serial", "<code>ai_analysis jsonb</code> · concurrency token <code>status</code>"),
             ("<code>answers</code>", "21", "<code>answer_id</code> serial", "<code>ai_analysis jsonb</code> · <code>attempt_count</code> for retry"),
             ("<code>media_uploads</code>", "21", "<code>upload_id varchar(64)</code>", "<code>received_chunk_indexes jsonb</code> · local-first + R2"),
             ("<code>exam_retakes</code>", "10", "<code>retake_id</code> serial", "CHECK duration · UQ result/session · concurrency <code>revision</code>"),
             ("<code>audit_logs</code>", "11", "<code>log_id bigint</code> identity", "<code>old_value/new_value jsonb</code>"),
         ]),
]

LEVELS_VI = [
    ("<b>Conceptual</b>", "Thực thể nghiệp vụ, quan hệ giữa chúng, cardinality",
     "<i>“Hệ thống lưu những gì nào và liên quan ra sao?”</i>"),
    ("<b>Logical</b>", "Relation chuẩn hóa 3NF, thuộc tính, PK/FK, ràng buộc nghiệp vụ",
     "<i>“Cấu trúc lưu trữ được chuẩn hóa ra sao?”</i>"),
    ("<b>Physical</b>", "Kiểu dữ liệu PostgreSQL, index, constraint, quy tắc ON DELETE",
     "<i>“Schema thật trên Neon trông như thế nào?”</i>"),
]
LEVELS_EN = [
    ("<b>Conceptual</b>", "Business entities, relationships between them, cardinality",
     "<i>“Which things does the system store and how are they related?”</i>"),
    ("<b>Logical</b>", "3NF-normalised relations, attributes, PK/FK, business rules",
     "<i>“How is the stored structure normalised?”</i>"),
    ("<b>Physical</b>", "PostgreSQL data types, indexes, constraints, ON DELETE rules",
     "<i>“What does the real Neon schema look like?”</i>"),
]

MD_TPL = """# {title}

> {subtitle}

![{diagram_title}](./{image_svg})

## {section_title}

{items_md}

## {table_title}

{table_md}

---

*Generated from `docs/architecture/erd/` &middot; sources: `{puml}`*

* [Diagram index](./erd-index{lang_suffix}.md)
* [All ERDs on one page](./all-erd{lang_suffix}.md)
"""


def build_diagram_pages():
    for d in DIAGRAMS:
        ncols = len(d["thead"].split("<th>")) - 1
        rows = "\n".join(
            "        <tr>" + "".join(f"<td>{c}</td>" for c in r) + "</tr>"
            for r in d["rows"]
        )
        items = "\n".join(f"      <li>{i}</li>" for i in d["items"])
        html = TEMPLATE.format(
            lang=d["lang"], title=d["title"], subtitle=d["subtitle"],
            diagram_title=d["diagram_title"], section_title=d["section_title"],
            items=items, table_title=d["table_title"], thead=d["thead"], rows=rows,
            image_svg=d["image_svg"] if "image_svg" in d else _svg(d),
            image_png=_png(d), puml=_puml(d),
            prev=_prev(d), prev_label=_prev_label(d),
            next=_next(d), next_label=_next_label(d),
            lang_suffix="" if d["lang"] == "vi" else "-en",
        )
        (OUT / f"{d['slug']}.html").write_text(html, encoding="utf-8")
        print("wrote", d["slug"] + ".html")

        items_md = "\n".join(f"- {i}" for i in d["items"])
        table_md = "\n".join("| " + " | ".join(r) + " |" for r in d["rows"])
        md = MD_TPL.format(
            title=d["title"].replace("&ndash;", "-"), subtitle=d["subtitle"],
            diagram_title=d["diagram_title"], section_title=d["section_title"],
            items_md=items_md, table_title=d["table_title"], table_md=table_md,
            image_svg=_svg(d), puml=_puml(d),
            lang_suffix="" if d["lang"] == "vi" else "-en",
        )
        (OUT / f"{d['slug']}.md").write_text(md, encoding="utf-8")
        print("wrote", d["slug"] + ".md")


def _slug_base(d):
    return d["slug"].replace("_en", "")


def _svg(d):
    base = _slug_base(d)
    num = base.split("-")[1]
    suffix = "_EN" if d["lang"] == "en" else ""
    names = {
        "01": "ERD_01_CONCEPTUAL",
        "02": "ERD_02_LOGICAL",
        "03": "ERD_03_PHYSICAL",
    }
    return f"{names[num]}{suffix}.svg"


def _png(d):
    return _svg(d).replace(".svg", ".png")


def _puml(d):
    base = _slug_base(d)
    suffix = "_en" if d["lang"] == "en" else ""
    return f"erd_{base.split('-')[1]}_{base.split('-')[2]}{suffix}.puml"


def _prev(d):
    base = _slug_base(d)
    idx = ["01", "02", "03"].index(base.split("-")[1])
    if idx == 0:
        return "erd-index.html" if d["lang"] == "vi" else "erd-index-en.html"
    prev_num = ["01", "02", "03"][idx - 1]
    slugs = {"01": "conceptual", "02": "logical", "03": "physical"}
    s = f"erd-{prev_num}-{slugs[prev_num]}"
    return f"{s}.html" if d["lang"] == "vi" else f"{s}_en.html"


def _prev_label(d):
    base = _slug_base(d)
    idx = ["01", "02", "03"].index(base.split("-")[1])
    names = {"01": "ERD 1", "02": "ERD 2", "03": "ERD 3"}
    if idx == 0:
        return "&larr; Index"
    return f"&larr; {names[['01', '02', '03'][idx - 1]]}"


def _next(d):
    base = _slug_base(d)
    idx = ["01", "02", "03"].index(base.split("-")[1])
    if idx == 2:
        return "erd-index.html" if d["lang"] == "vi" else "erd-index-en.html"
    next_num = ["01", "02", "03"][idx + 1]
    slugs = {"01": "conceptual", "02": "logical", "03": "physical"}
    s = f"erd-{next_num}-{slugs[next_num]}"
    return f"{s}.html" if d["lang"] == "vi" else f"{s}_en.html"


def _next_label(d):
    base = _slug_base(d)
    idx = ["01", "02", "03"].index(base.split("-")[1])
    names = {"01": "ERD 1", "02": "ERD 2", "03": "ERD 3"}
    if idx == 2:
        return "Index &rarr;"
    return f"{names[['01', '02', '03'][idx + 1]]} &rarr;"


def build_index(lang):
    if lang == "vi":
        cards = [d for d in DIAGRAMS if d["lang"] == "vi"]
        title = "AIVES &ndash; Danh mục ERD (3 mức)"
        subtitle = "Thành viên 5 · Bản tiếng Việt"
        sec_diagrams = "Các sơ đồ"
        sec_levels = "Ba mức ERD và ý nghĩa"
        th_level = "Mức"
        th_scope = "Phạm vi"
        th_answer = "Câu hỏi trả lời"
        levels = LEVELS_VI
        switch = "English version &rarr;"
        lang_suffix = ""
        md_index = "erd-index.md"
        main_title = "AIVES – Danh mục ERD (3 mức)"
        main_sub = "Thành viên 5 · Bản tiếng Việt"
    else:
        cards = [d for d in DIAGRAMS if d["lang"] == "en"]
        title = "AIVES &ndash; ERD Index (3 levels)"
        subtitle = "Member 5 · English version"
        sec_diagrams = "Diagrams"
        sec_levels = "The three ERD levels"
        th_level = "Level"
        th_scope = "Scope"
        th_answer = "Question answered"
        levels = LEVELS_EN
        switch = "Bản tiếng Việt &rarr;"
        lang_suffix = "-en"
        md_index = "erd-index-en.md"
        main_title = "AIVES – ERD Index (3 levels)"
        main_sub = "Member 5 · English version"

    card_html = []
    for d in cards:
        card_html.append(f"""    <div class="card">
      <h2>{d['title']}</h2>
      <div class="meta">{d['level']} &middot; {d['subtitle']}</div>
      <div class="thumb"><img src="./{_svg(d)}" alt="{d['diagram_title']}"></div>
      <div class="links">
        <a href="./{d['slug']}.html">Open page</a>
        <a href="./{d['slug']}.md">Markdown</a>
        <a href="./{_svg(d)}">SVG</a>
      </div>
    </div>""")
    lvl_html = "\n".join(f"      <tr><td>{a}</td><td>{b}</td><td>{c}</td></tr>" for a, b, c in levels)

    html = INDEX_TPL.format(lang=lang, title=title, subtitle=subtitle,
                            switch_label=switch, lang_suffix=lang_suffix,
                            sec_diagrams=sec_diagrams, sec_levels=sec_levels,
                            th_level=th_level, th_scope=th_scope, th_answer=th_answer,
                            md_index=md_index, cards="\n".join(card_html), levels=lvl_html)
    name = "erd-index.html" if lang == "vi" else "erd-index-en.html"
    (OUT / name).write_text(html, encoding="utf-8")
    print("wrote", name)

    # Markdown index
    md_cards = []
    for d in cards:
        md_cards.append(
            f"### [{d['title']}](./{d['slug']}.md)\n\n"
            f"![{d['diagram_title']}](./{_svg(d)})\n\n"
            f"- Level: **{d['level']}**\n"
            f"- Source: [`{_puml(d)}`](./{_puml(d)})\n"
            f"- Page: [HTML](./{d['slug']}.html) · Markdown: [MD](./{d['slug']}.md)\n"
        )
    md_levels = "\n".join(f"| {a} | {b} | {c} |" for a, b, c in levels)
    md = f"""# {main_title}

> {main_sub}

## {sec_diagrams}

{chr(10).join(md_cards)}
## {sec_levels}

| {th_level} | {th_scope} | {th_answer} |
|---|---|---|
{md_levels}

---

*Generated from `docs/architecture/erd/`*

* [All ERDs on one page](./all-erd{lang_suffix}.md)
"""
    md_name = "erd-index.md" if lang == "vi" else "erd-index-en.md"
    (OUT / md_name).write_text(md, encoding="utf-8")
    print("wrote", md_name)


if __name__ == "__main__":
    build_diagram_pages()
    build_index("vi")
    build_index("en")
