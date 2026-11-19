#!/usr/bin/env python3
"""
Rasterise a PlantUML sequence SVG to a high-resolution PNG.

PlantUML's own -tpng output is capped at 4096px, and the headless-browser
rasteriser in `_svg2png.py` works at 1:1 SVG pixels - on a diagram whose
intrinsic size is under 1000px wide that leaves hairline strokes that look
faded once the image is enlarged on a slide.

This renderer paints the SVG inside a page scaled by a factor (default 2.5),
so a 944px-wide diagram becomes a 2360px-wide PNG with proportionally
thicker lines.

Usage:
    python ops/_svg2png_hi.py <svg> [<svg> ...] [--scale 2.5]
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
    raise SystemExit("No Chromium-based browser found.")


def svg_size(p: pathlib.Path):
    t = p.read_text(encoding="utf-8", errors="ignore")
    head = re.search(r"<svg[^>]*>", t).group(0)
    w = float(re.search(r'width="([\d.]+)px"', head).group(1))
    h = float(re.search(r'height="([\d.]+)px"', head).group(1))
    return w, h


def main():
    args = sys.argv[1:]
    scale = 2.5
    if "--scale" in args:
        i = args.index("--scale")
        scale = float(args[i + 1])
        del args[i:i + 2]
    targets = [pathlib.Path(a) for a in args]
    if not targets:
        targets = sorted((ARCH / "simplified").glob("DIAGRAM_05*.svg"))
    for t in list(targets):
        if not t.is_absolute():
            targets[targets.index(t)] = (ROOT / t).resolve()
    targets = [t for t in targets if t.exists()]
    browser = find_browser()

    with tempfile.TemporaryDirectory(prefix="svg2png_hi_") as tmp:
        tmp = pathlib.Path(tmp)
        server = subprocess.Popen(
            [sys.executable, "-m", "http.server", "8898", "--bind", "127.0.0.1"],
            cwd=str(ARCH), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            time.sleep(2)
            for svg in targets:
                w, h = svg_size(svg)
                out = tmp / (svg.stem + ".png")
                rel = svg.relative_to(ARCH).as_posix()
                # Wrap the SVG in a page so the browser scales it up as a raster
                # image - the strokes are then painted at the higher resolution.
                page = tmp / (svg.stem + ".html")
                page.write_text(
                    "<!doctype html><html><head><meta charset='utf-8'>"
                    "<style>html,body{margin:0;padding:0;background:#fff;}"
                    "img{display:block;width:%dpx;height:%dpx;}</style></head>"
                    "<body><img src='http://127.0.0.1:8898/%s'></body></html>"
                    % (round(w * scale), round(h * scale), rel),
                    encoding="utf-8")
                subprocess.run(
                    [browser, "--headless", "--disable-gpu", "--hide-scrollbars",
                     "--default-background-color=FFFFFFFF",
                     f"--screenshot={out}",
                     f"--window-size={round(w * scale)},{round(h * scale)}",
                     page.as_uri()],
                    capture_output=True, text=True, timeout=180)
                if out.exists():
                    shutil.copy(out, svg.with_suffix(".png"))
                    print(f"{rel} -> {round(w * scale)}x{round(h * scale)} (scale {scale}x)")
                else:
                    print("FAILED:", rel)
        finally:
            server.terminate()


if __name__ == "__main__":
    main()
