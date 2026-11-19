#!/usr/bin/env python3
"""Generate the Markdown index for the sequence diagram set (VI + EN)."""
import pathlib

OUT = pathlib.Path(__file__).parent

VI = [
    ("seq-01-viva-flow", "SEQ 1 · Luồng thi Viva", "3 giai đoạn · 8 participant · 2271×2858",
     "Bắt đầu ca thi → ghi âm & nộp câu trả lời (STT + follow-up) → kết thúc & xếp hàng chấm",
     "seq_01_viva_flow.puml", "SEQ_01_VIVA_FLOW.svg"),
    ("seq-02-grading", "SEQ 2 · Chấm điểm AI", "5 giai đoạn · 7 participant · 2263×2021",
     "Hangfire worker + khóa phân tán → gọi LLM chấm rubric → lưu AiScore, retry backoff 30s/120s/300s",
     "seq_02_grading.puml", "SEQ_02_GRADING.svg"),
    ("seq-03-review-publish", "SEQ 3 · Review, Công bố & Xem kết quả", "6 giai đoạn · 7 participant · 2198×3287",
     "PENDING_REVIEW → REVIEWED → PUBLISHED; sinh viên chỉ thấy điểm khi PUBLISHED",
     "seq_03_review_publish.puml", "SEQ_03_REVIEW_PUBLISH.svg"),
    ("seq-04-admin-prep", "SEQ 4 · Chuẩn bị & Tổ chức thi", "7 giai đoạn · 9 participant · 1966×2627",
     "Tạo tài khoản → tạo môn học → upload .md → AI sinh câu hỏi → duyệt → lập lịch ca thi → ghi danh",
     "seq_04_admin_prep.puml", "SEQ_04_ADMIN_PREP.svg"),
]
EN = [
    ("seq-01-viva-flow_en", "SEQ 1-EN · Viva Exam Flow", "3 phases · 8 participants · 2293×2837",
     "Start the session → record & submit the answer (STT + follow-up) → finish and queue for grading",
     "seq_01_viva_flow_en.puml", "SEQ_01_VIVA_FLOW_EN.svg"),
    ("seq-02-grading_en", "SEQ 2-EN · AI Grading", "5 phases · 7 participants · 2320×2021",
     "Hangfire worker + distributed lock → LLM rubric grading → persist AiScore, backoff 30s/120s/300s",
     "seq_02_grading_en.puml", "SEQ_02_GRADING_EN.svg"),
    ("seq-03-review-publish_en", "SEQ 3-EN · Review, Publish & Result View", "6 phases · 7 participants · 2159×3285",
     "PENDING_REVIEW → REVIEWED → PUBLISHED; the student only sees the score once PUBLISHED",
     "seq_03_review_publish_en.puml", "SEQ_03_REVIEW_PUBLISH_EN.svg"),
    ("seq-04-admin-prep_en", "SEQ 4-EN · Preparation & Exam Scheduling", "7 phases · 9 participants · 1959×2606",
     "Create account → create course → upload .md → AI generates questions → approve → schedule → enrol",
     "seq_04_admin_prep_en.puml", "SEQ_04_ADMIN_PREP_EN.svg"),
]

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

TPL = """# {title}

> {subtitle}

## {sec_diagrams}

{diagram_rows}

## {sec_participants}

| Participant | Role |
|---|---|
{participant_rows}

## {sec_errors}

| Error branch | Condition | Handling |
|---|---|---|
{error_rows}

---

*...*
"""


def build(lang):
    vi = lang == "vi"
    items = VI if vi else EN
    parts = PARTICIPANTS_VI if vi else PARTICIPANTS_EN
    errs = ERRORS_VI if vi else ERRORS_EN

    diagram_rows = "\n".join(
        f"### [{name}](./{slug}.html)\n\n"
        f"![{name}](./{svg})\n\n"
        f"*{meta}* — {desc}\n\n"
        f"- {'Trang/Page' if vi else 'Page'}: [`{slug}.html`](./{slug}.html) · [Markdown](./{slug}.md)\n"
        f"- {'Nguồn/Source' if vi else 'Source'}: [`{puml}`](./{puml})\n"
        for slug, name, meta, desc, puml, svg in items)

    part_rows = "\n".join(f"| **{a}** | {b} |" for a, b in parts)
    err_rows = "\n".join(f"| {a} | `{b}` | {c} |" for a, b, c in errs)

    if vi:
        title = "AIVES – Danh mục Sequence Diagram"
        subtitle = "Thành viên 5 · Bản tiếng Việt"
        sec_diagrams = "Các sơ đồ"
        sec_participants = "Tất cả participant trong hệ thống"
        sec_errors = "Tổng hợp các nhánh lỗi"
        foot = ("*Bộ Sequence Diagram của AIVES · Thành viên 5 · "
                "[Tất cả trên 1 trang](../all-diagrams{all_suffix}.md)*")
        page_label = "Trang/Page"
        source_label = "Nguồn/Source"
    else:
        title = "AIVES – Sequence Diagram Index"
        subtitle = "Member 5 · English version"
        sec_diagrams = "Diagrams"
        sec_participants = "Every participant in the system"
        sec_errors = "Error branches summary"
        foot = ("*The AIVES Sequence Diagram set · Member 5 · "
                "[All on one page](../all-diagrams{all_suffix}.md)*")
        page_label = "Page"
        source_label = "Source"

    md = TPL.format(title=title, subtitle=subtitle, diagram_rows=diagram_rows,
                    participant_rows=part_rows, error_rows=err_rows,
                    sec_diagrams=sec_diagrams, sec_participants=sec_participants,
                    sec_errors=sec_errors, foot=foot.format(all_suffix="" if vi else "-en"),
                    page_label=page_label, source_label=source_label)
    name = "sequence-index.md" if vi else "sequence-index-en.md"
    (OUT / name).write_text(md, encoding="utf-8")
    print("wrote", name)


if __name__ == "__main__":
    build("vi")
    build("en")
