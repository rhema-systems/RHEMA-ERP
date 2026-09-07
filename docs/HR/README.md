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
| [`HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md`](HR-SHE-INSPECTION-CHECKLIST-BUILDER-DESIGN.md) | APPROVED: the checklist template builder (sections, header fields, critical items, scoring bands, outcomes, signatories, versioning, print) and how an inspection runs against one | Any work on inspection checklists or the inspection run |
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
