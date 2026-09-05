"""
Instrument 2 — leaf-segment URL-token coverage.

For every HR write endpoint, take the leaf literal segment that identifies the
sub-resource or action (duty-items, ppe-requirements, bank-details, set-primary...)
and ask whether that token ever appears in frontend source *in a URL position* —
quoted, or preceded/followed by a slash. This is independent of how the client
assembles the URL (template literal, helper call, constant), so it survives the
path-builder pattern that fooled instrument 1.

Only endpoints flagged by BOTH instruments are reported.
"""
import os, re, json, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
SP = os.path.join(HERE, "out")
os.makedirs(SP, exist_ok=True)
os.chdir(ROOT)

routes = json.load(open(os.path.join(SP, "hr_routes.json")))
unwired = json.load(open(os.path.join(SP, "unwired.json")))

hay = []
for dp, dns, fns in os.walk("frontend/src"):
    dns[:] = [d for d in dns if d not in ("node_modules", ".next")]
    for fn in fns:
        if fn.endswith((".ts", ".tsx")) and ".test." not in fn:
            hay.append(open(os.path.join(dp, fn), encoding="utf-8", errors="replace").read())
HAY = "\n".join(hay)
HAYL = HAY.lower()

_cache = {}
def url_token_present(seg):
    """Does `seg` appear anywhere in frontend source in a URL-ish position?"""
    s = seg.lower()
    if s in _cache:
        return _cache[s]
    pat = re.compile(r"""(?:['"`/])""" + re.escape(s) + r"""(?:['"`/]|\$\{)""")
    hit = bool(pat.search(HAYL))
    _cache[s] = hit
    return hit

def leaf(route):
    """Leaf literal segment — the one naming the sub-resource or action."""
    segs = [s for s in route.split("/") if s and not s.startswith("{")]
    return segs[-1] if segs else None

rows = []
for r in routes:
    if r["verb"] == "Get":
        continue
    lf = leaf(r["route"])
    rows.append(dict(r, leaf=lf, leaf_absent=bool(lf) and not url_token_present(lf)))

json.dump(rows, open(os.path.join(SP, "instrument2.json"), "w"), indent=0)

i1 = {(u["file"], u["verb"], u["route"]) for u in unwired}
agree = [r for r in rows if (r["file"], r["verb"], r["route"]) in i1 and r["leaf_absent"]]
only1 = [r for r in rows if (r["file"], r["verb"], r["route"]) in i1 and not r["leaf_absent"]]
only2 = [r for r in rows if (r["file"], r["verb"], r["route"]) not in i1 and r["leaf_absent"]]

print("write endpoints examined: %d" % len(rows))
print("  BOTH instruments say unreachable (high confidence): %d" % len(agree))
print("  route-matching only (leaf token IS present -> needs eyes): %d" % len(only1))
print("  literal-coverage only (route matched but leaf absent -> odd): %d" % len(only2))

aw = collections.Counter(r["file"] for r in routes if r["verb"] != "Get")
byfile = collections.Counter(r["file"] for r in agree)
print("\n=== HIGH CONFIDENCE — both instruments agree ===")
for f, n in byfile.most_common():
    print("\n  %s   %d of %d writes" % (f, n, aw[f]))
    seenr = set()
    for r in sorted([x for x in agree if x["file"] == f], key=lambda x: x["route"]):
        rt = re.sub(r"\{[^}]*\}", "{}", r["route"])
        if (r["verb"], rt) in seenr:
            continue
        seenr.add((r["verb"], rt))
        print("      %-6s %s" % (r["verb"].upper(), rt))
