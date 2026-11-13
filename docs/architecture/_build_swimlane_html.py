#!/usr/bin/env python3
"""Generate a standalone HTML page for each swimlane diagram (VI + EN separated)."""
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
  <a href="./swimlane-index{lang_suffix}.html">Index</a>
  <a href="./simplified/simplified-index{lang_suffix}.html">⚡ Simplified set</a>
  <a href="./sequence/sequence-index{lang_suffix}.html">🔄 Sequence set</a>
  <a href="./all-diagrams{lang_suffix}.html">📄 All on one page</a>
</nav>
<main>
  <div class="card">
    <h2>{diagram_title}</h2>
    <img class="diag" src="./{image_svg}" alt="{diagram_title}">
    <p><a href="./{image_png}" download>Download PNG</a> · <a href="./{image_svg}" download>Download SVG</a> · <a href="./{puml}" target="_blank">PlantUML source</a></p>
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
<footer>AIVES – Member 5 deliverable · swimlane diagram · generated page</footer>
</body>
</html>
"""

DIAGRAMS = [
    dict(slug="swimlane-01-preparation-scheduling", lang="vi",
         title="SWL-1 – Chuẩn bị & Tổ chức thi",
         subtitle="Thành viên 5 · Swimlane Diagram · Bản tiếng Việt",
         diagram_title="Chuẩn bị & Tổ chức thi (Preparation & Exam Scheduling)",
         table_title="Các lane & thông tin trách nhiệm",
         image_svg="SWIMLANE_01_PREPARATION.svg", image_png="SWIMLANE_01_PREPARATION.png",
         puml="swimlane_01_preparation_scheduling.puml",
         prev="swimlane-index.html", prev_label="← Index", next="swimlane-02-exam-grading-publish.html", next_label="SWL-2 →",
         thead="<th>Lane</th><th>Giữ thông tin / trách nhiệm</th><th>Tham chiếu SRS</th>",
         rows=[("ADMIN", "Tạo tài khoản, gán vai trò, tạo khóa học, phân công nhân sự giảng dạy", "FR-ADM-01/02, BR-03"),
               ("STAFF", "Upload nội dung <code>.md</code>, duyệt câu hỏi AI, dựng đề, lên lịch ca/kíp, ghi danh & phân phối", "FR-STF-01…07, BR-07/08/10/11"),
               ("AI Service", "Sinh câu hỏi từ Learning Objective → trạng thái <code>PENDING_REVIEW</code>", "FR-STF-03"),
               ("Backend / DB / Storage", "Lưu trữ câu hỏi, đề thi, lịch thi, ghi danh; audio trên R2", "FR-DATA-01/02")]),
    dict(slug="swimlane-02-exam-grading-publish", lang="vi",
         title="SWL-2 – Làm bài, Chấm & Công bố (đầy đủ)",
         subtitle="Thành viên 5 · Swimlane Diagram · Bản tiếng Việt",
         diagram_title="Làm bài, Chấm & Công bố (Exam Delivery, Grading & Publication)",
         table_title="Các lane & thông tin trách nhiệm",
         image_svg="SWIMLANE_02_EXAM_GRADING_PUBLISH.svg", image_png="SWIMLANE_02_EXAM_GRADING_PUBLISH.png",
         puml="swimlane_02_exam_grading_publish.puml",
         prev="swimlane-01-preparation-scheduling.html", prev_label="← SWL-1", next="swimlane-02a-exam-delivery-grading.html", next_label="SWL-2A →",
         thead="<th>Lane</th><th>Giữ thông tin / trách nhiệm</th><th>Tham chiếu SRS</th>",
         rows=[("STUDENT", "Tham gia thi, trả lời tuần tự, xem kết quả đã công bố", "UC-07, UC-11, FR-STU-01…07"),
               ("Client (Web / Windows)", "Thu âm WAV 16 kHz mono, upload chunked, hiển thị câu hỏi/follow-up, giữ bản nháp", "FR-STU-03/08, NFR-REL-02"),
               ("Backend / DB / Storage", "Kiểm tra luật nghiệp vụ, lưu Answer/Result, audio R2, audit log", "FR-DATA-01/02, BR-11/12/18"),
               ("AI Service (FastAPI)", "STT, sinh follow-up, chấm theo rubric trên văn bản", "FR-AI-01/02, BR-13"),
               ("Hangfire Worker", "Chấm cả bài theo hàng đợi, retry backoff 30s/120s/300s, khóa phân tán", "FR-STU-06, BR-15"),
               ("EXAMINER / ADMIN", "Review bằng chứng, chấm điểm chính thức, publish, retry, cấp lại thi", "UC-09/10, FR-EXA-01…06")]),
    dict(slug="swimlane-02a-exam-delivery-grading", lang="vi",
         title="SWL-2A – Làm bài & Chấm AI",
         subtitle="Thành viên 5 · Swimlane Diagram · Bản tiếng Việt",
         diagram_title="Làm bài & Chấm AI (Exam Delivery & AI Grading)",
         table_title="Các lane & thông tin trách nhiệm",
         image_svg="SWIMLANE_02A_EXAM_DELIVERY_GRADING.svg", image_png="SWIMLANE_02A_EXAM_DELIVERY_GRADING.png",
         puml="swimlane_02a_exam_delivery_grading.puml",
         prev="swimlane-02-exam-grading-publish.html", prev_label="← SWL-2", next="swimlane-02b-review-publish-incident.html", next_label="SWL-2B →",
         thead="<th>Lane</th><th>Giữ thông tin / trách nhiệm</th><th>Tham chiếu SRS</th>",
         rows=[("STUDENT", "Quyết định tham gia, trả lời tuần tự", "UC-07, FR-STU-01…06"),
               ("Client (Web / Windows)", "Ghi âm, upload chunked, render câu hỏi/follow-up, bản nháp cục bộ", "FR-STU-03/08, NFR-REL-02"),
               ("Backend / DB / Storage", "Kiểm tra điều kiện thi, lưu Answer ở trạng thái <code>PROCESSING</code>", "FR-DATA-01, BR-11/12"),
               ("AI Service (FastAPI)", "STT → transcript, sinh follow-up, chấm rubric", "FR-AI-01/02, BR-13"),
               ("Hangfire Worker", "Chấm cả bài, retry backoff, lưu <code>AiScore</code> ở <code>PENDING_REVIEW</code>", "FR-STU-06, BR-15")]),
    dict(slug="swimlane-02b-review-publish-incident", lang="vi",
         title="SWL-2B – Review, Công bố & Xử lý sự cố",
         subtitle="Thành viên 5 · Swimlane Diagram · Bản tiếng Việt",
         diagram_title="Review, Công bố & Xử lý sự cố (Review, Publication & Incident Handling)",
         table_title="Các lane & thông tin trách nhiệm",
         image_svg="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT.svg", image_png="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT.png",
         puml="swimlane_02b_review_publish_incident.puml",
         prev="swimlane-02a-exam-delivery-grading.html", prev_label="← SWL-2A", next="swimlane-index.html", next_label="Index →",
         thead="<th>Lane</th><th>Giữ thông tin / trách nhiệm</th><th>Tham chiếu SRS</th>",
         rows=[("Backend / DB / Storage", "Trạng thái Result, điểm chính thức, audit log, lịch sử bài bị vô hiệu hóa", "FR-DATA-01, BR-09/10/16/17"),
               ("EXAMINER", "Review bằng chứng, chấm điểm chính thức, publish", "UC-09, FR-EXA-01…04"),
               ("EXAMINER / ADMIN", "Xử lý sự cố: retry answer, cấp lại thi (retake)", "UC-10, FR-EXA-05/06"),
               ("STUDENT", "Xem kết quả sau khi công bố, làm bài thi bù", "UC-11, FR-STU-07, BR-18")]),
    dict(slug="swimlane-01-preparation-scheduling_en", lang="en",
         title="SWL-1-EN – Preparation & Exam Scheduling",
         subtitle="Member 5 · Swimlane Diagram · English version",
         diagram_title="Preparation & Exam Scheduling",
         table_title="Lanes & responsibility boundaries",
         image_svg="SWIMLANE_01_PREPARATION_EN.svg", image_png="SWIMLANE_01_PREPARATION_EN.png",
         puml="swimlane_01_preparation_scheduling_en.puml",
         prev="swimlane-index-en.html", prev_label="← Index", next="swimlane-02-exam-grading-publish_en.html", next_label="SWL-2-EN →",
         thead="<th>Lane</th><th>Responsibility</th><th>SRS reference</th>",
         rows=[("ADMIN", "Creates accounts, assigns roles, creates the course, allocates teaching staff", "FR-ADM-01/02, BR-03"),
               ("STAFF", "Uploads Markdown content, reviews AI questions, builds the paper, schedules periods/slots, enrolls & distributes", "FR-STF-01…07, BR-07/08/10/11"),
               ("AI Service", "Generates questions from the Learning Objective → <code>PENDING_REVIEW</code>", "FR-STF-03"),
               ("Backend / DB / Storage", "Stores questions, papers, schedules, enrollments; audio on R2", "FR-DATA-01/02")]),
    dict(slug="swimlane-02-exam-grading-publish_en", lang="en",
         title="SWL-2-EN – Exam Delivery, Grading & Publication (full)",
         subtitle="Member 5 · Swimlane Diagram · English version",
         diagram_title="Exam Delivery, Grading & Publication",
         table_title="Lanes & responsibility boundaries",
         image_svg="SWIMLANE_02_EXAM_GRADING_PUBLISH_EN.svg", image_png="SWIMLANE_02_EXAM_GRADING_PUBLISH_EN.png",
         puml="swimlane_02_exam_grading_publish_en.puml",
         prev="swimlane-01-preparation-scheduling_en.html", prev_label="← SWL-1-EN", next="swimlane-02a-exam-delivery-grading_en.html", next_label="SWL-2A-EN →",
         thead="<th>Lane</th><th>Responsibility</th><th>SRS reference</th>",
         rows=[("STUDENT", "Participates, answers sequentially, views published results", "UC-07, UC-11, FR-STU-01…07"),
               ("Client (Web / Windows)", "Audio capture WAV 16 kHz mono, chunked upload, renders questions/follow-ups, local drafts", "FR-STU-03/08, NFR-REL-02"),
               ("Backend / DB / Storage", "Business-rule checks, persists Answer/Result, R2 audio, audit log", "FR-DATA-01/02, BR-11/12/18"),
               ("AI Service (FastAPI)", "STT, follow-up generation, rubric-based grading on text", "FR-AI-01/02, BR-13"),
               ("Hangfire Worker", "Whole-session grading from a queue, backoff retry, distributed lock", "FR-STU-06, BR-15"),
               ("EXAMINER / ADMIN", "Evidence review, official grading, publication, retry, retake grant", "UC-09/10, FR-EXA-01…06")]),
    dict(slug="swimlane-02a-exam-delivery-grading_en", lang="en",
         title="SWL-2A-EN – Exam Delivery & AI Grading",
         subtitle="Member 5 · Swimlane Diagram · English version",
         diagram_title="Exam Delivery & AI Grading",
         table_title="Lanes & responsibility boundaries",
         image_svg="SWIMLANE_02A_EXAM_DELIVERY_GRADING_EN.svg", image_png="SWIMLANE_02A_EXAM_DELIVERY_GRADING_EN.png",
         puml="swimlane_02a_exam_delivery_grading_en.puml",
         prev="swimlane-02-exam-grading-publish_en.html", prev_label="← SWL-2-EN", next="swimlane-02b-review-publish-incident_en.html", next_label="SWL-2B-EN →",
         thead="<th>Lane</th><th>Responsibility</th><th>SRS reference</th>",
         rows=[("STUDENT", "Decides to participate, answers sequentially", "UC-07, FR-STU-01…06"),
               ("Client (Web / Windows)", "Audio capture, chunked upload, renders questions/follow-ups, local drafts", "FR-STU-03/08, NFR-REL-02"),
               ("Backend / DB / Storage", "Validates exam preconditions, persists Answer as <code>PROCESSING</code>", "FR-DATA-01, BR-11/12"),
               ("AI Service (FastAPI)", "STT → transcript, follow-up generation, rubric grading", "FR-AI-01/02, BR-13"),
               ("Hangfire Worker", "Grades the session, backoff retry, stores <code>AiScore</code> as <code>PENDING_REVIEW</code>", "FR-STU-06, BR-15")]),
    dict(slug="swimlane-02b-review-publish-incident_en", lang="en",
         title="SWL-2B-EN – Review, Publication & Incident Handling",
         subtitle="Member 5 · Swimlane Diagram · English version",
         diagram_title="Review, Publication & Incident Handling",
         table_title="Lanes & responsibility boundaries",
         image_svg="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT_EN.svg", image_png="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT_EN.png",
         puml="swimlane_02b_review_publish_incident_en.puml",
         prev="swimlane-02a-exam-delivery-grading_en.html", prev_label="← SWL-2A-EN", next="swimlane-index-en.html", next_label="Index →",
         thead="<th>Lane</th><th>Responsibility</th><th>SRS reference</th>",
         rows=[("Backend / DB / Storage", "Result state, official score, audit log, history of invalidated attempts", "FR-DATA-01, BR-09/10/16/17"),
               ("EXAMINER", "Evidence review, official grading, publication", "UC-09, FR-EXA-01…04"),
               ("EXAMINER / ADMIN", "Incident handling: retry answer, retake grant", "UC-10, FR-EXA-05/06"),
               ("STUDENT", "Views the result once published, takes the retake", "UC-11, FR-STU-07, BR-18")]),
]

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
  .card .thumb {{ width:100%; height:170px; overflow:hidden; border:1px solid #eceff2; border-radius:6px;
                   background:#fbfcfd; display:flex; align-items:flex-start; justify-content:center; }}
  .card .thumb img {{ width:100%; height:170px; object-fit:contain; display:block; }}
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
  <a href="./swimlane-index{lang_suffix}.html">{switch_label}</a>
  <a href="./{md_index}">Markdown index</a>
  <a href="./simplified/simplified-index{lang_suffix}.html">⚡ Simplified diagrams</a>
  <a href="./sequence/sequence-index{lang_suffix}.html">🔄 Sequence diagrams</a>
  <a href="./all-diagrams{lang_suffix}.html">📄 All on one page</a>
</div>
<main>
  <h3 class="sec">{sec_diagrams}</h3>
  <div class="grid">
{cards}
  </div>
  <h3 class="sec">{sec_errors}</h3>
  <table>
    <thead><tr><th>{th_branch}</th><th>{th_cond}</th><th>{th_handle}</th><th>{th_lane}</th></tr></thead>
    <tbody>
{errors}
    </tbody>
  </table>
</main>
<footer>AIVES – Member 5 deliverable · swimlane diagrams</footer>
</body>
</html>
"""

ERRORS_VI = [
    ("Chưa đủ điều kiện bắt đầu", "BR-11", "400/403, hiển thị thông báo, thử lại", "Client → STUDENT"),
    ("STT thất bại (nộp bài lỗi)", "FR-AI-03", "EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR, SV nhập lại hoặc text fallback", "Client → STUDENT"),
    ("Chấm AI lỗi (retryable)", "BR-15", "Hangfire retry tối đa 3 lḀn, backoff 30s/120s/300s", "Hangfire Worker"),
    ("Chấm AI lỗi (non-retryable) / hết lượt", "FR-AI-02", "FAILED + Answer.Status = ERROR + audit log", "Hangfire → Backend"),
    ("Publish khi chưa review / thiếu điểm", "BR-10", "API 409, review lại rồi publish", "EXAMINER"),
    ("Yêu cầu xem lại (retry answer)", "FR-EXA-05", "POST /api/answers/{id}/retry → RetryAnswerJob", "EXAMINER/ADMIN"),
    ("Sự cố cần thi lại (retake)", "FR-EXA-06, BR-16/17", "Result cũ → CANCELLED, ca thi mới, giữ lịch sử, audit log", "EXAMINER/ADMIN"),
]
ERRORS_EN = [
    ("Start preconditions not met", "BR-11", "400/403, message shown, retry", "Client → STUDENT"),
    ("STT failure (submit error)", "FR-AI-03", "EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR, re-answer or text fallback", "Client → STUDENT"),
    ("Grading error (retryable)", "BR-15", "Hangfire retries up to 3 times, backoff 30s/120s/300s", "Hangfire Worker"),
    ("Grading error (non-retryable) / attempts exhausted", "FR-AI-02", "FAILED + Answer.Status = ERROR + audit log", "Hangfire → Backend"),
    ("Publish before review / score missing", "BR-10", "API 409, review again then publish", "EXAMINER"),
    ("Request a re-check (retry answer)", "FR-EXA-05", "POST /api/answers/{id}/retry → RetryAnswerJob", "EXAMINER/ADMIN"),
    ("Incident requiring a new attempt (retake)", "FR-EXA-06, BR-16/17", "Old Result → CANCELLED, new session, history preserved, audit log", "EXAMINER/ADMIN"),
]

VI_CARDS = ["swimlane-01-preparation-scheduling", "swimlane-02-exam-grading-publish",
            "swimlane-02a-exam-delivery-grading", "swimlane-02b-review-publish-incident"]
EN_CARDS = ["swimlane-01-preparation-scheduling_en", "swimlane-02-exam-grading-publish_en",
            "swimlane-02a-exam-delivery-grading_en", "swimlane-02b-review-publish-incident_en"]

META = {
    "swimlane-01-preparation-scheduling": ("SWL-1 · Chuẩn bị & Tổ chức thi", "4 lane · 2084×3030"),
    "swimlane-02-exam-grading-publish": ("SWL-2 · Làm bài, Chấm & Công bố (đầy đủ)", "6 lane · 6411×4578"),
    "swimlane-02a-exam-delivery-grading": ("SWL-2A · Làm bài & Chấm AI", "5 lane · 5230×3047"),
    "swimlane-02b-review-publish-incident": ("SWL-2B · Review, Công bố & Xử lý sự cố", "4 lane · 2699×1737"),
    "swimlane-01-preparation-scheduling_en": ("SWL-1-EN · Preparation & Exam Scheduling", "4 lane · 2334×3000"),
    "swimlane-02-exam-grading-publish_en": ("SWL-2-EN · Exam Delivery, Grading & Publication (full)", "6 lane · 7042×4578"),
    "swimlane-02a-exam-delivery-grading_en": ("SWL-2A-EN · Exam Delivery & AI Grading", "5 lane · 5788×3047"),
    "swimlane-02b-review-publish-incident_en": ("SWL-2B-EN · Review, Publication & Incident Handling", "4 lane · 2874×1737"),
}
IMAGES = {d["slug"]: d["image_svg"] for d in DIAGRAMS}


def build_diagram_pages():
    for d in DIAGRAMS:
        rows = "\n".join(
            f"        <tr><td><strong>{a}</strong></td><td>{b}</td><td>{c}</td></tr>"
            for a, b, c in d["rows"]
        )
        html = TEMPLATE.format(lang=d["lang"], title=d["title"], subtitle=d["subtitle"],
                               diagram_title=d["diagram_title"], table_title=d["table_title"],
                               image_svg=d["image_svg"], image_png=d["image_png"], puml=d["puml"],
                               prev=d["prev"], prev_label=d["prev_label"],
                               next=d["next"], next_label=d["next_label"],
                               thead=d["thead"], rows=rows,
                               lang_suffix="" if d["lang"] == "vi" else "-en")
        (OUT / f"{d['slug']}.html").write_text(html, encoding="utf-8")
        print("wrote", d["slug"] + ".html")


def build_index(lang):
    if lang == "vi":
        cards = VI_CARDS
        title = "AIVES – Danh mục Swimlane Diagram"
        subtitle = "Thành viên 5 · Bản tiếng Việt"
        sec_diagrams = "Các sơ đồ"
        sec_errors = "Tổng hợp các nhánh lỗi"
        th_branch, th_cond = "Nhánh lỗi", "Điều kiện"
        th_handle, th_lane = "Xử lý", "Lane chịu trách nhiệm"
        errors = ERRORS_VI
        switch = "English version →"
        lang_suffix = "-en"
        md_index = "swimlane-index-en.md"
    else:
        cards = EN_CARDS
        title = "AIVES – Swimlane Diagram Index"
        subtitle = "Member 5 · English version"
        sec_diagrams = "Diagrams"
        sec_errors = "Error branches summary"
        th_branch, th_cond = "Error branch", "Condition"
        th_handle, th_lane = "Handling", "Responsible lane"
        errors = ERRORS_EN
        switch = "Bản tiếng Việt →"
        lang_suffix = ""
        md_index = "swimlane-index.md"

    card_html = []
    for slug in cards:
        name, meta = META[slug]
        card_html.append(f"""    <div class="card">
      <h2>{name}</h2>
      <div class="meta">{meta}</div>
      <div class="thumb"><img src="./{IMAGES[slug]}" alt="{name}"></div>
      <div class="links">
        <a href="./{slug}.html">Mở trang</a>
        <a href="./{slug}.md">Markdown</a>
        <a href="./{IMAGES[slug]}">SVG</a>
      </div>
    </div>""")
    err_html = "\n".join(
        f"      <tr><td><strong>{a}</strong></td><td>{b}</td><td>{c}</td><td>{d2}</td></tr>"
        for a, b, c, d2 in errors
    )
    html = INDEX_TPL.format(lang=lang, title=title, subtitle=subtitle,
                            switch_label=switch, lang_suffix=lang_suffix,
                            sec_diagrams=sec_diagrams, sec_errors=sec_errors,
                            th_branch=th_branch, th_cond=th_cond,
                            th_handle=th_handle, th_lane=th_lane, md_index=md_index,
                            cards="\n".join(card_html), errors=err_html)
    name = "swimlane-index.html" if lang == "vi" else "swimlane-index-en.html"
    (OUT / name).write_text(html, encoding="utf-8")
    print("wrote", name)


if __name__ == "__main__":
    build_diagram_pages()
    build_index("vi")
    build_index("en")
