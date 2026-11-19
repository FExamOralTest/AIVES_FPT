import subprocess
import collections
import re

REPO = r"C:\Users\thinh\Documents\GitHub\AIVES_FPT"

MAP = {
    "Hữu": ["Ngoc Huu", "Võ Trần Ngọc Hữu", "Vo Tran Ngoc Huu"],
    "Bắc": ["Nguyen Van Bac"],
    "Hưng": ["Nguyen Do Bao Hung", "hungnguyen9999123-max"],
    "Huyền": ["huyenphuong2964.z@gmail.com", "Thu Huyen", "Thu - Huyen Nguyen"],
    "Trí": ["NguyenLeNgocTriSE", "superVT94"],
    "Thịnh": ["Alex-Broly", "thinhphse180133@fpt.edu.vn"],
}

out = subprocess.run(
    ["git", "log", "--all", "--pretty=format:%an||%s"],
    cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout

by_author = collections.defaultdict(list)
for line in out.splitlines():
    if "||" not in line:
        continue
    a, s = line.split("||", 1)
    by_author[a].append(s)

TOTAL = sum(len(v) for v in by_author.values())
print(f"TOTAL COMMITS (all branches): {TOTAL}\n")

summary = {}
for member, aliases in MAP.items():
    subs = []
    for a in aliases:
        for s in by_author.get(a, []):
            if s.startswith("Merge "):
                continue
            subs.append(s)
    summary[member] = subs
    print(f"===== {member} ({len(subs)} commits, non-merge) =====")
    for s in subs:
        print("  -", s)
    print()

print("=========== SUMMARY COUNTS ===========")
for m, subs in summary.items():
    print(f"{m}: {len(subs)}")
