"""
Instrument 3 - write-DTO field coverage.

Endpoint coverage (instruments 1 and 2) only proves a screen CALLS an endpoint.
It cannot see a form that calls the right endpoint but omits half the payload -
which is how a field ends up unreachable even on a wired screen.

For every Create*Dto / Update*Dto under DTOs/HR, take each settable property and
ask whether its camelCase name appears as an identifier anywhere in frontend/src.
A property never mentioned cannot be being sent.
"""
import os, re, json

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SP = os.path.join(HERE, "out")
os.makedirs(SP, exist_ok=True)
os.chdir(ROOT)

tokens = set()
nbytes = 0
for dp, dns, fns in os.walk("frontend/src"):
    dns[:] = [d for d in dns if d not in ("node_modules", ".next")]
    for fn in fns:
        if fn.endswith((".ts", ".tsx")) and ".test." not in fn:
            t = open(os.path.join(dp, fn), encoding="utf-8", errors="replace").read()
            nbytes += len(t)
            tokens.update(re.findall(r"[A-Za-z_][A-Za-z0-9_]*", t))
print("frontend: %.1f MB, %d distinct identifiers" % (nbytes / 1e6, len(tokens)), flush=True)


def camel(n):
    """Replicate JsonNamingPolicy.CamelCase: lowercase the leading uppercase run,
    but keep the last capital if it starts the next word (IPAddress -> ipAddress)."""
    if not n or not n[0].isupper():
        return n
    i = 0
    while i < len(n) and n[i].isupper():
        i += 1
    if i > 1 and i < len(n):
        i -= 1
    return n[:i].lower() + n[i:]

SKIP = {"Id", "TenantId", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy", "ModifiedAt",
        "ModifiedBy", "IsDeleted", "DeletedAt", "DeletedBy", "RowVersion", "ConcurrencyToken"}
CLASS = re.compile(r"^\s*public\s+(?:sealed\s+|partial\s+|abstract\s+)*class\s+((?:Create|Update)\w*Dto)\b")
ANYDECL = re.compile(r"^\s*public\s+(?:sealed\s+|partial\s+|abstract\s+)*(?:class|enum|record|interface)\s+")
PROP = re.compile(r"^\s*public\s+\S+\s+(\w+)\s*\{\s*get;\s*set;")

report = []
for dp, dns, fns in os.walk("src/ErpSystem.Core/DTOs/HR"):
    for fn in sorted(fns):
        if not fn.endswith(".cs"):
            continue
        state = {"dto": None, "props": []}

        def flush():
            if state["dto"] and state["props"]:
                miss = [p for p in state["props"] if camel(p) not in tokens]
                if miss:
                    report.append({"file": fn, "dto": state["dto"],
                                   "total": len(state["props"]), "missing": miss})

        for line in open(os.path.join(dp, fn), encoding="utf-8", errors="replace"):
            cm = CLASS.match(line)
            if cm:
                flush()
                state = {"dto": cm.group(1), "props": []}
                continue
            if ANYDECL.match(line):
                flush()
                state = {"dto": None, "props": []}
                continue
            if state["dto"]:
                pm = PROP.match(line)
                if pm and pm.group(1) not in SKIP:
                    state["props"].append(pm.group(1))
        flush()

report.sort(key=lambda r: -len(r["missing"]))
print("write DTOs carrying unreachable fields: %d" % len(report))
print("unreachable fields total: %d\n" % sum(len(r["missing"]) for r in report))
json.dump(report, open(os.path.join(SP, "dto_gaps.json"), "w"), indent=0)
for r in report[:45]:
    print("  %-38s %-40s %2d/%-2d" % (r["file"], r["dto"], len(r["missing"]), r["total"]))
    print("      " + ", ".join(r["missing"][:16]) + (" ..." if len(r["missing"]) > 16 else ""))
