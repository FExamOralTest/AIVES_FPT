#!/usr/bin/env python3
"""
Tổng hợp kết quả demo Nginx (Thịnh) thành bảng before/after cho báo cáo.

Đọc các file `k6-summary-<label>-<timestamp>.json` trong
monitoring/results/<ngày>/nginx-demo-*/<vUs>/ rồi xuất:

  - bảng Markdown (đưa thẳng vào báo cáo / slide)
  - bảng CSV (đưa vào Excel)
  - so sánh "trước tối ưu" (direct → 1 backend) vs
    "sau tối ưu" (nginx → Nginx + 3 backend replica)

Cách dùng:
    python monitoring/k6/summarize-nginx-results.py
    python monitoring/k6/summarize-nginx-results.py --out docs/loadtest-results.md
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
RESULTS_ROOT = os.path.abspath(os.path.join(HERE, "..", "results"))

PROFILE_ORDER = [50, 100, 200, 500]

PROFILE_LABEL = {
    50: "Load 1 — 50 users",
    100: "Load 2 — 100 users",
    200: "Load 3 — 200 users",
    500: "Spike / stress — 500 users",
}

VARIANT_LABEL = {
    "direct": "1 backend (trực tiếp)",
    "nginx": "Nginx + 3 backend replica",
}


def load_runs() -> list[dict]:
    """Đọc mọi file k6 summary của các lượt chạy demo Nginx."""
    runs: list[dict] = []
    if not os.path.isdir(RESULTS_ROOT):
        return runs

    for day in sorted(os.listdir(RESULTS_ROOT)):
        day_dir = os.path.join(RESULTS_ROOT, day)
        if not os.path.isdir(day_dir):
            continue
        for demo_dir in sorted(os.listdir(day_dir)):
            if not demo_dir.startswith("nginx-demo-"):
                continue
            demo_path = os.path.join(day_dir, demo_dir)
            if not os.path.isdir(demo_path):
                continue
            for vus_dir in sorted(os.listdir(demo_path)):
                vus_path = os.path.join(demo_path, vus_dir)
                if not os.path.isdir(vus_path) or not vus_dir.isdigit():
                    continue
                for fname in sorted(os.listdir(vus_path)):
                    m = re.match(r"^k6-summary-(direct|nginx)-(\d{8}-\d{6})\.json$", fname)
                    if not m:
                        continue
                    path = os.path.join(vus_path, fname)
                    try:
                        with open(path, encoding="utf-8") as fh:
                            data = json.load(fh)
                    except (OSError, json.JSONDecodeError):
                        continue

                    metrics = data.get("metrics", {})
                    # `--summary-export` ghi metric ở dạng phẳng:
                    #   {"metrics": {"http_req_duration": {"avg": ..., "p(95)": ...}}}
                    # (khác với stdout của handleSummary vốn dùng {"values": {...}})
                    #
                    #  Rate/counter dùng key "value";  trend dùng "avg"/"p(95)"...
                    dur = metrics.get("http_req_duration", {})
                    failed = metrics.get("http_req_failed", {})
                    checks = metrics.get("checks", {})
                    reqs = metrics.get("http_reqs", {})
                    iters = metrics.get("iterations", {})

                    runs.append({
                        "day": day,
                        "file": fname,
                        "path": os.path.relpath(path, os.path.join(HERE, "..", "..")).replace("\\", "/"),
                        "variant": m.group(1),
                        "timestamp": m.group(2),
                        "vus": int(vus_dir),
                        "requests": int(reqs.get("count", 0) or 0),
                        "throughput": reqs.get("rate", 0) or 0,
                        "iterations": int(iters.get("count", 0) or 0),
                        "avg": dur.get("avg"),
                        "p50": dur.get("med"),
                        "p90": dur.get("p(90)"),
                        "p95": dur.get("p(95)"),
                        "p99": dur.get("p(99)"),
                        "max": dur.get("max"),
                        "error_rate": failed.get("value"),
                        "check_rate": checks.get("value"),
                    })

    return runs


def fmt_ms(value) -> str:
    if value is None:
        return "—"
    return f"{value:.0f} ms"


def fmt_pct(value) -> str:
    if value is None:
        return "—"
    return f"{value * 100:.2f} %"


def latest_by_variant(runs: list[dict]) -> dict[tuple[int, str], dict]:
    """Giữ lượt chạy mới nhất cho mỗi (vUs, variant)."""
    grouped: dict[tuple[int, str], dict] = {}
    for r in runs:
        key = (r["vus"], r["variant"])
        prev = grouped.get(key)
        if prev is None or r["timestamp"] > prev["timestamp"]:
            grouped[key] = r
    return grouped


def build_table(grouped: dict[tuple[int, str], dict]) -> str:
    lines = [
        "| Profile | Cấu hình | VUs | Requests | Throughput | Avg | p95 | p99 | Error rate | Checks |",
        "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    for vus in PROFILE_ORDER:
        for variant in ("direct", "nginx"):
            r = grouped.get((vus, variant))
            if not r:
                continue
            lines.append(
                f"| {PROFILE_LABEL.get(vus, str(vus))} | {VARIANT_LABEL[variant]} | {r['vus']} "
                f"| {r['requests']} | {r['throughput']:.2f} req/s "
                f"| {fmt_ms(r['avg'])} | {fmt_ms(r['p95'])} | {fmt_ms(r['p99'])} "
                f"| {fmt_pct(r['error_rate'])} | {fmt_pct(r['check_rate'])} |"
            )
    return "\n".join(lines) + "\n"


def build_comparison(grouped: dict[tuple[int, str], dict]) -> str:
    """Bảng so sánh trước/sau tối ưu — mẫu 7.1 của kế hoạch."""
    lines = [
        "| Profile | p95 trước | p95 sau | Thay đổi | Error rate trước | Error rate sau | Ghi chú |",
        "|---|---|---|---|---|---|---|",
    ]
    for vus in PROFILE_ORDER:
        before = grouped.get((vus, "direct"))
        after = grouped.get((vus, "nginx"))
        if not before and not after:
            continue

        note = ""
        if before and after:
            if before["p95"] and after["p95"]:
                delta = (after["p95"] - before["p95"]) / before["p95"] * 100
                note = f"p95 {delta:+.1f} %"
        elif after:
            note = "chỉ có bản Nginx"
        else:
            note = "chỉ có bản trực tiếp"

        lines.append(
            f"| {PROFILE_LABEL.get(vus, str(vus))} "
            f"| {fmt_ms(before['p95']) if before else '—'} "
            f"| {fmt_ms(after['p95']) if after else '—'} "
            f"| {note} "
            f"| {fmt_pct(before['error_rate']) if before else '—'} "
            f"| {fmt_pct(after['error_rate']) if after else '—'} "
            f"| {note} |"
        )
    return "\n".join(lines) + "\n"

def build_markdown(runs: list[dict]) -> str:
    grouped = latest_by_variant(runs)
    parts = [
        "# Kết quả demo Nginx Load Balancing (Thịnh)",
        "",
        "> **Nguồn số liệu:** mọi giá trị dưới đây đến từ lượt chạy THẬT của k6 "
        "(`monitoring/k6/nginx-submit.js`) trên Docker Desktop, lưu tại "
        "`monitoring/results/<ngày>/nginx-demo-*/`.  Kịch bản: "
        "`login → start → submit-answer`, mỗi VU = 1 học sinh, 1 lần nộp bài.",
        "",
        "## 1. Cấu hình hai lượt so sánh",
        "",
        "| Nhãn | Đường đi | Số backend |",
        "|---|---|---:|",
        "| `direct` | k6 → `backend:8080` | 1 |",
        "| `nginx` | k6 → `nginx:8088` → backend-1..3 | 3 |",
        "",
        "Cả hai lượt dùng **cùng DB local PostgreSQL**, **cùng payload** "
        "(1 câu trả lời text), **cùng executor** (`per-vu-iterations`, `iterations = 1`).",
        "",
        "## 2. Test matrix — số liệu tổng hợp",
        "",
        build_table(grouped),
        "",
        "## 3. So sánh trước / sau tối ưu",
        "",
        build_comparison(grouped),
        "",
        "## 4. Các lượt chạy đã lưu",
        "",
        "| File | Ngày | VUs | Cấu hình | Requests | p95 | Error rate |",
        "|---|---|---:|---|---:|---:|---:|",
    ]
    for r in sorted(runs, key=lambda x: (x["vus"], x["variant"], x["timestamp"])):
        parts.append(
            f"| `{r['path']}` | {r['day']} | {r['vus']} | {r['variant']} "
            f"| {r['requests']} | {fmt_ms(r['p95'])} | {fmt_pct(r['error_rate'])} |"
        )
    parts.append("")
    return "\n".join(parts)


def build_csv(runs: list[dict]) -> str:
    import io
    buf = io.StringIO()
    w = csv.writer(buf)
    w.writerow([
        "date", "variant", "vus", "requests", "throughput_rps",
        "avg_ms", "p50_ms", "p90_ms", "p95_ms", "p99_ms", "max_ms",
        "error_rate", "check_rate", "file",
    ])
    for r in sorted(runs, key=lambda x: (x["vus"], x["variant"], x["timestamp"])):
        def ms(v):
            return "" if v is None else f"{v:.2f}"
        w.writerow([
            r["day"], r["variant"], r["vus"], r["requests"], f"{r['throughput']:.4f}",
            ms(r["avg"]), ms(r["p50"]), ms(r["p90"]), ms(r["p95"]), ms(r["p99"]), ms(r["max"]),
            "" if r["error_rate"] is None else f"{r['error_rate']:.6f}",
            "" if r["check_rate"] is None else f"{r['check_rate']:.6f}",
            r["path"],
        ])
    return buf.getvalue()


def main() -> int:
    parser = argparse.ArgumentParser(description="Tổng hợp kết quả demo Nginx")
    parser.add_argument(
        "--out",
        default=os.path.abspath(os.path.join(HERE, "..", "..", "docs", "nginx-loadtest-results.md")),
    )
    parser.add_argument(
        "--csv",
        default=os.path.abspath(os.path.join(HERE, "..", "results", "nginx-summary.csv")),
    )
    args = parser.parse_args()

    runs = load_runs()
    md = build_markdown(runs)

    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        fh.write(md)

    os.makedirs(os.path.dirname(args.csv), exist_ok=True)
    with open(args.csv, "w", encoding="utf-8", newline="") as fh:
        fh.write(build_csv(runs))

    print(f"wrote {args.out}")
    print(f"wrote {args.csv}")
    print(f"({len(runs)} lượt chạy)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
