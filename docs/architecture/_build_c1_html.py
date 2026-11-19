#!/usr/bin/env python3
"""Generate the standalone HTML page for the C1 Container Diagram (VI + EN separated)."""
import html
import pathlib

OUT = pathlib.Path(__file__).parent

# Bump this whenever the images change - it is appended to every image URL so
# the browser never shows a cached (stale) diagram.
ASSET_VERSION = "v13"

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
  .card img {{ max-width:100%; display:block; margin:0 auto; }}
  h2 {{ font-size:18px; color:#0f3d5c; margin:28px 0 10px; }}
  table {{ border-collapse:collapse; width:100%; font-size:13.5px; background:#fff; }}
  th,td {{ border:1px solid #dfe5ea; padding:8px 11px; text-align:left; vertical-align:top; }}
  th {{ background:#eef3f7; color:#0f3d5c; }}
  td code {{ background:#eef3f7; padding:1px 5px; border-radius:4px; font-size:12.3px; }}
  .hint {{ background:#fff8e1; border-left:5px solid #B8860B; padding:12px 16px;
           border-radius:0 8px 8px 0; font-size:13.5px; margin:0 0 20px; }}
  footer {{ margin-top:30px; font-size:12.5px; color:#5b6b7a; }}
  a {{ color:#0f3d5c; }}
</style>
</head>
<body>
<header>
  <h1>{title}</h1>
  <p>{subtitle}</p>
</header>

<nav>
  <a href="./all-diagrams{all_suffix}.html">{nav_all}</a>
  <a href="./c1-container{all_suffix}.html">{nav_c1}</a>
  <a href="./simplified/simplified-index{all_suffix}.html">{nav_sim}</a>
  <a href="./sequence/sequence-index{all_suffix}.html">{nav_seq}</a>
</nav>

<main>
  <div class="hint">{hint}</div>

  <div class="card">
    <img src="./C1_Container.svg?{asset_version}" alt="C1 Container Diagram">
  </div>

  <h2>{table_title}</h2>
  <table>
    <thead><tr><th>{t_a}</th><th>{t_b}</th></tr></thead>
    <tbody>
{rows}
    </tbody>
  </table>

  <h2>{table_title2}</h2>
  <table>
    <thead><tr><th>{t_c}</th><th>{t_d}</th></tr></thead>
    <tbody>
{rows2}
    </tbody>
  </table>

  <h2>{table_title3}</h2>
  <table>
    <thead><tr><th>{t_e}</th><th>{t_f}</th></tr></thead>
    <tbody>
{rows3}
    </tbody>
  </table>

  <footer>
    * PlantUML: <a href="./c1_container.puml">c1_container.puml</a> &middot;
      SVG: <a href="./C1_Container.svg">C1_Container.svg</a> &middot;
      PNG: <a href="./C1_Container.png">C1_Container.png</a> *
  </footer>
</main>
</body>
</html>
"""

ROWS_VI = [
    ("Web Application", "React 19 SPA + Nginx — phục vụ giao diện phòng thi, quản lý ca thi và cổng thẩm định của khảo thí; reverse proxy <code>/api</code> và <code>/uploads</code> về backend"),
    ("Backend Web API", "ASP.NET Core 8 — xác thực JWT, RBAC, điều phối ca thi, ký presigned URL; đồng thời chạy **hai Hangfire server**: <code>aives-api-*</code> (5 worker, queue <code>default</code> + <code>answers</code>) và <code>aives-grading-*</code> (1 worker, queue <code>grading</code>)"),
    ("Message Queue / Job Storage", "Hangfire lưu job trong PostgreSQL schema <code>hangfire</code> — <code>QueuePollInterval = 2s</code>, <code>InvisibilityTimeout = 10m</code>, retry 30s → 120s → 300s tối đa 3 lần (BR-17)"),
    ("AI Worker & Inference Service", "Python FastAPI — Faster-Whisper STT, TTS và chấm điểm rubric bằng LLM; chỉ nội bộ, không public ra Internet"),
    ("Relational Database", "Neon PostgreSQL — users, RBAC, đề thi, ca thi, câu trả lời (status, audio_url) và kết quả chấm"),
]

ROWS2_VI = [
    ("frontend", "React SPA build tĩnh, Nginx phục vụ trên cổng <code>:80</code> (host <code>:5173</code> khi dev)"),
    ("backend", "ASP.NET Core 8 trên <code>:8080</code> (host <code>:5000</code>), chứa Hangfire server + Swagger"),
    ("ai-service", "FastAPI / uvicorn trên <code>:8000</code>, chỉ truy cập nội bộ qua Docker DNS <code>http://ai-service:8000</code>"),
    ("postgres", "PostgreSQL 16 (profile <code>local-db</code>); mặc định dùng Neon PostgreSQL quản lý"),
]

ROWS_EN = [
    ("Web Application", "React 19 SPA + Nginx — serves the viva room, exam management and the examiner review portal; reverse-proxies <code>/api</code> and <code>/uploads</code> to the backend"),
    ("Backend Web API", "ASP.NET Core 8 — JWT auth, RBAC, session orchestration, presigned URL signing; it also hosts **two Hangfire servers**: <code>aives-api-*</code> (5 workers, queues <code>default</code> + <code>answers</code>) and <code>aives-grading-*</code> (1 worker, queue <code>grading</code>)"),
    ("Message Queue / Job Storage", "Hangfire stores jobs in the PostgreSQL schema <code>hangfire</code> — <code>QueuePollInterval = 2s</code>, <code>InvisibilityTimeout = 10m</code>, retry 30s → 120s → 300s for at most 3 attempts (BR-17)"),
    ("AI Worker & Inference Service", "Python FastAPI — Faster-Whisper STT, TTS and rubric grading with an LLM; internal only, never published to the internet"),
    ("Relational Database", "Neon PostgreSQL — users, RBAC, exams, sessions, answers (status, audio_url) and scores"),
]

ROWS2_EN = [
    ("frontend", "Static React SPA build served by Nginx on <code>:80</code> (host <code>:5173</code> in dev)"),
    ("backend", "ASP.NET Core 8 on <code>:8080</code> (host <code>:5000</code>), hosting the Hangfire servers + Swagger"),
    ("ai-service", "FastAPI / uvicorn on <code>:8000</code>, reachable only internally through the Docker DNS name <code>http://ai-service:8000</code>"),
    ("postgres", "PostgreSQL 16 (profile <code>local-db</code>); by default the managed Neon PostgreSQL is used"),
]


ROWS3_VI = [
    ("Base URL", "Nội bộ Docker DNS — <code>http://ai-service:8000</code> (không public ra Internet)"),
    ("Header xác thực", "<code>X-Internal-Token: &lt;AI_SERVICE_SECRET_KEY&gt;</code> trên mọi request <code>/ai/*</code>"),
    ("<code>POST /ai/speech/transcribe-object</code>", "Nhận <code>{ answerId, objectKey }</code>, tự kéo file từ R2 qua S3 API rồi chạy STT; trả transcript cho câu trả lời"),
    ("<code>POST /ai/evaluate/session</code>", "Chấm điểm theo rubric của ca thi, trả điểm AI + nhận xét"),
    ("<code>POST /ai/interview/follow-up</code>", "Sinh câu hỏi phỏng vấn tiếp theo dựa trên rubric & câu trả lời hiện tại"),
    ("Timeout", "<code>AI__TimeoutSeconds = 300</code> giây cho mỗi lần gọi"),
    ("Phân loại lỗi", "Retry được: <code>timeout</code> · <code>connection_error</code> · <code>internal_error</code> · <code>http_error</code>; lỗi không retry đưa vào dead-letter"),
    ("Hàng đợi", "Job STT & chấm điểm chạy nền qua Hangfire (schema <code>hangfire</code>), retry 30s → 120s → 300s, tối đa 3 lần (BR-17)"),
]

ROWS3_EN = [
    ("Base URL", "Internal Docker DNS — <code>http://ai-service:8000</code> (never published to the internet)"),
    ("Auth header", "<code>X-Internal-Token: &lt;AI_SERVICE_SECRET_KEY&gt;</code> on every <code>/ai/*</code> request"),
    ("<code>POST /ai/speech/transcribe-object</code>", "Accepts <code>{ answerId, objectKey }</code>, pulls the file from R2 via the S3 API and runs STT; returns the answer transcript"),
    ("<code>POST /ai/evaluate/session</code>", "Grades against the session rubric and returns the AI score + comments"),
    ("<code>POST /ai/interview/follow-up</code>", "Generates the next interview question from the rubric and the current answer"),
    ("Timeout", "<code>AI__TimeoutSeconds = 300</code> seconds per call"),
    ("Error taxonomy", "Retryable: <code>timeout</code> · <code>connection_error</code> · <code>internal_error</code> · <code>http_error</code>; non-retryable errors go to the dead-letter queue"),
    ("Queue", "STT and grading jobs run in the background through Hangfire (schema <code>hangfire</code>), retry 30s → 120s → 300s for at most 3 attempts (BR-17)"),
]


def build():
    for lang, rows, rows2, rows3, t in [
        ("vi", ROWS_VI, ROWS2_VI, ROWS3_VI, dict(
            title="C1 – Sơ đồ Container (multi-container)",
            subtitle="Thành viên 5 · C4 Container Diagram · Bản tiếng Việt",
            nav_all="← Tất cả sơ đồ", nav_c1="C1 Container", nav_sim="Bộ rút gọn", nav_seq="Sequence Diagram",
            hint="<strong>C1 Container</strong> cho thấy AIVES được deploy thành <strong>4 container</strong> "
                 "(frontend · backend · ai-service · postgres) cùng các dịch vụ bên ngoài "
                 "(Neon PostgreSQL · Cloudflare R2 · LLM). Phần ghi chú mô tả <strong>hàng đợi Hangfire</strong> "
                 "và <strong>giao thức nội bộ Backend ↔ AI</strong>.",
            table_title="Container & trách nhiệm",
            table_title2="Topology Docker Compose",
            table_title3="Giao thức nội bộ Backend ↔ AI",
            t_a="Container", t_b="Trách nhiệm / Cấu hình",
            t_c="Service", t_d="Cổng & ghi chú",
            t_e="Hạng mục", t_f="Chi tiết",
        )),
        ("en", ROWS_EN, ROWS2_EN, ROWS3_EN, dict(
            title="C1 – Container Diagram (multi-container)",
            subtitle="Member 5 · C4 Container Diagram · English version",
            nav_all="← All diagrams", nav_c1="C1 Container", nav_sim="Simplified set", nav_seq="Sequence Diagrams",
            hint="<strong>C1 Container</strong> shows AIVES deployed as <strong>4 containers</strong> "
                 "(frontend · backend · ai-service · postgres) plus the external services "
                 "(Neon PostgreSQL · Cloudflare R2 · LLM). The notes cover the <strong>Hangfire queue</strong> "
                 "and the <strong>internal Backend ↔ AI protocol</strong>.",
            table_title="Containers & responsibilities",
            table_title2="Docker Compose topology",
            table_title3="Internal Backend ↔ AI protocol",
            t_a="Container", t_b="Responsibility / configuration",
            t_c="Service", t_d="Ports & notes",
            t_e="Item", t_f="Detail",
        )),
    ]:
        body = "\n".join(f"      <tr><td><strong>{a}</strong></td><td>{b}</td></tr>" for a, b in rows)
        body2 = "\n".join(f"      <tr><td><code>{a}</code></td><td>{b}</td></tr>" for a, b in rows2)
        body3 = "\n".join(f"      <tr><td><strong>{a}</strong></td><td>{b}</td></tr>" for a, b in rows3)
        suffix = "" if lang == "vi" else "-en"
        page = TEMPLATE.format(
            lang=lang, asset_version=ASSET_VERSION, all_suffix=suffix,
            rows=body, rows2=body2, rows3=body3, **t)
        name = "c1-container.html" if lang == "vi" else "c1-container_en.html"
        (OUT / name).write_text(page, encoding="utf-8")
        print("wrote", name)


if __name__ == "__main__":
    build()
