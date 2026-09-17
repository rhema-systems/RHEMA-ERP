# HR & SHE Module — Documentation Index and Start-Here

**Created:** 2026-09-02 (the vetting pass that re-verified every document in this folder against
the code). **Owner:** whoever is doing HR work in this repo. Keep this page short; it is an
index, not a document.

---

## Start here if you are picking up HR work

1. **The governing rules** (each is recorded in detail elsewhere; this is the list so none is
   re-derived):
   - **Payroll is another developer's module.** HR reads it, never edits it. If HR needs something
     payroll doesn't expose, raise it — the `docs/HANDOFF-PAYROLL-*.md` shape. → `HR-PAYROLL-BOUNDARY.md`
   - **HR does not post to the General Ledger per area.** Record the money event, register it in
     `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`, and leave the accounting to one comprehensive sweep
     after the whole module. **Master data is not deferred** — read Finance's `Currency` and
     `ExchangeRate` through `HrCurrencyBridge` now; never keep a parallel copy. → `HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`
   - **Never build a module-specific approval UI.** Plug into the workflow engine with the
     four-step recipe. The one deliberate exception is `EmployeeGoal`. → `HR-WORKFLOW-ENGINE-INTEGRATION.md`
   - **A bulk endpoint loops the real service call.** Same authorization, same workflow
     transition, same notification, per item. → `HR-BULK-OPERATIONS-CATALOGUE.md` §4.1
   - **Ported services stamp `TenantId` explicitly.** The DbContext auto-stamp is inert. Fix
     per service; do not re-wire the global filter.
   - **Gate vertically with role/permission policies and horizontally on the ownership helper.**
     When the entitled party is named on the row (reviewer, subject, assignee), use plain
     `[Authorize]` and read entitlement off the record; a permission gate there makes the action
     reachable by nobody. Do not seed HR permissions per area — the `HR.<Area>.{Read,Write,Admin}`
     taxonomy is in `src/ErpSystem.Shared/HrPermissions.cs` (66 policies).
   - **Defects found in other teams' modules are recorded, not fixed** —
     `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`, actioned at finalization.
   - **Run harnesses in Staging with the JWT key passed in**, or every refusal is a 500 and every
     login a 400. → `HR-VERIFICATION-HARNESS-GUIDE.md`
   - **Stage, the user commits.** Never `git commit`; never `dotnet build` — stop and ask.
2. **Where the work stands:** `docs/HR-FINISH-PLAN.md` → "Where to start next". What was
   decided and why: `docs/HR-CLOSURE-LEDGER.md` (decisions **D-01…D-40** in §B — ⚠ hand-curated;
   its generator writes a gitignored companion and must never overwrite it).
3. **What TDC still owes an answer on:** `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` (~16 items).

⚠ **Two decision namespaces collide.** The ledger's `D-NN` (D-01…D-40) and each build plan's own
area-local `D1`/`D2`/`D3` (e.g. area 16's "decision D1" = HR reads Finance fixed assets read-only,
area 16's "D3" = assets on the workflow engine). When citing an area-local one, say the area.

---

## This folder (`docs/HR/`) — 14 documents

All were re-verified against the working tree on **2026-09-02**; each carries a "Vetting pass"
block listing what was corrected. Read that block before acting on the document.

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`](HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md) | One-page briefing: the Finance rules, the governance message, the key facts | Any task that touches money |
| [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) | Every HR/SHE entity that carries money (87 rows), its direction, its Finance target, and the 14 open decisions | Planning the GL sweep; adding a money field |
| [`HR-PAYROLL-BOUNDARY.md`](HR-PAYROLL-BOUNDARY.md) | Payroll is another developer's; what HR reads, the three bridges, defect #23, and which Finance-sweep items are the payroll owner's | Anything near salary, grades, pay components, `IsOnPayroll` |
| [`HR-MODULE-INTEGRATION-MAP.md`](HR-MODULE-INTEGRATION-MAP.md) | Every other module HR/SHE touches (36 rows), both directions, with a verdict per link | Adding a cross-module FK; deciding who owns a record |
| [`HR-SHE-INTEGRATION-AND-BOUNDARIES.md`](HR-SHE-INTEGRATION-AND-BOUNDARIES.md) | The Safety, Health & Environment sub-module as a whole: its shape, its boundaries with Medical/Training/Maintenance/Inventory/Estate, its gates, jobs, money and known gaps | Any SHE work |
| [`HR-SHE-PPE-INVENTORY-INTEGRATION-DESIGN.md`](HR-SHE-PPE-INVENTORY-INTEGRATION-DESIGN.md) | PROPOSED: Inventory owns PPE stock, SHE keeps policy and custody; the consume path, its traps, the two open decisions | Anything near `api/safety/ppe` or PPE stock |
| [`HR-DEMO-FEEDBACK-ROUND-3-PLAN.md`](HR-DEMO-FEEDBACK-ROUND-3-PLAN.md) | PLANNED 2026-09-11: every bullet of the round-3 demo feedback (employee details, job description, staff unions, requisition, recruitment) mapped to code, the eight decisions and thirteen defaults, nineteen slices in order, and the Finance/payroll asks (banks wait on Finance) | Picking up any round-3 item; before touching salary writes, candidate fields or shortlisting criteria |
| [`HR-DEMO-FEEDBACK-ROUND-2B-RECRUITMENT-PLAN.md`](HR-DEMO-FEEDBACK-ROUND-2B-RECRUITMENT-PLAN.md) | IN BUILD 2026-09-10: round 2b — the recruitment section of the second feedback document (manpower budget ↔ establishment ↔ requisition, retirements in the plan, salary from the scale, Excel round-trip) plus the Finance follow-up on requisition costs; nine slices R1–R8, the decisions, and the ask to the Finance owner | Any manpower-budget, establishment or requisition item; before touching `StaffRequisitionCost` |
| [`HR-DEMO-FEEDBACK-ROUND-2-PLAN.md`](HR-DEMO-FEEDBACK-ROUND-2-PLAN.md) | PLANNED 2026-09-08: every bullet of the round-2 demo feedback (org structure, positions, skills, employee profile) mapped to code, the five decisions taken, six build lanes, the payroll/finance asks, and the finish-plan claims it corrects | Picking up any round-2 item; before trusting lane 3a's "done" ticks |
| [`HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md`](HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md) | APPROVED: the checklist template builder (sections, header fields, critical items, scoring bands, outcomes, signatories, versioning, print) and how an inspection runs against one | Any work on inspection checklists or the inspection run |
| [`HR-JOB-DUTY-VS-RESPONSIBILITY.md`](HR-JOB-DUTY-VS-RESPONSIBILITY.md) | The job-analysis distinction: a responsibility is an outcome you answer for, a duty is an activity you perform — and which machinery each feeds (appraisal and valuation vs the offer letter) | Authoring a job description; touching `JobAnalysisEntities.cs` |
| [`HR-WORKFLOW-ENGINE-INTEGRATION.md`](HR-WORKFLOW-ENGINE-INTEGRATION.md) | The four-step plug-in recipe, the traps, the engine defects, which families are on the engine | Wiring any approval |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md) | Engineering patterns other modules solved that HR hasn't adopted (notifications, concurrency, budget commitment, audit, asset lifecycle) | Designing a new HR capability |
| [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) | What reports exist, which are missing, the three patterns, and the one to default to | Building a report or export |
| [`HR-BULK-OPERATIONS-CATALOGUE.md`](HR-BULK-OPERATIONS-CATALOGUE.md) | Which single-item actions need a bulk sibling, what exists, how to build one safely | Building a multi-select action |
| [`HR-IMPORT-EXPORT-CATALOGUE.md`](HR-IMPORT-EXPORT-CATALOGUE.md) | Which entities need file import/export, what exists, the staff-number rule an importer must honour | Building an import |
| [`HR-VERIFICATION-HARNESS-GUIDE.md`](HR-VERIFICATION-HARNESS-GUIDE.md) | Where the harnesses are, how to run them, the environment traps, the fixture conventions, what green proves | Before any harness run |
| [`HR-UAT-DEMO-PRESENTATION-PLAN.md`](HR-UAT-DEMO-PRESENTATION-PLAN.md) | The two-day acceptance demo: agenda, sign-off matrix, what changed since it was drafted | Preparing the demo |
| [`HR-DOCUMENTATION-CATALOGUE.md`](HR-DOCUMENTATION-CATALOGUE.md) | Documentation as a deliverable: what to produce for developers, users and stakeholders | Planning docs work |
| `README.md` | This index | — |

**Deliberately not in `docs/HR-FINISH-PLAN.md`:** the capability catalogues above (reports, bulk,
import/export, patterns, documentation, UAT) are a **second programme**, excluded from the finish
plan by the user's decision. Do not fold them in without being asked.

### Screen-by-screen system guides

A separate, growing series: every screen of one area walked from the source, with a plain-language
layer for HR users and a technical layer mapping each control to its endpoint, service and table.
They are the reference for what a screen actually does — and the second one doubles as a
performable demonstration script.

| Guide | Covers | Notes |
|---|---|---|
| [`HR-RECRUITMENT-SYSTEM-GUIDE.md`](HR-RECRUITMENT-SYSTEM-GUIDE.md) | 32 recruitment screens, establishment → hire | Walked 2026-09-14/15; its 70 gaps were closed 2026-09-15/16. **Its gap blocks are history, not a to-do list** — live state is in `HR-RECRUITMENT-GAP-CLOSURE-PLAN.md` |
| [`HR-EMPLOYEES-SYSTEM-GUIDE.md`](HR-EMPLOYEES-SYSTEM-GUIDE.md) | The whole **Employees** menu group — 14 screens, the 35-tab profile, and the 3 portal screens feeding its queues | Written 2026-09-16. Also a demonstration workbook: prep checklist, click-by-click walks, 9 numbered live writes and a reset chapter. **Chapter 2 is mandatory before demonstrating** — the demo database leaves 15 profile tabs and 4 menu items empty (finding E-12) |
| [`HR-LEAVE-SYSTEM-GUIDE.md`](HR-LEAVE-SYSTEM-GUIDE.md) | The whole **Leave Management** menu group — 10 menu items, the 4-screen Leave Types rulebook under Administration, the 7 portal screens, and the reminder engine that has no screen. **25 screens, 23 chapters** | **Rewritten 2026-09-17 after the closure build**, so it describes the module as it now is. Also a demonstration workbook: 14 numbered live writes, a reset chapter and a 25-minute short path. **Read the three rules above chapter 1 first** — leave now takes **two** approvals, the Approvals screen shows what is waiting on *you*, and `hr.head` has no Admin tier. ⚠ **Written from source; F1 has not run**, so treat two-stage approval, the attendance write and the new queue with a little scepticism until it does. § 23 lists all 37 original findings with their current state — 28 closed, 9 open |
| [`HR-LEAVE-CLOSURE-PLAN.md`](HR-LEAVE-CLOSURE-PLAN.md) | **IN PROGRESS — waves A–E BUILT 2026-09-17, only F1 (the harness) left.** The 50-item gap register, eleven decisions, and 18 slices in six waves | **Start here for any leave work, and read § 0 first** — it is the only part of either document that describes the code as it stands. The system guide is the evidence the plan was built from, not the current state |
| [`HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md`](HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md) | The whole **Attendance & Time** menu group — 11 menu items + 10 unlisted screens, the 8 setup areas under Administration → Time, Attendance & Leave, and the portal's My Attendance. 36 screens over 24 tables | Written 2026-09-17. Also a demonstration workbook: 16 numbered live writes, a reset chapter and a 25-minute short path. **Read the five rules above chapter 1 first** — a punch never computes lateness (A-1), a pasted CSV import writes nothing while reporting success (A-2), Recalculate blanks four columns (A-3), a payroll export always reports zero (A-4), and `hr.head` cannot delete anything (A-5). **§ 1.7 is the prepared answer to the buddy-punching question** (A-93 / A-94) — the punch proves a session and a location, never a person, and the reader connector is the piece that would close it |
| [`HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md`](HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md) | The whole **Company Schedule** menu group — both menu items + 6 unlisted screens, the 4 setup areas under Administration → HR → Company Schedule, and the Company Profile screen (same permission family, different menu). 17 screens over 13 tables | Written 2026-09-17. Also a demonstration workbook: 13 numbered live writes, a reset chapter and a 20-minute short path. **Read the six rules above chapter 1 first** — the event page's red Delete button 403s for `hr.head` (C-1), the reschedule dialog's promise about keeping the original dates is false (C-2), "Approve" here is a flag rather than the workflow engine (C-3), the room's duration and advance-booking rules are never enforced (C-4), business closures reach neither leave nor attendance (C-5), and event/booking numbers can repeat after a delete (C-6) |
| [`HR-STAFF-TRAVEL-SYSTEM-GUIDE.md`](HR-STAFF-TRAVEL-SYSTEM-GUIDE.md) | The whole **Staff Travel** menu group — all 6 menu items + 6 unlisted screens, the 2 setup areas under Administration → HR → Travel, and the 4 portal screens. 20 screens over 31 tables, with 7 tabs inside a single request | Written 2026-09-17. Also a demonstration workbook: 13 numbered live writes, a reset chapter and a 25-minute short path. **Read the six rules above chapter 1 first** — every travel policy is unapproved so no cap binds (T-1), `hr.head` cannot approve a policy or authorise a breach (T-2), two dropdown values on the request form 400 (T-3), the policy rule register is enforced by nothing and ships read-only on purpose (T-4), Finance's currency conversion is inverted and travel inherits it deliberately (T-5), and a cash advance is recovered on payment rather than approval (T-6) |
| [`HR-PERFORMANCE-SYSTEM-GUIDE.md`](HR-PERFORMANCE-SYSTEM-GUIDE.md) | The whole **Talent & Performance → Performance** menu group — all 19 menu items + 15 unlisted detail routes, the 12 setup screens under Administration → HR → Performance, and the 15 portal screens. **62 screens over 51 tables**, and the largest guide in the series | Written 2026-09-17, verified field-by-field against `ErpSystemDB_UAT`. Also a demonstration workbook: 39 chapters, 16 numbered live writes, a reset chapter and a 25-minute short path. **Read the nine rules above chapter 1 first** — one cycle/profile/form is all there is (Rule 1), calibration is a real gate but the panel's number is overwritten at HR sign-off (Rule 2), Finalise is not disabled by that gate (Rule 3), nine goals sit at 0% so ~16 read as at risk (Rule 4), no TDC employee goal is aligned to anything (Rule 5), the conversations diary and the calibration Open tab are both empty (Rule 6), `hr.head` cannot delete anything (Rule 7), five fixture appraisals open completely empty (Rule 8), and an appeal can contest a competency only (Rule 9) |
| [`HR-APPRAISAL-SETTINGS-AUDIT.md`](HR-APPRAISAL-SETTINGS-AUDIT.md) | Field-by-field audit of the **Appraisal Settings** profile: which of its 50 values actually drive behaviour | Written 2026-09-17. **36 enforced · 9 advisory · 2 client-side only · 3 complete ghosts.** Every claim carries a `file:line`. The headline structural finding is that the module has **two pipeline resolvers with different rule sets** — `GetCurrentPhase` enforces, `AppraisalSubStatusResolver` (dashboard + deadline advance) has five extra gates — so the analytics screen reports a stricter pipeline than the system enforces. Ends with a prioritised fix list. Read it before describing the policy screen to a customer |
| [`HR-PERFORMANCE-SEED-GAP-PLAN.md`](HR-PERFORMANCE-SEED-GAP-PLAN.md) | What the performance demo dataset is still missing, and the order to do the work in | Written 2026-09-17. **Table coverage is already complete** — all 44 required tables hold rows, so `verify-tables.mjs` cannot see the problem. The gaps are **shape**: 17 states the data never reaches (S-1…S-17), six of which leave a screen blank. Sequencing: fix **three** defects first (P-55, P-28, P-40) because they make you seed data that lands wrong, decide P-39, then seed, then everything else. Ends with a proposal to add shape assertions to the coverage check |

---

## HR documents elsewhere in the repo

| Path | What it is |
|---|---|
| `docs/HR-FINISH-PLAN.md` | Everything HR still owes, in lanes 0–9 + 3f, each with a size, a blocker and a "done when". **Start with its "Where to start next" table.** |
| `docs/HR-CLOSURE-LEDGER.md` | Hand-curated ledger: position table (2,157 write endpoints, 1,908 wired), §A decisions, §B blockers D-01…D-40, §C build checklists, §D dispositions, §E fields no form can set, §F demo-feedback backlog, §G out of scope |
| `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` | The living decision register for the post-module GL sweep (opened 2026-08-17) |
| `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` | Questions and data asks needing a TDC answer |
| `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` | Defects HR found in other teams' modules (#1–#23); recorded, not fixed |
| `docs/HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md` | Defect #23 hand-off to the payroll owner — the template for raising anything with another module's owner |
| `docs/UAT-DEMO-DATABASE.md` + `scripts/New-UatDatabase.ps1` | Build / switch / reset / check the `ErpSystemDB_UAT` demo database |
| `dev-harness/hr-demo-smoke/runbook/` | Books 0–4 + cheat-sheet: the per-persona, click-by-click demo script (print-to-PDF HTML) |
| `docs/hr-port-data-migration.md` | Data-migration runbook for the HR port |
| `docs/HR_PAYROLL_ORACLE_FORMS_MIGRATION_PLAN.md`, `…_MENU_FIELD_CROSSWALK.md`, `…_REPORTS_CROSSWALK.md` | Legacy Oracle Forms payroll → new system (payroll owner's territory) |
| `HR_MODULE_PORT_PLAN.md`, `HR_PORT_CROSS_MODULE_CHANGES.md` (repo root) | The original port strategy and its ripple effects |
| `docs/Finance/finance-integration-contract-catalogue.md` | FIN-INT-001…016 — the Finance mechanisms HR will call; FIN-INT-011 is HR-shaped and ownerless |
| `docs/Finance/finance-integration-consumer-test-template.md`, `finance-integration-adapter-checklist.md` | Required for any HR→Finance integration per the Finance owner's governance message |
| `docs/tdc-fleet-management-gap-implementation-tracker.md` | Fleet interface contract across HR/Maintenance/Inventory/Procurement/Finance — read beside the integration map §3 |

## Build plans (`plans/`) — 15

`HR-Area-9b-Separation-Exit`, `9c-Grievance-Employee-Relations`, `11-Medical`, `12-Travel`,
`13-Succession`, `14-Awards`, `15b-Probation-Confirmation`, `16-HR-Assets`,
`17-Job-Architecture-Competency-Establishment`, `19-23-Tier-B-Tail`, `25-Employee-Self-Service`
(+ `25-Slice0-Census`), plus `HR-Consultant-Client-Portal-Closure`, `HR-Recruitment-Closure` and
`HR-W3-Permissions-Sweep`. Areas built before area 9b (leave, attendance, performance, training,
orientation, recruitment, movements, discipline, SHE) have their plans outside the repo or in the
closure ledger; the SHE plan's substance is summarised in `HR-SHE-INTEGRATION-AND-BOUNDARIES.md`.

## Harnesses — outside the repo, `D:\Rhema\TDC ERPS\dev-harness\hr-*`

26 directories (assets, awards, company-schedule, consulting, demo-smoke, discipline,
employee-docs, employee-relations, finish-lane4, geofence, jobarch, medical, movements, orientation,
payroll-membership, performance, portal, probation, recruitment, safety, separation, succession,
tierb-tail, training, travel, w3-permissions). See `HR-VERIFICATION-HARNESS-GUIDE.md`.

## Requirement-ID namespaces

`FR-HR-###` (30 distinct ids, in `plans/`, `docs/` and `src/`); `FR-SHE-###` (20), `FR-ENV-###`
(26), `FR-CON-###` (1) and `FR-PTW-###`/`FR-INC-###` (SHE sub-namespaces) appear **only in
`src/`** — the SHE build plan lives outside the repo. Grep `src/` for a SHE id, `plans/` for an HR
one.

## Still owed for this index to be complete

- One link line to this page from the repo-wide `docs/README.md` (a platform file; also carries a
  literal `$(date)` placeholder on line 65 that was never substituted).
- The auto-generated API reference: `.github/workflows/documentation.yml` exists but `docs/api/`
  holds no swagger or html — the artefact has never landed.
