"""Validate relative links inside the root-level diagram-*.md copies."""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[1]
TARGETS = sorted(ROOT.glob("diagram-*.md"))

bad = []
for f in TARGETS:
    txt = f.read_text(encoding="utf-8")
    links = set(re.findall(r"\]\((\.\.?/[^)#]+)\)", txt))
    for l in links:
        if not (ROOT / l).resolve().exists():
            bad.append(f"{f.name} -> {l}")

print("BROKEN:" if bad else "ALL LINKS OK")
for b in bad:
    print(" ", b)
sys.exit(1 if bad else 0)
