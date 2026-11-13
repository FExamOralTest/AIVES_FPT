#!/usr/bin/env python3
"""Check that every relative link inside the sequence folder resolves to a real file.

Usage:  python docs/architecture/sequence/_check_links.py
"""
import pathlib
import re
import sys

D = pathlib.Path(__file__).parent
bad = []

files = sorted(list(D.glob("*.html")) + list(D.glob("*.md")))
for f in files:
    txt = f.read_text(encoding="utf-8")
    links = (set(re.findall(r"\]\(\./([^)#]+)\)", txt))
             | set(re.findall(r'href="\./([^"#]+)"', txt))
             | set(re.findall(r'src="\./([^"]+)"', txt)))
    for l in links:
        if not (D / l).exists():
            bad.append(f"{f.name} -> {l}")

print("BROKEN:" if bad else "ALL LINKS OK")
for b in bad:
    print(" ", b)
sys.exit(1 if bad else 0)
