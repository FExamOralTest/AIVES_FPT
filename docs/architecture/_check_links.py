import pathlib, re, sys
d = pathlib.Path(__file__).parent
bad = []
for f in sorted(list(d.glob("swimlane-*.html")) + list(d.glob("swimlane-*.md"))):
    txt = f.read_text(encoding="utf-8")
    links = set(re.findall(r"\]\(\./([^)#]+)\)", txt)) | set(re.findall(r'href="\./([^"#]+)"', txt)) | set(re.findall(r'src="\./([^"]+)"', txt))
    for l in links:
        if not (d / l).exists():
            bad.append(f"{f.name} -> {l}")
print("BROKEN:" if bad else "ALL LINKS OK")
for b in bad:
    print(" ", b)
sys.exit(1 if bad else 0)
