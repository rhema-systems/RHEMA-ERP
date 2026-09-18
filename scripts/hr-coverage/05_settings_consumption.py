"""
05 — settings consumption: find settings that are saved, editable, and read by NOTHING.

    python scripts/hr-coverage/05_settings_consumption.py <entity-path> <ClassName> [plumbing,files]

    python scripts/hr-coverage/05_settings_consumption.py \
        src/ErpSystem.Core/Entities/HR/CompanyHrPolicySettings.cs CompanyHrPolicySettings \
        CompanyHrPolicySettingsDTOs.cs,CompanyHrPolicyMappingExtensions.cs,\
        policy-settings.ts,settings/policy

⚠ The last fragment is the setting's own EDIT SCREEN. Pass it, or every setting looks consumed:
the screen mentions all of them, and mentioning is not consuming.

WHY THIS EXISTS
---------------
`docs/HR-CONFIGURATION-REGISTER.md` polices one rule:

    A setting that saves and is read by nothing is worse than a hardcoded constant.

A constant is honest — nobody believes it is configurable. A dead setting invites somebody to set
it, believe it binds, and discover months later that it never did. This repo has form: the appraisal
audit found 14 of 50 fields not enforcing what they claimed, and a policy-settings pass found three
more (VoluntaryRetirementAge, ReviewDueLeadDays, MinimumWorkingAge) saved and consumed by nothing.

WHAT IT PROVES, AND WHAT IT DOES NOT
------------------------------------
It reads every .cs/.ts/.tsx under src/ and frontend/src once, and for each property of the named
class records which files mention it — discarding the files that merely CARRY the value (the entity,
its DTOs, its mapper, its service, its controller, the DbContext, the TS type).

  * ZERO remaining references  -> a GHOST, definitively. Nothing can be consuming it.
  * Only its own edit screen   -> also a ghost: that screen is where you SET it, not where it acts.
  * One or more services       -> it is referenced. ⚠ That is NOT proof it is ENFORCED.

⚠ The last distinction is the one this tool cannot make. A service may read a setting and only
display it (Advisory), or the UI may honour it while the API ignores it (Client-side). Telling those
apart needs a human read, which is what the appraisal audit did by hand. **Use this to find ghosts
cheaply; classify the survivors by reading them.**

⚠ It matches on `.Property` and `.property`, so a property whose name is a common word (Name, Code,
IsActive, Description, RequiresApproval) will report hundreds of unrelated hits. Those are noise,
not evidence — read the file list, not the count.
"""
import io
import os
import re
import sys

# The Windows console defaults to cp1252, which cannot encode the warning sign this file prints.
# Reconfiguring here rather than dropping to ASCII: the marks are how every other instrument and
# document in this repo flags the thing worth reading twice.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except (AttributeError, OSError):  # pragma: no cover - very old Python, or a redirected stream
    pass

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SKIP_DIRS = {"node_modules", "bin", "obj", ".next", ".git", "Migrations", "__pycache__"}

# Audit columns every TenantEntity carries; never interesting here.
BASE_COLUMNS = {
    "Id", "TenantId", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy",
    "CreatedById", "LastModifiedById", "IsDeleted", "DeletedAt", "DeletedBy",
}


def survey(entity_rel, class_name, extra_plumbing=()):
    entity_path = os.path.join(ROOT, entity_rel.replace("/", os.sep))
    text = io.open(entity_path, encoding="utf-8").read()

    # Only the named class, not the whole file — these entity files hold several.
    start = text.index("class " + class_name)
    nxt = text.find("\npublic class ", start + 1)
    body = text[start: nxt if nxt > 0 else len(text)]

    props = [
        p for p in re.findall(
            r"public\s+(?:virtual\s+)?[\w<>?\[\],\s]+?\s+(\w+)\s*\{\s*get;\s*set;\s*\}", body)
        if p not in BASE_COLUMNS
    ]
    camel = {p: p[0].lower() + p[1:] for p in props}

    # ⚠ Matched as PATH fragments, not basenames. `page.tsx` is a hopeless discriminator — this
    # repo has hundreds — and the one that matters here is a setting's own edit screen, which must
    # count as plumbing: that screen is where you SET a value, never where it acts.
    plumbing = [entity_rel.replace("/", os.sep)] + [e.replace("/", os.sep) for e in extra_plumbing]
    hits = {p: set() for p in props}

    scanned = 0
    for base in (os.path.join(ROOT, "src"), os.path.join(ROOT, "frontend", "src")):
        for dirpath, dirnames, filenames in os.walk(base):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                if not fn.endswith((".cs", ".ts", ".tsx")):
                    continue
                full = os.path.join(dirpath, fn)
                rel = os.path.relpath(full, ROOT)
                if any(frag in rel for frag in plumbing):
                    continue
                try:
                    content = io.open(full, encoding="utf-8", errors="ignore").read()
                except OSError:
                    continue
                scanned += 1
                for p in props:
                    if ("." + p) in content or ("." + camel[p]) in content:
                        hits[p].add(rel)

    return props, hits, scanned


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 1

    entity_rel, class_name = sys.argv[1], sys.argv[2]
    extra = sys.argv[3].split(",") if len(sys.argv) > 3 else []

    props, hits, scanned = survey(entity_rel, class_name, extra)

    print("%s — %d settings, %d files scanned\n" % (class_name, len(props), scanned))

    ghosts = []
    for p in props:
        files = sorted(hits[p])
        if not files:
            ghosts.append(p)
        shown = ", ".join(os.path.basename(f) for f in files[:5])
        print("  %-38s %-6s %s" % (p, len(files) or "GHOST", shown))

    print("\n⚠ GHOSTS — saved, editable, read by nothing: %d" % len(ghosts))
    for g in ghosts:
        print("    %s" % g)

    print("\n⚠ A non-zero count means REFERENCED, not ENFORCED. Read the survivors to tell")
    print("   Enforced from Advisory from Client-side; this tool only finds the dead ones.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
