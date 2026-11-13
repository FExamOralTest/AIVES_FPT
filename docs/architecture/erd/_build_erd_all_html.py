#!/usr/bin/env python3
"""Build a single self-contained page per language for the AIVES ERD suite.

Mirrors the structure of docs/architecture/_build_all_html.py but only for the
three ERD levels, so the ERD deliverable can be shared on its own.

Usage:
    python docs/architecture/erd/_build_erd_all_html.py
"""
import pathlib

OUT = pathlib.Path(__file__).parent

ERD = [
    dict(key="erd1",
         name_vi="ERD 1 · Sơ đồ ERD khái niệm (Conceptual)",
         name_en="ERD 1 · Conceptual ERD",
         meta_vi="20 thực thể · 4 nhóm nghiệp vụ · 1149×1071",
         meta_en="20 entities · 4 business areas · 1008×935",
         svg_vi="ERD_01_CONCEPTUAL.svg", png_vi="ERD_01_CONCEPTUAL.png",
         svg_en="ERD_01_CONCEPTUAL_EN.svg", png_en="ERD_01_CONCEPTUAL_EN.png",
         puml_vi="erd_01_conceptual.puml", puml_en="erd_01_conceptual_en.puml",
         page_vi="erd-01-conceptual.html", page_en="erd-01-conceptual_en.html",
         md_vi="erd-01-conceptual.md", md_en="erd-01-conceptual_en.md",
         desc_vi="Mức khái niệm: thực thể nghiệp vụ và quan hệ giữa chúng — không thuộc tính, không khóa, không kiểu dữ liệu.",
         desc_en="Conceptual level: business entities and their relationships — no attributes, no keys, no data types."),
    dict(key="erd2",
         name_vi="ERD 2 · Sơ đồ ERD logic (Logical)",
         name_en="ERD 2 · Logical ERD",
         meta_vi="21 quan hệ 3NF · PK/FK · ràng buộc · 2710×2619",
         meta_en="21 3NF relations · PK/FK · constraints · 2751×2645",
         svg_vi="ERD_02_LOGICAL.svg", png_vi="ERD_02_LOGICAL.png",
         svg_en="ERD_02_LOGICAL_EN.svg", png_en="ERD_02_LOGICAL_EN.png",
         puml_vi="erd_02_logical.puml", puml_en="erd_02_logical_en.puml",
         page_vi="erd-02-logical.html", page_en="erd-02-logical_en.html",
         md_vi="erd-02-logical.md", md_en="erd-02-logical_en.md",
         desc_vi="Mức logic: quan hệ đã chuẩn hóa 3NF kèm thuộc tính, khóa chính, khóa ngoại và quy tắc nghiệp vụ.",
         desc_en="Logical level: 3NF-normalised relations with attributes, primary keys, foreign keys and business rules."),
    dict(key="erd3",
         name_vi="ERD 3 · Sơ đồ ERD vật lý (Physical)",
         name_en="ERD 3 · Physical ERD",
         meta_vi="21 bảng Neon · kiểu Postgres · index · CHECK · 3446×1877",
         meta_en="21 Neon tables · Postgres types · indexes · CHECK · 3450×1877",
         svg_vi="ERD_03_PHYSICAL.svg", png_vi="ERD_03_PHYSICAL.png",
         svg_en="ERD_03_PHYSICAL_EN.svg", png_en="ERD_03_PHYSICAL_EN.png",
         puml_vi="erd_03_physical.puml", puml_en="erd_03_physical_en.puml",
         page_vi="erd-03-physical.html", page_en="erd-03-physical_en.html",
         md_vi="erd-03-physical.md", md_en="erd-03-physical_en.md",
         desc_vi="Mức vật lý: ánh xạ 1:1 với schema Neon thật — kiểu dữ liệu, UNIQUE, FK kèm ON DELETE, CHECK constraint.",
         desc_en="Physical level: 1:1 with the live Neon schema — data types, UNIQUE, FK with ON DELETE rules, CHECK constraints."),
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

  main {{ padding:22px 32px 60px; max-width:1500px; }}
  .banner {{ background:#fff; border:1px solid var(--line); border-left:5px solid var(--brand);
             border-radius:8px; padding:16px 20px; margin-bottom:26px; font-size:14px; line-height:1.7; }}

  .sec-head {{ display:flex; align-items:baseline; gap:12px; margin:34px 0 16px; }}
  .sec-head h2 {{ margin:0; font-size:20px; color:var(--brand); }}
  .sec-head .note {{ font-size:13px; color:#5b6b7a; }}
  .tag {{ display:inline-block; padding:3px 10px; border-radius:99px; background:#e3edf5;
          color:var(--brand); font-size:12px; font-weight:700; letter-spacing:.3px; }}
  .tag.warn {{ background:#fdf0d8; color:#8a6410; }}

  .grid {{ display:grid; grid-template-columns:repeat(auto-fill,minmax(400px,1fr)); gap:18px; }}
  .card {{ background:#fff; border:1px solid var(--line); border-radius:8px; padding:18px;
           display:flex; flex-direction:column; }}
  .card h3 {{ margin:0 0 6px; font-size:17px; color:var(--brand); }}
  .card .meta {{ font-size:12.5px; color:#5b6b7a; margin-bottom:8px; }}
  .card .desc {{ font-size:13.5px; color:#3c5b73; line-height:1.6; margin-bottom:12px; }}
  .card .thumb {{ width:100%; height:260px; overflow:hidden; border:1px solid #eceff2; border-radius:6px;
                   background:#fbfcfd; display:flex; align-items:flex-start; justify-content:center; cursor:zoom-in; }}
  .card .thumb img {{ width:100%; height:260px; object-fit:contain; display:block; }}
  .card .links {{ margin-top:12px; font-size:13px; }}
  .card .links a {{ margin-right:12px; font-weight:600; text-decoration:none; }}
  .card .links a:hover {{ text-decoration:underline; }}
  .card .links a.pri {{ background:var(--brand); color:#fff; padding:4px 12px; border-radius:5px; }}

  table {{ border-collapse:collapse; width:100%; font-size:13.5px; background:#fff; }}
  th,td {{ border:1px solid var(--line); padding:8px 10px; text-align:left; vertical-align:top; }}
  th {{ background:#eef3f7; color:var(--brand); }}
  code {{ background:#eef3f7; padding:1px 5px; border-radius:4px; font-size:12.5px; }}

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
  <a href="#diagrams">{nav_diag}</a>
  <a href="#levels">{nav_levels}</a>
  <a href="#files">{nav_files}</a>
  <a class="lang" href="./{lang_link}.html">{lang_switch}</a>
</div>

<main>
  <div class="banner">{banner}</div>

  <section id="diagrams">
    <div class="sec-head">
      <span class="tag">{tag_diag}</span>
      <h2>{h_diag}</h2>
      <span class="note">{n_diag}</span>
    </div>
    <div class="grid">
{erd_cards}
    </div>
  </section>

  <section id="levels">
    <div class="sec-head">
      <h2>{h_levels}</h2>
    </div>
    <table>
      <thead><tr><th>{th_level}</th><th>{th_scope}</th><th>{th_answer}</th></tr></thead>
      <tbody>
{level_rows}
      </tbody>
    </table>
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

<footer>AIVES &ndash; Member 5 deliverable &middot; ERD suite &middot; {foot}</footer>

<script>
  const lb = document.getElementById('lb'), lbImg = document.getElementById('lbImg');
  document.querySelectorAll('.thumb').forEach(t => t.addEventListener('click', () => {{
    lbImg.src = t.dataset.full; lb.classList.add('on');
  }}));
  lb.addEventListener('click', () => lb.classList.remove('on'));
  document.getElementById('lbClose').addEventListener('click', () => lb.classList.remove('on'));
  document.addEventListener('keydown', e => {{ if (e.key === 'Escape') lb.classList.remove('on'); }});
</script>
</body>
</html>
"""

MD_PAGE = """# {title}

> {subtitle}

{diag_md}
## {h_levels}

| {th_level} | {th_scope} | {th_answer} |
|---|---|---|
{level_rows}

---

## {h_files}

| {t_file} | {t_what} | {t_use} |
|---|---|---|
{files_rows}

---

*Generated from `docs/architecture/erd/` · [Trang HTML](./{lang_link}.html)*
"""


def card(d, lang):
    suffix = "_vi" if lang == "vi" else "_en"
    name = d[f"name_{lang}"]
    meta = d[f"meta_{lang}"]
    desc = d[f"desc_{lang}"]
    open_label = "Mở trang" if lang == "vi" else "Open page"
    return f"""    <div class="card">
      <h3>{name}</h3>
      <div class="meta">{meta}</div>
      <div class="desc">{desc}</div>
      <div class="thumb" data-full="./{d[f'png{suffix}']}"><img src="./{d[f'svg{suffix}']}" alt="{name}"></div>
      <div class="links">
        <a class="pri" href="./{d[f'page{suffix}']}">{open_label}</a>
        <a href="./{d[f'md{suffix}']}">MD</a>
        <a href="./{d[f'png{suffix}']}" download>PNG</a>
        <a href="./{d[f'svg{suffix}']}" download>SVG</a>
        <a href="./{d[f'puml{suffix}']}" target="_blank">PUML</a>
      </div>
    </div>"""


FILES = [
    ("docs/architecture/erd/erd_0*.puml", "PlantUML source (tự chứa, render offline)",
     "Render lại 3 sơ đồ ERD"),
    ("docs/architecture/erd/ERD_*.svg", "Ảnh vector, co giãn vô hạn",
     "Nhét vào PowerPoint / slide"),
    ("docs/architecture/erd/ERD_*.png", "Ảnh raster full-resolution (render từ SVG)",
     "Tài liệu cần ảnh bitmap"),
    ("docs/architecture/erd/erd-*.md", "Tài liệu markdown kèm bảng thực thể & ràng buộc",
     "Đọc / in phần giải thích"),
    ("ops/tools/dump_neon_schema.py", "Script dump schema thật từ Neon",
     "Sinh lại sơ đồ mức vật lý"),
]
FILES_EN = [
    ("docs/architecture/erd/erd_0*.puml", "PlantUML source (self-contained, renders offline)",
     "Regenerating the three ERD diagrams"),
    ("docs/architecture/erd/ERD_*.svg", "Vector image, infinitely scalable",
     "Dropping into PowerPoint / slides"),
    ("docs/architecture/erd/ERD_*.png", "Full-resolution raster (rendered from the SVG)",
     "Documents that need a raster image"),
    ("docs/architecture/erd/erd-*.md", "Markdown documentation with entity & constraint tables",
     "Reading / printing the explanation"),
    ("ops/tools/dump_neon_schema.py", "Script that dumps the live Neon schema",
     "Regenerating the physical level"),
]


def build(lang):
    vi = lang == "vi"
    cards = "\n".join(card(d, lang) for d in ERD)
    levels = LEVELS_VI if vi else LEVELS_EN
    level_rows = "\n".join(
        f"      <tr><td>{a}</td><td>{b}</td><td>{c}</td></tr>" for a, b, c in levels)
    files = FILES if vi else FILES_EN
    files_rows = "\n".join(
        f"      <tr><td><code>{a}</code></td><td>{b}</td><td>{c}</td></tr>" for a, b, c in files)

    if vi:
        t = dict(
            title="AIVES – Toàn bộ ERD (3 mức)",
            subtitle="Thành viên 5 · Bộ 3 sơ đồ ERD: Conceptual · Logical · Physical",
            badge="3 mức ERD · 21 bảng Neon · 2 ngôn ngữ",
            nav_diag="Sơ đồ", nav_levels="Ba mức ERD", nav_files="File nguồn",
            lang_switch="English →", lang_link="all-erd-en",
            banner=("<strong>Bộ ERD được tách riêng.</strong> Mỗi mức trả lời một câu hỏi khác nhau: "
                    "<strong>Conceptual</strong> — hệ thống lưu những gì nào; "
                    "<strong>Logical</strong> — cấu trúc chuẩn hóa thế nào; "
                    "<strong>Physical</strong> — schema PostgreSQL/Neon thật trông ra sao. "
                    "<strong>Bấm vào ảnh</strong> để xem kích thước đầy đủ."),
            tag_diag="ERD", h_diag="Ba sơ đồ ERD", n_diag="Conceptual · Logical · Physical",
            h_levels="Ba mức ERD và ý nghĩa",
            th_level="Mức", th_scope="Phạm vi", th_answer="Câu hỏi trả lời",
            h_files="File nguồn & cách dùng",
            t_file="File", t_what="Nội dung", t_use="Dùng để",
            foot="Bản tiếng Việt",
        )
    else:
        t = dict(
            title="AIVES – All ERDs (3 levels)",
            subtitle="Member 5 · The three ERD levels: Conceptual · Logical · Physical",
            badge="3 ERD levels · 21 Neon tables · 2 languages",
            nav_diag="Diagrams", nav_levels="The three levels", nav_files="Files",
            lang_switch="← Tiếng Việt", lang_link="all-erd",
            banner=("<strong>The ERD suite, split out on its own.</strong> Each level answers a different "
                    "question: <strong>Conceptual</strong> — what does the system store; "
                    "<strong>Logical</strong> — how is the structure normalised; "
                    "<strong>Physical</strong> — what does the real Neon schema look like. "
                    "<strong>Click any image</strong> to view it at full size."),
            tag_diag="ERD", h_diag="The three ERD diagrams", n_diag="Conceptual · Logical · Physical",
            h_levels="The three ERD levels",
            th_level="Level", th_scope="Scope", th_answer="Question answered",
            h_files="Source files & how to use them",
            t_file="File", t_what="Content", t_use="Used for",
            foot="English version",
        )

    html = PAGE.format(lang=lang, erd_cards=cards, level_rows=level_rows,
                       files_rows=files_rows, **t)
    name = "all-erd.html" if vi else "all-erd-en.html"
    (OUT / name).write_text(html, encoding="utf-8")
    print("wrote", name)

    diag_md = []
    for d in ERD:
        suffix = "_vi" if vi else "_en"
        diag_md.append(
            f"### [{d[f'name_{lang}']}](./{d[f'page{suffix}']})\n\n"
            f"![{d[f'name_{lang}']}](./{d[f'svg{suffix}']})\n\n"
            f"{d[f'desc_{lang}']}\n\n"
            f"- Mức/Level: **{'Conceptual' if d['key'] == 'erd1' else 'Logical' if d['key'] == 'erd2' else 'Physical'}**"
            f" · Kích thước/size: `{d[f'meta_{lang}'].split('·')[-1].strip()}`\n"
            f"- Nguồn/Source: [`{d[f'puml{suffix}']}`](./{d[f'puml{suffix}']})\n"
        )
    level_md = "\n".join(f"| {a} | {b} | {c} |" for a, b, c in levels)
    files_md = "\n".join(f"| `{a}` | {b} | {c} |" for a, b, c in files)
    md = MD_PAGE.format(
        title=t["title"].replace("–", "-"), subtitle=t["subtitle"],
        diag_md="\n".join(diag_md) + "\n",
        h_levels=t["h_levels"], th_level=t["th_level"], th_scope=t["th_scope"],
        th_answer=t["th_answer"], level_rows=level_md,
        h_files=t["h_files"], t_file=t["t_file"], t_what=t["t_what"],
        t_use=t["t_use"], files_rows=files_md,
        lang_link="all-erd" if vi else "all-erd-en",
    )
    md_name = "all-erd.md" if vi else "all-erd-en.md"
    (OUT / md_name).write_text(md, encoding="utf-8")
    print("wrote", md_name)


if __name__ == "__main__":
    build("vi")
    build("en")
