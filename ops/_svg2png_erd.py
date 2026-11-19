#!/usr/bin/env python3
"""Rasterise the ERD SVGs to full-resolution PNGs (headless Edge, exact aspect ratio).

PlantUML's own -tpng output is capped at 4096px, which truncates wide diagrams.
Usage:
    python ops/_svg2png_erd.py
"""
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
import time

ROOT = pathlib.Path(__file__).resolve().parents[1]
ARCH = ROOT / "docs" / "architecture" / "erd"

EDGE_CANDIDATES = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
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
    svgs = sorted(ARCH.glob("*.svg"))
    if not svgs:
        raise SystemExit("No SVG found to rasterise.")

    with tempfile.TemporaryDirectory(prefix="svg2png_") as tmp:
        tmp = pathlib.Path(tmp)
        server = subprocess.Popen(
            [sys.executable, "-m", "http.server", "8898", "--bind", "127.0.0.1"],
            cwd=str(ARCH), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            time.sleep(2)
            for svg in svgs:
                w, h = svg_size(svg)
                out = tmp / (svg.stem + ".png")
                rel = svg.name
                subprocess.run(
                    [browser, "--headless", "--disable-gpu", "--hide-scrollbars",
                     "--default-background-color=FFFFFFFF",
                     f"--screenshot={out}", f"--window-size={w},{h}",
                     f"http://127.0.0.1:8898/{rel}"],
                    capture_output=True, text=True, timeout=180)
                if out.exists():
                    shutil.copy(out, svg.with_suffix(".png"))
                    print(f"{rel} -> {w}x{h}")
                else:
                    print("FAILED:", rel)
        finally:
            server.terminate()


if __name__ == "__main__":
    main()
