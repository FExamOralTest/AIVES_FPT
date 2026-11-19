#!/usr/bin/env python3
"""Build ONE single, self-contained page per language containing every AIVES diagram,
from the simplified (slide-ready) set down to the full detailed swimlanes.

Usage:  python docs/architecture/_build_all_html.py
"""
import pathlib

OUT = pathlib.Path(__file__).parent
SIMPLE = OUT / "simplified"

# Bump this whenever the images change - it is appended to every image URL so
# the browser never shows a cached (stale) diagram.
ASSET_VERSION = "v13"

# ---------------------------------------------------------------- data

SIMPLIFIED = [
    dict(key="uc",
         name_vi="DIAGRAM 1 · Use Case Diagram",
         name_en="DIAGRAM 1 · Use Case Diagram",
         meta_vi="4 actor · 13 use case · 5 nhóm",
         meta_en="4 actors · 13 use cases · 5 groups",
         svg_vi="simplified/DIAGRAM_01_USECASE.svg", png_vi="simplified/DIAGRAM_01_USECASE.png",
         svg_en="simplified/DIAGRAM_01_USECASE_EN.svg", png_en="simplified/DIAGRAM_01_USECASE_EN.png",
         puml_vi="simplified/diagram_01_usecase.puml", puml_en="simplified/diagram_01_usecase_en.puml",
         page_vi="simplified/diagram-01-usecase.html", page_en="simplified/diagram-01-usecase_en.html",
         md_vi="simplified/diagram-01-usecase.md", md_en="simplified/diagram-01-usecase_en.md",
         desc_vi="Toàn cảnh chức năng: ai dùng, dùng để làm gì, quan hệ giữa các chức năng.",
         desc_en="Functional overview: who uses what, and how the capabilities relate."),
    dict(key="sw",
         name_vi="DIAGRAM 2 · Swimlane rút gọn (1 activity = 1 luồng)",
         name_en="DIAGRAM 2 · Simplified Swimlane (one box = one flow)",
         meta_vi="5 lane · 13 hộp · 3 nhánh lỗi",
         meta_en="5 lanes · 13 boxes · 3 error branches",
         svg_vi="simplified/DIAGRAM_02_SWIMLANE.svg", png_vi="simplified/DIAGRAM_02_SWIMLANE.png",
         svg_en="simplified/DIAGRAM_02_SWIMLANE_EN.svg", png_en="simplified/DIAGRAM_02_SWIMLANE_EN.png",
         puml_vi="simplified/diagram_02_swimlane.puml", puml_en="simplified/diagram_02_swimlane_en.puml",
         page_vi="simplified/diagram-02-swimlane.html", page_en="simplified/diagram-02-swimlane_en.html",
         md_vi="simplified/diagram-02-swimlane.md", md_en="simplified/diagram-02-swimlane_en.md",
         desc_vi="Luồng đi qua 5 vai trò, 3 nhánh lỗi (nộp bài lỗi · chấm lỗi · yêu cầu xem lại).",
         desc_en="The flow across 5 roles with 3 error branches (submit error, grading error, re-check request)."),
    dict(key="ctx",
         name_vi="DIAGRAM 3 · System Context Diagram",
         name_en="DIAGRAM 3 · System Context Diagram",
         meta_vi="4 actor · 4 năng lực · 3 hệ thống ngoài",
         meta_en="4 actors · 4 capabilities · 3 external systems",
         svg_vi="simplified/DIAGRAM_03_CONTEXT.svg", png_vi="simplified/DIAGRAM_03_CONTEXT.png",
         svg_en="simplified/DIAGRAM_03_CONTEXT_EN.svg", png_en="simplified/DIAGRAM_03_CONTEXT_EN.png",
         puml_vi="simplified/diagram_03_context.puml", puml_en="simplified/diagram_03_context_en.puml",
         page_vi="simplified/diagram-03-context.html", page_en="simplified/diagram-03-context_en.html",
         md_vi="simplified/diagram-03-context.md", md_en="simplified/diagram-03-context_en.md",
         desc_vi="Ranh giới hệ thống AIVES, 4 vai trò và 3 dịch vụ bên ngoài (R2 · LLM · Whisper STT).",
         desc_en="The AIVES boundary, 4 roles and 3 external services (R2, LLM, Whisper STT)."),
    dict(key="sw10",
         name_vi="DIAGRAM 4 · Swimlane 10 Activity",
         name_en="DIAGRAM 4 · Swimlane 10 Activity",
         meta_vi="5 lane · **10 activity** · 3 nhánh lỗi",
         meta_en="5 lanes · **10 activities** · 3 error branches",
         svg_vi="simplified/DIAGRAM_04_SWIMLANE_10.svg", png_vi="simplified/DIAGRAM_04_SWIMLANE_10.png",
         svg_en="simplified/DIAGRAM_04_SWIMLANE_10_EN.svg", png_en="simplified/DIAGRAM_04_SWIMLANE_10_EN.png",
         puml_vi="simplified/diagram_04_swimlane_10.puml", puml_en="simplified/diagram_04_swimlane_10_en.puml",
         page_vi="simplified/diagram-04-swimlane-10.html", page_en="simplified/diagram-04-swimlane-10_en.html",
         md_vi="simplified/diagram-04-swimlane-10.md", md_en="simplified/diagram-04-swimlane-10_en.md",
         desc_vi="Bản swimlane cô đọng nhất: **đúng 10 activity** (UC-01…UC-10) + 3 nhánh lỗi (nộp bài · chấm · công bố).",
         desc_en="The most compact swimlane: **exactly 10 activities** (UC-01…UC-10) + 3 error branches (submit, grading, publish)."),
    dict(key="seq5a",
         name_vi="DIAGRAM 5A · Live Viva Exam",
         name_en="DIAGRAM 5A · Live Viva Exam",
         meta_vi="6 lifeline (gồm DB) · **3 giai đoạn** · phần thi trực tiếp",
         meta_en="6 lifelines (incl. DB) · **3 phases** · the live viva",
         svg_vi="simplified/DIAGRAM_05A_VIVA_EXAM.svg", png_vi="simplified/DIAGRAM_05A_VIVA_EXAM.png",
         svg_en="simplified/DIAGRAM_05A_VIVA_EXAM_EN.svg", png_en="simplified/DIAGRAM_05A_VIVA_EXAM_EN.png",
         puml_vi="simplified/diagram_05a_viva_exam.puml", puml_en="simplified/diagram_05a_viva_exam_en.puml",
         page_vi="simplified/diagram-05a-viva-exam.html", page_en="simplified/diagram-05a-viva-exam_en.html",
         md_vi="", md_en="",
         desc_vi="Phần 1: sinh viên ghi âm trên Web App, audio upload lên Cloudflare R2, AI Engine chuyển giọng nói + sinh câu hỏi tiếp theo.",
         desc_en="Part 1: the student records in the Web App, the audio goes to Cloudflare R2, the AI Engine transcribes the speech + generates the next question."),
    dict(key="seq5b",
         name_vi="DIAGRAM 5B · AI chấm điểm & Khảo thí review",
         name_en="DIAGRAM 5B · AI Scoring & Examiner Review",
         meta_vi="8 lifeline · **18 message** · phần chấm & công bố",
         meta_en="8 lifelines · **18 messages** · scoring & publication",
         svg_vi="simplified/DIAGRAM_05B_GRADING_REVIEW.svg", png_vi="simplified/DIAGRAM_05B_GRADING_REVIEW.png",
         svg_en="simplified/DIAGRAM_05B_GRADING_REVIEW_EN.svg", png_en="simplified/DIAGRAM_05B_GRADING_REVIEW_EN.png",
         puml_vi="simplified/diagram_05b_grading_review.puml", puml_en="simplified/diagram_05b_grading_review_en.puml",
         page_vi="simplified/diagram-05b-grading-review.html", page_en="simplified/diagram-05b-grading-review_en.html",
         md_vi="", md_en="",
         desc_vi="Phần 2: bài nộp lưu ở trạng thái PENDING_REVIEW, chỉ khi khảo thí cho phép chấm AI thì mới xếp hàng job Hangfire; Back-end Service (Grading) gọi AI service chấm rubric rồi lưu vào Result store, khảo thí chấm điểm chính thức và công bố.",
         desc_en="Part 2: the submission stays PENDING_REVIEW; only when the examiner authorizes the AI grading is the Hangfire job queued. The Back-end Service (Grading) calls the AI service to grade against the rubric and stores it in the Result store, then the examiner assigns the official score and publishes."),
    dict(key="seqs",
         name_vi="DIAGRAM 5 · Sequence Diagram rút gọn",
         name_en="DIAGRAM 5 · Simplified Sequence Diagram",
         meta_vi="6 participant · **3 giai đoạn** · 3 nhánh lỗi",
         meta_en="6 participants · **3 phases** · 3 error branches",
         svg_vi="simplified/DIAGRAM_05_SEQUENCE.svg", png_vi="simplified/DIAGRAM_05_SEQUENCE.png",
         svg_en="simplified/DIAGRAM_05_SEQUENCE_EN.svg", png_en="simplified/DIAGRAM_05_SEQUENCE_EN.png",
         puml_vi="simplified/diagram_05_sequence.puml", puml_en="simplified/diagram_05_sequence_en.puml",
         page_vi="simplified/diagram-05-sequence.html", page_en="simplified/diagram-05-sequence_en.html",
         md_vi="simplified/diagram-05-sequence.md", md_en="simplified/diagram-05-sequence_en.md",
         desc_vi="Thứ tự gọi thực tế: làm bài → AI chấm → khảo thí công bố → sinh viên xem điểm.",
         desc_en="The real call order: take the viva → AI grades → the examiner publishes → the student reads the score."),
    dict(key="c1",
         name_vi="C1 · Container Diagram (multi-container)",
         name_en="C1 · Container Diagram (multi-container)",
         meta_vi="5 container · **4 container Docker** · 2 hệ thống ngoài",
         meta_en="5 containers · **4 Docker containers** · 2 external systems",
         svg_vi="C1_Container.svg", png_vi="C1_Container.png",
         svg_en="C1_Container.svg", png_en="C1_Container.png",
         puml_vi="c1_container.puml", puml_en="c1_container.puml",
         page_vi="c1-container.html", page_en="c1-container_en.html",
         md_vi="", md_en="",
         desc_vi="Kiến trúc container: Web (React + Nginx) · Backend (ASP.NET Core, chạy Hangfire) · AI service (FastAPI) · Neon PostgreSQL · Cloudflare R2 — kèm giao thức nội bộ `X-Internal-Token` và topology Docker Compose.",
         desc_en="The container architecture: Web (React + Nginx) · Backend (ASP.NET Core, hosting Hangfire) · AI service (FastAPI) · Neon PostgreSQL · Cloudflare R2 — plus the internal `X-Internal-Token` protocol and the Docker Compose topology."),
]

SEQUENCE = [
    dict(key="seq1",
         name_vi="SEQ 1 · Luồng thi Viva",
         name_en="SEQ 1 · Viva Exam Flow",
         meta_vi="3 giai đoạn · 8 participant · 2271×2858",
         meta_en="3 phases · 8 participants · 2293×2837",
         svg_vi="sequence/SEQ_01_VIVA_FLOW.svg", png_vi="sequence/SEQ_01_VIVA_FLOW.png",
         svg_en="sequence/SEQ_01_VIVA_FLOW_EN.svg", png_en="sequence/SEQ_01_VIVA_FLOW_EN.png",
         puml_vi="sequence/seq_01_viva_flow.puml", puml_en="sequence/seq_01_viva_flow_en.puml",
         page_vi="sequence/seq-01-viva-flow.html", page_en="sequence/seq-01-viva-flow_en.html",
         md_vi="sequence/seq-01-viva-flow.md", md_en="sequence/seq-01-viva-flow_en.md",
         desc_vi="Bắt đầu ca thi → ghi âm & nộp bài (STT + sinh follow-up) → kết thúc & xếp hàng chấm.",
         desc_en="Start the session → record & submit (STT + follow-up) → finish and queue for grading."),
    dict(key="seq2",
         name_vi="SEQ 2 · Chấm điểm AI",
         name_en="SEQ 2 · AI Grading",
         meta_vi="5 giai đoạn · 7 participant · 2263×2021",
         meta_en="5 phases · 7 participants · 2320×2021",
         svg_vi="sequence/SEQ_02_GRADING.svg", png_vi="sequence/SEQ_02_GRADING.png",
         svg_en="sequence/SEQ_02_GRADING_EN.svg", png_en="sequence/SEQ_02_GRADING_EN.png",
         puml_vi="sequence/seq_02_grading.puml", puml_en="sequence/seq_02_grading_en.puml",
         page_vi="sequence/seq-02-grading.html", page_en="sequence/seq-02-grading_en.html",
         md_vi="sequence/seq-02-grading.md", md_en="sequence/seq-02-grading_en.md",
         desc_vi="Hangfire worker + khóa phân tán → gọi LLM chấm rubric → lưu AiScore, retry backoff.",
         desc_en="Hangfire worker + distributed lock → LLM rubric grading → persist AiScore, backoff retry."),
    dict(key="seq3",
         name_vi="SEQ 3 · Review, Công bố & Xem kết quả",
         name_en="SEQ 3 · Review, Publish & Result View",
         meta_vi="6 giai đoạn · 7 participant · 2198×3287",
         meta_en="6 phases · 7 participants · 2159×3285",
         svg_vi="sequence/SEQ_03_REVIEW_PUBLISH.svg", png_vi="sequence/SEQ_03_REVIEW_PUBLISH.png",
         svg_en="sequence/SEQ_03_REVIEW_PUBLISH_EN.svg", png_en="sequence/SEQ_03_REVIEW_PUBLISH_EN.png",
         puml_vi="sequence/seq_03_review_publish.puml", puml_en="sequence/seq_03_review_publish_en.puml",
         page_vi="sequence/seq-03-review-publish.html", page_en="sequence/seq-03-review-publish_en.html",
         md_vi="sequence/seq-03-review-publish.md", md_en="sequence/seq-03-review-publish_en.md",
         desc_vi="PENDING_REVIEW → REVIEWED → PUBLISHED; sinh viên chỉ thấy điểm khi PUBLISHED.",
         desc_en="PENDING_REVIEW → REVIEWED → PUBLISHED; the student only sees the score once PUBLISHED."),
    dict(key="seq4",
         name_vi="SEQ 4 · Chuẩn bị & Tổ chức thi",
         name_en="SEQ 4 · Preparation & Exam Scheduling",
         meta_vi="7 giai đoạn · 9 participant · 1966×2627",
         meta_en="7 phases · 9 participants · 1959×2606",
         svg_vi="sequence/SEQ_04_ADMIN_PREP.svg", png_vi="sequence/SEQ_04_ADMIN_PREP.png",
         svg_en="sequence/SEQ_04_ADMIN_PREP_EN.svg", png_en="sequence/SEQ_04_ADMIN_PREP_EN.png",
         puml_vi="sequence/seq_04_admin_prep.puml", puml_en="sequence/seq_04_admin_prep_en.puml",
         page_vi="sequence/seq-04-admin-prep.html", page_en="sequence/seq-04-admin-prep_en.html",
         md_vi="sequence/seq-04-admin-prep.md", md_en="sequence/seq-04-admin-prep_en.md",
         desc_vi="Tạo tài khoản → môn học → upload .md → AI sinh câu hỏi → duyệt → lịch thi → ghi danh.",
         desc_en="Account → course → upload .md → AI questions → approve → schedule → enrol."),
]

DETAILED = [
    dict(key="swl1",
         name_vi="SWL-1 · Chuẩn bị & Tổ chức thi",
         name_en="SWL-1 · Preparation & Exam Scheduling",
         meta_vi="4 lane · 2084×3030",
         meta_en="4 lanes · 2334×3000",
         svg_vi="SWIMLANE_01_PREPARATION.svg", png_vi="SWIMLANE_01_PREPARATION.png",
         svg_en="SWIMLANE_01_PREPARATION_EN.svg", png_en="SWIMLANE_01_PREPARATION_EN.png",
         puml_vi="swimlane_01_preparation_scheduling.puml", puml_en="swimlane_01_preparation_scheduling_en.puml",
         page_vi="swimlane-01-preparation-scheduling.html", page_en="swimlane-01-preparation-scheduling_en.html",
         md_vi="swimlane-01-preparation-scheduling.md", md_en="swimlane-01-preparation-scheduling_en.md",
         desc_vi="ADMIN · STAFF · AI Service · Backend — tài liệu → câu hỏi → đề thi → lịch thi.",
         desc_en="ADMIN, STAFF, AI Service, Backend — material → questions → paper → schedule."),
    dict(key="swl2",
         name_vi="SWL-2 · Làm bài, Chấm & Công bố (đầy đủ)",
         name_en="SWL-2 · Exam Delivery, Grading & Publication (full)",
         meta_vi="6 lane · 6411×4578",
         meta_en="6 lanes · 7042×4578",
         svg_vi="SWIMLANE_02_EXAM_GRADING_PUBLISH.svg", png_vi="SWIMLANE_02_EXAM_GRADING_PUBLISH.png",
         svg_en="SWIMLANE_02_EXAM_GRADING_PUBLISH_EN.svg", png_en="SWIMLANE_02_EXAM_GRADING_PUBLISH_EN.png",
         puml_vi="swimlane_02_exam_grading_publish.puml", puml_en="swimlane_02_exam_grading_publish_en.puml",
         page_vi="swimlane-02-exam-grading-publish.html", page_en="swimlane-02-exam-grading-publish_en.html",
         md_vi="swimlane-02-exam-grading-publish.md", md_en="swimlane-02-exam-grading-publish_en.md",
         desc_vi="Bản đầy đủ 6 lane: STUDENT · Client · Backend · AI · Hangfire · EXAMINER, 8 nhánh lỗi.",
         desc_en="The complete 6-lane version: STUDENT, Client, Backend, AI, Hangfire, EXAMINER, 8 error branches."),
    dict(key="swl2a",
         name_vi="SWL-2A · Làm bài & Chấm AI",
         name_en="SWL-2A · Exam Delivery & AI Grading",
         meta_vi="5 lane · 5230×3047",
         meta_en="5 lanes · 5788×3047",
         svg_vi="SWIMLANE_02A_EXAM_DELIVERY_GRADING.svg", png_vi="SWIMLANE_02A_EXAM_DELIVERY_GRADING.png",
         svg_en="SWIMLANE_02A_EXAM_DELIVERY_GRADING_EN.svg", png_en="SWIMLANE_02A_EXAM_DELIVERY_GRADING_EN.png",
         puml_vi="swimlane_02a_exam_delivery_grading.puml", puml_en="swimlane_02a_exam_delivery_grading_en.puml",
         page_vi="swimlane-02a-exam-delivery-grading.html", page_en="swimlane-02a-exam-delivery-grading_en.html",
         md_vi="swimlane-02a-exam-delivery-grading.md", md_en="swimlane-02a-exam-delivery-grading_en.md",
         desc_vi="Phần 1 của SWL-2 tách riêng: thi & chấm AI (STT, retry backoff, PENDING_REVIEW).",
         desc_en="Part 1 of SWL-2, split out: exam delivery & AI grading (STT, backoff retry, PENDING_REVIEW)."),
    dict(key="swl2b",
         name_vi="SWL-2B · Review, Công bố & Xử lý sự cố",
         name_en="SWL-2B · Review, Publication & Incident Handling",
         meta_vi="4 lane · 2699×1737",
         meta_en="4 lanes · 2874×1737",
         svg_vi="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT.svg", png_vi="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT.png",
         svg_en="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT_EN.svg", png_en="SWIMLANE_02B_REVIEW_PUBLISH_INCIDENT_EN.png",
         puml_vi="swimlane_02b_review_publish_incident.puml", puml_en="swimlane_02b_review_publish_incident_en.puml",
         page_vi="swimlane-02b-review-publish-incident.html", page_en="swimlane-02b-review-publish-incident_en.html",
         md_vi="swimlane-02b-review-publish-incident.md", md_en="swimlane-02b-review-publish-incident_en.md",
         desc_vi="Phần 2 của SWL-2 tách riêng: review bằng chứng, publish, retry answer, retake.",
         desc_en="Part 2 of SWL-2, split out: evidence review, publication, retry answer, retake."),
]

ERRORS_VI = [
    ("Chưa đủ điều kiện bắt đầu", "BR-11", "400/403, hiển thị thông báo, thử lại", "Client → STUDENT", "DIAGRAM 2 · SWL-2"),
    ("STT thất bại (nộp bài lỗi)", "FR-AI-03", "EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR, nhập lại hoặc text fallback", "Client → STUDENT", "DIAGRAM 2 · SWL-2A"),
    ("Chấm AI lỗi (retryable)", "BR-15", "Hangfire retry tối đa 3 lḀn, backoff 30s/120s/300s", "Hangfire Worker", "DIAGRAM 2 · SWL-2A"),
    ("Chấm AI lỗi (không retry) / hết lượt", "FR-AI-02", "FAILED + Answer.Status = ERROR + audit log", "Hangfire → Backend", "DIAGRAM 2 · SWL-2A"),
    ("Publish khi chưa review / thiếu điểm", "BR-10", "API 409, review lại rồi publish", "EXAMINER", "DIAGRAM 2 · SWL-2B"),
    ("Yêu cầu xem lại (retry answer)", "FR-EXA-05", "POST /api/answers/{id}/retry → RetryAnswerJob", "EXAMINER / ADMIN", "SWL-2B"),
    ("Sự cố cần thi lại (retake)", "FR-EXA-06, BR-16/17", "Result cũ → CANCELLED, ca thi mới, giữ lịch sử, audit log", "EXAMINER / ADMIN", "SWL-2B"),
]

ERRORS_EN = [
    ("Start preconditions not met", "BR-11", "400/403, message shown, retry", "Client → STUDENT", "DIAGRAM 2 · SWL-2"),
    ("STT failure (submit error)", "FR-AI-03", "EMPTY_AUDIO / TIMEOUT / INTERNAL_ERROR, re-answer or text fallback", "Client → STUDENT", "DIAGRAM 2 · SWL-2A"),
    ("Grading error (retryable)", "BR-15", "Hangfire retries up to 3 times, backoff 30s/120s/300s", "Hangfire Worker", "DIAGRAM 2 · SWL-2A"),
    ("Grading error (non-retryable) / attempts exhausted", "FR-AI-02", "FAILED + Answer.Status = ERROR + audit log", "Hangfire → Backend", "DIAGRAM 2 · SWL-2A"),
    ("Publish before review / score missing", "BR-10", "API 409, review again then publish", "EXAMINER", "DIAGRAM 2 · SWL-2B"),
    ("Request a re-check (retry answer)", "FR-EXA-05", "POST /api/answers/{id}/retry → RetryAnswerJob", "EXAMINER / ADMIN", "SWL-2B"),
    ("Incident requiring a new attempt (retake)", "FR-EXA-06, BR-16/17", "Old Result → CANCELLED, new session, history preserved, audit log", "EXAMINER / ADMIN", "SWL-2B"),
]

ORDER_VI = [
    ("1", "Bối cảnh – ai dùng hệ thống, hệ thống dùng gì", "DIAGRAM 3"),
    ("2", "Hệ thống làm gì – 13 chức năng & ai dùng", "DIAGRAM 1"),
    ("3", "Luồng đi qua ai – 5 vai trò, 3 nhánh lỗi", "DIAGRAM 2"),
    ("4", "Cô đọng nhất – 10 activity, vừa 1 slide", "DIAGRAM 4"),
    ("5", "Thứ tự gọi thực tế giữa các thành phần", "DIAGRAM 5"),
    ("6+", "Chi tiết (khi GV hỏi sâu)", "SWL-1 / SWL-2A / SWL-2B · SEQ-1…SEQ-4"),
]
ORDER_EN = [
    ("1", "Context – who uses the system, what the system uses", "DIAGRAM 3"),
    ("2", "What the system does – 13 capabilities & who uses them", "DIAGRAM 1"),
    ("3", "Who the flow passes through – 5 roles, 3 error branches", "DIAGRAM 2"),
    ("4", "Most compact – 10 activities, fits one slide", "DIAGRAM 4"),
    ("5", "The real call order between the components", "DIAGRAM 5"),
    ("6+", "Details (when asked deeper)", "SWL-1 / SWL-2A / SWL-2B · SEQ-1…SEQ-4"),
]

# ---------------------------------------------------------------- template

PAGE = """<!DOCTYPE html>
<html lang="{lang}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>
  :root {{ color-scheme: light; --ink:#1f2933; --brand:#0f3d5c; --line:#dfe5ea; }}
  * {{ box-sizing:border-box; }}
  html {{ scroll-behavior:smooth; }}
  body {{ margin:0; font-family:"Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
         background:#f4f6f8; color:var(--ink); font-size:15px; }}
  a {{ color:var(--brand); }}

  header {{ background:var(--brand); color:#fff; padding:30px 32px 26px; }}
  header h1 {{ margin:0 0 6px; font-size:27px; letter-spacing:.2px; }}
  header p {{ margin:0; opacity:.86; font-size:14px; }}
  header .badge {{ display:inline-block; margin-top:12px; padding:4px 11px; border-radius:99px;
                   background:rgba(255,255,255,.16); font-size:12.5px; font-weight:600; }}

  .topbar {{ position:sticky; top:0; z-index:20; background:#fff; border-bottom:1px solid var(--line);
             padding:0 32px; display:flex; gap:4px; flex-wrap:wrap; box-shadow:0 1px 3px rgba(0,0,0,.05); }}
  .topbar a {{ padding:11px 15px; font-size:13.5px; font-weight:600; color:#3c5b73; text-decoration:none;
               border-bottom:3px solid transparent; }}
  .topbar a:hover {{ color:var(--brand); border-bottom-color:#b7c9d6; }}
  .topbar a.lang {{ margin-left:auto; color:var(--brand); }}

  main {{ padding:26px 32px 70px; max-width:1500px; margin:0 auto; }}

  .banner {{ background:#fff8e1; border-left:5px solid #B8860B; padding:13px 18px; border-radius:0 8px 8px 0;
            font-size:13.5px; margin-bottom:24px; }}
  .banner strong {{ color:#7a5c00; }}

  section {{ margin-bottom:38px; }}
  .sec-head {{ display:flex; align-items:baseline; gap:12px; flex-wrap:wrap;
               border-bottom:2px solid #dbe3ea; padding-bottom:9px; margin-bottom:16px; }}
  .sec-head h2 {{ margin:0; font-size:20px; color:var(--brand); }}
  .sec-head .tag {{ font-size:11.5px; font-weight:700; text-transform:uppercase; letter-spacing:.6px;
                    padding:3px 9px; border-radius:5px; background:var(--brand); color:#fff; }}
  .sec-head .tag.warn {{ background:#B8860B; }}
  .sec-head .note {{ font-size:13px; color:#5b6b7a; }}

  .grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(430px,1fr)); gap:18px; }}
  .card {{ background:#fff; border:1px solid var(--line); border-radius:10px; overflow:hidden;
           display:flex; flex-direction:column; box-shadow:0 1px 3px rgba(0,0,0,.05); }}
  .card .head {{ padding:13px 16px; border-bottom:1px solid var(--line); }}
  .card .head h3 {{ margin:0 0 3px; font-size:16px; color:var(--brand); }}
  .card .head .meta {{ font-size:12px; color:#5b6b7a; }}
  .card .head .desc {{ font-size:13px; color:#3c5b73; margin-top:7px; }}
  .card .thumb {{ background:#fbfcfd; border-bottom:1px solid var(--line); padding:8px; height:280px;
                  display:flex; align-items:center; justify-content:center; cursor:zoom-in; }}
  .card .thumb img {{ max-width:100%; max-height:100%; object-fit:contain; }}
  .card .links {{ padding:10px 16px 13px; font-size:13px; display:flex; flex-wrap:wrap; gap:8px; }}
  .card .links a {{ padding:4px 11px; border:1px solid #c8d6e0; border-radius:6px; text-decoration:none;
                    font-weight:600; color:var(--brand); background:#f7fafc; }}
  .card .links a:hover {{ background:#e9f1f6; }}
  .card .links a.pri {{ background:var(--brand); color:#fff; border-color:var(--brand); }}

  table {{ border-collapse:collapse; width:100%; font-size:13.5px; background:#fff; border:1px solid var(--line); }}
  th,td {{ border:1px solid var(--line); padding:9px 11px; text-align:left; vertical-align:top; }}
  th {{ background:#eef3f7; color:var(--brand); font-size:13px; }}
  td code {{ background:#eef3f7; padding:1px 5px; border-radius:4px; font-size:12.3px; }}

  .steps {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(230px,1fr)); gap:12px; }}
  .step {{ background:#fff; border:1px solid var(--line); border-left:4px solid var(--brand);
           border-radius:0 8px 8px 0; padding:12px 14px; }}
  .step .n {{ font-size:11.5px; font-weight:800; color:var(--brand); letter-spacing:.6px; }}
  .step .t {{ font-size:14px; margin:3px 0 4px; }}
  .step .d {{ font-size:12.5px; color:#5b6b7a; }}

  .legend {{ display:flex; gap:18px; flex-wrap:wrap; font-size:13px; color:#3c5b73;
             margin-top:12px; }}
  .legend span::before {{ content:"●"; color:var(--brand); margin-right:6px; }}
  .legend span.b::before {{ color:#B8860B; }}

  /* lightbox */
  .lb {{ position:fixed; inset:0; background:rgba(10,20,30,.92); display:none; z-index:99;
         align-items:center; justify-content:center; padding:24px; }}
  .lb.on {{ display:flex; }}
  .lb img {{ max-width:100%; max-height:100%; background:#fff; border-radius:6px; }}
  .lb .close {{ position:absolute; top:16px; right:22px; color:#fff; font-size:32px; cursor:pointer; line-height:1; }}

  footer {{ padding:22px 32px; font-size:12.5px; color:#5b6b7a; border-top:1px solid var(--line); }}
  @media (max-width:700px) {{ main {{ padding:18px 16px 50px; }} .grid {{ grid-template-columns:1fr; }} }}
</style>
</head>
<body>
<header>
  <h1>{title}</h1>
  <p>{subtitle}</p>
  <span class="badge">{badge}</span>
</header>

<div class="topbar">
  <a href="#sim">⚡ {nav_sim}</a>
  <a href="#full">{nav_full}</a>
  <a href="#seq">{nav_seq}</a>
  <a href="#err">{nav_err}</a>
  <a href="#order">{nav_order}</a>
  <a href="#files">{nav_files}</a>
  <a href="./{erd_link}.html">{nav_erd}</a>
  <a class="lang" href="./{lang_link}.html">{lang_switch}</a>
</div>

<main>
  <div class="banner">{banner}</div>

  <section id="sim">
    <div class="sec-head">
      <span class="tag">{tag_sim}</span>
      <h2>{h_sim}</h2>
      <span class="note">{n_sim}</span>
    </div>
    <div class="grid">
{sim_cards}
    </div>
  </section>

  <section id="full">
    <div class="sec-head">
      <span class="tag warn">{tag_full}</span>
      <h2>{h_full}</h2>
      <span class="note">{n_full}</span>
    </div>
    <div class="grid">
{full_cards}
    </div>
  </section>

  <section id="seq">
    <div class="sec-head">
      <span class="tag">{tag_seq}</span>
      <h2>{h_seq}</h2>
      <span class="note">{n_seq}</span>
    </div>
    <div class="grid">
{seq_cards}
    </div>
  </section>

  <section id="err">
    <div class="sec-head">
      <h2>{h_err}</h2>
      <span class="note">{n_err}</span>
    </div>
    <table>
      <thead><tr><th>{t_branch}</th><th>{t_cond}</th><th>{t_handle}</th><th>{t_lane}</th><th>{t_where}</th></tr></thead>
      <tbody>
{err_rows}
      </tbody>
    </table>
  </section>

  <section id="order">
    <div class="sec-head">
      <h2>{h_order}</h2>
    </div>
    <div class="steps">
{order_steps}
    </div>
  </section>

  <section id="files">
    <div class="sec-head">
      <h2>{h_files}</h2>
    </div>
    <table>
      <thead><tr><th>{t_file}</th><th>{t_what}</th><th>{t_use}</th></tr></thead>
      <tbody>
{files_rows}
      </tbody>
    </table>
  </section>
</main>

<div class="lb" id="lb">
  <span class="close" id="lbClose">&times;</span>
  <img id="lbImg" alt="" src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7">
</div>

<footer>AIVES – Member 5 deliverable · all diagrams on one page · {foot}</footer>

<script>
  const lb = document.getElementById('lb'), lbImg = document.getElementById('lbImg');
  document.querySelectorAll('.thumb').forEach(t => t.addEventListener('click', () => {{
    lbImg.src = t.dataset.full; lb.classList.add('on');
  }}));
  document.getElementById('lbClose').addEventListener('click', () => lb.classList.remove('on'));
  lb.addEventListener('click', e => {{ if (e.target === lb) lb.classList.remove('on'); }});
  document.addEventListener('keydown', e => {{ if (e.key === 'Escape') lb.classList.remove('on'); }});
</script>
</body>
</html>
"""


def card(d, lang):
    suffix = "_vi" if lang == "vi" else "_en"
    asset_version = ASSET_VERSION
    name = d[f"name_{lang}"]
    meta = d[f"meta_{lang}"]
    desc = d[f"desc_{lang}"]
    svg = d[f"svg{suffix}"]
    png = d[f"png{suffix}"]
    puml = d[f"puml{suffix}"]
    page = d[f"page{suffix}"]
    md = d[f"md{suffix}"]
    md_link = f'<a href="./{md}">MD</a>' if md else ""
    open_label = "Mở trang" if lang == "vi" else "Open page"
    return f"""    <div class="card">
      <div class="head">
        <h3>{name}</h3>
        <div class="meta">{meta}</div>
        <div class="desc">{desc}</div>
      </div>
      <div class="thumb" data-full="./{png}"><img src="./{svg}?{asset_version}" alt="{name}"></div>
      <div class="links">
        <a class="pri" href="./{page}">{open_label}</a>
        {md_link}
        <a href="./{png}" download>PNG</a>
        <a href="./{svg}" download>SVG</a>
        <a href="./{puml}" target="_blank">PUML</a>
      </div>
    </div>"""


def build(lang):
    vi = lang == "vi"
    sfx = "" if vi else "-en"

    sim_cards = "\n".join(card(d, lang) for d in SIMPLIFIED)
    full_cards = "\n".join(card(d, lang) for d in DETAILED)
    seq_cards = "\n".join(card(d, lang) for d in SEQUENCE)

    errors = ERRORS_VI if vi else ERRORS_EN
    err_rows = "\n".join(
        f"      <tr><td><strong>{a}</strong></td><td><code>{b}</code></td><td>{c}</td>"
        f"<td>{d}</td><td>{e}</td></tr>"
        for a, b, c, d, e in errors)

    order = ORDER_VI if vi else ORDER_EN
    order_steps = "\n".join(
        f'      <div class="step"><div class="n">SLIDE {a}</div>'
        f'<div class="t">{b}</div><div class="d">{c}</div></div>'
        for a, b, c in order)

    if vi:
        t = dict(
            title="AIVES – Toàn bộ Sơ đồ",
            subtitle="Thành viên 5 · Một trang duy nhất: từ bản ngắn gọn (để trình bày) đến bản đầy đủ (để tham khảo)",
            badge="5 diagram rút gọn · 4 swimlane chi tiết · 2 ngôn ngữ",
            nav_sim="Ngắn gọn", nav_full="Đầy đủ", nav_seq="Sequence", nav_err="Nhánh lỗi", nav_order="Thứ tự trình bày", nav_files="File",
            nav_erd="&#128202; Bộ ERD riêng", erd_link="erd/all-erd",
            lang_switch="English →", lang_link="all-diagrams-en",
            banner=("<strong>Một trang duy nhất.</strong> Phần đầu là bộ <strong>rút gọn</strong> "
                    "(vừa 1 slide, dùng để thuyết trình). Phần sau là bộ <strong>chi tiết</strong> "
                    "(6 lane, ~40 hộp, 8 nhánh lỗi — dùng để tham khảo & trả lời câu hỏi sâu). "
                    "<strong>Bấm vào ảnh</strong> để xem kích thước đầy đủ."),
            tag_sim="Dùng để thuyết trình", h_sim="Bộ rút gọn — nhét vào slide",
            n_sim="5 diagram · vừa 1 slide",
            tag_full="Tham khảo", h_full="Bộ chi tiết — đầy đủ, giữ nguyên",
            n_full="4 swimlane · 6 lane · 8 nhánh lỗi",
            tag_seq="Tương tác", h_seq="Sequence Diagram — 4 luồng chính",
            n_seq="4 sơ đồ · 2 ngôn ngữ",
            h_err="Tổng hợp nhánh lỗi", n_err="7 nhánh · ánh xạ về từng sơ đồ",
            h_order="Thứ tự trình bày đề xuất",
            h_files="File nguồn & cách dùng",
            t_branch="Nhánh lỗi", t_cond="Điều kiện / SRS", t_handle="Xử lý", t_lane="Lane chịu trách nhiệm", t_where="Có trong sơ đồ",
            t_file="File", t_what="Nội dung", t_use="Dùng để",
            foot="Bản tiếng Việt",
        )
    else:
        t = dict(
            title="AIVES – All Diagrams on One Page",
            subtitle="Member 5 · A single page: from the simplified (presentation) set down to the full detailed swimlanes",
            badge="5 simplified diagrams · 4 detailed swimlanes · 2 languages",
            nav_sim="Simplified", nav_full="Detailed", nav_seq="Sequence", nav_err="Error branches", nav_order="Presentation order", nav_files="Files",
            nav_erd="&#128202; Separate ERD suite", erd_link="erd/all-erd-en",
            lang_switch="← Tiếng Việt", lang_link="all-diagrams",
            banner=("<strong>One single page.</strong> The top section is the <strong>simplified</strong> set "
                    "(fits one slide, use it for presenting). Below is the <strong>detailed</strong> set "
                    "(6 lanes, ~40 boxes, 8 error branches — for reference & deep questions). "
                    "<strong>Click any image</strong> to view it at full size."),
            tag_sim="For presenting", h_sim="Simplified set — drop into slides",
            n_sim="5 diagrams · fits one slide",
            tag_full="Reference", h_full="Detailed set — full, kept unchanged",
            n_full="4 swimlanes · 6 lanes · 8 error branches",
            tag_seq="Interaction", h_seq="Sequence Diagram — 4 main flows",
            n_seq="4 diagrams · 2 languages",
            h_err="Error branches summary", n_err="7 branches · mapped to each diagram",
            h_order="Suggested presentation order",
            h_files="Source files & how to use them",
            t_branch="Error branch", t_cond="Condition / SRS", t_handle="Handling", t_lane="Responsible lane", t_where="Appears in",
            t_file="File", t_what="Content", t_use="Used for",
            foot="English version",
        )

    files = [
        ("docs/architecture/simplified/diagram_0*.puml", "PlantUML source (self-contained, renders offline)",
         "Regenerating the simplified diagrams"),
        ("docs/architecture/swimlane_0*.puml", "PlantUML source of the detailed swimlanes",
         "Regenerating the detailed diagrams"),
        ("docs/architecture/*/DIAGRAM_*.svg", "Vector image, infinitely scalable",
         "Dropping into PowerPoint / slides"),
        ("docs/architecture/*/SWIMLANE_*.svg", "Vector image of the detailed swimlanes",
         "Printing or zoomed viewing"),
        ("docs/architecture/*/DIAGRAM_*.png", "Full-resolution raster (rendered from the SVG)",
         "Documents that need a raster image"),
        ("docs/architecture/*.md, simplified/*.md", "Markdown documentation for every diagram",
         "Reading / printing the explanation"),
        ("docs/architecture/architecture.md", "C4 context + C1 container + sequence + state machine",
         "The overall architecture document"),
    ]
    files_rows = "\n".join(
        f"      <tr><td><code>{a}</code></td><td>{b}</td><td>{c}</td></tr>"
        for a, b, c in files)

    html = PAGE.format(lang=lang, sim_cards=sim_cards, full_cards=full_cards,
                       seq_cards=seq_cards,
                       err_rows=err_rows, order_steps=order_steps, files_rows=files_rows, **t)
    name = "all-diagrams.html" if vi else "all-diagrams-en.html"
    (OUT / name).write_text(html, encoding="utf-8")
    print("wrote", name)


if __name__ == "__main__":
    build("vi")
    build("en")
