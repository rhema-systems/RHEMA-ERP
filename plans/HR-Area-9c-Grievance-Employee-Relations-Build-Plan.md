# HR Area 9c — Grievance & Employee Relations: Build Plan

**Opened 2026-08-27.** The last functional gap in the internal HR module, and the second of the two
modules [[hr-deferred-modules]] recorded as consciously deferred during area 9 planning
(2026-08-16). Area 9 shipped a **first cut** — the FR-HR-181 ladder and the grievance record — and
said so in its own doc comments. This area builds the module that first cut was a down-payment on.

Third cut off area 9, after 9b (separation & exit). Numbered **9c** for the same reason 9b was:
it inherits area 9's data and extends its store rather than starting empty.

---

## 1. How to use this document

Section **3 is measured, not remembered** — every count came from a live query against
`ErpSystemDB` on 2026-08-27, and every endpoint list came from reading the controller. Read it
before writing a line of code. Section 4 is the requirement gap, quoted from the spec PDF rather
than paraphrased. Section 5 holds the decisions (four are already taken by the user, 2026-08-27).
Section 7 is the slice plan. Section 8 is the running log — one entry per slice as it closes, in
the area-16 / area-25 style.

**Standing conventions, not restated per slice:** the user runs all builds
(`user-runs-builds`); I stage and hand over the commit message (`stage-user-commits`); migrations
are scaffolded by the user, listed by me in `FastBuildMigrationMetadata`, and updated by the user
(`migration-ownership-and-chain`); harnesses run against **Staging** with the JWT key passed
(`hr-harness-run-environment`) and **run twice**; the API is killed before asking for a rebuild
(`stop-backend-before-user-builds`); uploads need `clamd-stub.mjs`.

**The two-actor rule applies throughout.** HR bypasses its own guards, so every ownership and
refusal assertion must be probed as a *linked non-HR employee fixture*, never as HR.

---

## 2. Status at a glance

| Slice | Title | Status |
|---|---|---|
| 0 | Survey, fixtures, endpoint + payload census | **COMPLETE 2026-08-27** — 147 assertions ×2, zero failures; the ladder proven end to end, and `GrievanceStatus.Closed` found to have no writer |
| 1 | The ER case register: case types, parties, representation | **COMPLETE 2026-08-27** — 119 assertions ×2 + the 147 slice-0 ladder = **266**; migration `AddEmployeeRelationsCaseTypeAndParties`; all 87 pre-existing rows correct with no back-fill |
| 2 | FR-HR-181's missing artefacts: HR interpretation, investigation, resolution decision | **COMPLETE 2026-08-27** — 111 ×2 + the 151/119 ladder = **381**; obligations 5, 7, 8 closed; `GrievanceStatus.Closed` given its first writer |
| 3 | Documents on the controlled upload gate + the final signed agreement | **COMPLETE 2026-08-27** — 70 ×2 + the 151/119/111 ladder = **451**; obligation 9 closed; D-10 judged — the agreement stays off the workflow engine |
| 4 | Case conferencing & mediation; union consultation | **COMPLETE 2026-08-27** — 86 ×2 + the 151/119/111/70 ladder = **537**; obligation 6 closed. **FR-HR-181 IS NOW FULLY DELIVERED** |
| 5 | The responder matrix (FR-HR-084) + rung resolution | **COMPLETE 2026-08-27** — 66 ×2 + the 151/119/111/70/86 ladder = **603**; migration `AddEmployeeRelationsResponderMatrix`; the org-authority gap stops being worked around |
| 6 | Anonymous / whistleblower concern intake | **COMPLETE 2026-08-27** — 56 ×2 + the ladder = **659**, plus **6 row-level SQL checks**; migration `AddEmployeeRelationsConcerns` |
| 7 | Reminder-sweep extension + notifications | **COMPLETE 2026-08-27** — 46 ×2 + the ladder = **705**; migration `AddEmployeeRelationsReminderSettings`; the rung clock is a setting at last, and 3 defects were caught by the harness |
| 8 | ER analytics & reporting | **COMPLETE 2026-08-28** — 104 ×2 + the ladder = **811**; no migration. Found a live defect: four readers of "an answer is owed" had drifted, 227 vs 174 |
| 9 | Cross-links: SHE incidents, PIPs, disciplinary cases | ⏳ |
| 10 | Desk screens: the ER case register and case file | ⏳ |
| 11 | Portal screens: the employee's side | ⏳ |
| 12 | Closing audit: content audit ×2, the two greps, route resolution, polish | ⏳ |

---

## 3. Measured ground truth (2026-08-27, DEFAULT tenant unless stated)

### 3.1 What area 9 slice 7 actually built, and it works

| Artefact | Where | Size |
|---|---|---|
| Entities | `Core/Entities/HR/StaffGrievanceEntities.cs` | 209 lines — `StaffGrievance`, `StaffGrievanceStep` (+ the shared discipline reminder engine) |
| Enums | `Core/Enums/HREnums.cs:2781-2835` | `GrievanceEscalationLevel` (6 rungs), `GrievanceStatus` (6), `GrievanceStepOutcome` (3) |
| DTOs | `Core/DTOs/HR/StaffGrievanceDTOs.cs` | 181 lines |
| Interface | `Core/Interfaces/HR/IStaffGrievanceService.cs` | 84 lines |
| Service | `Core/Services/HR/StaffGrievanceService.cs` | 420 lines |
| Controller | `Api/Controllers/HR/StaffGrievancesController.cs` | 149 lines, **15 endpoints** on `api/grievances` |
| Migration | `20260816225939_AddStaffGrievances` | applied |
| Frontend | `services/hr/grievance.service.ts` (89), `types/hr/grievance.ts` (119) | 11 client methods |
| Screens | `/hr/grievances`, `/me/grievances`, `/me/grievances/new`, `/me/grievances/[id]` | 4 |

**This is not a dead path** — unlike several features earlier areas inherited. Live data:

| | |
|---|---|
| `StaffGrievances` | **69** |
| `StaffGrievanceSteps` | **112** |
| distinct grievers | **41** |
| date range | 2026-08-16 → 2026-08-27 |
| statuses present | Filed 9, UnderReview 19, Escalated 18, Resolved 5, Withdrawn 18 |
| rungs reached | all six — Supervisor 69, HOD 23, HR 5, GM F&A 5, MD 5, Board 5 |
| steps assigned a responder | 62 of 112 |

The ladder has been walked end to end. **Treat this area as harden-and-extend, not resurrect**
— the opposite of area 11's starting position.

### 3.2 The org-authority data, re-measured — it got worse

The `ExpectedHeadcount` lesson says measure before designing on it. Measured today against the
figures [[hr-deferred-modules]] recorded on 2026-08-16:

| | 2026-08-16 | 2026-08-27 | |
|---|---|---|---|
| Active employees | 1,210 | **8,353** | the tenant was loaded in the interim |
| `Employees.ManagerId` populated | 175 (14%) | **486 (5.8%)** | ⚠ **worse**, not better |
| `Employees.OrganizationUnitId` populated | 1,186 (98%) | **8,329 (99.7%)** | the only usable one |
| `OrganizationUnits.HeadEmployeeId` populated | 0 of 41 | **2 of 48** | |
| `JobReportingRelationships` | — | **87** | job-level, not person-level |
| `Departments` | — | **7** | too coarse to route on |

**Conclusion, and it is now a measurement rather than a prediction:** deriving the Supervisor and
HOD rungs from org data would resolve to nobody for **94% of staff**. This is the fourth
requirement to hit this wall (FR-HR-080, FR-HR-173, FR-HR-181, now FR-HR-084). Decision D-3 below
stops working around it and builds the explicit alternative we recommended to TDC in
`docs/HR-OPEN-QUESTIONS-FOR-TDC.md` §4.

### 3.3 Adjacent surfaces this area will read or extend

| | Measured | Note |
|---|---|---|
| `Unions` | **0 rows** | area 21 built the register; nothing has been entered. There is **no employee↔union membership entity at all** — the only `UnionId` FKs are on `CollectiveBargainingAgreement` and `JobAnalysisEntities.cs:125`. |
| `SafetyIncidents` | 76 | the SHE link target |
| `PerformanceImprovementPlans` | — | the performance link target |
| `StaffDisciplinaryActions` | 690 | the discipline link target; the grievance store already shares its permission family |
| `StaffDisciplineInvestigations` | 36 | **the template** for the grievance investigation entity |
| `StaffDisciplineDocuments` | — | **the template** for grievance documents: a `Scope` enum discriminating Case / ActionStep / Appeal |
| `StaffDisciplineNotifications` | — | the template for ER notifications |
| `DisciplineReminderService` | — | already sweeps `GrievanceUnanswered` at `GrievanceRungChaseDays = 5` (`:81`) |
| `HrPermissions.CategoryDiscipline` | — | **already named "HR - Discipline & Grievance"**, and all three permission descriptions already mention grievance. No new permission family is needed. |
| `ControlledFileUploadCategories` | 15 HR families | needs one new constant; every HR family is private + scan-mandatory |

### 3.4 Three defects found during the survey, before any code was written

1. **`frontend/src/app/hr/grievances/[id]/` is an empty, untracked directory.** `git ls-files`
   returns only `page.tsx` for that folder. Somebody started HR's detail screen and it was never
   written. Harmless today (Next.js ignores a folder with no `page.tsx`) but it must be either
   filled or removed — slice 10 fills it.
2. **HR's register links every row to the employee's portal screen.**
   `frontend/src/app/hr/grievances/page.tsx:129` renders
   `<Link href={`/me/grievances/${g.id}`}>`. So an HR officer browsing the register is sent into
   `/me/...`. It happens to work — `/me/grievances/[id]` carries the assign and respond mutations
   as well as escalate and withdraw — but the desk and the portal are sharing one screen by
   accident, not design, and the portal shell is the wrong frame for HR's own work.
3. **`getByStatus` has no caller.** One of eleven client methods and one of fifteen endpoints is
   dead. Minor, but it is exactly the shape the area-16 two-greps check exists to surface, so it
   is recorded here rather than discovered again in slice 12.

---

## 4. What FR-HR-181 actually requires — quoted, and scored

The requirement is **Mandatory** (`Pri. M`, source `SRS1 3.15 / HR-REQ-093, 094`). Its full text,
from `TDC_ERPS_HR_Payroll_SHE_Environment_Requirements_Specification_v0.3 - EDITED.pdf` p.25:

> The system shall escalate grievances through Employee → Supervisor → HOD → HR → GM Finance &
> Administration → Managing Director → Board, **retaining the grievance statement, supervisor
> response, HOD comments, HR interpretation, union consultation notes, investigation report,
> resolution decision and final signed agreement.**

Nine obligations. Scored against what exists:

| # | Obligation | Today | Slice |
|---|---|---|---|
| 1 | Escalate through the six rungs | ✅ `GrievanceEscalationLevel` + `EscalateAsync` | — |
| 2 | Retain the grievance statement | ✅ `StaffGrievance.Statement`, never overwritten | — |
| 3 | Supervisor response | ✅ step response at `Level = Supervisor` | — |
| 4 | HOD comments | ✅ step response at `Level = HeadOfDepartment` | — |
| 5 | **HR interpretation** | ✅ **slice 2** — `HrInterpretation` + author + date, amendable while open, frozen once closed | 2 |
| 6 | **Union consultation notes** | ✅ **slice 4** — a `UnionConsultation` conference naming the union it consulted, with its notes redacted to HR and the chair | 4 |
| 7 | **Investigation report** | ✅ **slice 2** — `StaffGrievanceInvestigation`, internal or external investigator, natural-justice gate, completion refused without findings | 2 |
| 8 | **Resolution decision** | ✅ **slice 2** — `StaffGrievanceResolution` with outcome, decision, remedy, decider, date and the rung it was decided at; frozen on write | 2 |
| 9 | **Final signed agreement** | ✅ **slice 3** — a scoped document on the controlled upload gate, a supplied signing date, and the employee's own acceptance | 3 |

**As surveyed: four of nine absent, one partial** — a Mandatory requirement roughly half
delivered, which was the strongest evidence for building this area, stronger than the deferred
wish-list.

✅ **AFTER SLICE 4: ALL NINE OBLIGATIONS DELIVERED.** `run-slice0.mjs`'s census reads
*0 of 8 artefact groups absent*. What remains in this area is the rest of the employee-relations
module (D-1) — the responder matrix, anonymous intake, reminders, analytics, cross-links and the
screens — not FR-HR-181 itself.

Also in scope from the spec: **FR-HR-084** (`Pri. D`, source WN) — *"The system shall model a
grievance hierarchy defining reporting lines."* Decision D-3 delivers this as an explicitly
maintained matrix rather than a derived hierarchy, for the reason measured in §3.2.

---

## 5. Decisions

### Taken by the user, 2026-08-27

- **D-1 — Full ER module.** Complete FR-HR-181's five missing/partial artefacts **and** the
  deferred list: case conferencing and mediation, representation and union handling, anonymous
  intake, analytics, and the SHE/performance links.
- **D-2 — Anonymous intake is a separate `EmployeeRelationsConcern` entity.** No employee FK; a
  retrieval code lets the reporter follow up; HR triage may convert it into a named case if the
  reporter identifies themselves. Chosen over a nullable griever on `StaffGrievance` so that the
  read gate and the escalate/withdraw ownership rules stay intact for the 69 existing rows.
- **D-3 — Rungs resolve from an explicit responder matrix**, per organisation unit with a
  tenant-wide default, maintained by HR on an admin screen. HR's per-grievance `assign` stays as
  the override. This is FR-HR-084, and it is the alternative already put to TDC in open question
  §4 rather than a fifth work-around.
- **D-4 — A general ER case register.** Grievance becomes one case type among several
  (conflict/mediation, welfare/counselling, union consultation, other). Existing grievances
  migrate in as `CaseType = Grievance`.

### Proposed here — flag now if any of these is wrong

- **D-5 — The store is extended in place; no table is renamed.** `StaffGrievances` /
  `StaffGrievanceSteps` keep their names and their 69 + 112 rows, and gain `CaseType` defaulting
  to `Grievance` so every existing row is correct without a data fix. Renaming to
  `EmployeeRelationsCase` would mean a table rename, an EF-config rewrite, a snapshot
  regeneration and a rename across entity/DTO/service/controller/types/service-client/4 screens,
  for no functional gain and real regression risk on a working portal.
- **D-6 — The route is renamed, with the old one kept as an alias.** The controller carries
  **both** `[Route("api/hr/employee-relations")]` (the correct name for a register that holds
  union consultations) and `[Route("api/grievances")]`, so nothing the portal calls breaks. The
  frontend client migrates to the new route in slice 10/11 and the alias is dropped in slice 12
  — *after* the two greps prove nothing still calls it.
- **D-7 — `StaffGrievance.EmployeeId` stays required, as the case's primary party.** Multi-party
  cases (respondent, representative, union rep, witness, mediator) are modelled by a new
  `StaffGrievanceParty` child. Assumption worth stating: **every ER case at TDC concerns at least
  one identifiable employee.** A union consultation that concerns a class of staff rather than a
  person names the affected employee or the union representative as primary. If TDC has genuinely
  employee-less ER cases, D-7 is wrong and `EmployeeId` must go nullable — say so now, because it
  is cheap before slice 1 and expensive after.
- **D-8 — One conference entity, typed.** Case conferencing, mediation and union consultation are
  three uses of the same shape (a convened meeting: date, venue, chair, attendees, notes,
  outcome), so they are one `StaffGrievanceConference` with a `ConferenceType` and a nullable
  `UnionId`, not three tables. FR-HR-181's "union consultation notes" is a conference of that
  type.
- **D-9 — Anonymous means unattributed, not unauthenticated.** The concern endpoint requires a
  valid internal token and simply never records who called it — no employee id, no user id, no
  `CreatedBy`. A truly public unauthenticated intake would be only the second anonymous surface in
  the whole application (the first, training certificate verification, is still awaiting TDC's
  answer) and needs its own security review. ⚠ Note the honest limit: request logs and the
  reverse proxy still see the caller. If TDC needs untraceable reporting, that is a different
  build and should be raised as such.
- **D-10 — This area stays OFF the workflow engine**, extending area 9 slice 7's recorded call
  ([[goal-approval-stays-bespoke]] is the same class). The engine models approval; an ER case is
  *answered*, and the decision to escalate belongs to the griever. The one place to re-examine is
  the **final signed agreement** — a signature is closer to an approval — and slice 3 will judge
  it on the evidence rather than assume.
- **D-11 — No new permission family.** `HrPermissions.CategoryDiscipline` is already
  *"HR - Discipline & Grievance"* and all three of its permission descriptions already name
  grievance. New endpoints join `DisciplineRead/Write/Admin`. The employee's own acts (file,
  escalate, withdraw, report a concern) stay ungated with the ownership check on the service, per
  [[hr-area-authz-pattern]].

### Open — for TDC, not for us to decide

- **How long may a case sit at one rung?** Still `GrievanceRungChaseDays = 5`, still our number,
  still unanswered (open question §2). Slice 7 makes it configurable rather than a `const` so the
  answer costs a settings change, not a deploy.
- **Union membership.** `Unions` has 0 rows and no employee link exists. Slice 4 builds the
  consultation record against the union register as it stands; **who represents whom cannot be
  derived** and the union rep is named per case. Raise with TDC whether union membership should
  be maintained as employee data.
- **Whether reporting lines will ever be maintained** — open question §4, now with a worse
  number to quote. D-3 makes this area not care, but FR-HR-080 in discipline still does.

---

## 6. Scope

**In:** the ER case register and its case types; parties and representation; FR-HR-181's five
missing artefacts; the document surface on the controlled upload gate; conferencing, mediation
and union consultation; the responder matrix; anonymous concern intake and its two-way thread;
the reminder-sweep extension and notifications; ER analytics; cross-links to SHE incidents, PIPs
and disciplinary cases; the desk screens and the portal screens; the closing audit.

**Out:** anything that changes the disciplinary case itself (area 9 is closed — a defect found
here is recorded, not fixed, unless it is in grievance code); payroll writes; GL/money events
(none arise here — an ER settlement with money in it is a separation, which is 9b's); the
**public** unauthenticated intake surface (D-9); union membership as employee master data;
FR-HR-080's HOD sanction rule (discipline's, and still blocked on the gate not the matrix —
though D-3's matrix is the thing that would unblock it, which slice 5 should note for whoever
reopens area 9).

---

## 7. Slice plan

Every slice: a harness at `dev-harness/hr-employee-relations/run-sliceN-*.mjs`, Staging + JWT key,
run **twice from different DB states**, plus the cumulative no-regression ladder of this area's
earlier runners. `npx tsc --noEmit` + lint after frontend work. A **UI-payload probe before any
TypeScript** for every form (enums as strings, `datetime-local` wall-clock, `null` not `""` for an
untouched optional date — the area-12 lessons), and the probe **covers writes, not only reads**
(the area-16 lesson).

- **Slice 0 — survey, fixtures, census.** Diagnostic only, no schema change. (a) Probe all 15
  existing endpoints as each actor — HR, the griever, an assigned responder, an unrelated
  employee — recording status **and body shape**; an empty read gives no shape, so seed a fixture
  first. (b) Census every field of `StaffGrievanceDto` against what the four screens render.
  (c) Fixtures: `er.griever` (linked, no HR role), `er.responder`, `er.unrelated`, `er.hruser`,
  a unit with a head and one without. (d) Confirm the §3.4 template entities compile-read as
  expected. (e) FRD grep for every ER-adjacent requirement so slice 12 has a checklist.
- **Slice 1 — the register.** `CaseType` enum + column (default `Grievance`);
  `StaffGrievanceParty` (employee, party role, is-primary, representation flag); the paged ER
  register read with case-type/status/rung/unit filters; party CRUD with the ownership rules.
  Migration. **Assert all 69 existing rows read back as `Grievance` and every existing endpoint
  still answers identically** — the no-regression floor for the whole area.
- **Slice 2 — FR-HR-181's missing artefacts.** `HrInterpretation` (+ author, date) on the case;
  `StaffGrievanceInvestigation` modelled on `StaffDisciplineInvestigation`;
  `StaffGrievanceResolution` one-to-one (decision, remedy/undertakings, decided-by, decided-date)
  — and **fix the partial**: `RespondAsync` currently copies the last response into
  `ResolutionSummary`, which conflates *an answer* with *the decision*. Migration.
- **Slice 3 — documents + the signed agreement.** `StaffGrievanceDocument` with a `Scope`
  discriminator (Case / Step / Investigation / Conference / Agreement) on the
  `HrAttachmentUpload` → `HrDocumentDownload` gate, a new
  `ControlledFileUploadCategories.HrGrievanceDocuments` constant; the signed agreement as a
  scoped document plus its signature/acceptance stamps. ⚠ needs `clamd-stub.mjs`. Judge D-10's
  open question here.
- **Slice 4 — conferencing, mediation, union consultation.** `StaffGrievanceConference` +
  `StaffGrievanceConferenceAttendee`, typed per D-8, nullable `UnionId`. Attendance and notes are
  author-or-HR readable — the area-7 field-level rule, since conference notes name people who are
  not the griever.
- **Slice 5 — the responder matrix (FR-HR-084).** `EmployeeRelationsResponder`
  (organisation unit nullable = tenant default, rung, responder, active window); resolution
  order **matrix-for-unit → matrix-default → HR assigns by hand**; auto-assign on file and on
  escalate, with HR's `assign` still overriding. Admin screen under
  `/administration/hr/employee-relations/responders`. **Assert the fallback explicitly** — a
  grievance in a unit with no matrix row must still be fileable and land unassigned, not 500.
- **Slice 6 — anonymous concerns.** `EmployeeRelationsConcern` +
  `EmployeeRelationsConcernUpdate` per D-2/D-9; retrieval code (hashed, shown once); HR triage
  queue; convert-to-case. **The assertion that matters: nothing anywhere records the reporter** —
  probe the row, `CreatedBy`, and the audit trail, not just the response body.
- **Slice 7 — reminders + notifications.** Extend `DisciplineReminderService` with the new clocks
  (investigation overdue, conference upcoming, concern untriaged, agreement unsigned);
  `GrievanceRungChaseDays` moves from a `const` to policy settings. ⚠ Keep the existing rule that
  **a reminder carries a case number and a date and nothing else** — an ER reminder travels
  further than the record it is about.
- **Slice 8 — analytics.** By type, rung, unit, outcome; time-to-resolution; escalation rate;
  the where-is-it-stuck view. ⚠ The area-7 lesson is mandatory here: assert every number against
  an independently-queried ground truth, and **never ship a rate without its denominator**.
- **Slice 9 — cross-links.** Nullable `SafetyIncidentId`, `PerformanceImprovementPlanId`,
  `StaffDisciplinaryActionId` on the case, with a raise-an-ER-case-from-here affordance on each
  source. Read-only in both directions; no cascade.
- **Slice 10 — desk screens.** `/hr/employee-relations` register + `[id]` case file (the tabs:
  ladder, parties, investigation, conferences, documents, resolution, links). **Fills the empty
  `[id]` folder** and **fixes §3.4 defect 2** — HR's rows link to the desk screen, not `/me/`.
  Client migrates to the new route.
- **Slice 11 — portal screens.** `/me/grievances` extended: report a concern, see conferences
  they attend, their representative, the signed agreement to accept. The griever's rules are
  unchanged — HR still cannot file, escalate or withdraw for them.
- **Slice 12 — closing audit.** Content audit run **twice**; **the two greps** (every service
  method ↔ the screens that call it; every non-GET route ↔ a service method) — this is the check
  that catches a capability with no UI, which an endpoint audit cannot; route resolution over
  every new screen; drop the `api/grievances` alias; the FR checklist from slice 0; kill
  `getByStatus` or give it a caller.

---

## 8. Running log

### Slice 0 — the survey. CLOSED 2026-08-27.

`dev-harness/hr-employee-relations/run-slice0.mjs`, **147 assertions, run twice from different
DB states, zero failures.** No schema change, no code change — diagnostic only.

**The headline is that it all passed.** Area 9 slice 7's first cut is not a dead path and not a
ported shell: all six FR-HR-181 rungs walk end to end, every ownership rule holds two-sided, and
the numbering, validation and terminal-state guards are correct. **This area is harden-and-extend**
— the opposite of area 11's starting position, and unlike area 8's movements or area 25's job
board, nothing here has to be resurrected before it can be built on.

Specifically proven, and now the no-regression floor for slices 1–12:

- **The ladder.** Supervisor → HOD → HR → GM F&A → MD → Board, six rungs, each appended not
  amended. The supervisor's original answer survives five escalations **verbatim and still
  attributed to the original responder** — which is FR-HR-181's central obligation and the thing
  every later slice writes near.
- **Escalation belongs to the griever.** HR escalating → 403 *"Only the employee who raised…"*;
  an unrelated employee → 403; skipping a rung that has not answered → 422; past the Board → 422
  *"final level"*. Withdrawal likewise: HR → 403.
- **HR genuinely cannot file on anyone's behalf.** Asserted by consequence rather than by
  restating the doc comment: HR posting a grievance creates **HR's own**, because the caller's
  token is the only source of the griever. The UI must never offer "raise on behalf of".
- **The read gate is four-sided.** Griever ✅, HR ✅, an employee named on a step ✅ — *and only
  after being named* (the same actor is refused beforehand, which is what makes `assign` the
  admission mechanism rather than a label). Unrelated employee → 403 both on the record and by
  absence from `mine`. The HR register itself → 403 to an ordinary employee.
- **`status/{status}` binds both the enum name and the int** — worth knowing because it has no
  UI caller (§3.4 defect 3) and its binding had therefore never been exercised.

**Two defects found, both recorded as `CURRENT POSITION` assertions so the fix is visible when it
lands** (the area-9 slice-2 convention):

1. **`GrievanceStatus.Closed` has no writer anywhere.** `grep -rn GrievanceStatus.Closed src/`
   returns three hits and **all three are read filters**; 0 of 69 rows carry it. Its own doc
   comment says it means *"Closed without resolution — the ladder was exhausted at Board level"*.
   So a grievance the Board has answered without resolving has **no terminal state** — it sits
   `UnderReview` for ever, is counted as open by the reminder sweep for ever, and the one status
   the enum defines for the end of the road is unreachable. The [[hr-dead-path-defects]] shape.
   **Slice 2 gives it its first writer.**
2. **`ResolutionSummary` is a verbatim copy of the last response.** `RespondAsync` assigns
   `dto.Response` straight into it, so the system cannot distinguish *an answer* from *the
   decision* — no decider, no decision date of its own, no remedy or undertaking. This is
   FR-HR-181 obligation 8 scored as partial in §4, now measured rather than read.

**The artefact census** (printed, not asserted — these keys arrive during slices 1–4 and an
assertion on their absence would have to be deleted rather than updated): `StaffGrievanceDto`
carries **19 keys**, `StaffGrievanceStepDto` **14**, and **8 of 8** owed artefact groups are
absent — HR interpretation, union consultation notes, investigation report, resolution decision,
signed agreement, parties, conferences, case type.

⚠ **A harness bug worth remembering, because it will recur in every slice that walks the ladder:**
the walk loop answers at the Board rung (it must, or the past-the-Board refusal returns *"has not
answered yet"* instead of *"final level"* — `EscalateAsync` checks the unanswered-rung rule
first). So the ladder grievance is **spent** by the end of §5 and any terminal-state test needs
its own fresh grievance. Three of the four grievances this run files exist for that reason.

**Owed to later slices from here:** fill or delete the empty untracked
`frontend/src/app/hr/grievances/[id]/` folder and stop HR's register linking into `/me/`
(both slice 10); give `getByStatus` a caller or remove it (slice 12).

---

### Slice 1 — the employee-relations register. CLOSED 2026-08-27.

`run-slice1.mjs` **119 assertions ×2** green, plus the slice-0 ladder re-run at 147 =
**266 for the area**. Migration `20260827164241_AddEmployeeRelationsCaseTypeAndParties`
(guarded SQL, registered in `FastBuildMigrationMetadata`).

**Delivered:** `EmployeeRelationsCaseType` + `CaseType` on the case (D-4); `StaffGrievanceParty`
with representation, union and external parties (D-7); the DB-paged register with case-type /
status / rung / unit / stuck / search filters; `POST cases` for the non-grievance types;
`POST {id}/parties` and `{id}/parties/{partyId}/remove`; and the `api/hr/employee-relations`
route with `api/grievances` kept as the alias (D-6).

**The migration's default is the whole no-back-fill story, and it was verified in the database
rather than inferred.** `EmployeeRelationsCaseType.Grievance` is enum member 1 and the column is
added `NOT NULL CONSTRAINT DF_StaffGrievances_CaseType DEFAULT (1)`, so all **87** existing rows
became correct as the column landed — confirmed by direct query (`CaseType 1 → 87`, nothing
else). There is no back-fill statement in that migration because none is needed, and none may be
added later on the assumption that there is one. ⚠ **Nothing in that enum may be renumbered.**

**Three refusals that are the point of the slice, all asserted two-sided:**

1. **`POST cases` refuses `CaseType.Grievance` (422).** That route takes an explicit employee id
   because the desk opens a mediation ABOUT somebody; permitting Grievance would be the
   raise-on-behalf-of that `FileAsync`'s whole shape prevents, reachable by exactly the actor who
   must not have it.
2. **Being a party does not confer a right to read.** The respondent on a mediation is refused
   `GET {id}` (403 *"not yours"*) and it does not appear in what they owe. A respondent must not
   receive the complainant's statement by being recorded as a respondent; what they are owed is
   disclosure the process makes, not a row in a table.
3. **A closed case takes no more parties** (422), and standing a party down is not a delete —
   `RemovedDate` + a reason, with the row still in the file.

**A bug found in my own code before the build, and now asserted so it cannot return.** Standing a
party down also stands down anyone acting FOR them. The comparison
`p.RepresentsEmployeeId == party.EmployeeId` is `null == null` for an **external** party — a union
official or a lawyer has no employee id — so removing one would have stood down every party on
the case who represents nobody. Guarded on `party.EmployeeId is Guid principalId`; the harness
adds an unrelated witness, removes the external union rep, and asserts the witness is untouched.

⚠ **A harness defect the twice-from-different-states rule caught, and worth generalising.** §1
asserted *"every pre-existing case reads as a Grievance"* — which passed on the first run and
**failed on the second**, because §4 of the same file opens a mediation and a welfare case. The
assertion was true only against a virgin database. Replaced with two that survive: every case
carries a valid type name (a failed DEFAULT would surface as 0 or unknown), and the
Grievance-typed count is **≥ 87**, the measured pre-migration baseline. **A no-regression
assertion written against "everything currently in the table" is not a no-regression assertion —
it is a snapshot, and the run that proves it is the second one.**

**Two decisions taken in code and worth knowing before slice 2:**

- **Non-grievance cases open at the HR rung**, not at Supervisor. Nobody escalated anything to
  reach them — the desk opened them — and starting at Supervisor would have invented a rung that
  never happened.
- **The register pages in the DATABASE and projects lean.** It uses its own query with no
  includes, not the `Scoped()` graph the case file uses, and the harness asserts a register row
  carries no `steps`, no `parties` and no `statement`. `GetAllAsync` is left alone for its
  existing callers. ⚠ `Employee.FullName` is `[NotMapped]`, so the name parts are projected and
  composed after materialisation — a `Select` that touches `FullName` will not translate.
- The paged "stuck" predicate is SQL over the highest-sequence step; the unpaged one is
  `CurrentStep()` in memory. **They must agree**, and the harness asserts they do rather than
  trusting that they will.

**Not a problem after all:** the D-6 route alias gives `GetById` two endpoints, and
`CreatedAtAction` is used by both `File` and `OpenCase`. Link generation resolved it; both create
paths return 201. No named route needed.

---

### Slice 2 — FR-HR-181's missing artefacts. CLOSED 2026-08-27.

`run-slice2.mjs` **111 assertions ×2**, ladder re-run at 151 / 119 = **381 for the area**.
Migration `20260827171146_AddGrievanceInterpretationInvestigationAndResolution` (guarded SQL,
registered). **FR-HR-181 goes from four absent obligations to two** — 6 (union consultation) and
9 (signed agreement), owed to slices 4 and 3.

**Obligation 5 — HR interpretation.** A field on the case with author and date, and the harness
asserts what makes it a *distinct* artefact rather than a duplicate: recording it does not touch
the ladder and does not answer the rung. The step response is HR answering the employee; this is
HR's position on the merits, which the GM, MD and Board read on the way up. Amendable while the
case is open and re-stamped each time; refused once the case is terminal, because an
interpretation edited after the outcome is what makes the outcome indefensible.

**Obligation 7 — the investigation.** `StaffGrievanceInvestigation`, one per case. Three rules
that are the substance rather than the shape:

- **The investigator may be external.** A grievance about senior management is exactly the one
  that gets an outside investigator; an `Employee` FK alone would have made the commonest serious
  case unrecordable. Same reasoning as slice 1's external parties.
- **Natural justice is enforced, and it was not before.** Neither the complainant nor a respondent
  may investigate (422). Area 9 built this rule for the disciplinary case; the grievance half
  never had it.
- **Completion is a gate, not a flag.** Concluding without findings is refused, and a concluded
  report can no longer be edited. An investigation reported complete with nothing in it is worse
  than one still open, because every rung above it will rely on it.

⚠ The update path is a **field-level patch**: a null field means "leave alone", not "clear", so a
form that edited only the recommendation cannot wipe the findings. That is the *opposite* of this
repo's `replace-set-payload-convention`, and it is commented at the call site for exactly that
reason.

**Obligation 8 — the resolution decision, and the judgment call in this slice.** The clean fix
would have broken `/me/grievances/[id]`, which calls `respond(resolvesGrievance: true)`. So that
path **still works and still resolves**, but now leaves a real `StaffGrievanceResolution` behind,
marked `Outcome = NotRecorded`. That is an honest gap — *"resolved, and nobody captured what was
decided"* — rather than an invented outcome, and it is measurable: HR can be asked to fill it in.

- `POST resolve` records a real decision with outcome, remedy, decider, date and **the rung that
  decided it, stamped rather than derived**, and is frozen on write.
- Used a second time on a `NotRecorded` resolution it fills the outcome in and **deliberately
  ignores the supplied decision text** — completing a record is not amending a decision. The
  harness asserts the original decision survives that call verbatim.
- `NotRecorded` is enum member 1 and **cannot be chosen** (422).
- `ResolutionSummary` survives as a denormalised mirror of `Decision`, because the portal renders
  it — so slice 0's assertion still matches text-for-text on that path, and its label was
  rewritten to say why rather than deleted.

**`GrievanceStatus.Closed` has a writer at last.** `POST close`, refused unless the case is at the
Board **and** the Board has answered — otherwise the desk could end a live grievance the employee
is still entitled to escalate. The harness proves the case is terminal in every direction
afterwards (interpretation, investigation, escalate, withdraw, close again all 422) and that it
drops out of the stuck queue, which is the whole point of having a terminal state.

**Both of slice 0's `CURRENT POSITION` assertions were rewritten in this commit, not deleted.**
That is the convention working as intended: the closed-status one now asserts what remains true
(closing is a deliberate act, never automatic), and the resolution one now asserts the artefact
exists and its outcome is honestly missing. ⚠ Slice 0's printed census also had a **stale key** —
it looked for `resolutionDecision` where the field shipped as `resolution`, so it scored
obligation 8 as absent after slice 2 delivered it. Corrected. *A census that names the field it
expects is only as good as that name; re-read it after the slice that fills it.*

**No back-fill, and none is possible.** Cases resolved through the old shorthand before this
migration have no resolution row: what was actually *decided*, as opposed to what the responder
wrote, was never captured. Manufacturing it in a migration would be worse than leaving the gap
visible, and the migration's remarks say so, so nobody adds one later believing it was forgotten.

---

### Slice 3 — documents and the signed agreement. CLOSED 2026-08-27.

`run-slice3.mjs` **70 assertions ×2**, ladder 151 / 119 / 111 / 70 = **451 for the area**.
Migration `20260827174902_AddGrievanceDocumentsAndSignedAgreement` (guarded SQL, registered).
**FR-HR-181 now has one obligation outstanding** — 6, union consultation notes, owed to slice 4.

**Before this slice a grievance had no document surface at all.** Not a broken one: none. Now
`StaffGrievanceDocument`, scoped Case / Step / Investigation / Agreement, entirely on the
controlled upload gate under a new scan-mandatory `hr-grievance-documents` category. The harness
asserts the gate actually *ran* — a row carries `fileUploadRecordId` and `documentRecordId` — and
that `filePath` is not a URL, because the shape this replaces elsewhere in the port is a create
endpoint taking `fileName` and `filePath` as JSON and storing nothing.

**D-10 judged, not deferred: the agreement stays off the workflow engine.** The engine models a
proposal somebody with authority confirms or refuses, and routes onward on refusal. This is a
two-party acceptance: if the employee declines, nothing routes anywhere — the case is simply not
settled and their remedy is the ladder they already have. Modelling it as an approval would also
put a decision about the employee's own case into a queue somebody else can action, which this
module refuses everywhere else. Recorded on the entity and the endpoint, not only here.

Two details of the agreement worth keeping: **`AgreementSignedDate` is supplied, not stamped**
(the signing happens in a room and the scan arrives afterwards, so `UtcNow` would record when
somebody got round to uploading it), and the employee's acceptance note has **its own column** —
appending it to `RemedyOrUndertakings` would edit part of the frozen decision to hold a remark
made after it was taken. The harness asserts the decision and the remedy are untouched by
acceptance.

⚠ **Three bugs found in my own code before the build, all the same misconception**, and worth
stating as a rule: **a terminal state is not one gate.** An agreement is the written form of a
decision, so it necessarily arrives on a case that has just been RESOLVED — but uploads, and
document deletion, were both gated on `EnsureOpen`, which refuses resolved cases. Obligation 9
would have been **unreachable by construction**, and every test of it would have read as a
permissions bug. There is now a weaker `EnsureNotAbandoned` (everything but Withdrawn and Closed),
and the harness asserts explicitly that a resolved case really does accept its agreement — because
that rule is the one most likely to be "tidied" back into `EnsureOpen` by a later slice.

⚠ **Two defects the harness found that review would not have.**

1. **The include graph crossed SQL Server's 8060-byte row limit.** Slices 1, 2 and 3 each added
   includes to `Scoped()`; individually fine, together one JOIN across five collections and two
   one-to-ones. Slice 3's documents tipped it over, and the failure mode is the nasty one:
   **`FileAsync` saved the row and then died reading it back**, so filing a grievance 500'd while
   the grievance was created. Same 8060 shape as [[hr-movements-area-survey]]'s area-8 create.
   Fixed with `.AsSplitQuery()`, which is required here rather than an optimisation and also kills
   the cartesian explosion. **An include graph has a size limit, and it bites on the READ.**
2. **`HrAttachmentUpload.ExecuteAsync` swallows business-rule exceptions.** It catches everything
   its `persist` callback throws — it must, so a scanned and registered document is never left
   pointing at a row that was never written — and answers a generic 500. So **five rules that were
   working perfectly could not say so**: 12 assertions failed, every one of them a correct refusal
   rendered as *"An error occurred while adding the attachment"*. Fixed by validating placement
   **before** the upload (`ValidateDocumentPlacementAsync`), which also means a refused placement
   never stores a file, never scans one and never needs rolling back. The checks stay in
   `AddDocumentAsync` too — the controller asks first, the service is the last word.

   ⚠ **This generalises beyond HR area 9c and is owed to slice 12.** That helper is used by
   performance (check-ins, calibration, unit goals, appraisals), recruitment requisitions and
   assets — all closed areas. Any business rule any of them raises inside `persist` has the same
   defect: a correct rule, an opaque 500. **Check those call sites; do not assume.**

---

### Slice 4 — conferencing, mediation and union consultation. CLOSED 2026-08-27.

`run-slice4.mjs` **86 assertions ×2**, ladder 151 / 119 / 111 / 70 / 86 = **537 for the area**.
Migration `20260827205357_AddGrievanceConferences` (guarded SQL, registered).

# ✅ FR-HR-181 IS FULLY DELIVERED

Slice 0's census now reads **0 of 8 artefact groups absent**. A Mandatory requirement that was
four-of-nine absent and one partial when this area opened is complete: the ladder, the statement,
the supervisor response, the HOD comments, HR interpretation, union consultation notes, the
investigation report, the resolution decision and the final signed agreement.

**One entity, three uses (D-8).** A case conference, a mediation and a union consultation are the
same shape — convened on a date, at a place, chaired by somebody, attended by named people,
producing notes and an outcome. Three tables would have been these columns three times over.
Obligation 6 is a `UnionConsultation` conference that **must** name the union it consulted, and
nothing else may name one: a row that does not say which union retains nothing the requirement
asks for.

**The notes redaction is the security property of this slice, not a UI nicety.** A mediation's
notes record what the *other party* said in a room they were promised was private, and a union
consultation's record what the union said about a member — and the case read rule admits the
complainant, rightly. Without redaction, **filing a grievance would be a route to the respondent's
position verbatim.** Notes are visible to HR and to whoever chaired *that* meeting; everyone who
may read the case still sees the meeting, its type, date, venue, attendees and outcome. The
harness asserts both arms, including that a chair sees their own meeting's notes and **not** those
of a meeting they did not chair.

⚠ `NotesRedacted` exists because *"there are notes you may not see"* and *"there are no notes"*
are different facts, and a reader who cannot tell them apart does not know to ask. Asserted both
ways.

⚠ **The redaction lives entirely in `StaffGrievanceService.MaySeeConferenceNotes`. Nothing in the
schema enforces it.** Any new reader of that column must apply the same rule or it leaks — said in
the migration remarks as well as here.

**Smaller calls worth keeping.** `DidAttend` is **nullable**: null means not recorded, `false`
means asked and did not come — different facts, and the difference matters when a grievance turns
on whether somebody was heard; `NOT NULL DEFAULT 0` would silently assert the second whenever the
first was true. `Held` is a gate, not a flag: it refuses to run without an outcome. And the chair
may write up the meeting without being in HR, because a mediator usually is not.

⚠ **The stale-key trap recurred, exactly where slice 2 predicted it would.** Slice 0's census
scored obligation 6 as ABSENT after slice 4 delivered it, because it looked for a key called
`unionConsultations` when the obligation shipped as a conference *type* inside `conferences`. That
is the second time this census has been wrong about a name. **A census is only as good as the names
it guesses, and it must be re-read by the slice that fills something in** — a green harness beside
a stale census reads as a gap that is not there.

*(Harness bug, for completeness: one probe's outcome string was under `MinLength(20)`, so it 400'd
on validation before reaching the attendee check it existed to make. Fixed and commented.)*

---

### Slice 5 — the responder matrix (FR-HR-084). CLOSED 2026-08-27.

`run-slice5.mjs` **66 assertions ×2**, ladder 151 / 119 / 111 / 70 / 86 / 66 = **603 for the
area**. Migration `20260827215716_AddEmployeeRelationsResponderMatrix` (guarded SQL, registered).
New service, controller and admin surface at `api/hr/employee-relations/responders`.

**The org-authority gap stops being worked around.** §3.2's measurement — `ManagerId` 486 of 8,353
(5.8%), unit heads 2 of 48, both *worse* than six weeks earlier — is why FR-HR-181's Supervisor
and HOD rungs cannot be derived. This is the fourth requirement to hit that wall, and rather than
work around it a fourth time, slice 5 builds what was already recommended to TDC: HR names who
answers, on a screen. Resolution is **unit row → tenant default → nobody**; the case is auto-
assigned on file, on open-case and **on every escalation**, which is the point — before this, every
escalation landed on a rung nobody was named for and sat there until an HR officer happened to look.

⚠ **The most important assertion in this slice is the negative one, and it runs FIRST, before any
matrix row exists.** A case in an uncovered unit must still file and simply arrive unassigned.
The matrix covers 0 of 48 units on a fresh tenant; if that path ever throws, the matrix will have
turned a working system into a broken one for 94% of staff. For the same reason `resolve` answers
**200 with `resolved: false`**, never 404 — showing HR where the matrix resolves to nobody is the
admin screen's main job, and a lookup that errors cannot be rendered as a gap.

**No unique index on (unit, level), and one must not be added.** A slot legitimately holds several
rows over time: acting cover while the usual responder is on leave is exactly what the effective
window is for. What must be refused is two rows *in force on the same day* — a temporal overlap no
unique index can express, enforced in `ValidateAsync`. A unique index would not tighten the rule,
it would delete the feature. Said in the EF config and in the migration remarks, because it looks
like an omission.

**The griever is never auto-assigned to answer their own case.** The matrix can legitimately name
them — somebody who answers the HOD rung can also raise a grievance — and `RespondAsync` would then
refuse the only person the step names, stranding the case with no way out but an HR override.

⚠ **Two defects the teardown found, and the teardown only found them because it was ASSERTED.**

1. **The delete gate is `DisciplineAdmin`, not `DisciplineWrite`** — matching that permission's own
   description, *"delete discipline and grievance records and catalogue entries"*. Changing who
   answers a rung is a Write act (update the row, or end its window); deleting the record that
   somebody was responsible is administrative. The harness assumed HR could, and its `try/catch`
   **silently swallowed six 403s while reporting a clean matrix** — the [[hr-tierb-tail-area-survey]]
   shape exactly. It now asserts the 403 and tears down as admin.
2. **The overlap refusal could not name who was blocking the slot.** It read *"Somebody already
   answers Supervisor for this scope"* because the sibling query had no `Include` on the employee.
   A rule that fires correctly but cannot say whose assignment to end first leaves the HR officer
   exactly where they started. Fixed.

**Also owed, and recorded rather than done:** this matrix is what would unblock **FR-HR-080** — a
head of department may issue only verbal warnings — which area 9 built correctly and left inert
because the system could not tell who a head of department was. Opening it is a **gate change in
the discipline area**, not a rule change, and it is out of scope here. Noted on the controller for
whoever reopens area 9.

⚠ *A recurring harness trap, now commented in the file: `fileCase()` leaves the token as the
GRIEVER's. Every section that writes must re-authenticate as HR — forgetting it reads as a
permissions defect in code that is correct, and cost one run here.*

---

### Slice 6 — anonymous / whistleblower intake. CLOSED 2026-08-27.

`run-slice6.mjs` **56 assertions ×2**, ladder 151 / 119 / 111 / 70 / 86 / 66 / 56 = **659 for the
area**, **plus 6 row-level SQL checks**. Migration `20260827223812_AddEmployeeRelationsConcerns`
(guarded SQL, registered).

**⚠ The harness cannot prove this slice's central claim, so it does not pretend to.** An API test
can only show the endpoint does not *echo* the reporter back — a column could hold the id, never be
mapped onto the DTO, and every assertion in that file would still pass.
`verify-slice6-anonymity.sql` reads the stored rows instead: no reporter-bearing column,
`CreatedBy`/`CreatedById` NULL on every concern and every reporter message, hashes at PBKDF2 length
with a distinct salt per row — **and the converse, that HR's replies ARE attributed**, so check 3
cannot pass merely because nothing was written. All six PASS. *Where a guarantee is an ABSENCE, the
test must read the store, not the response.*

**The foundation was verified before being relied on.** `ApplicationDbContext.UpdateAuditableEntities`
stamps timestamps, the tenant and the soft-delete flag and **nothing else** — no actor. So leaving
`CreatedBy` alone is sufficient, *and only while that stays true*. That conditional is written into
the entity, the interface, the migration and the SQL check, because if anything ever starts stamping
an actor centrally this breaks **silently**.

**`ReportAsync` takes no actor parameter at all** — there is no argument through which a caller's
identity could reach the row even by mistake. The comment says not to "fix" it by threading the
caller through for consistency.

**Decisions that are security properties, not preferences:**

- **`track` gives the same refusal whichever half is wrong.** Distinguishing "no such concern" from
  "wrong code" makes the endpoint an oracle for whether a given number exists — a slow but perfectly
  good way to discover that somebody reported something. Fixed-time comparison; both endpoints carry
  `SensitivePolicy`, because brute-forcing a retrieval code is the obvious attack here.
- **The code is stored only as a PBKDF2 hash and can never be reissued.** A reporter who loses it has
  lost their thread. That is the correct trade: a recoverable code would have to be recoverable BY
  somebody, and that somebody could then read the thread.
- **`IsFromReporter` is the discriminator, never the absence of an author** — otherwise the absence
  becomes the tell.
- **A concern can never become a grievance**, the same refusal as `OpenCaseAsync` for the same
  reason. And the converted case carries the subject and statement **but not the thread**, which may
  hold things the reporter said precisely because they were anonymous and which the case's primary
  party could then read.
- **Only the reporter is anonymous.** HR triaging, replying, closing and converting is attributed by
  name — the desk is accountable for what it does with a report.

⚠ **Anonymous means UNATTRIBUTED, not UNAUTHENTICATED (D-9), and the limit is stated rather than
glossed:** request logs and the reverse proxy still see the caller. If TDC needs untraceable
reporting that is a different build, and it should be raised as one rather than assumed to be what
this is.

⚠ **A harness lesson, and I walked into it having written the warning myself.** The file's header
notes that `SensitivePolicy` allows 5 calls per minute *per caller* — and the first draft then made
**seven** as one actor, so the last two returned 429 and read exactly like a broken feature. They
are now spread across four actors with the budget written out in the header, which is a **better**
test anyway: it demonstrates what the design actually claims — the *retrieval code* is the
credential, not the identity of whoever holds it. *A rate limit is part of an endpoint's contract;
a harness has to budget for it the way a client would.*

---

### Slice 7 — the reminder sweep's employee-relations clocks. CLOSED 2026-08-27.

`run-slice7.mjs` **46 assertions ×2**, ladder 151 / 119 / 111 / 70 / 86 / 66 / 56 / 46 = **705 for
the area**. Migration `20260827231835_AddEmployeeRelationsReminderSettings`.

**The rung clock is a setting at last.** `GrievanceRungChaseDays` was a `const` in
`DisciplineReminderService`. FR-HR-181 names the escalation route and sets **no time limit at any
rung**, so five days is OUR assumption — raised with TDC in `HR-OPEN-QUESTIONS-FOR-TDC.md` §2 and
still unanswered — and as a constant their eventual answer would have cost a code change and a
deploy. The harness proves the move: it sets the clock to 20, watches day 6 fall silent, day 21
fire, and restores it.

**Four new clocks, each with a reason to exist rather than symmetry:**

- **`ConcernUntriaged` — the one clock whose subject can do nothing for themselves.** Every other
  deadline in this engine belongs to somebody who can chase it; a whistleblower cannot, because
  asking about their report is the act that would identify them. Three days, not five.
- **`GrievanceAgreementUnsigned` — nothing else would ever surface it.** A case resolved
  `SettledByAgreement` reads as resolved and drops off every open queue, so FR-HR-181's retained
  agreement quietly goes missing. Restricted to that outcome: a case decided `NotUpheld` has nothing
  to sign, and the harness asserts it is never chased.
- **`GrievanceInvestigationOverdue` fires only where a target date was actually set.** There is no
  statutory grievance-investigation clock — FR-HR-178's four weeks is the *disciplinary* one — and
  defaulting to it would invent a deadline the requirement does not set and then chase people
  against it. Asserted: an untargeted investigation is silent even a year out.
- **`GrievanceConferenceUpcoming`** is a due-soon ladder, because the value of that reminder is that
  people turn up.

Every new reminder carries **the case or concern number and nothing else** — no subject, no
category, no venue, no attendee list. The harness asserts the complaint text does not appear in the
serialised reminder. *"Fraud concern outstanding" in a notification list is a far smaller haystack
for anybody trying to work out who reported it.*

⚠ **Three defects, all caught by the harness, none by review.**

1. **The setting was a DEAD FIELD.** Added to the entity, read by the service — and absent from the
   read DTO, the update DTO and both mapping halves. The engine honoured a value **nobody could
   change**, which is precisely the state this slice existed to end. *A setting is only configurable
   if it reaches both DTOs and both mapping halves; miss one and it is live but unreachable.*
2. **⚠ The scaffolded migration would have set every non-seeded tenant's clocks to ZERO.** EF
   generated `defaultValue: 0` plus a single `UpdateData` fixing only the DEFAULT-tenant row. Zero is
   not a neutral default here, it is the worst possible value: `ReportedAt <= today.AddDays(0)` is
   true for everything, so every unanswered rung and every new concern is overdue the instant it is
   created, and the first sweep fires on the entire back catalogue — the flood `BacklogHorizonDays`
   exists to prevent, arriving through the front door. Rewritten with real defaults plus an
   idempotent corrective `UPDATE`. **Second time this area has met it: the default is load-bearing,
   not cosmetic.**
3. **A time-of-day trap in the conference horizon.** `today` is midnight, so `today.AddDays(7)` is
   midnight on day 7 — and a meeting is a `DATETIME` with a real time on it. A 3pm meeting exactly
   seven days out fell *outside* the horizon, so **the 7-day rung never fired for any meeting not
   scheduled at midnight**. It would still have been caught at the 3- and 1-day rungs, which is
   exactly why this could have shipped unnoticed. *Comparing a DATETIME against a midnight-anchored
   horizon silently loses the last day.*

⚠ **And one of my own assertions was vacuous: `nonBlank(String(x))` can never fail**, because
`String(undefined)` is `"undefined"`. It passed twice on fields that were missing from the DTO
entirely, and hid defect 1. Both now assert the value.

⚠ **A second harness lesson: a wiring proof cannot be a late snapshot.** §6 asserted that one sweep
produced all the new kinds — and failed, because §2 had *concluded* the investigation and §5 had
*triaged* the concern, correctly. Asserting against that moment was asserting that the fixtures were
still broken. `preview()` now accumulates every kind seen across the whole run, which is what
actually proves each is wired into `CollectPendingAsync`.

⚠ **A design-time trap worth knowing before adding any setting:** `CompanyHrPolicySettings` is
seeded with `HasData` using an **anonymous type**, so EF matches it property by property. A new
non-nullable property that is not listed there does not take its C# default — it makes the whole
`DbContext` unbuildable at design time (*"the seed entity cannot be added because no value was
provided for the required property"*), and `dotnet ef migrations add` fails before it writes
anything. Commented at the seed.

---

### Slice 8 — employee-relations analytics. CLOSED 2026-08-28.

`run-slice8.mjs` **104 assertions ×2**, ladder 151 / 121 / 111 / 70 / 86 / 66 / 56 / 46 / 104 =
**811 for the area**. No migration — analytics is read-only.

**Two of area 7's lessons are structural here rather than remembered.**

1. **One endpoint, one window, one materialised set.** Not a family of per-figure methods. Area 7
   shipped a dashboard and an analytics page that disagreed about the same number because each ran
   its own query over a slightly different set. This service loads the window once and computes
   every figure from that list — slower in principle, impossible to make internally inconsistent,
   which matters more on a page whose only job is to be believed.
2. **A rate is a TYPE, not a number.** `ErRateDto` carries numerator and denominator, and its
   percentage is **null** — never 0 — when the denominator is zero; `ErCountSliceDto` carries the
   total every count is a share of. There is deliberately no way to construct a bare percentage.
   Area 7's compliance figure read 0% whether nobody complied or nobody was asked; that cannot be
   expressed here.

**Two judgements worth keeping.** Withdrawn cases are **excluded from time-to-resolution but counted
in the total** — a withdrawal is not a resolution, and including them would shorten the average
every time somebody gave up, so the figure would *improve as the process got worse*. And **concerns
are counted, never cross-tabbed**: no breakdown by unit or reporter, asserted by their absence,
because in a unit of four "one fraud concern this quarter" is an identification, not a statistic.

## ⚠ The defect this slice found, and why it hid for eight slices

**Four readers of "an answer is owed" had drifted apart.** `GetAwaitingResponseAsync` required the
case to be OPEN; the summary DTO's `AwaitingResponse`, the register's `awaitingResponseOnly` filter
and the lean projection all checked only the STEP. On live data: **227 versus 174 — and every one of
the 53 extra was WITHDRAWN.** Nobody owes an answer on a case the employee took back. Fixed with a
single `IsOpen` used by all four.

**Slice 1's harness claimed to check exactly this, and could not have caught it.** Two independent
weaknesses, either of which alone was fatal:

- It asked for **one page of 200 — the service's own cap** — and treated that as the whole set. *A
  single capped page compares a truncated set against a complete one.*
- It asserted **subset**, not set equality. *A subset check passes happily while one side silently
  carries more.*

It now pages to exhaustion, compares both directions, and separately asserts that no terminal case
appears in the stuck queue. **The dataset growing past 200 is what exposed this, not any code
change** — which is the general lesson: an assertion that fits inside one page today is a snapshot
of today's data volume, not a check.

⚠ **And a near miss worth recording.** Slice 8's own ground truth was written as
`cases.filter(c => c.awaitingResponse && isOpen(c))` — I added the `&&` because I already sensed the
field alone was not trustworthy. **That instinct should have been a question, not a workaround.**
Writing a defensive clause around a field is evidence the field is wrong; the clause hides it
instead of reporting it.
