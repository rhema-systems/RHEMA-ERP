# HR Documentation Catalogue — What to Produce, For Whom, and Where It Should Live

**Generated:** 2026-08-31
**Purpose:** A comprehensive list of the documentation and outputs the HR module should produce
for its three real audiences — developers, end users, and other stakeholders — with a concrete
recommendation for where and how each should be exposed.

---

## 0. Relationship to the other docs in this folder

| Document | Focus |
|---|---|
| [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) | Money-carrying entities and Finance interaction. |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md) | Reusable engineering patterns HR hasn't adopted. |
| [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) | What reports HR needs and how to build them. |
| [`HR-BULK-OPERATIONS-CATALOGUE.md`](HR-BULK-OPERATIONS-CATALOGUE.md) | Which single-item actions need a bulk equivalent. |
| **This document** | Documentation itself, as a deliverable: what to write, for whom, and where it lives. |

This document is partly about the four docs above: they are **developer** documentation, and §3.1
below places them in the wider picture rather than repeating them.

---

## ⚠ Vetting pass, 2026-09-02 — corrections, and what this pass itself produced

| Where | Verdict | Corrected fact |
|---|---|---|
| §1 / §2.1 — "14 detailed build plans" | Count wrong. | `plans/HR-Area-*.md` = **12** (11, 12, 13, 14, 15b, 16, 17, 19-23, 25, 25-Slice0-Census, 9b, 9c) + three other HR plans (Consultant-Client Portal closure, Recruitment closure, W3 permissions sweep) = **15**. The named list in §2.1 was right; the number was not. |
| §1 / §2.1 — "the five docs in this folder" | Now **14**. | Nine existed by 2026-09-02 (this pass added the finance quick reference, import/export catalogue and module integration map to the original six); this pass added five more: `README.md`, `HR-PAYROLL-BOUNDARY.md`, `HR-SHE-INTEGRATION-AND-BOUNDARIES.md`, `HR-WORKFLOW-ENGINE-INTEGRATION.md`, `HR-VERIFICATION-HARNESS-GUIDE.md`. |
| §1 / §3.1 — "a live, auto-generated API reference … already exposed at `docs/api/swagger.json` / `docs/api/html/index.html`" | **Aspirational.** | `.github/workflows/documentation.yml` exists and would fetch swagger and commit to master, but `docs/api/` in the repo contains only `JobCardCompletionWorkflow.md` — **no swagger.json, no html**. The artefact has never landed (or is never committed). Treat the API reference as *not yet published* until someone checks the workflow's run history. |
| §3.1 — "HR docs index — missing" | **Now written.** | `docs/HR/README.md` (this pass). `docs/README.md` still does not link it — that one line is still owed (platform file). |
| §3.1 — "Decision log (ADR-equivalent) — missing, actively confusing" | **Largely wrong — it exists.** | `docs/HR-CLOSURE-LEDGER.md` §B is the decision register, **D-01…D-40**, hand-curated (⚠ its generator once destroyed it; it now writes a gitignored companion). The D-numbers cited across this folder resolve there. What is still missing is the *area-level* decisions embedded in build plans ("decision D1" in the assets area is area 16's D1, not the ledger's D-01) — the namespace collision is the real confusion, and the index in `README.md` now says which is which. |
| §3.1 — "Testing guide — missing" | **Now written.** | `docs/HR/HR-VERIFICATION-HARNESS-GUIDE.md`. |
| §3.1 — "HR-originated integration contracts — missing" | Partly covered now. | `HR-PAYROLL-BOUNDARY.md` (the three bridges and the defect #23 fallback), `HR-WORKFLOW-ENGINE-INTEGRATION.md` (how HR plugs into the engine), and the integration map's §12/§13 (what other modules take from HR). A FIN-INT-style numbered catalogue is still not written; the material to number is now in one place. |
| §3.1 — requirement-ID namespaces | Confirmed. | 30 distinct FR-HR, 20 FR-SHE, 26 FR-ENV, 1 FR-CON ids exist; the SHE/ENV/CON ones appear only in `src/` (never in `plans/`). |
| §3.3 — access-control matrix "missing" | Right, with the source named. | The matrix *is* `src/ErpSystem.Shared/HrPermissions.cs`: **66 permissions / 66 policies across 22 categories**, with `RoleGrants` and `GrantsFor`. Rendering that class is the compliance-pack task; `plans/HR-W3-Permissions-Sweep-Build-Plan.md` is the narrative. `ExternalUserAccessMiddleware` is the first layer above it (a prefix allowlist for external tokens). |
| §3.2 — "in-app help … needs a small reusable help-tooltip component if one doesn't already exist (not found)" | Confirmed. | No `HelpTooltip`/`InfoTooltip`/`/help` route in the frontend (only `/helpdesk`). |
| §2.1 — HR docs directly under `docs/` | Incomplete. | Also: `HR-CLOSURE-LEDGER.md`, `HR-FINISH-PLAN.md`, `HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md`, `UAT-DEMO-DATABASE.md`, `demo-runbook/` (Books 0–4 + cheat-sheet), `hr-port-data-migration.md`, `CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`. All indexed in `README.md`. |
| §6 priorities | Re-sequenced. | Items 1 (index) and 5's decision-log half are done; the testing guide is done. Remaining order: ESS quick-start + FAQ → Payroll admin guide (packaging, and coordinate with the payroll owner) → HR officer guide → FIN-INT-style HR contract catalogue → compliance pack (render `HrPermissions`) → Help Center infrastructure → stakeholder outputs. |

---

## 1. Executive summary

**HR's documentation today is almost entirely developer-facing, produced ad hoc, and not
indexed anywhere a person would find it.** Concretely, what already exists:

- **14 detailed build plans** (`plans/HR-Area-*.md`) — one per functional area, genuinely
  thorough, but there is no index of them; a new developer has to already know they exist.
- **A handful of root-level and `docs/`-level HR docs** — `HR_MODULE_PORT_PLAN.md`,
  `HR_PORT_CROSS_MODULE_CHANGES.md`, the three `HR_PAYROLL_ORACLE_*` migration/crosswalk docs,
  `HR-OPEN-QUESTIONS-FOR-TDC.md`, `HR-FINANCE-INTEGRATION-BACKLOG.md`.
- **The five docs in this `docs/HR/` folder**, all produced this session.
- **A live, auto-generated API reference** — `.github/workflows/documentation.yml` builds an
  OpenAPI spec and an interactive HTML explorer from Swagger on every push (`docs/api/swagger.json`,
  `docs/api/html/index.html`), covering HR's controllers along with every other module's.
- **A repo-wide `docs/README.md` index** — but it does not mention HR at all, and one line in
  it (`*Documentation auto-generated on $(date)*`) is a template placeholder that was never
  substituted, which is worth noting as a cautionary example: an index nobody wired up is worse
  than no index, because it looks authoritative while being wrong.

**What does not exist at all today:** any end-user-facing documentation (no user guide for any
HR role, no in-app help, no FAQ, no onboarding walkthrough), and any stakeholder-facing output
beyond the TDC open-questions doc (no compliance pack, no executive brief, no data-protection
statement, no UAT sign-off template).

**The core recommendation:** treat HR documentation as three genuinely different products with
three different homes, not one folder trying to serve everyone —

1. **Developer documentation** stays as source-controlled markdown in `docs/`/`docs/HR/`/`plans/`
   plus the already-working auto-generated API reference. It needs an **index**, not a new format.
2. **End-user documentation** needs a **new home**: an in-app Help Center plus role-based guides,
   because HR staff and employees will never open this repository.
3. **Stakeholder documentation** needs periodic, **curated summaries** pulled from the other two
   — not raw developer docs handed to an executive, and not a user guide handed to an auditor.

---

## 2. Inventory of what already exists

### 2.1 Developer-facing (exists)
- `plans/HR-Area-*.md` ×14 — per-area build plans (Medical, Travel, Succession, Awards,
  Probation/Confirmation, HR Assets, Job Architecture/Competency/Establishment, Tier B tail areas,
  Employee Self-Service, a slice-0 census, Separation/Exit, Grievance/Employee Relations,
  Consultant/Client Portal closure, Recruitment closure, and a permissions sweep).
- `HR_MODULE_PORT_PLAN.md`, `HR_PORT_CROSS_MODULE_CHANGES.md` — the port strategy and its
  ripple effects on other modules.
- `docs/HR_PAYROLL_ORACLE_FORMS_MIGRATION_PLAN.md`,
  `docs/HR_PAYROLL_ORACLE_FORMS_MENU_FIELD_CROSSWALK.md`,
  `docs/HR_PAYROLL_ORACLE_REPORTS_CROSSWALK.md` — legacy-to-new mapping for Payroll specifically.
- `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`, `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` — living
  decision/question registers.
- `docs/HR/*.md` ×14 (this folder, as of 2026-09-02) — see `README.md` for the index.
- `docs/HR-CLOSURE-LEDGER.md` (decisions D-01…D-40, blockers, endpoint checklists),
  `docs/HR-FINISH-PLAN.md` (what is still owed, by lane), `docs/UAT-DEMO-DATABASE.md`,
  `dev-harness/hr-demo-smoke/runbook/`, `docs/HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md`,
  `docs/hr-port-data-migration.md`, `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`.
- A CI workflow that *would* publish Swagger/OpenAPI + an HTML explorer to `docs/api/` — but the
  artefact has never landed in the repo (corrected 2026-09-02).

### 2.2 End-user-facing (does not exist)
Nothing found: no user guide, no in-app contextual help, no onboarding walkthrough, no FAQ, no
release notes aimed at end users, in any HR area.

### 2.3 Stakeholder-facing (mostly does not exist)
Only `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` — and that is a narrow slice (specific policy questions
needing a TDC decision), not a general-purpose stakeholder communication.

---

## 3. Master list, by audience

### 3.1 Developer documentation

| Output | Purpose | Status | Where it should live |
|---|---|---|---|
| **HR docs index** | One page listing every developer doc that exists for HR (the 14 in this folder, the 15 build plans, the migration/crosswalk docs, the ledger/plan/backlog/open-questions registers) with a one-line description each | ✅ **written 2026-09-02** — `docs/HR/README.md` | Still owed: one link line from the repo-wide `docs/README.md` (which currently doesn't mention HR at all — platform file) |
| **API reference** | Endpoint-level reference for every HR controller | 🟠 workflow exists, artefact never published (`docs/api/` holds no swagger/html) | `.github/workflows/documentation.yml` — check its run history; when it works, **make sure HR controllers carry XML doc comments and `[ProducesResponseType]` attributes so the output is useful**, rather than building a second API doc by hand |
| **Data dictionary / entity glossary** | A single reference of all **596** HR entity classes (648 with Payroll; 88 of them SHE), their purpose and key fields, without the money/pattern framing already covered elsewhere | 🟡 partially covered (the Finance sweep now inventories 87 money-carrying rows; the integration map §13 lists what other modules FK to; a full glossary does not exist) | `docs/HR/` — generate it from the entity files rather than writing it by hand; 596 classes is too many to maintain manually |
| **Decision log (ADR-equivalent)** | The D-numbers cited across this folder | ✅ **exists** — `docs/HR-CLOSURE-LEDGER.md` §B holds **D-01…D-40** (corrected 2026-09-02). ⚠ Two namespaces collide: the ledger's D-NN and each build plan's own area-local D1/D2/D3 (e.g. area 16's "decision D1" = HR reads Finance fixed assets read-only). | Keep the ledger as the register; when citing an area-local decision, name the area ("area 16 D1"), never the bare number |
| **Requirement-ID namespace note** | Confirmed 2026-08-31: requirement IDs are not all `FR-HR-###`. Safety/SHE uses its own `FR-SHE-###`, `FR-ENV-###` and `FR-CON-###` namespaces (e.g. FR-SHE-200 stop-work authority, FR-ENV-033 monthly environmental report, FR-CON-001 contractor ranking), found nowhere in the main `plans/HR-Area-*.md` set | 🔲 not indexed anywhere | Fold into the same decisions-log index above rather than a separate document — one place that resolves any requirement ID regardless of which prefix it uses |
| **HR-originated integration contracts** | Finance's `finance-integration-contract-catalogue.md` (FIN-INT-###) is the model: a stable ID, an owner, a status, and a boundary description for anything one module calls into another. HR is a **producer** for: the built `SendForMaintenanceAsync` → `AssetAdmission` push; the `FleetTrip` creation from travel bookings; the asset-surcharge/rental payroll-deduction projections; the salary-structure and pay-component projections *from* payroll; the payroll-membership push; and ~60 Maintenance FKs that read HR's `Employee` | 🟡 the material is now in `HR-PAYROLL-BOUNDARY.md`, `HR-WORKFLOW-ENGINE-INTEGRATION.md` and `HR-MODULE-INTEGRATION-MAP.md` §12–13; not yet numbered as a catalogue | `docs/HR/hr-integration-contract-catalogue.md`, modeled directly on the Finance one, numbering what those three docs already describe |
| **Testing guide** | Where HR harnesses live, how to run them, the environment traps, the fixture conventions, what "done" looks like for a slice | ✅ **written 2026-09-02** — `docs/HR/HR-VERIFICATION-HARNESS-GUIDE.md` | — |
| **New-developer onboarding page** | "Start here if you're picking up HR work" — links to the docs index, the open-questions register, the Finance backlog, and states the governing rules already established | ✅ **written 2026-09-02** — top of `docs/HR/README.md` | — |

### 3.2 End-user documentation

HR has genuine end-user variety — this is not one audience. At minimum:

| Role | What they need | Status |
|---|---|---|
| **Every employee** (self-service) | How to update contact details, request leave, submit a travel claim, view payslips, request an HR letter, acknowledge a policy, view benefits | 🔲 nothing found, despite Area 25 (Employee Self-Service) being a real, built feature |
| **Line manager** | How to approve leave/travel/goals, run appraisals, initiate a staff movement, respond to a grievance escalation, use whatever bulk-approval tooling results from `HR-BULK-OPERATIONS-CATALOGUE.md` | 🔲 nothing found |
| **HR officer/administrator** | Full administrative capability across every HR area — the largest and most complex guide needed | 🔲 nothing found |
| **Payroll administrator** | Running a payroll cycle, the Oracle-crosswalk report set, statutory submissions (PAYE/SSF), bank schedule generation — the highest-stakes, most compliance-sensitive role | 🔲 nothing found |
| **Recruiter/hiring manager** | Vacancy creation, shortlisting, interview scheduling, offer management | 🔲 nothing found |
| **Safety/SHE officer** | Incident reporting/investigation, permit-to-work issuance, risk assessments, stop-work authority, audits, environmental reviews — confirmed 2026-08-31 to be a large (60+ entity), largely undocumented sub-module in its own right, not a minor add-on to a general HR guide | 🔲 nothing found |

**Outputs to produce per role:**
1. **A role-based user guide** — task-oriented ("how do I request leave"), not a feature-by-feature
   tour of every screen.
2. **A first-login quick-start checklist** — the 3-5 things a new user of that role should do
   first (complete profile, check benefits enrollment, review pending approvals, etc.).
3. **In-app contextual help** — short inline explanations on the fields/screens most likely to
   confuse someone (e.g. what "encashment" means, why a leave request needs a reliever, what a
   benefit-in-kind valuation means for tax). None of this exists today.
4. **An FAQ / troubleshooting page** — the recurring questions any support desk gets ("why is my
   payslip not showing," "why can't I submit a leave request," "who approves my travel claim").
5. **Release notes for end users** — plain-language "what changed" whenever a slice ships,
   distinct from the developer-facing PR/build-plan history.
6. **Printable one-pagers for process-critical tasks** — e.g. a one-page "how to file a grievance"
   for staff who may not regularly use the system, mirroring the printable Asset Register Report
   convention already established in `HR-REPORTS-CATALOGUE.md`.

**A pattern already built in HR that user *guides* should reuse, not duplicate:** `HrPolicyDocument`
+ `HrPolicyAcknowledgement` already implements publish → distribute → require-acknowledgement for
company *policies*. A mandatory-reading user guide (e.g. "how payroll now works after the Oracle
migration") is the same shape of problem — publish it through the same mechanism rather than
inventing a second one just because the content is procedural rather than policy.

### 3.3 Stakeholder documentation

| Stakeholder | What they need | Status |
|---|---|---|
| **TDC leadership / executives** | A short, periodic HR module brief: headcount, turnover, key KPIs, what shipped, what's still open — not a raw dashboard export | 🔲 missing (raw dashboards exist per `HR-REPORTS-CATALOGUE.md`, a curated executive summary does not) |
| **Finance team** | The current state of HR-Finance integration: what posts today, what's deferred, what decisions are still open | ✅ effectively covered by `HR-FINANCE-INTEGRATION-BACKLOG.md` and the entity sweep — **already good**, just make sure Finance's team actually knows these exist |
| **IT / Security / Compliance** | Data retention policy for HR records, an access-control matrix (who can see salary/medical/bank/disciplinary data), a description of the audit trail mechanism, and — per the open question already flagged in `HR-REPORTS-CATALOGUE.md` §5.12 — how exported personal data is governed | 🔲 missing — but the matrix's **source** is `src/ErpSystem.Shared/HrPermissions.cs` (66 permissions / 66 policies / 22 categories, `RoleGrants`), layered under `ExternalUserAccessMiddleware`'s prefix allowlist; render, don't author |
| **Auditors (internal/external)** | Same as above, plus: how a specific historical decision (a disciplinary sanction, a separation settlement, a payroll adjustment) can be reconstructed from the system | 🔲 missing — the mechanism exists (audit interceptor, workflow history) but nothing explains it in stakeholder terms |
| **Regulators (GRA, SSNIT/SSF, Labour Department)** | Nothing produced *for* them directly by this system, but the statutory reports in `HR-REPORTS-CATALOGUE.md` §3.17 are the artifacts that ultimately satisfy them — worth a short compliance-mapping doc that says which system report satisfies which legal obligation | 🔲 missing |
| **TDC's HR/Payroll operations team, during cutover from Oracle Forms** | A change-management/training/rollout plan — this is a real transition (see the Oracle crosswalk docs), and the people running payroll today need a plan for how they start running it in the new system, not just a technically correct crosswalk | 🔲 missing |
| **Project sponsors, during UAT** | A sign-off template per HR area — acceptance criteria, what was tested, what TDC is being asked to accept | 🔲 missing (build plans describe what was built; nothing packages it as an acceptance artifact) |

---

## 4. Where and how each should be exposed

| Category | Primary channel | Format | Update cadence | Notes |
|---|---|---|---|---|
| Developer architecture/design docs | `docs/`, `docs/HR/`, `plans/` (source-controlled) | Markdown | As each area/slice ships | Already the working convention — just needs the index (§3.1) |
| API reference | Auto-generated, `docs/api/` | OpenAPI JSON + static HTML explorer | Every push (CI already does this) | No action needed beyond keeping controllers well-annotated |
| Decision log / integration contracts | `docs/HR/` | Markdown, ID-indexed | As decisions are made / contracts change | New, modeled on Finance's existing contract catalogue |
| Role-based user guides | **New: in-app Help Center** (a `/help` section in the frontend) | Structured articles, searchable, task-oriented | Whenever a workflow changes materially | Do not bury these in the source-controlled `docs/` folder — the audience will never open a code repository |
| In-app contextual help | Inline tooltips/help icons on the relevant screens | Short inline text, linking out to the full Help Center article for depth | With each screen's own release | Needs a small, reusable "help tooltip" component if one doesn't already exist (not found in this codebase today) |
| FAQ / troubleshooting | Same Help Center | Searchable Q&A | Ongoing, driven by support-desk tickets | Keep a lightweight feedback loop from support tickets → new FAQ entries |
| Release notes (end-user) | In-app "What's New" panel, or at minimum an email/notification | Plain-language, short | Per release/slice that touches an end-user-visible workflow | Ties directly to the notification gap already flagged in `HR-CROSS-MODULE-PATTERNS-SWEEP.md` §3.1 — worth building alongside that fix, not separately |
| Printable one-pagers / mandatory-reading guides | Distributed through `HrPolicyDocument`/`HrPolicyAcknowledgement` | PDF, with acknowledgement tracking | As needed | Reuse the existing mechanism (§3.2) rather than a new one |
| Executive brief | Periodic PDF/slide summary, or a dedicated read-only exec dashboard view | Short, KPI-first | Monthly/quarterly | Should pull from the same dashboards in `HR-REPORTS-CATALOGUE.md`, curated rather than raw |
| Compliance/audit/data-protection pack | `docs/HR/` for the source content; a formatted export for anything handed to an external auditor | Markdown source, PDF export for external sharing | Reviewed at least annually or on regulatory change | Keep the authoritative version in-repo; export a static copy when it needs to leave the organisation |
| Statutory-compliance mapping | `docs/HR/` | Markdown, table of "obligation → system report/feature" | On regulatory change | Complements, doesn't replace, the Oracle reports crosswalk |
| Cutover/training/rollout plan | Project management artifact — could live in `plans/` alongside the build plans, or wherever TDC's own change-management materials live | Plan document + a session/training-material set (outside this repo's normal scope, flag for the project team) | Once, ahead of go-live, revised per wave if phased | This is the one output here that may legitimately live partly outside the codebase (training slides, a rollout calendar) — but the plan itself should still be indexed from `docs/HR/README.md` so developers know it exists |
| UAT sign-off template | `docs/HR/` or `plans/`, one instance per area | A short template: scope, acceptance criteria, tester, date, outcome | Once per area, at the point it's presented for acceptance | Could be a single reusable template document plus one filled-in instance per area |

---

## 5. Cross-cutting considerations

1. **Every document needs a visible "last updated" date and, ideally, a version.** The five docs
   already in this folder do this (`**Generated:** 2026-08-31`); carry the convention forward.
   `docs/README.md`'s literal, un-substituted `$(date)` placeholder (§1) is the cautionary
   counter-example — a date that was never actually filled in is worse than no date, because it
   looks trustworthy while being false.
2. **Ownership.** Developer docs are naturally owned by whoever is building that area. User guides
   and the Help Center should have an explicit owner (HR/training function, not engineering) or
   they will drift the moment a workflow changes and nobody updates the guide. Stakeholder
   documents need an owner who is close enough to the work to keep them accurate but senior enough
   to represent it externally.
3. **Docs must not go stale silently.** Tie user-guide updates to the same slice/PR process that
   changes the underlying workflow, the same way this session's docs were updated the moment new
   facts (PRs #70/#72, the corrected reporting-framework claim) came in, rather than left wrong.
4. **Security of the documents themselves.** Internal decision registers
   (`HR-FINANCE-INTEGRATION-BACKLOG.md`, the proposed decision log) can reference sensitive
   business logic and should stay developer/internal-only. User guides must never be illustrated
   with screenshots of real employee data — use synthetic example data, the same discipline this
   session's docs already apply by describing entities rather than dumping live records.
5. **Accessibility and reading level.** Role-based user guides are for HR staff and general
   employees, not developers — plain language, task-first structure ("how do I…"), not the
   architecture-document register used in `docs/HR/`. Confirm with TDC whether guides need to be
   bilingual or otherwise localized; this codebase gives no signal either way, so treat it as an
   open question alongside the others already tracked in `docs/HR-OPEN-QUESTIONS-FOR-TDC.md`.
6. **Don't let the API reference become the only "developer doc."** Swagger tells you what an
   endpoint accepts; it cannot tell you *why* HR decided a surcharge is a receivable rather than a
   payroll deduction, or why `AssetAdmission` should copy `ProjectService.MaintenanceFollowThrough`
   rather than reinvent it. That reasoning only exists in the build plans and this folder's docs —
   protect it by indexing it, not by assuming the auto-generated API docs make it redundant.

---

## 6. Suggested build priority

1. ~~**`docs/HR/README.md` index**~~ — **done 2026-09-02.** Still owed: the one link line in
   `docs/README.md`.
2. **Employee Self-Service quick-start + FAQ** — Area 25 is already built and has real daily
   users; this is the highest-return first user-facing doc.
3. **Payroll Administrator guide** — highest compliance stakes, and the Oracle-crosswalk docs
   already contain most of the raw material; this is packaging existing knowledge for a different
   audience more than new research. ⚠ Coordinate with the payroll developer — it documents their
   module.
4. **HR Officer/Administrator guide** — the largest guide, build incrementally per sub-module
   rather than as one document, likely mirroring this folder's sub-domain groupings.
5. **HR-originated integration contract catalogue** — a numbering exercise over
   `HR-PAYROLL-BOUNDARY.md`, `HR-WORKFLOW-ENGINE-INTEGRATION.md` and the integration map §12–13.
   (The decision log half of this item is done: the closure ledger is the register.)
6. **Data-protection / access-control / audit-trail compliance pack** — do this once the
   Finance-sweep's sensitivity/masking decisions (already flagged across `HR-FINANCE-ENTITY-SWEEP.md`
   and `HR-REPORTS-CATALOGUE.md`) are settled, so the pack describes the real access model rather
   than a moving target.
7. **In-app Help Center infrastructure, contextual tooltips, and the "What's New" panel** — the
   biggest engineering lift in this list; sequence after the content above exists in some form
   (even as plain markdown) so building the delivery mechanism isn't blocked on having nothing to
   put in it.
8. **Executive brief, statutory-compliance mapping, cutover/training plan, UAT sign-off template**
   — stakeholder-facing outputs, reasonably built last since each draws on the developer and
   user-facing material above being reasonably mature first.
