# HR Module UAT / Acceptance Demo — Presentation Plan

**Generated:** 2026-08-31
**Audience:** Mixed room (TDC executives, HR operational staff, technical/IT counterparts)
**Purpose:** Formal UAT / acceptance sign-off for the HR module
**Scope:** Everything built and stable today, plus a clearly-separated roadmap preview of what's
still in progress
**Format:** Functional/business walkthrough (no dedicated architecture segment)
**Duration:** One day, extending into a second day where the material genuinely needs it

---

## 0. Relationship to the other docs in this folder

| Document | What it's for here |
|---|---|
| [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) | Source for the "what's deferred and why" framing on anything money-related (Payroll GL posting, employee receivables, budgets). |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md) | Source for honest caveats (no bulk-approval UI yet, reminders don't notify yet) so these aren't a surprise if asked about live. |
| [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) | The reports/dashboards segment of the demo draws directly from this. |
| [`HR-BULK-OPERATIONS-CATALOGUE.md`](HR-BULK-OPERATIONS-CATALOGUE.md) | Source for "why does the approver click each item individually" if asked. |
| [`HR-DOCUMENTATION-CATALOGUE.md`](HR-DOCUMENTATION-CATALOGUE.md) | The general documentation strategy; this document is one specific output of it (a demo/UAT artifact). |
| `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` | Direct source for the "questions we need answered" segment. |
| `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` | Direct source for the Finance-integration roadmap segment. |
| `plans/HR-Area-*.md` | Direct source for the requirement-ID evidence in §3.2 below — each FR-HR-### claim in this document is quoted from these. |

---

## 1. Demo objectives and ground rules

**What "acceptance" means for this session.** For each module demoed, the room is being asked one
of three things, and the presenter should say which one out loud before moving on:
- **"Accept as delivered"** — the feature is built, stable, and TDC is signing off that it meets
  the requirement.
- **"Accept with a noted caveat"** — the feature is built and correct, but something outside the
  system (usually TDC's own organisational data — see §6) currently prevents it from being fully
  exercised. TDC is accepting the *build*, not yet the *live behaviour*.
- **"Preview only, not for sign-off"** — a roadmap item shown for visibility. Say this explicitly
  every time, or a roadmap preview risks being remembered as something that was accepted.

**What is explicitly out of scope for sign-off in this session** (state this once, at the start,
and again before the roadmap segment):
- Payroll's migration from its legacy GL-posting path onto the new `IFinancePostingEngine` —
  still in progress, governed by a formal Finance-owner decision (see
  `HR-FINANCE-INTEGRATION-BACKLOG.md`'s 2026-08-31 governance entry).
- Bulk/multi-select actions on approval queues (Leave, Travel, Medical claims, etc.) — not built
  yet; see `HR-BULK-OPERATIONS-CATALOGUE.md`.
- Reminder-driven notifications (probation, separation, asset return, disciplinary deadlines) —
  the sweeps run and log correctly; the emails/alerts themselves are not wired yet.
- Any Tier-B "tail" area still marked in-progress at demo time (confirm current status against
  `plans/HR-Area-19-23-Tier-B-Tail-Build-Plan.md` before finalizing the agenda — do not assume
  the status recorded when this document was written still holds).

---

## 2. Pre-demo preparation checklist

- [ ] **Confirm current build status per area** against the relevant `plans/HR-Area-*.md` before
      finalizing which modules go in "accept" vs "roadmap preview" — build status moves faster
      than any document describing it; treat §5 below as a starting draft, not a final agenda.
- [ ] **Prepare a demo tenant with realistic, synthetic (non-real) data.** No real employee PII —
      salary, medical, bank, disciplinary data must all be fabricated. This matters doubly here
      because Payroll's own live data was measured at points during build (e.g. "202 of 3,883
      employees have a salary on record" is a real finding from the codebase) — do not let a real
      data gap become visible or discussed mid-demo by accident.
- [ ] **Seed at least one live example per approval chain** (a pending leave request, a pending
      separation, a pending requisition at each of its three approval steps) so workflow state
      transitions can be shown rather than described.
- [ ] **Assign at least one user to every role a workflow depends on** — critically, `Internal
      Audit` (needed for the separation final-settlement review, FR-HR-185) and organisational
      unit heads (needed for probation month-5 routing, disciplinary sanction authority, and
      establishment enforcement — see §6, the single biggest caveat in the whole demo). If these
      roles are unassigned in the demo tenant the same way they were found unassigned in
      production data during build, several "accept" moments will silently become "cannot
      demonstrate" moments live.
- [ ] **Rehearse the roadmap segment separately from the accepted-feature segments** so the tone
      shift (from "here's what works" to "here's what's still coming") is deliberate, not
      apologetic or defensive.
- [ ] **Print or PDF the Requirements Traceability & Sign-off Matrix (§3.2)** for every attendee
      who needs to actually sign something — do not rely on a live screen for the artifact that
      needs a signature.
- [ ] **Confirm attendee list against agenda segments** — a mixed audience means not everyone
      needs to sit through every segment; identify who can join late for Day 2's Talent/Exit
      block if only the HR operations team needs full two-day attendance.

---

## 3. Documents to produce

### 3.1 Master agenda (one-pager)
A printable extract of §5 below — segment name, time box, presenter, and the accept/caveat/
preview flag for each — handed out at the start of Day 1.

### 3.2 Requirements Traceability & Sign-off Matrix — the primary UAT artifact

One row per requirement (`FR-HR-###`), with columns: **Requirement** (quoted or paraphrased) |
**Module** | **Demonstrated?** | **TDC decision** (Accept / Accept with caveat / Deferred /
Rejected) | **Caveat, if any** | **Signature/initials**.

The table below is a **real, verified starting draft** built from direct quotes in the build
plans — not a placeholder. Extend it by grepping `FR-HR-\d+` across `plans/**` for any area not
yet covered here before the actual session, since this list is not exhaustive of every
requirement in the FRD, only the ones confirmed during this research pass.

| Requirement | What it requires | Module | Status found in build plans | Suggested TDC decision |
|---|---|---|---|---|
| FR-HR-030 | Oath of secrecy | Probation/Onboarding | ✅ Delivered | Accept |
| FR-HR-031 | Probation length by staff category (6mo senior / 3mo junior) | Probation | ✅ Delivered, validated against live position data | Accept |
| FR-HR-032 | Month-5 probation form routed to "the head"; confirmation letter generated | Probation | ✅ Delivered, **but** routing depends on org units having an assigned head — see §6 | Accept with caveat |
| FR-HR-033 | Appointment letter | Probation | ✅ Delivered (reuses the offer-letter service) | Accept |
| FR-HR-046 | Leave accrues near retirement; encashed only on exit, no other route | Leave / Separation | ✅ Delivered | Accept |
| FR-HR-090 | Manage terminations and designations | Separation | ✅ Delivered | Accept |
| FR-HR-091 | Completed clearance form required before separation | Separation | ✅ Delivered — clearance is a hard gate | Accept |
| FR-HR-092 | MD signs all terminations except procedural ones (HR signs those) | Separation | ✅ Delivered | Accept |
| FR-HR-093 | Retirement at 60, effective on birthday, with advance alerts | Separation | ✅ Delivered | Accept |
| FR-HR-111 | 30-day advance alerts for retirement/contract-expiry | Separation | ✅ Delivered (reminder sweep) | Accept — **note:** the sweep runs and logs; whether it *notifies* anyone is the cross-module sweep's flagged gap |
| FR-HR-113 | Report on long-service-award eligibility | Awards | ✅ Delivered | Accept |
| FR-HR-134 | Approved job descriptions maintained against positions | Job Architecture | ✅ Delivered | Accept |
| FR-HR-135 | Manpower requisitions through an approval chain (Dept Head → HR → MD) | Job Architecture / Manpower Budget | ✅ Delivered | Accept |
| FR-HR-136 | Verify a position against the approved establishment before a vacancy is approved | Job Architecture | ✅ Delivered, enforced as a hard block | Accept |
| FR-HR-140 | Notify employee/supervisor/HR ahead of probation expiry | Probation | ✅ Delivered (reminder sweep) | Accept, same notification-wiring caveat as FR-HR-111 |
| FR-HR-152 | Leave encashment on separation capped at 56 days | Leave / Separation | ✅ Delivered | Accept |
| FR-HR-181 | Grievance escalation ladder (6 rungs), HR interpretation, investigation, resolution, union consultation | Grievance/Employee Relations | ✅ **Fully delivered** (explicitly marked complete in the build plan) | Accept |
| FR-HR-182 | Support every named separation type (resignation through dismissal, plus contract expiry) | Separation | ✅ Delivered | Accept |
| FR-HR-183 | Exit clearance across loans, advances, company property, office equipment, keys, documents, payroll recoveries | Separation + Assets | ✅ Delivered (built jointly across two areas) | Accept |
| FR-HR-184 | Final settlement = unpaid salary + notice pay + leave encashment + benefits − deductions | Separation | ✅ Delivered | Accept |
| FR-HR-185 | Internal Audit reviews the final settlement before payment release | Separation | ✅ Delivered **in code**; **confirmed not reachable** in the data measured during build because nobody held the required Internal Audit role | Accept with caveat — confirm the role is assigned in TDC's real org before go-live |
| FR-HR-084 | Model a responder matrix for who handles what at each escalation rung | Grievance/Employee Relations | ✅ Delivered | Accept |
| FR-HR-004 | Manpower/establishment planning linked to strategic planning cycles | Job Architecture | Marked "Desirable" priority (not Mandatory) — confirm current delivery status before the demo | Accept, or explicitly defer if not yet built |
| FR-HR-080 | Head-of-Department disciplinary sanction authority rule | Discipline | ⚠ **Blocked** — the org-authority data needed to resolve "who is this employee's HOD" was measured as largely absent | Deferred — flag to TDC as needing their organisational data, not a system defect |
| FR-HR-173 | Establishment/headcount advisory rule (Movements area) | Staff Movements / Job Architecture | Promoted from advisory to an enforced block once FR-HR-136's data existed | Accept, confirm same org-data dependency as FR-HR-080/032 |
| FR-SHE-200 | Stop-work authority and clearance | Safety/SHE | ✅ Confirmed built (`SheStopWorkOrder`) | Accept |
| FR-SHE-229 | SHE audit management (planning → closure) | Safety/SHE | ✅ Confirmed built (`SheAudit`, `SheAuditFinding`) | Accept |
| FR-SHE-230/232/248 | KPI computation, incident rate, hazard heat-map | Safety/SHE | ✅ Confirmed built (`ShePerformanceSnapshot`, `SheKpiComputationService`) | Accept |
| FR-SHE-103 | Statutory incident submission trail | Safety/SHE | ✅ Confirmed built (`SheStatutoryIncidentSubmission`) | Accept |
| FR-ENV-001–016 | Environmental compliance review lifecycle | Safety/SHE | ✅ Confirmed built (`SheEnvironmentalReview`) | Accept |
| FR-ENV-033/034 | Monthly environmental report, auto-generated and submitted to management | Safety/SHE | ✅ Confirmed built, and the most complete auto-generated report anywhere in HR (`SheMonthlyEnvironmentalReport`) | Accept |
| FR-CON-001 | Contractor SHE ranking by compliance score | Safety/SHE | ✅ Confirmed built (`SheKpiComputationService.GetContractorRankingAsync`) — **note:** ranking is a compliance dashboard metric only; no monetary penalty/payment consequence is modelled anywhere, so don't imply one exists live | Accept, with that one caveat stated out loud |

**Safety, Health & Environment (SHE) — verified 2026-08-31.** The rows above are real, found by a
direct field-level pass (60+ entities across incident management, hazard/risk assessment,
inspections/audits, permit-to-work, PPE, contractor management, training, waste, environmental
management, occupational health, emergency preparedness, and more) — this module is larger and
more built-out than the placeholder note in this document originally assumed. **Three confirmed
engineering caveats to be ready for if asked, all consistent with gaps already documented
elsewhere in this folder rather than unique to Safety:**
- No entity carries a `WorkflowInstanceId` — approvals (permit-to-work, risk assessment,
  environmental review) are bare `ApprovedById`/`ApprovedDate` pairs, same gap as Medical/Benefits
  (`HR-CROSS-MODULE-PATTERNS-SWEEP.md` §3.3).
- No entity carries a `RowVersion` — same concurrency gap as the rest of HR (§3.2 of that doc).
- The incident/audit/environmental-review registers exist as real, filterable APIs but have no
  CSV/PDF export yet (`HR-REPORTS-CATALOGUE.md` §3.17) — if asked to export the incident register
  live, the honest answer is "the data and filters exist, the export button doesn't yet."

**Still needed from the module owner before the session:** confirmation of current build/test
status per row (this pass confirms the code exists, not that every path has been through the same
UAT rigor as the rest of this matrix), and any additional `FR-SHE-###`/`FR-ENV-###` requirements
not captured above (this list covers the ones found during this pass, not necessarily the FRD's
complete set for this area).

### 3.3 Per-module demo script/runbook
For each agenda segment in §5: the exact click-path to follow, the seeded example record to use,
the requirement ID(s) it satisfies (from §3.2), and the one sentence that states what's being
asked for sign-off. Keep this as the presenter's private crib sheet, not a handout.

### 3.4 Known issues & data-readiness caveats handout
A short, honest one-pager covering **§6's org-unit-head gap** (the single issue that touches the
most requirements: FR-HR-032, FR-HR-080, FR-HR-136, FR-HR-173, and the responder matrix), the
FR-HR-185 Internal-Audit-role gap, and any other data-readiness item confirmed at prep time. This
is what turns "why didn't that work" into "we told you this was the caveat" during the live demo.

### 3.5 Roadmap / deferred items handout
Plain-language versions of: the Payroll→`IFinancePostingEngine` migration status, the bulk-action
gaps, the reminder-notification wiring gap, and current Tier-B/portal completion status. Pull the
substance from `HR-FINANCE-INTEGRATION-BACKLOG.md` and `HR-CROSS-MODULE-PATTERNS-SWEEP.md` but
translate it out of engineering language — this handout is for the same room as §3.4, not for
developers.

### 3.6 Glossary / terminology cheat sheet
A one-pager for terms the room will hit repeatedly and may not share a definition for: staff
level, emolument, benefit-in-kind, encashment, surcharge, establishment, procedural termination,
clearance, notice pay in lieu. Cheap to produce, prevents the demo stalling on vocabulary.

### 3.7 Demo data / seed script checklist
The concrete list of what needs to exist in the demo tenant before Day 1 starts (from §2), kept as
its own short document so whoever preps the environment doesn't have to read this whole plan.

### 3.8 Action-item / follow-up tracker
One row per open item raised during the demo — who owns it, by when, and which document it feeds
back into (usually `HR-OPEN-QUESTIONS-FOR-TDC.md` or the Finance backlog). Start this empty and
fill it live during the session; circulate within 48 hours of the last day.

### 3.9 Sub-module briefing one-pagers (produce for the highest-complexity areas only)
- **Payroll** — one-pager summarizing the Oracle-Forms-to-new-system migration story (the
  crosswalk docs already contain the substance; this is a business-readable summary of it).
- **Separation & Final Settlement** — the settlement calculation basis (currently monthly salary
  × 12 ÷ 365 for daily-rate conversions) and why it's flagged as an open question for TDC.
- **Assets & Surcharge Recovery** — how custody, damage recovery, and Finance's fixed-asset
  register relate (three different things people tend to conflate).
- **Job Architecture / Manpower Budget / Establishment** — how a headcount request becomes an
  approved vacancy, since this segment ties together the most moving parts (JD approval,
  requisition chain, establishment enforcement) into one story.
- **Safety, Health & Environment (SHE)** — permit-to-work, risk assessment, stop-work authority,
  audits and the environmental review cycle, tying into the LTIFR/TRIR-style safety KPIs on its
  dashboard. Owned by the module lead handling this area directly — see §5's Day 2 segment.

---

## 4. What "day 1" and "day 2" are for

**Day 1 covers the core employee lifecycle and the highest-frequency operational areas** — the
modules every employee and manager touches regularly. If only one day is available, Day 1 alone
should still tell a complete, coherent story: hire → onboard → work day-to-day → get paid → use
benefits.

**Day 2 covers talent/performance, risk-and-compliance areas, and exit** — lower-frequency but
often higher-stakes, plus the reports/roadmap/sign-off wrap-up. These are the modules to compress
or defer to a follow-up session if time runs out, not Day 1's.

---

## 5. Order of presentation

### Day 1 — Core employee lifecycle & daily operations

| # | Segment | Time box | Sign-off ask |
|---|---|---|---|
| 1 | **Opening & framing** — scope, what "accept" means today, what's explicitly out of scope (§1) | 15 min | — |
| 2 | **Job Architecture & Manpower Planning** — job descriptions, positions, manpower budget, the Dept-Head→HR→MD requisition chain, establishment enforcement | 45 min | FR-HR-134, 135, 136, 004 |
| 3 | **Recruitment** — vacancy → shortlist → interview → offer → hire | 45 min | (confirm requirement IDs for this area before the session — not fully captured in this research pass) |
| 4 | **Onboarding, Probation & Confirmation** — oath of secrecy, category-based probation length, month-5 routing, confirmation/appointment letters | 40 min | FR-HR-030, 031, 032, 033, 140 |
| — | *Break* | 15 min | |
| 5 | **Core Employee Record & Self-Service portal** — profile, bank details, dependents, emergency contacts, the employee's own view | 30 min | — |
| 6 | **Leave Management** — types, request/approval flow, balances, encashment rules | 40 min | FR-HR-046, 152 (partial — encashment cap) |
| 7 | **Attendance & Time** | 25 min | — |
| — | *Lunch* | 45 min | |
| 8 | **Payroll & Compensation** — emoluments, pay components, running a payroll cycle, the payslip/bank-schedule/statutory report set — **explicitly flag GL-posting migration as roadmap here, not accepted today** | 60 min | (confirm payroll-specific requirement IDs before the session) |
| 9 | **Benefits & Medical** — enrollment, valuation, claims | 40 min | — |
| — | *Break* | 15 min | |
| 10 | **Day 1 wrap-up** — recap what was accepted, preview Day 2 | 15 min | — |

**Day 1 total: ≈6.5 hours of content** (fits a standard working day with breaks/lunch).

### Day 2 — Talent, performance, risk/compliance, exit & roadmap

| # | Segment | Time box | Sign-off ask |
|---|---|---|---|
| 11 | **Performance & Appraisal** — cycle phases, goals, self/manager/peer evaluation, calibration | 45 min | — |
| 12 | **Training & Development** | 30 min | — |
| 13 | **Awards & Recognition** — including the long-service eligibility report | 25 min | FR-HR-113 |
| 14 | **Staff Movements** — promotion, transfer, secondment approval chain | 30 min | — |
| — | *Break* | 15 min | |
| 15 | **Assets & Property** — custody, assignment, surcharge recovery, the exit-clearance link | 35 min | FR-HR-183 (asset side) |
| 16 | **Staff Travel** | 25 min | — |
| 17 | **Succession & Talent** | 25 min | — |
| — | *Lunch* | 45 min | |
| 18 | **Safety, Health & Environment (SHE)** — incident register, permit-to-work, risk assessment, stop-work orders, audits, environmental review, monthly environmental report, contractor ranking. **Owned and presented by the module lead handling this area**; verified requirement rows now in §3.2 — confirm test status and any additional FR-SHE/FR-ENV items with the owner before the session | 35 min | FR-SHE-103, 200, 229, 230/232/248; FR-ENV-001–016, 033/034; FR-CON-001 |
| 19 | **Staff Discipline** — case, investigation, hearing, sanction — **flag FR-HR-080's org-data caveat live, in context** | 30 min | FR-HR-080 (deferred, with explanation) |
| 20 | **Grievance & Employee Relations** — the full 6-rung escalation ladder | 30 min | FR-HR-181, 084 |
| 21 | **Separation & Exit** — every separation type, clearance gate, final settlement, retirement, the Internal-Audit review step | 45 min | FR-HR-090, 091, 092, 093, 111, 152, 182, 183, 184, 185 |
| — | *Break* | 15 min | |
| 22 | **Reports & Analytics overview** — what exists today across payroll/awards/assets/dashboards; point to `HR-REPORTS-CATALOGUE.md`'s gap list as the known-missing set | 30 min | — |
| 23 | **Roadmap & deferred items** — Finance-posting migration, bulk-action gaps, notification-wiring gaps, remaining Tier-B/portal work — **explicitly preview-only** | 30 min | — (no sign-off; preview) |
| 24 | **Open questions for TDC & sign-off walkthrough** — go through §3.2's matrix, capture decisions live | 40 min | — |
| 25 | **Wrap-up, Q&A, next steps** — action items into §3.8's tracker | 20 min | — |

**Day 2 total: ≈7 hours of content** (the Safety/SHE segment adds ~35 min over the original
estimate — trim a lower-priority segment, such as Staff Travel or Succession, if the day needs to
stay closer to 6.5 hours).

If genuinely only one day is available, compress Day 2 into a 90-minute "highlights and roadmap"
add-on to Day 1 rather than dropping segments silently — the sign-off matrix should still get its
own dedicated time even in a compressed format.

---

## 6. The single biggest cross-cutting caveat to prepare for

**Several requirements route decisions to "the head of the unit," and TDC's organisational data,
as measured during build, mostly does not have unit heads assigned** (one build plan states this
plainly: unit heads assigned in roughly 2 of 48 units at the time it was written). This is not a
defect in the HR module — the routing logic is built and correct — but it means **FR-HR-032
(probation routing), FR-HR-080 (disciplinary sanction authority), and the establishment/movement
rules tied to FR-HR-136/173** may not be demonstrable exactly as designed unless the demo tenant's
organisational structure has heads assigned, or unless this is explained as a data-readiness item
TDC needs to complete before go-live rather than a gap in the system. **Decide before the demo
whether to (a) seed the demo tenant with heads assigned so the flow can be shown working, or
(b) show it honestly as blocked and use it as the concrete example of what "accept with caveat"
means.** Either is defensible; walking into it without a plan is not.

---

## 7. Logistics and contingency

- **If the demo environment has an issue mid-segment:** move to the next segment and return, don't
  let one technical hiccup consume the room's patience for the rest of the day — this is exactly
  why §3.3's runbook should name a backup path (a screenshot walkthrough) per segment.
- **If asked to bulk-approve something live:** this is the expected moment `HR-BULK-OPERATIONS-CATALOGUE.md`'s
  gap analysis becomes relevant — have the answer ready ("not built yet, here's the plan") rather
  than looking surprised.
- **If asked why a reminder wasn't actually emailed:** same posture — the sweep and the dispatch
  log are real and can be shown; the notification itself is the known, already-documented gap.
- **Keep the roadmap segment (#22) visually distinct** (a different slide background, a clear
  "ROADMAP — NOT FOR SIGN-OFF" header) so it cannot be mistaken for an acceptance segment in
  meeting notes taken by someone who steps out and back in.

---

## 8. Post-demo follow-through

1. Circulate the filled-in §3.2 matrix and §3.8 action-item tracker within 48 hours.
2. File every new question that came up under `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` rather than
   letting it live only in meeting notes.
3. File every new deferred-item commitment under `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` (if
   money-related) or the relevant build plan (if not), following those documents' own existing
   rules for how new items get registered.
4. Schedule the org-data readiness work (§6) as its own tracked item if it wasn't resolved before
   the demo — it blocks more than one requirement's live behaviour and is TDC's data to complete,
   not a build task.
