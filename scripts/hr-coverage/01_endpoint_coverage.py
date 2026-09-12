import os, re, json, collections
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
os.chdir(ROOT)
SP = os.path.join(HERE, "out")
os.makedirs(SP, exist_ok=True)

# ---------------- BACKEND: every HR write endpoint ----------------
CTRL_DIR = "src/ErpSystem.Api/Controllers/HR"
ROUTE_ATTR = re.compile(r'^\s*\[Route\("([^"]+)"\)\]', re.M)
CLASS_DECL = re.compile(r'^(?P<indent>\s*)(?:public\s+|internal\s+)?(?P<abstract>abstract\s+)?'
                        r'(?:sealed\s+)?(?:partial\s+)?class\s+(?P<name>\w+?)(?:Controller)?\b\s*:',
                        re.M)


def class_spans(text):
    """
    Every controller class in the file, with the character span it owns and the [Route] attributes
    declared on IT rather than on a sibling.

    ⚠ Two HR files hold two routed controllers each (`StaffDisciplineLookupController.cs` and
    `CompetencyController.cs`). Crossing every route in the file with every action in it invented
    a phantom route for each real one — `api/discipline/action-types/{id}/procedures` was reported
    for months and answers 404. Area 9 slice 10 proved it. Attribute per class instead.
    """
    decls = list(CLASS_DECL.finditer(text))
    out = []
    for i, m in enumerate(decls):
        start = m.start()
        end = decls[i + 1].start() if i + 1 < len(decls) else len(text)
        # The attributes sit above the declaration, after the previous class's span.
        attr_from = decls[i - 1].start() if i else 0
        header = text[attr_from:start]
        bases = [r.group(1).replace("[controller]", m.group("name"))
                 for r in ROUTE_ATTR.finditer(header)]
        out.append({"name": m.group("name"), "abstract": bool(m.group("abstract")),
                    "start": start, "end": end, "bases": bases})
    return out


routes, skipped = [], []
for fn in sorted(os.listdir(CTRL_DIR)):
    if not fn.endswith(".cs"):
        continue
    src = open(os.path.join(CTRL_DIR, fn), encoding="utf-8", errors="replace").read()
    spans = class_spans(src)
    if not spans:
        skipped.append((fn, "no class")); continue
    if all(s["abstract"] for s in spans):
        skipped.append((fn, "abstract base")); continue
    for span in spans:
        if span["abstract"]:
            continue
        body = src[span["start"]:span["end"]]
        bases = span["bases"]
        for mm in re.finditer(r'\[Http(Post|Put|Patch|Delete|Get)(?:\("([^"]*)"\))?\]', body):
            verb, sub = mm.group(1), (mm.group(2) or "")
            line = src[:span["start"] + mm.start()].count("\n") + 1
            if not bases:
                fulls = [sub]
            elif sub.startswith(("/", "~/")):
                fulls = [sub.lstrip("~").lstrip("/")]
            else:
                fulls = [b + ("/" + sub if sub else "") for b in bases]
            for full in fulls:
                routes.append({"file": fn, "verb": verb, "route": re.sub(r"/+", "/", full),
                               "bases": bases, "line": line})

# ---------------- FRONTEND: every HTTP call site ----------------
FE = "frontend/src"
DECL = re.compile(r"(?:private\s+readonly\s+|private\s+|const\s+|readonly\s+)(\w+)\s*(?::\s*string\s*)?=\s*['\"]([^'\"]+)['\"]")
CALL = re.compile(r"\b(?:apiService|apiClient|api)\.(get|post|put|patch|delete)\s*(?:<[^(]*?>)?\s*\(\s*(`[^`]*`|'[^']*'|\"[^\"]*\"|this\.\w+|\w+)")
REQ = re.compile(r"\bapiRequest\s*(?:<[^(]*?>)?\s*\(\s*(`[^`]*`|'[^']*'|\"[^\"]*\"|this\.\w+|\w+)([\s\S]{0,220})")
calls, unresolved = [], 0
for dp, dns, fns in os.walk(FE):
    dns[:] = [d for d in dns if d not in ("node_modules", ".next")]
    for fn in fns:
        if not fn.endswith((".ts", ".tsx")) or ".test." in fn:
            continue
        p = os.path.join(dp, fn).replace("\\", "/")
        src = open(p, encoding="utf-8", errors="replace").read()
        decls = {}
        for m in DECL.finditer(src):
            decls.setdefault(m.group(1), []).append((m.start(), m.group(2)))

        def resolve(name, at):
            lst = decls.get(name)
            if not lst:
                return None
            prior = [v for pos, v in lst if pos < at]
            return prior[-1] if prior else lst[0][1]

        def expand(raw, at):
            global unresolved
            if raw.startswith(("`", "'", '"')):
                u = raw[1:-1]
                u = re.sub(r"\$\{(?:this\.)?(\w+)\}", lambda mm: resolve(mm.group(1), at) or "{}", u)
                return re.sub(r"\$\{[^}]*\}", "{}", u)
            u = resolve(raw.split(".")[-1], at)
            if u is None:
                unresolved += 1
            return u

        for m in CALL.finditer(src):
            u = expand(m.group(2), m.start())
            if u:
                calls.append({"file": p, "verb": m.group(1).upper(), "url": u,
                              "line": src[:m.start()].count("\n") + 1})
        for m in REQ.finditer(src):
            u = expand(m.group(1), m.start())
            if not u:
                continue
            vm = re.search(r"method\s*:\s*['\"](\w+)['\"]", m.group(2))
            calls.append({"file": p, "verb": (vm.group(1).upper() if vm else "GET"), "url": u,
                          "line": src[:m.start()].count("\n") + 1})

# ---------------- DIFF ----------------
def norm(u, add_api=False):
    u = u.split("?")[0].strip().strip("/")
    if add_api and not u.lower().startswith("api/") and u.lower() != "api":
        u = "api/" + u
    out = []
    for s in u.split("/"):
        if not s:
            continue
        out.append("*" if (s.startswith("{") or s == "*") else re.sub(r"[-_]", "", s.lower()))
    return "/".join(out)

fe_exact, fe_path, fe_blob = collections.defaultdict(list), collections.defaultdict(set), set()
for c in calls:
    p = norm(c["url"], True)
    fe_exact[(c["verb"], p)].append("%s:%d" % (c["file"], c["line"]))
    fe_path[p].add(c["verb"])
    segs = p.split("/")
    for i in range(1, len(segs) + 1):
        fe_blob.add("/".join(segs[:i]))

V = {"Post": "POST", "Put": "PUT", "Patch": "PATCH", "Delete": "DELETE", "Get": "GET"}
tiers, unwired = collections.Counter(), []
for r in routes:
    if r["verb"] == "Get":
        continue
    v, p = V[r["verb"]], norm(r["route"])
    bases = [norm(b) for b in r["bases"]] or ["/".join(p.split("/")[:2])]
    if (v, p) in fe_exact:
        tiers["wired"] += 1; continue
    if p in fe_path:
        tiers["verb missing"] += 1; unwired.append(dict(r, tier="verb", path=p)); continue
    if any(b in fe_blob for b in bases):
        tiers["endpoint unreached"] += 1; unwired.append(dict(r, tier="endpoint", path=p)); continue
    tiers["controller unreached"] += 1; unwired.append(dict(r, tier="controller", path=p))

print("backend write endpoints: %d | frontend call sites: %d | unresolved idents: %d"
      % (sum(tiers.values()), len(calls), unresolved))
for k, v in tiers.most_common():
    print("  %5d  %s" % (v, k))
print("\nskipped controller files: %d  %s" % (len(skipped), [s[0] for s in skipped]))

json.dump(unwired, open(os.path.join(SP, "unwired.json"), "w"), indent=0)
json.dump(calls, open(os.path.join(SP, "fe_calls.json"), "w"), indent=0)
json.dump(routes, open(os.path.join(SP, "hr_routes.json"), "w"), indent=0)

aw = collections.Counter(r["file"] for r in routes if r["verb"] != "Get")
byfile = collections.defaultdict(list)
for u in unwired:
    byfile[u["file"]].append(u)
print("\n=== controllers with NO write endpoint wired ===")
for f, n, t in sorted([(f, len(v), aw[f]) for f, v in byfile.items() if len(v) == aw[f]], key=lambda x: -x[1]):
    print("  %3d/%-3d %s" % (n, t, f))
print("\n=== partially wired ===")
part = sorted([(f, len(v), aw[f]) for f, v in byfile.items() if len(v) < aw[f]], key=lambda x: -x[1])
for f, n, t in part[:35]:
    print("  %3d/%-3d %s" % (n, t, f))
print("  ... partially-wired controllers total: %d" % len(part))
