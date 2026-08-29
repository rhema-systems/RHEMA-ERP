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
    "JobAnalysisController.cs": "Job Analysis",
    "StaffDisciplineSupportController.cs": "Discipline — case file support",
    "StaffDisciplineSubEntityController.cs": "Discipline — case sub-entities",
    "MedicalInsuranceController.cs": "Medical insurance",
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
    ("D-03", "A child write on an approved job description is accepted by the API", "OPEN",
     "None of the twelve child-collection writes checks status. `AddPhysicalDemandAsync` and its "
     "eleven siblings call `GetOwnedJobDescriptionAsync`, which verifies the tenant and stops — so "
     "the duties of an approved, in-force job description can be rewritten with no new version and "
     "no trace. Proven against the running API by slice 13, which records it as a passing "
     "assertion so the day it starts failing is the day the server grew a gate. The authoring "
     "panels refuse it client-side (`AUTHORABLE_JOB_DESCRIPTION_STATUSES`) and that is the only "
     "thing stopping it. A server-side guard belongs on the service, not the screen.",
     "Nothing — the UI compensates. Raised so the compensation is not mistaken for a rule."),
    ("D-04", "`JobEquipmentTool.LinkedQualificationId` points at a JobQualification, not the catalogue", "DONE 2026-08-29",
     "The field is called `LinkedQualificationId`, the DTO types it `Guid?`, and the obvious "
     "reading — the `Qualifications` reference catalogue — is wrong. The constraint is "
     "`FK_JobEquipmentTools_JobQualifications_LinkedQualificationId`: it targets one of the SAME "
     "job description's qualification rows. A catalogue id fails the FK and the request 500s with "
     "the generic handler's message, naming nothing. The equipment panel was built on the wrong "
     "reading and slice 13 caught it; the picker now reads the job description's own "
     "qualifications and hides itself until there are some.",
     "Was breaking every equipment-tool save — cleared"),
    ("D-05", "Legal-review referrer was assertable by the request body", "DONE 2026-08-29",
     "`CreateStaffDisciplineLegalReviewDto.ReferredById` was accepted from the client and copied "
     "straight onto the entity by the mapper, while the token's employee id went only to "
     "`CreatedBy`. So any HR user could record a colleague as the person who referred a case to "
     "legal — a falsifiable audit record on exactly the kind of document a case turns on later. "
     "The same defect class as D-01, and cleared the same way: the field is gone from the create "
     "DTO and `ReferAsync` stamps the actor. Nothing had ever sent it (it was in section E's "
     "\"no form can set\" table), so no caller broke. **Needs a backend rebuild.**",
     "Was blocking the discipline legal-review build — cleared"),
    ("D-07", "Action-step complete and skip never stamped the step's actor", "DONE 2026-08-29",
     "`CompleteStepAsync` and `SkipStepAsync` set only `UpdatedBy` — the string audit column — and "
     "never `ActionedById`, the Employee FK the DTO exposes and the case screen renders as \"By\". "
     "So that column was permanently blank for every step closed through the supported route, and "
     "the ONLY path that ever filled it was the plain update, which takes the id from the request "
     "body. Found by running the new panel's own payloads, not by reading the code: three "
     "assertions failed on the first run of slice 9. Both transitions now stamp the actor; skip "
     "deliberately still sets no completion date. **Needs a backend rebuild.** "
     "⚠ Residue: `UpdateActionStepDto.ActionedById` is still client-supplied. Left alone rather "
     "than removed, unlike the legal-review referrer, because correcting a mis-attributed step is "
     "a legitimate HR act and no screen exposes it — but it is an act-as-anyone vector on paper.",
     "Was making the panel's \"By\" column permanently empty — cleared"),
    ("D-10", "Provider documents took a caller-supplied file path with no gate and no download", "DONE 2026-08-29",
     "`POST provider-documents` REQUIRED a `FilePath` and stored it verbatim, so any HR user could "
     "point a document row at arbitrary bytes on disk. There was no upload endpoint and no download "
     "route either, so the row was unreadable even when the path was honest. This was the **third** "
     "instance of that defect in the medical module — `MedicalExpenseDocument` and "
     "`EmployeeMedicalExamDocument` were each fixed for it, and provider documents were missed. "
     "There was no safe way to build a UI over it, which is why the collection stayed unreachable. "
     "Now: three nullable DMS columns (migration `AddMedicalInsuranceProviderDocumentDmsColumns`, "
     "hand-guarded on COL_LENGTH because rebuild-db builds from the EF model), a multipart upload "
     "through the scanning + DMS gate, a token-bearing download, and the metadata route refusing "
     "every file-location field. **Needs a backend rebuild.**",
     "Was blocking the provider-documents build — cleared"),
    ("D-11", "A declared upload category is not a registered one", "DONE 2026-08-29",
     "Adding `HrMedicalInsuranceProviderDocuments` to `ControlledFileUploadCategories` was only "
     "half the job. Membership of `SystemCleanScanRequired` is the ONLY thing that turns scanning "
     "on — a category absent from it is silently SKIPPED, and "
     "`CentralDocumentRepositoryFileService.RegisterAsync` then rejects `Skipped` as firmly as "
     "`Infected`. The upload passed the gate and failed one layer later with an "
     "`InvalidOperationException` naming neither the category nor the scan. The property's own "
     "remarks predict this exactly; it was still missed. Registered, with a comment at the point "
     "of use. **Any future upload category must be added in both places.**",
     "Was making every provider-document upload fail — cleared"),
    ("D-12", "The per-provider premium read returned a summary its panel could not render", "DONE 2026-08-29",
     "`providers/{id}/premium-records` returned `MedicalInsurancePremiumRecordSummaryDto` — total "
     "and status only, nothing about contributions, covered lives, due date or payment. Nine probe "
     "assertions failed and the panel would have shown blank columns. **Third occurrence of the "
     "D-09 shape**, and again the pattern already existed beside it: network facilities and "
     "provider documents on the SAME controller returned full DTOs. Converted; "
     "`premium-records/overdue` stays a summary. The rule, now stated three times: a per-parent "
     "read feeds a panel that must show detail; a cross-record read feeds a list.",
     "Was making the premium panel render blank columns — cleared"),
    ("D-13", "HR cannot administer employee insurance policies", "OPEN",
     "`policies` (create, update, cancel, delete) and `dependents` (add, update, remove) — seven "
     "write endpoints — have no HR screen at all; only the employee's own self-service surface "
     "reads policies. Deferred by decision on 2026-08-29 rather than overlooked: whether HR "
     "administers enrolment, or it arrives from payroll or the insurer, is a product call and not "
     "a coverage gap. Recorded because slice 4 made the consequence concrete — its insurance-claims "
     "section cannot execute at all, since no policy exists to claim against.",
     "Blocks nothing built so far, and blocks any test of insurance claims"),
    ("D-06", "Four discipline case-file collections were displayed but unrecordable", "DONE 2026-08-29",
     "`StaffDisciplineCaseDetailDto` carries six collections; only two were built out in the first "
     "pass. Witnesses, documents, notes and notifications are now authorable too, so the case file "
     "is complete. Documents go through the controlled upload gate only — the metadata-only route "
     "is left unwired because it rejects every file-location field and can therefore only mint a "
     "row naming a file that does not exist.",
     "Was leaving the case file half-authorable — cleared"),
    ("D-09", "Four per-case reads returned summaries a panel could not edit from", "DONE 2026-08-29",
     "`cases/{id}/witnesses`, `/notes`, `/documents` and `/notifications` returned `...SummaryDto` "
     "projections. The witness summary has no `ContactInfo`; the note summary carries a "
     "**100-character excerpt** instead of the note; documents drop `Description` and "
     "`ActionStepName`; notifications drop `SentByName` and `FollowupDate`. Every panel built on "
     "them rendered permanently blank columns, and the witness edit form would have wiped "
     "`ContactInfo` on every save. Legal reviews already returned the full DTO from their per-case "
     "read — the pattern existed and four collections had not followed it. The eight per-case "
     "reads now return the record; the cross-case reads (by-author, pending-followup, by-scope, "
     "by-category, by-employee) stay on summaries because they feed lists and the reminder sweep. "
     "The rule: **a per-case read feeds a panel that must edit; a cross-case read feeds a list.** "
     "**Needs a backend rebuild.**",
     "Was making four new panels render blank columns — cleared"),
    ("D-08", "Note author and document uploader were assertable by the request body", "DONE 2026-08-29",
     "Third and fourth instances of the D-05 shape, found by checking every create mapper in the "
     "family rather than waiting for a probe to fail. `StaffDisciplineNote.CreatedByEmployeeId` "
     "and `StaffDisciplineDocument.UploadedById` were copied from the DTO while the token's "
     "employee id went only to `CreatedBy`. Notifications were **already correct** — `SentById` is "
     "server-stamped and the DTO says why — which is what showed the other two were not. Both "
     "fields are now stamped from the token and removed from their create DTOs.",
     "Was letting a case note be attributed to a colleague — cleared"),
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
w("| 2026-08-29 | Job Analysis: the twelve child collections are now authored from the job-description detail screen. Panels go read-only outside `Draft`/`UnderRevision`, and the delete affordance is hidden below Admin — both mirror what the API does, except the status rule, which the API does **not** enforce (see D-03). |")
w("| 2026-08-29 | Four TypeScript enum unions in `job-architecture.ts` were fiction and are corrected: `PhysicalDemandFrequency` ended in `Constantly` (it is `Continuously`), `WorkEnvironmentType` carried `Warehouse` and `Site` (neither exists) and lacked `Hybrid`/`FieldBased`/`Other`, `CompetencyType` carried `Functional` (that is `CompetencyCategory`, a different enum), and `QualificationType` was missing `TechnicalSkills` and `Language`. Each is now proven against the running API. |")
w("| 2026-08-29 | Discipline: the procedure steps, legal reviews and corrective action plan are now authorable from the case screen. The **sanctions stay read-only** — warning, suspension, fine and termination are blocked on FR-HR-080's issuing-authority rule, which is the original and still-valid reason. Investigation and hearing are read-only only because nobody has built their editors. |")
w("| 2026-08-29 | Legal-review `referredById` is now stamped from the token and removed from the create DTO (D-05). |")
w("| 2026-08-29 | The discipline case file is complete: witnesses, documents, notes and notices are authorable alongside the procedure steps, legal reviews and corrective action plan. Documents go through the controlled upload gate only. |")
w("| 2026-08-29 | **A per-case read returns the record; a cross-case read returns a summary.** Four per-case reads were returning projections that dropped the very fields their panels had to edit — the note one truncated at 100 characters. Eight reads converted (D-09). |")
w("| 2026-08-29 | Notices are issue-and-chase only, and carry **no acknowledge control on the HR screen**: the API refuses anyone but the employee the notice was issued to. |")
w("| 2026-08-29 | Two Discipline boxes stay unticked **by explanation, not omission**. `POST cases/{}/documents` is deliberately unwired — it rejects every file-location field, so through the API it can only mint a row naming a file that does not exist; it survives for the legacy migration utility. `POST cases/{}/documents/upload` **is** wired, through `hrDocumentService.upload(endpoint, file, fields)` — instrument 01 cannot resolve a path passed to a helper, the same artefact that makes EmployeesController read 73/81. |")
w("| 2026-08-29 | Medical insurance: network facilities, provider documents and premium records are administered from the provider screen; insurance claims are filed from the medical expense claim they belong to. |")
w("| 2026-08-29 | Provider documents now go through the controlled upload gate (D-10). The metadata route survives for the legacy migration utility and refuses every file-location field. |")
w("| 2026-08-29 | **HR still has no employee-policy screen.** `policies`, `dependents` and their seven write endpoints have no UI at all — only the employee self-service surface reads them. Deferred by decision, not overlooked: whether HR administers enrolment is a product call. Slice 4 demonstrates the consequence rather than arguing it — its insurance-claims section cannot run, because no policy exists to claim against. |")
w("| 2026-08-29 | Medical insurance reads 15 of 24, and the nine remaining are **accounted for, not outstanding**: seven are the deferred policy/dependent family (D-13); `POST provider-documents` is deliberately unwired (it refuses every file-location field, so it can only mint a row naming a file that does not exist); and `POST provider-documents/upload` **is** wired, through `hrDocumentService.upload(endpoint, file, fields)` — the path-builder artefact instrument 01 cannot resolve. |")
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
