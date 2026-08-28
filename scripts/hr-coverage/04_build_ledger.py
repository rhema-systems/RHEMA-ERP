"""
Builds docs/HR-CLOSURE-LEDGER.md — the checkable master list for the HR Closure Programme.

Machine-derived sections come from the instrument output so nothing is hand-missed; the
disposition column is authored here (DISPOSITIONS below) because only a human can say whether
an unreachable endpoint is a gap or a deliberate boundary.

Run 01, 02 and 03 first, then this.
"""
import os, re, json, collections

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
OUT = os.path.join(HERE, "out")
LEDGER = os.path.join(ROOT, "docs", "HR-CLOSURE-LEDGER.md")

routes = json.load(open(os.path.join(OUT, "hr_routes.json")))
unwired = json.load(open(os.path.join(OUT, "unwired.json")))
inst2 = json.load(open(os.path.join(OUT, "instrument2.json")))
dto_gaps = json.load(open(os.path.join(OUT, "dto_gaps.json")))

UNWIRED_KEYS = {(u["file"], u["verb"], u["route"]) for u in unwired}
HIGH = [r for r in inst2 if r["leaf_absent"] and (r["file"], r["verb"], r["route"]) in UNWIRED_KEYS]
HIGH_KEYS = {(r["file"], r["verb"], r["route"]) for r in HIGH}

# ---------------------------------------------------------------------------
# Authored dispositions. Anything not listed defaults to REVIEW.
#   BUILD       - real gap, needs a UI
#   INTENTIONAL - correct as-is; never report again
#   FALSE       - instrument artefact, not a real gap
#   DECIDE      - needs a call from the user or TDC
# ---------------------------------------------------------------------------
DISPOSITIONS = {
    "CompanyScheduleController.cs":
        ("DONE", "Built 2026-08-28. All 40 writes wired; 8 screens under /hr/company-schedule and "
                 "/administration/hr/company-schedule. Actor fix D-01 cleared first, and the three "
                 "StationId FKs repointed from the vestigial WorkStation to the live Location tree."),
    "StaffDisciplineLookupController.cs":
        ("BUILD", "Offence catalogue is GET-only in the UI; clients cannot maintain their own offence library."),
    "TalentPoolController.cs":
        ("BUILD", "Recruitment candidate CRM. Controller remarks already say 'bare since the port, no screen calling it'. Fold into the existing candidate screens."),
    "EmployeeBanksController.cs":
        ("BUILD", "Banks and branches reference data has no maintenance screen."),
    "LocationContactController.cs":
        ("BUILD", "No screen."),
    "EmployeeCareerPathController.cs":
        ("BUILD", "Decided 2026-08-28: career paths are NOT server-write-only and should have a UI."),
    "SalaryNotchesController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "SalaryLevelsController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "SalaryGradesController.cs":
        ("INTENTIONAL", "Decided 2026-08-28: read-only by intent — payroll owns the grade master."),
    "PayrollController.cs":
        ("INTENTIONAL", "Another team's module; HR integrates read-only."),
    "CandidatePortalController.cs":
        ("BUILD", "Candidate-facing recruitment portal. Reuses the external portal per the standing decision."),
    "CandidatePortalAuthController.cs":
        ("BUILD", "Candidate portal authentication."),
    "PublicRecruitmentController.cs":
        ("BUILD", "Public job board / anonymous apply."),
    "OfferResponseController.cs":
        ("BUILD", "Candidate offer response."),
    "ClientTimesheetConfirmationController.cs":
        ("BUILD", "Anonymous client confirmation link. Without it consultant billing has no client step."),
    "ConsultantClientPortalController.cs":
        ("BUILD", "Client portal for consultant engagements."),
    "ConsultantClientPortalAuthController.cs":
        ("BUILD", "Client portal authentication."),
    "HrLegacyFileMigrationController.cs":
        ("INTENTIONAL", "One-off ops tool, invoked by script."),
    "EmployeesController.cs":
        ("FALSE", "Fully wired through the path-builder helper employeeService.sub(id, 'contacts'). Instrument 01 cannot resolve a method call."),
    "PerformanceImprovementPlansController.cs":
        ("REVIEW", "18 of 22 flags are the api/PerformanceImprovementPlans alias of api/Pip. Real: attachments, review meetings, complete."),
    "JobAnalysisController.cs":
        ("BUILD", "The reference case. Eleven child collections with full CRUD and a read-only UI."),
    "StaffDisciplineSupportController.cs":
        ("BUILD", "Action steps and legal reviews are displayed but can never be recorded."),
    "StaffDisciplineSubEntityController.cs":
        ("BUILD", "Corrective action items cannot be edited or removed."),
    "MedicalInsuranceController.cs":
        ("BUILD", "Network facilities, premium records, provider documents, insurance claims."),
    "JobVacancyController.cs":
        ("BUILD", "Stage assignments — create, edit, skip, delete."),
    "AwardsController.cs":
        ("BUILD", "Nomination attachments — edit and delete."),
    "EmployeeCompetencyController.cs":
        ("BUILD", "Batch assessment."),
    "SeparationsController.cs":
        ("BUILD", "Clearance — refresh assets."),
    "AppraisalWorkflowController.cs":
        ("REVIEW", "Single transition endpoint; check whether the appraisal screens drive transitions elsewhere."),
    "StaffMovementsController.cs":
        ("REVIEW", "summaries/by-ids looks like an internal batch read."),
    "PerformanceAppraisalsController.cs":
        ("REVIEW", "calculate-score may be server-driven on submit."),
}

# Curated items that no static instrument can see — the demo-feedback backlog and the scheduler.
DEMO_FEEDBACK = [
    ("Employee Master", "Disability tick and description sit on EmployeeDependent, not Employee", "BUILD", "Wrong entity, not merely absent"),
    ("Employee Master", "Probation dates stay editable after confirmation", "BUILD", "No confirmed-state guard on contract update"),
    ("Employee Master", "Manager picker ignores ReportsToPosition", "BUILD", "Field exists and is populated; picker does not use it"),
    ("Employee Master", "Gender 'Other' has no description field", "BUILD", ""),
    ("Employee Master", "Hometown absent from the employee record", "BUILD", ""),
    ("Employee Master", "Guarantor has no guaranteed amount and no photograph", "BUILD", ""),
    ("Employee Master", "Referees cannot carry a reference letter", "BUILD", ""),
    ("Employee Master", "Expatriate: no issue dates, no resident permit, no family members", "BUILD", "FamilyAccompanying is a bare bool"),
    ("Employee Master", "IdentificationType has no expiry notification lead days", "BUILD", "Per-type setting"),
    ("Employee Master", "Certification bodies are free text, not a lookup", "BUILD", "EmployeeSkill.CertifyingBody"),
    ("Employee Master", "No mandatory documents against a position", "BUILD", ""),
    ("Employee Master", "No employee document attachments", "BUILD", "Controlled upload gate already exists"),
    ("Employee Master", "Qualification level is not a dimension", "BUILD", "QualificationType is a category, not an academic level"),
    ("Employee Master", "Staff number auto/manual is behaviour, not configuration", "BUILD", "No mode flag, no prefix/format"),
    ("Employee Master", "Exit interview questions are fixed fields", "BUILD", "Not a configurable question set; no attachments"),
    ("Employee Master", "No appointment letter templates", "BUILD", "Only email templates exist"),
    ("Employee Master", "No labour-law checklist", "BUILD", ""),
    ("Employee Master", "No mass application of benefits to dependents", "BUILD", ""),
    ("Employee Master", "Workflow step checklists unused by any HR process", "DECIDE", "Candidates: onboarding, separation clearance, probation"),
    ("Leave", "Compassionate leave cannot be set off against annual leave", "DECIDE", "No offset/advance concept anywhere; needs a design call"),
    ("Leave", "Adjustment form does not show the employee's balance", "BUILD", ""),
    ("Leave", "Reliever clashes are not visible on the plan", "BUILD", ""),
    ("Leave", "Free-text field still labelled 'Reason', not 'Remarks'", "BUILD", "Cosmetic"),
    ("Leave", "Leave request numbering still uses a max+1 scan", "BUILD", "Move to NumberSequence as Training already did"),
    ("Succession", "Criteria candidate search has no screen", "BUILD", "successionSearchService.searchCandidates has no caller"),
    ("Succession", "Candidate age and service-years-left not displayed", "BUILD", "API returns both"),
    ("Training", "Bulk nomination has no UI", "BUILD", "setBulkResult in NomineesPanel.tsx is dead code"),
    ("Training", "Bulk completion has no UI and no client method", "BUILD", ""),
    ("Training", "Nominee availability check never shown", "BUILD", "availability-check endpoint has no caller"),
    ("Training", "No 'Training Activities' grouped screen", "BUILD", ""),
    ("Training", "Mentoring still inside the Training menu", "BUILD", "Wants its own nav section"),
    ("Training", "Certificate does not gate completion", "BUILD", "Per-program flag"),
    ("Training", "Menu order differs from TDC's suggestion", "DECIDE", "Setup/operations split may be deliberate"),
    ("Recruitment", "Menu still reads 'Manpower Budgets'", "BUILD", "Rename to Manpower Recruitment Budget"),
    ("Appraisal", "Check-in link to company objectives", "DECIDE", "Confirm with TDC that it matches intent"),
    ("Platform", "No AddHostedService registration for HR", "BUILD", "Forfeiture, carry-over expiry, vacancy and retirement alerts, all reminder sweeps"),
]

# Controllers we have committed to building. For these the ledger enumerates EVERY write
# endpoint as a checkbox, not just the ones the instruments flagged — a build checklist has to
# be complete to be checkable.
BUILD_CHECKLISTS = {
    "CompanyScheduleController.cs": "Company Schedule",
    "EmployeeCareerPathController.cs": "Employee career paths",
}

BLOCKERS = [
    ("D-01", "Company Schedule actor ids are client-supplied", "DONE 2026-08-28",
     "organizerId, approvedById, markedById, bookedById and announcedById arrived as query "
     "parameters. Opening a UI over them unchanged would have shipped an act-as-anyone surface — "
     "the controller's own W3 remarks said so. All six now derive the actor from the token via "
     "HrControllerBase.TryGetEmployeeWriteContext and no longer accept it from the client. "
     "Safe to change signatures because no caller existed. **Needs a backend rebuild.**",
     "Was blocking the Company Schedule build — cleared"),
    ("D-02", "Self-service invitation response still act-as-anyone", "OPEN",
     "events/{id}/participants/respond takes a ParticipantId and sits on the HR-desk Write "
     "policy, so today it means 'HR records the response'. That is correct for the HR screens "
     "being built now. Before any /me surface offers 'accept this invitation', it needs a "
     "self-or-permission check against the participant's own employee id.",
     "Blocks a future self-service calendar only"),
]


def rows_for(files):
    out = collections.defaultdict(list)
    for u in unwired:
        if u["file"] in files:
            out[u["file"]].append(u)
    return out


def esc(s):
    return s.replace("|", "\\|")


lines = []
w = lines.append

w("# HR Closure Programme — master ledger")
w("")
w("> Generated by `scripts/hr-coverage/04_build_ledger.py`. Re-run the four scripts in order to")
w("> refresh the machine-derived sections. Dispositions live in the generator, not in this file —")
w("> edit `DISPOSITIONS` / `DEMO_FEEDBACK` there so a regeneration never loses a decision.")
w("")
w("**Disposition key** — `BUILD` real gap · `DONE` built and verified · `INTENTIONAL` correct")
w("as-is, never report again · `FALSE` instrument artefact · `DECIDE` needs a call · `REVIEW` not")
w("yet classified.")
w("")

total_w = len([r for r in routes if r["verb"] != "Get"])
w("## Position")
w("")
w("| Measure | Count |")
w("| --- | ---: |")
w("| HR write endpoints | %d |" % total_w)
w("| Wired to a screen | %d |" % (total_w - len(unwired)))
w("| No caller found (instrument 01) | %d |" % len(unwired))
w("| Confirmed unreachable (01 ∩ 02) | %d |" % len(HIGH))
w("| Write-DTO fields no form can set | %d across %d DTOs |"
  % (sum(len(d["missing"]) for d in dto_gaps), len(dto_gaps)))
w("")

w("## A. Decisions taken")
w("")
w("| Date | Decision |")
w("| --- | --- |")
w("| 2026-08-28 | Build the Company Schedule UI in full. |")
w("| 2026-08-28 | Salary grades, levels and notches are read-only **by intent** — payroll owns the grade master. Closed; do not re-raise. |")
w("| 2026-08-28 | Employee career paths are **not** server-write-only and should get a UI. |")
w("| 2026-08-28 | Company Schedule station FKs repointed from `WorkStation` to `Location`. `WorkStation` has an empty table, no repository implementation and no endpoint, so a required station made the meeting-room form unfillable. **Needs a migration.** |")
w("")

w("## B. Blockers — must clear before the dependent build starts")
w("")
for bid, title, state, detail, blocks in BLOCKERS:
    box = "x" if state.startswith("DONE") else " "
    w("- [%s] **%s — %s** · `%s`" % (box, bid, title, state))
    w("")
    w("  %s" % detail)
    w("")
    w("  _%s_" % blocks)
    w("")

w("## C. Confirmed unreachable endpoints (01 ∩ 02)")
w("")
w("Both instruments agree, and each was hand-verified in source. This is the trustworthy list.")
w("")
by_file = collections.defaultdict(list)
for r in HIGH:
    by_file[r["file"]].append(r)
for f in sorted(by_file, key=lambda x: -len(by_file[x])):
    disp, note = DISPOSITIONS.get(f, ("REVIEW", ""))
    w("### %s — `%s`" % (f.replace("Controller.cs", ""), disp))
    if note:
        w("")
        w("%s" % note)
    w("")
    seen = set()
    for r in sorted(by_file[f], key=lambda x: x["route"]):
        rt = re.sub(r"\{[^}]*\}", "{}", r["route"])
        if (r["verb"], rt) in seen:
            continue
        seen.add((r["verb"], rt))
        w("- [ ] `%-6s %s`" % (r["verb"].upper(), rt))
    w("")

w("## C2. Build checklists — committed work, every endpoint enumerated")
w("")
w("**These tick themselves.** A box is checked when instrument 01 finds a frontend call for that")
w("exact verb and path, so the list cannot drift from the code. Writes are what the instrument")
w("measures; reads are listed for completeness and are checked the same way.")
w("")
UNWIRED_ALL = {(u["file"], u["verb"], u["route"]) for u in unwired}
for cfile, label in BUILD_CHECKLISTS.items():
    rs = [r for r in routes if r["file"] == cfile]
    wr = [r for r in rs if r["verb"] != "Get"]
    gt = [r for r in rs if r["verb"] == "Get"]
    done_w = sum(1 for r in wr if (r["file"], r["verb"], r["route"]) not in UNWIRED_ALL)
    w("### %s — %d of %d writes wired" % (label, done_w, len(wr)))
    w("")
    for title, group in (("Writes", wr), ("Reads", gt)):
        w("**%s**" % title)
        w("")
        seen = set()
        for r in sorted(group, key=lambda x: (x["route"], x["verb"])):
            rt = re.sub(r"\{[^}]*\}", "{}", r["route"])
            if (r["verb"], rt) in seen:
                continue
            seen.add((r["verb"], rt))
            # Reads are not tracked by instrument 01's unwired set, so only writes self-tick.
            if r["verb"] == "Get":
                box = " "
            else:
                box = " " if (r["file"], r["verb"], r["route"]) in UNWIRED_ALL else "x"
            w("- [%s] `%-6s %s`" % (box, r["verb"].upper(), rt))
        w("")

w("## D. Hand-review queue (flagged by 01 only)")
w("")
w("Instrument 01 found no caller but instrument 02 still sees the path segment in the frontend, so")
w("each of these is *either* a real gap *or* one of the three documented artefacts. Classify, then")
w("move the real ones into section C's disposition map.")
w("")
w("| Controller | Flagged | Total writes | Disposition | Note |")
w("| --- | ---: | ---: | --- | --- |")
totals = collections.Counter(r["file"] for r in routes if r["verb"] != "Get")
counts = collections.Counter(u["file"] for u in unwired
                             if (u["file"], u["verb"], u["route"]) not in HIGH_KEYS)
for f, n in counts.most_common():
    disp, note = DISPOSITIONS.get(f, ("REVIEW", ""))
    w("| %s | %d | %d | `%s` | %s |" % (esc(f.replace("Controller.cs", "")), n, totals[f], disp, esc(note)))
w("")

w("## E. Fields no form can set (instrument 03)")
w("")
w("These endpoints *are* wired. The form omits fields, so the feature is degraded rather than")
w("missing — the class an endpoint audit cannot see.")
w("")
w("| DTO | Unreachable | Fields |")
w("| --- | ---: | --- |")
for d in dto_gaps:
    fields = ", ".join(d["missing"][:12]) + (" …" if len(d["missing"]) > 12 else "")
    w("| `%s` | %d of %d | %s |" % (d["dto"], len(d["missing"]), d["total"], esc(fields)))
w("")

w("## F. Demo-feedback backlog")
w("")
w("From the five pre-port feedback documents. No static instrument can find these — they are")
w("things absent from *both* sides, or present but wrong.")
w("")
w("| Area | Item | Disposition | Note |")
w("| --- | --- | --- | --- |")
for area, item, disp, note in DEMO_FEEDBACK:
    w("| %s | %s | `%s` | %s |" % (esc(area), esc(item), disp, esc(note)))
w("")

w("## G. Out of scope")
w("")
w("Closed decisions. Listed so a future sweep does not re-open them.")
w("")
for f, (disp, note) in sorted(DISPOSITIONS.items()):
    if disp == "INTENTIONAL":
        w("- **%s** — %s" % (f.replace("Controller.cs", ""), note))
w("")

os.makedirs(os.path.dirname(LEDGER), exist_ok=True)
open(LEDGER, "w", encoding="utf-8").write("\n".join(lines))
print("wrote %s (%d lines)" % (LEDGER, len(lines)))
print("  confirmed unreachable: %d across %d controllers" % (len(HIGH), len(by_file)))
print("  hand-review queue:     %d across %d controllers" % (sum(counts.values()), len(counts)))
print("  DTO field gaps:        %d across %d DTOs" % (sum(len(d['missing']) for d in dto_gaps), len(dto_gaps)))
print("  demo-feedback rows:    %d" % len(DEMO_FEEDBACK))
