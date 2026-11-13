#!/usr/bin/env python3
"""Rasterise the sequence-diagram SVGs to full-resolution PNGs (headless Edge).

PlantUML's own -tpng output is capped at 4096px (PLANTUML_LIMIT_SIZE), which
truncates the tall sequence diagrams. Rendering the SVG with headless Edge
keeps the exact aspect ratio at full size.

Usage:
    python ops/_svg2png_sequence.py
"""
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
import time

ROOT = pathlib.Path(__file__).resolve().parents[1]
ARCH = ROOT / "docs" / "architecture"
SEQ = ARCH / "sequence"

EDGE_CANDIDATES = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    shutil.which("msedge") or "",
    shutil.which("chrome") or "",
]


def find_browser():
    for p in EDGE_CANDIDATES:
        if p and pathlib.Path(p).exists():
            return p
    raise SystemExit("No Chromium-based browser found for SVG -> PNG rasterisation.")


def svg_size(p: pathlib.Path):
    t = p.read_text(encoding="utf-8", errors="ignore")
    m = re.search(r"<svg[^>]*>", t)
    w = re.search(r'width="([\d.]+)px"', m.group(0))
    h = re.search(r'height="([\d.]+)px"', m.group(0))
    return int(float(w.group(1))), int(float(h.group(1)))


def main():
    browser = find_browser()
    svgs = sorted(SEQ.glob("SEQ_*.svg"))
    if not svgs:
        raise SystemExit("No sequence SVG found to rasterise.")

    with tempfile.TemporaryDirectory(prefix="svg2png_seq_") as tmp:
        tmp = pathlib.Path(tmp)
        server = subprocess.Popen(
            [sys.executable, "-m", "http.server", "8897", "--bind", "127.0.0.1"],
            cwd=str(SEQ), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            time.sleep(2)
            for svg in svgs:
                w, h = svg_size(svg)
                out = tmp / (svg.stem + ".png")
                subprocess.run(
                    [browser, "--headless", "--disable-gpu", "--hide-scrollbars",
                     "--default-background-color=FFFFFFFF",
                     f"--screenshot={out}", f"--window-size={w},{h}",
                     f"http://127.0.0.1:8897/{svg.name}"],
                    capture_output=True, text=True, timeout=180)
                if out.exists():
                    shutil.copy(out, svg.with_suffix(".png"))
                    print(f"{svg.name} -> {w}x{h}")
                else:
                    print("FAILED:", svg.name)
        finally:
            server.terminate()


if __name__ == "__main__":
    main()
