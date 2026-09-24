# HR & SHE Module — Documentation Index and Start-Here

**Created:** 2026-09-02 (the vetting pass that re-verified every document in this folder against
the code). **Owner:** whoever is doing HR work in this repo. Keep this page short; it is an
index, not a document.

---

## Start here if you are picking up HR work

1. **The governing rules** (each is recorded in detail elsewhere; this is the list so none is
   re-derived):
   - **Payroll is another developer's module.** HR reads it, never edits it. If HR needs something
     payroll doesn't expose, raise it — the `docs/HR/integration/handoffs/HANDOFF-PAYROLL-*.md` shape. → [`HR-PAYROLL-BOUNDARY.md`](integration/HR-PAYROLL-BOUNDARY.md)
   - **HR posts to the General Ledger through ONE adapter, `IHrFinancePostingAdapter`, with one
     treatment** (since 2026-09-20; medical and travel are wired, the rest are queued). Never write
     a Finance journal, invent a payment-status machine or hard-code an account; add a catalogue
     event and a factory builder. Areas not yet wired still register their money events in
     `docs/HR/integration/HR-FINANCE-INTEGRATION-BACKLOG.md`. **Master data is not deferred** — read Finance's `Currency` and
     `ExchangeRate` through `HrCurrencyBridge`; never keep a parallel copy. → [`HR-FINANCE-POSTING-DESIGN.md`](integration/HR-FINANCE-POSTING-DESIGN.md), [`HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`](integration/HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md)
   - **Never build a module-specific approval UI.** Plug into the workflow engine with the
     four-step recipe. The one deliberate exception is `EmployeeGoal`. → [`HR-WORKFLOW-ENGINE-INTEGRATION.md`](integration/HR-WORKFLOW-ENGINE-INTEGRATION.md)
   - **A bulk endpoint loops the real service call.** Same authorization, same workflow
     transition, same notification, per item. → [`HR-BULK-OPERATIONS-CATALOGUE.md`](catalogues/HR-BULK-OPERATIONS-CATALOGUE.md) §4.1
   - **Ported services stamp `TenantId` explicitly.** The DbContext auto-stamp is inert. Fix
     per service; do not re-wire the global filter.
   - **Gate vertically with role/permission policies and horizontally on the ownership helper.**
     When the entitled party is named on the row (reviewer, subject, assignee), use plain
     `[Authorize]` and read entitlement off the record; a permission gate there makes the action
     reachable by nobody. Do not seed HR permissions per area — the `HR.<Area>.{Read,Write,Admin}`
     taxonomy is in `src/ErpSystem.Shared/HrPermissions.cs` (66 policies).
   - **Defects found in other teams' modules are recorded, not fixed** —
     [`integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`](integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md), actioned at finalization.
   - **Run harnesses in Staging with the JWT key passed in**, or every refusal is a 500 and every
     login a 400. → [`HR-VERIFICATION-HARNESS-GUIDE.md`](operations/HR-VERIFICATION-HARNESS-GUIDE.md)
   - **Stage, the user commits.** Never `git commit`; never `dotnet build` — stop and ask.
2. **Where the work stands:** [`programme/HR-FINISH-PLAN.md`](programme/HR-FINISH-PLAN.md) → "Where to start next". What was
   decided and why: [`programme/HR-CLOSURE-LEDGER.md`](programme/HR-CLOSURE-LEDGER.md) (decisions **D-01…D-40** in §B — ⚠ hand-curated;
   its generator writes a gitignored companion and must never overwrite it).
3. **What TDC still owes an answer on:** [`programme/HR-OPEN-QUESTIONS-FOR-TDC.md`](programme/HR-OPEN-QUESTIONS-FOR-TDC.md) (~16 items).

⚠ **Two decision namespaces collide.** The ledger's `D-NN` (D-01…D-40) and each build plan's own
area-local `D1`/`D2`/`D3` (e.g. area 16's "decision D1" = HR reads Finance fixed assets read-only,
area 16's "D3" = assets on the workflow engine). When citing an area-local one, say the area.

---

## How this folder is laid out

Every HR document lives under `docs/HR/`, grouped by subject (moved here 2026-09-20). What stays
loose in `docs/` is only what HR does not own alone: the three `HR_PAYROLL_ORACLE_*` crosswalks
(payroll's territory), `GEOGRAPHY-REFERENCE-DESIGN.md` (a shared reference module) and
`LOCAL-FAST-EF-BUILD.md` (repo-wide build tooling).

```text
docs/HR/
  README.md            this index
  areas/               one folder per functional area — the system guide plus that area's plans
    recruitment/  leave/  performance/  attendance/  travel/
    company-schedule/  employees/  she/
  integration/         every boundary HR shares with another module
    handoffs/          reports raised with another module's owner
  programme/           where the work stands: finish plan, ledger, open questions, demo rounds
  catalogues/          capability catalogues — the second programme
  operations/          running things: harnesses, the UAT database, the port migration
```

Documents carry a **"Vetting pass"** block from the 2026-09-02 pass that re-verified them against
the code, and the system guides carry their own dates. Read that block before acting on one.

---

## `areas/` — one folder per functional area

The screen-by-screen **system guides** live here beside the plans for the same area. A guide walks
every screen of one area from the source, with a plain-language layer for HR users and a technical
layer mapping each control to its endpoint, service and table; most double as a performable
demonstration workbook.

### `areas/recruitment/` — Recruitment

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-RECRUITMENT-SYSTEM-GUIDE.md`](areas/recruitment/HR-RECRUITMENT-SYSTEM-GUIDE.md) | 32 recruitment screens, establishment → hire | Walked 2026-09-14/15; its 70 gaps were closed 2026-09-15/16. **Its gap blocks are history, not a to-do list** — live state is in `HR-RECRUITMENT-GAP-CLOSURE-PLAN.md` |
| [`HR-RECRUITMENT-GAP-CLOSURE-PLAN.md`](areas/recruitment/HR-RECRUITMENT-GAP-CLOSURE-PLAN.md) | **All 70 gaps from the recruitment guide's Appendix C closed 2026-09-15/16.** Lane-by-lane record of what was changed and the two items still open | Anything recruitment — this, not the guide's gap blocks, is the live state |
| [`HR-RECRUITMENT-DEMO-DATA-INVENTORY.md`](areas/recruitment/HR-RECRUITMENT-DEMO-DATA-INVENTORY.md) | What the recruitment module holds, what the demo database holds, and where the two disagree (2026-09-14, `ErpSystemDB_UAT`) | Before regenerating recruitment demo data |

### `areas/leave/` — Leave

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-LEAVE-SYSTEM-GUIDE.md`](areas/leave/HR-LEAVE-SYSTEM-GUIDE.md) | The whole **Leave Management** menu group — 10 menu items, the 4-screen Leave Types rulebook under Administration, the 7 portal screens, and the reminder engine that has no screen. **25 screens, 23 chapters** | **Current as of 2026-09-18**, after three builds — the closure plan, the residue plan, and Wave 1 + 2a/2e of the entitlement plan. Also a demonstration workbook: **17 numbered live writes**, a reset chapter and a 25-minute short path. **Read the four rules above chapter 1 first** — leave takes **two** approvals, the Approvals screen shows what is waiting on *you*, `hr.head` has no Admin tier, and **the newest rules ship switched off**. ⚠ **Verified: `dev-harness/hr-leave`, 472 assertions across eleven slices, green twice.** § 23 lists all 37 original findings — **36 closed, 1 open (payroll's)** — plus the nine the entitlement plan added. ⚠ **§ 2.3b is not optional**: 53 of the demo database's 97 annual leave balances carry the wrong entitlement until somebody presses the repair button, and chapter 13 asks you to read that column aloud |
| [`HR-LEAVE-CLOSURE-PLAN.md`](areas/leave/HR-LEAVE-CLOSURE-PLAN.md) | **COMPLETE — waves A–E built 2026-09-17, F1 (the harness) done.** The 50-item gap register, eleven decisions, and 18 slices in six waves | ⚠ **History, not current state.** Two plans have landed on top of it: the residue plan (G1–G5) and the entitlement plan. For what the code does *today*, read the guide — it is the one document that has been kept current through all three builds |
| [`HR-LEAVE-RESIDUE-CLOSURE-PLAN.md`](areas/leave/HR-LEAVE-RESIDUE-CLOSURE-PLAN.md) | G1–G5 **built and verified** 2026-09-18 (including the endpoints no screen could reach, and the fifth in Medical); G6 surveyed leave's two settings entities and left the rest of HR's | Leave work after the closure plan; the settings survey instrument |
| [`HR-LEAVE-ENTITLEMENT-AND-YEAR-PLAN.md`](areas/leave/HR-LEAVE-ENTITLEMENT-AND-YEAR-PLAN.md) | **NOTHING BUILT, no decision taken.** The entitlement engine, accrual, and the assumption that a leave year is a calendar year — nine items in three groups, written up after the question *"how does leave work for someone who just joined?"* | ⚠ **Read § 3 before starting any slice** — all eight decisions are recommendations awaiting the module owner, unlike the two plans above it. § 4 is the one section to read if you read one: where each new value belongs, and why only two of eight go on the HR Policy Settings screen. ⚠ **W1a has a live consequence** — every Entitled figure on the demo database is the leave type's default rather than the employee's staff-level allocation |

### `areas/performance/` — Performance

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-PERFORMANCE-SYSTEM-GUIDE.md`](areas/performance/HR-PERFORMANCE-SYSTEM-GUIDE.md) | The whole **Talent & Performance → Performance** menu group — all 19 menu items + 15 unlisted detail routes, the 12 setup screens under Administration → HR → Performance, and the 15 portal screens. **62 screens over 51 tables**, and the largest guide in the series | Written 2026-09-17, verified field-by-field against `ErpSystemDB_UAT`. Also a demonstration workbook: 39 chapters, 16 numbered live writes, a reset chapter and a 25-minute short path. **Read the nine rules above chapter 1 first** — one cycle/profile/form is all there is (Rule 1), calibration is a real gate but the panel's number is overwritten at HR sign-off (Rule 2), Finalise is not disabled by that gate (Rule 3), nine goals sit at 0% so ~16 read as at risk (Rule 4), no TDC employee goal is aligned to anything (Rule 5), the conversations diary and the calibration Open tab are both empty (Rule 6), `hr.head` cannot delete anything (Rule 7), five fixture appraisals open completely empty (Rule 8), and an appeal can contest a competency only (Rule 9) |
| [`HR-APPRAISAL-SETTINGS-AUDIT.md`](areas/performance/HR-APPRAISAL-SETTINGS-AUDIT.md) | Field-by-field audit of the **Appraisal Settings** profile: which of its 50 values actually drive behaviour | Written 2026-09-17. **36 enforced · 9 advisory · 2 client-side only · 3 complete ghosts.** Every claim carries a `file:line`. The headline structural finding is that the module has **two pipeline resolvers with different rule sets** — `GetCurrentPhase` enforces, `AppraisalSubStatusResolver` (dashboard + deadline advance) has five extra gates — so the analytics screen reports a stricter pipeline than the system enforces. Ends with a prioritised fix list. Read it before describing the policy screen to a customer |
| [`HR-PERFORMANCE-SEED-GAP-PLAN.md`](areas/performance/HR-PERFORMANCE-SEED-GAP-PLAN.md) | What the performance demo dataset is still missing, and the order to do the work in | Written 2026-09-17. **Table coverage is already complete** — all 44 required tables hold rows, so `verify-tables.mjs` cannot see the problem. The gaps are **shape**: 17 states the data never reaches (S-1…S-17), six of which leave a screen blank. Sequencing: fix **three** defects first (P-55, P-28, P-40) because they make you seed data that lands wrong, decide P-39, then seed, then everything else. Ends with a proposal to add shape assertions to the coverage check |

### `areas/attendance/` — Attendance & Time

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md`](areas/attendance/HR-ATTENDANCE-TIME-SYSTEM-GUIDE.md) | The whole **Attendance & Time** menu group — 11 menu items + 10 unlisted screens, the 8 setup areas under Administration → Time, Attendance & Leave, and the portal's My Attendance. 36 screens over 24 tables | Written 2026-09-17. Also a demonstration workbook: 16 numbered live writes, a reset chapter and a 25-minute short path. **Read the five rules above chapter 1 first** — a punch never computes lateness (A-1), a pasted CSV import writes nothing while reporting success (A-2), Recalculate blanks four columns (A-3), a payroll export always reports zero (A-4), and `hr.head` cannot delete anything (A-5). **§ 1.7 is the prepared answer to the buddy-punching question** (A-93 / A-94) — the punch proves a session and a location, never a person, and the reader connector is the piece that would close it |

### `areas/travel/` — Staff Travel

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-STAFF-TRAVEL-SYSTEM-GUIDE.md`](areas/travel/HR-STAFF-TRAVEL-SYSTEM-GUIDE.md) | The whole **Staff Travel** menu group — all 6 menu items + 6 unlisted screens, the 2 setup areas under Administration → HR → Travel, and the 4 portal screens. 20 screens over 31 tables, with 7 tabs inside a single request | Written 2026-09-17. Also a demonstration workbook: 13 numbered live writes, a reset chapter and a 25-minute short path. **Read the six rules above chapter 1 first** — every travel policy is unapproved so no cap binds (T-1), `hr.head` cannot approve a policy or authorise a breach (T-2), two dropdown values on the request form 400 (T-3), the policy rule register is enforced by nothing and ships read-only on purpose (T-4), Finance's currency conversion is inverted and travel inherits it deliberately (T-5), and a cash advance is recovered on payment rather than approval (T-6) |

### `areas/company-schedule/` — Company Schedule

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md`](areas/company-schedule/HR-COMPANY-SCHEDULE-SYSTEM-GUIDE.md) | The whole **Company Schedule** menu group — four menu items (two added in round 4: *My Schedule* and *Team Schedule*) + 6 unlisted screens, the 4 setup areas under Administration → HR → Company Schedule, and the Company Profile screen (same permission family, different menu). 19 screens over 13 tables | Written 2026-09-17; **updated 2026-09-24 for round 4** (lanes D-1, D-2, N-b2). Also a demonstration workbook: 14 numbered live writes, a reset chapter and a 20-minute short path. **Read the eight rules above chapter 1 first.** Round 4 hid the event page's red Delete from `hr.head` but left eight other Admin-only removes on offer (C-1), keeps a rescheduled event's original dates without showing them (C-2), enforces the room's own rules (C-4) and stopped numbers repeating (C-6). Approve is still a flag (C-3), and closures still reach neither leave nor attendance (C-5). The two new rules are about the demo database: **nothing is emailed on it** (no mail server), and it must be **freshly rebuilt**, because the harness leaves fixtures behind. 26 round 4 findings, `R4-…`, listed in § 21 |

### `areas/employees/` — Employees & job analysis

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-EMPLOYEES-SYSTEM-GUIDE.md`](areas/employees/HR-EMPLOYEES-SYSTEM-GUIDE.md) | The whole **Employees** menu group — 14 screens, the 35-tab profile, and the 3 portal screens feeding its queues | Written 2026-09-16. Also a demonstration workbook: prep checklist, click-by-click walks, 9 numbered live writes and a reset chapter. **Chapter 2 is mandatory before demonstrating** — the demo database leaves 15 profile tabs and 4 menu items empty (finding E-12) |
| [`HR-EMPLOYEE-IMPORT-DESIGN.md`](areas/employees/HR-EMPLOYEE-IMPORT-DESIGN.md) | The bulk employee import: column mapping from TDC's own register and salary scale, the draft template, and the phase-0 decisions (§7) | Anything near the import wizard or the staff-number rule |
| [`HR-ORGANOGRAM-REDESIGN.md`](areas/employees/HR-ORGANOGRAM-REDESIGN.md) | **BUILT 2026-09-08, browser walk still outstanding (§9).** The custom render engine at `/hr/organogram`, three additive DTO fields, no migration | Touching the organogram screen or its export |
| [`HR-JOB-DUTY-VS-RESPONSIBILITY.md`](areas/employees/HR-JOB-DUTY-VS-RESPONSIBILITY.md) | The job-analysis distinction: a responsibility is an outcome you answer for, a duty is an activity you perform — and which machinery each feeds (appraisal and valuation vs the offer letter) | Authoring a job description; touching `JobAnalysisEntities.cs` |

### `areas/she/` — Safety, Health & Environment

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-SHE-INTEGRATION-AND-BOUNDARIES.md`](areas/she/HR-SHE-INTEGRATION-AND-BOUNDARIES.md) | The Safety, Health & Environment sub-module as a whole: its shape, its boundaries with Medical/Training/Maintenance/Inventory/Estate, its gates, jobs, money and known gaps | Any SHE work |
| [`HR-SHE-PPE-INVENTORY-INTEGRATION-DESIGN.md`](areas/she/HR-SHE-PPE-INVENTORY-INTEGRATION-DESIGN.md) | PROPOSED: Inventory owns PPE stock, SHE keeps policy and custody; the consume path, its traps, the two open decisions | Anything near `api/safety/ppe` or PPE stock |
| [`HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md`](areas/she/HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md) | APPROVED: the checklist template builder (sections, header fields, critical items, scoring bands, outcomes, signatories, versioning, print) and how an inspection runs against one | Any work on inspection checklists or the inspection run |

---

## `integration/` — the module boundaries

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-FINANCE-ACCOUNTING-PRIMER.md`](integration/HR-FINANCE-ACCOUNTING-PRIMER.md) | **Start here if you have no accounting background.** Teaches the accounting itself — receivable vs payable, debits and credits, accrual, clearing accounts, subledgers, the close — using only this repo's own accounts and events. Nine lessons, plus every HR/SHE event's journal and a glossary | Before your first money event; whenever a Finance term is doing work you can't follow |
| [`HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`](integration/HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md) | One-page briefing: the Finance rules, the governance message, the key facts | Any task that touches money |
| [`HR-FINANCE-ENTITY-SWEEP.md`](integration/HR-FINANCE-ENTITY-SWEEP.md) | Every HR/SHE entity that carries money (87 rows), its direction, its Finance target, and the 14 open decisions | Planning the GL sweep; adding a money field |
| [`HR-FINANCE-INTEGRATION-BACKLOG.md`](integration/HR-FINANCE-INTEGRATION-BACKLOG.md) | The living decision register for the post-module GL sweep (opened 2026-08-17; **the sweep started 2026-09-20**) | — |
| [`HR-FINANCE-POSTING-DESIGN.md`](integration/HR-FINANCE-POSTING-DESIGN.md) | **The one accounting treatment and what is built** (lane 8 slice 1): five account roles, recognise/settle/advance, the adapter on FIN-INT-001, the register, retry, reversal, the decisions taken and the events still queued | Wiring any HR money event; reading a Finance posting row |
| [`HR-PAYROLL-BOUNDARY.md`](integration/HR-PAYROLL-BOUNDARY.md) | Payroll is another developer's; what HR reads, the three bridges, defect #23, and which Finance-sweep items are the payroll owner's | Anything near salary, grades, pay components, `IsOnPayroll` |
| [`HR-MODULE-INTEGRATION-MAP.md`](integration/HR-MODULE-INTEGRATION-MAP.md) | Every other module HR/SHE touches (36 rows), both directions, with a verdict per link | Adding a cross-module FK; deciding who owns a record |
| [`HR-WORKFLOW-ENGINE-INTEGRATION.md`](integration/HR-WORKFLOW-ENGINE-INTEGRATION.md) | The four-step plug-in recipe, the traps, the engine defects, which families are on the engine | Wiring any approval |
| [`HR-WORKFLOW-AUTOAPPROVE-CLOSURE-PLAN.md`](integration/HR-WORKFLOW-AUTOAPPROVE-CLOSURE-PLAN.md) | **CLOSED 2026-09-16 across all 26 engine-wired HR services.** Submitting where no workflow definition is published used to approve the record outright — no approver, no SoD check | Wiring a new approval; auditing an existing one — read it beside the engine doc |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](integration/HR-CROSS-MODULE-PATTERNS-SWEEP.md) | Engineering patterns other modules solved that HR hasn't adopted (notifications, concurrency, budget commitment, audit, asset lifecycle) | Designing a new HR capability |
| [`CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`](integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md) | Defects HR found in other teams' modules (#1–#23); recorded, not fixed | — |

### `integration/handoffs/` — raised with another module's owner

Each is self-contained: what is broken · what was proven · what it blocks · what a fix needs.

| Document | What it answers | Read it when |
|---|---|---|
| [`HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md`](integration/handoffs/HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md) | Defect **#23** — payroll's upsert cannot create a profile. HR carries a workaround to be deleted once it is fixed. **The template for raising anything with another module's owner** | Raising anything with another owner; touching payroll membership |
| [`HANDOFF-PAYROLL-LEAVE.md`](integration/handoffs/HANDOFF-PAYROLL-LEAVE.md) | Three leave settings HR stores correctly and only payroll can honour (2026-09-17, wave B3) | Leave settings that imply a deduction |
| [`HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md`](integration/handoffs/HANDOFF-PAYROLL-HR-SETTINGS-REGISTER.md) | Four concepts modelled twice across HR and payroll, with no bridge. A register, not a change request — **nothing in it has been built** | Deciding which system owns a salary-shaped setting |
| [`HANDOFF-FINANCE-HR-POSTING-ROUTES.md`](integration/handoffs/HANDOFF-FINANCE-HR-POSTING-ROUTES.md) | HR now posts through FIN-INT-001; the four things only Finance can settle — routes, the clearing account, FX evidence, a catalogue row | Anything Finance asks about HR journals |
| [`HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md`](integration/handoffs/HANDOFF-FINANCE-HR-RECRUITMENT-COST-AP.md) | Not a defect — an integration request: recruitment costs need Finance approval and payment. HR built its half (R7); the adapter (R8) waits on the answers in §5 | Picking up R8; anything near `StaffRequisitionCost` |

---

## `programme/` — where the work stands

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-FINISH-PLAN.md`](programme/HR-FINISH-PLAN.md) | Everything HR still owes, in lanes 0–9 + 3f, each with a size, a blocker and a "done when". **Start with its "Where to start next" table.** | — |
| [`HR-CLOSURE-LEDGER.md`](programme/HR-CLOSURE-LEDGER.md) | Hand-curated ledger: position table (2,157 write endpoints, 1,908 wired), §A decisions, §B blockers D-01…D-40, §C build checklists, §D dispositions, §E fields no form can set, §F demo-feedback backlog, §G out of scope | — |
| [`HR-OPEN-QUESTIONS-FOR-TDC.md`](programme/HR-OPEN-QUESTIONS-FOR-TDC.md) | Questions and data asks needing a TDC answer | — |
| [`HR-CONFIGURATION-REGISTER.md`](programme/HR-CONFIGURATION-REGISTER.md) | Every HR setting surveyed against the code: enforced, advisory, client-side only or a ghost. All of `CompanyHrPolicySettings`, `LeaveType` and leave's four child tables | Before promising a customer that a settings screen does something |
| [`HR-FINANCE-INTEGRATION-EXECUTIVE-BRIEF.md`](programme/HR-FINANCE-INTEGRATION-EXECUTIVE-BRIEF.md) | **The stakeholder-facing account of lane 8.** What now reaches Finance automatically (26 events), who does what, the two capabilities that ship switched OFF, and the three decisions TDC owes — cost attribution, the payroll component mapping, budgets. No accounting or technical detail | Briefing TDC or the Finance owner; any conversation about what the integration delivers. ⚠ It supersedes the presentation plan's "Finance posting is deferred / preview-only" framing |
| [`HR-UAT-DEMO-PRESENTATION-PLAN.md`](programme/HR-UAT-DEMO-PRESENTATION-PLAN.md) | The two-day acceptance demo: agenda, sign-off matrix, what changed since it was drafted. ⚠ Drafted 2026-08-31, **before lane 8**: its §23 still lists Finance posting as preview-only. Read the executive brief beside it | Preparing the demo |
| [`HR-DEMO-FEEDBACK-ROUND-3-PLAN.md`](programme/HR-DEMO-FEEDBACK-ROUND-3-PLAN.md) | PLANNED 2026-09-11: every bullet of the round-3 demo feedback (employee details, job description, staff unions, requisition, recruitment) mapped to code, the eight decisions and thirteen defaults, nineteen slices in order, and the Finance/payroll asks (banks wait on Finance) | Picking up any round-3 item; before touching salary writes, candidate fields or shortlisting criteria |
| [`HR-DEMO-FEEDBACK-ROUND-2B-RECRUITMENT-PLAN.md`](programme/HR-DEMO-FEEDBACK-ROUND-2B-RECRUITMENT-PLAN.md) | IN BUILD 2026-09-10: round 2b — the recruitment section of the second feedback document (manpower budget ↔ establishment ↔ requisition, retirements in the plan, salary from the scale, Excel round-trip) plus the Finance follow-up on requisition costs; nine slices R1–R8, the decisions, and the ask to the Finance owner | Any manpower-budget, establishment or requisition item; before touching `StaffRequisitionCost` |
| [`HR-DEMO-FEEDBACK-ROUND-2-PLAN.md`](programme/HR-DEMO-FEEDBACK-ROUND-2-PLAN.md) | PLANNED 2026-09-08: every bullet of the round-2 demo feedback (org structure, positions, skills, employee profile) mapped to code, the five decisions taken, six build lanes, the payroll/finance asks, and the finish-plan claims it corrects | Picking up any round-2 item; before trusting lane 3a's "done" ticks |

---

## `catalogues/` — the capability catalogues

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-REPORTS-CATALOGUE.md`](catalogues/HR-REPORTS-CATALOGUE.md) | What reports exist, which are missing, the three patterns, and the one to default to | Building a report or export |
| [`HR-BULK-OPERATIONS-CATALOGUE.md`](catalogues/HR-BULK-OPERATIONS-CATALOGUE.md) | Which single-item actions need a bulk sibling, what exists, how to build one safely | Building a multi-select action |
| [`HR-IMPORT-EXPORT-CATALOGUE.md`](catalogues/HR-IMPORT-EXPORT-CATALOGUE.md) | Which entities need file import/export, what exists, the staff-number rule an importer must honour | Building an import |
| [`HR-DOCUMENTATION-CATALOGUE.md`](catalogues/HR-DOCUMENTATION-CATALOGUE.md) | Documentation as a deliverable: what to produce for developers, users and stakeholders | Planning docs work |

**Deliberately not in `programme/HR-FINISH-PLAN.md`:** these catalogues (reports, bulk,
import/export, documentation) plus the UAT presentation plan are a **second programme**, excluded
from the finish plan by the user's decision. Do not fold them in without being asked.

---

## `operations/` — running it

| Document | What it answers | Read it when |
|---|---|---|
| [`HR-VERIFICATION-HARNESS-GUIDE.md`](operations/HR-VERIFICATION-HARNESS-GUIDE.md) | Where the harnesses are, how to run them, the environment traps, the fixture conventions, what green proves | Before any harness run |
| [`HR-FINANCE-POSTING-OPERATIONS-MANUAL.md`](operations/HR-FINANCE-POSTING-OPERATIONS-MANUAL.md) | **How to run finance posting day to day**, per role: what changes for the HR desk, reading the Finance box on a record, the register and its five statuses, setting up account roles and events, reversal, what the Finance desk receives, the messages you will actually see, and the routine checks | Operating or supporting the integration; onboarding anyone onto the HR or Finance desk; writing the training material |
| [`UAT-DEMO-DATABASE.md`](operations/UAT-DEMO-DATABASE.md) | Build / switch / reset / check the `ErpSystemDB_UAT` demo database | — |
| [`hr-port-data-migration.md`](operations/hr-port-data-migration.md) | Data-migration runbook for the HR port | — |

---

## HR material elsewhere in the repo

| Path | What it is |
|---|---|
| `scripts/New-UatDatabase.ps1` | Build / switch / reset / check `ErpSystemDB_UAT` — the runbook is `operations/UAT-DEMO-DATABASE.md` |
| `scripts/hr-coverage/` | The coverage instruments; `04_build_ledger.py` writes a **gitignored companion** to `programme/HR-CLOSURE-LEDGER.md` and must never overwrite it |
| `dev-harness/hr-demo-smoke/runbook/` | Books 0–4 + cheat-sheet: the per-persona, click-by-click demo script (print-to-PDF HTML) |
| `docs/HR_PAYROLL_ORACLE_FORMS_MIGRATION_PLAN.md`, `…_MENU_FIELD_CROSSWALK.md`, `…_REPORTS_CROSSWALK.md` | Legacy Oracle Forms payroll → new system (payroll owner's territory) |
| `docs/GEOGRAPHY-REFERENCE-DESIGN.md` | The shared administrative-geography model — every module that stores an address, not just HR |
| `docs/LOCAL-FAST-EF-BUILD.md` | The fast EF build, and why `FastBuildMigrationMetadata.cs` is gone (repo-wide) |
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
closure ledger; the SHE plan's substance is summarised in [`areas/she/HR-SHE-INTEGRATION-AND-BOUNDARIES.md`](areas/she/HR-SHE-INTEGRATION-AND-BOUNDARIES.md).

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
