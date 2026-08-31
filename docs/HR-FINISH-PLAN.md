# HR Module — Finish Plan

> The single tracking document for everything still owed by the HR module, sequenced into lanes
> that can be worked and ticked off. Companion to [`HR-CLOSURE-LEDGER.md`](HR-CLOSURE-LEDGER.md),
> which records *what was decided and why*; this file records *what is left and in what order*.
>
> **Scope note:** the six capability catalogues in [`docs/HR/`](HR/) — reports, bulk operations,
> import/export, cross-module patterns, documentation, UAT — are **deliberately excluded** from
> this plan for now, by decision on 2026-08-31. They are a second programme, not the tail of this
> one. Everything else the module owes is here.

**Created:** 2026-08-31 · **Position at creation:** 27 of 27 functional areas closed; W3 permissions
sweep closed; coverage queue 3 real endpoints from empty.

**Updated 2026-08-31 (later the same day).** TDC answered question 4 and lane 7 was built to the answer, so it is no longer the blocker this plan was drafted around. Lane 2 shrank as a result: eight of its questions become configuration with a default rather than a wait. The lanes that were not touched are unchanged and still correct.

---

## Summary

| Lane | Title | Items | Size | Blocked by |
| --- | --- | ---: | --- | --- |
| **0** | Ledger truth — make the instruments tell the truth again | ✅ **done 2026-08-31** | — | — |
| **1** | Close the coverage queue | ✅ **done 2026-08-31** | — | — |
| **2** | Decisions owed — 6 settings ✅ built, the rest is a memo | 13 asks | memo | TDC / the user |
| **3** | Employee Master feedback block | 19 | 4 slices | partly lane 2 |
| **4** | Leave · Training · Succession · Recruitment feedback | 15 | 2 slices | partly lane 2 |
| **5** | Section E — fields no form can set | 69 fields / 27 DTOs | 2 slices | own classification pass |
| **6** | Deferred area residues | 13 | 2 slices | 4 are blocked outside HR |
| **7** | Org-authority model | ✅ built · 4 residues | — | — |
| **8** | HR ↔ Finance GL posting sweep | 7 prerequisites + the adapters | 5+ slices | lane 2, Finance owner, Payroll owner |
| **9** | Hand-offs — real, but not HR's to fix | 3 | file and acknowledge | other owners |

**Buildable by this team: roughly 14–15 slices**, of which lanes 5 and 6 are unblocked today. Lane 7 is done; lane 2 is now a memo plus a small settings slice that can start immediately.

### Critical path

~~Lane 0~~ ✅ → ~~lane 1~~ ✅ **the queue is empty (0 BUILD).** Next is lane 2's settings half, then lane 5 or 6. Lane 2 is a **memo, and it should still go out on day one** — it gates the largest build (lane 3) and the money decisions in lane 8. It no longer gates lane 7, which is built.

⚠ **Lane 2 is smaller than it looks.** Eight of its questions convert to `CompanyHrPolicySettings` entries whose defaults are what the system already does, so those ship now and TDC's answer later costs an edit rather than a deploy. Do that half (½ slice) without waiting for anybody. Lanes 5 and 6 run in parallel with the wait. Lane 8 is the long pole and cannot finish without two other module owners.

---

## Standing rules for every lane

These are the house rules that have already been paid for once each. They are restated here so a
session picking up this plan does not relearn them.

1. **Probe the payload before writing any TypeScript.** A type written from an endpoint name is
   fiction that type-checks. Probe the **writes** as well as the reads — an empty read gives no
   shape, and four TS enum unions have already shipped as invention.
2. **A ledger entry is a claim, not a finding, until something has run.** D-30 was scheduled as
   work and was simply wrong. Every unverified row in this plan is marked `<unverified>`; verify
   before building, and record the verification.
3. **Finish an area with the two greps**: every service method matched against the screens that
   call it, and every non-GET route template matched against the service. An endpoint audit cannot
   see a capability that has no screen.
4. **Harness assertions, run twice.** The second run inherits the first's tombstones, which is the
   shape of the soft-delete/unique-index defect this module has now met nine times.
5. **Run harnesses in Staging with the JWT key**; the dev exception page hides every status code.
   Uploads need `clamd-stub.mjs` or the gate refuses with 422 and the failures read as defects.
6. **Type-check against a scoped `tsconfig`** compared to a stashed baseline — a full-project
   `tsc --noEmit` crashes on this repo (see lane 9).
7. **Migrations:** the user scaffolds, I edit and list it in `FastBuildMigrationMetadata`, the user
   updates. A schema change has three homes including the snapshot. A migration not listed is inert.
8. **I never run `dotnet build` and never commit.** Kill the running API process before asking for
   a rebuild; stage the slice and hand over the message.

---

## Lane 0 — Ledger truth · ½ slice · unblocked

The ledger is one commit stale and structurally over-counts. Do this first, or every later lane
argues with a document that disagrees with the code.

- [x] Re-run the four `scripts/hr-coverage/` instruments and regenerate. **Done 2026-08-31**:
      queue **245 → 235**, verdicts **13 BUILD → 3**, confirmed-unreachable 6, DTO gaps 69/27.
      The ten sanction rows **left on their own** — nothing was hand-edited to remove them, which
      is the check that the wiring is real. ⚠ Two controllers rose (`PerformanceAppraisals` 13 → 14,
      `SalaryGrades` 4 → 5) and it is **not** ours: those three commits touched only discipline
      files, so the call sites are unchanged and the drift predates them. Every row in both is
      already covered by an existing disposition, so nothing new needs a decision.
- [x] **Retire the ten discipline-sanction rows.** **Done**: `_SANCTION` in the generator is now
      `DONE`, carrying the D-18 correction — the block was half wrong (the "no actor reaches the
      rule" half rested on two probe assertions expecting 401 where a refusal is 403, against stale
      tokens) and half right and understated (three of four sanctions were accepted on an undecided
      case). `StaffDisciplineSubEntity` no longer appears anywhere in the ledger.
- [x] **Record D-29's over-count in the Position table's own note.** **Done**: the table now carries
      it as a general rule rather than one instance — instrument 01 matches the frontend SERVICE
      layer, so any endpoint withheld from the UI on purpose still counts as wired. "Wired to a
      screen" is a **ceiling on coverage, never a measurement of it**, and the check that sees
      through it is the two greps.
- [x] **Re-verify section F before anyone builds from it.** **Done 2026-08-31 — and it earned its
      place.** ~24 of 35 rows hand-verified in source. The Employee Master block (lane 3) is
      **entirely real**: disability sits on `EmployeeDependent` (class at `HREntities.cs:1004`),
      no `Hometown`, no gender-other description, guarantor has no amount or photo, referee has no
      letter field, `IdentificationType` carries only `HasExpiryDate` and no lead days,
      `QualificationType` is a category (`Education`/`Experience`/`Certification`/`License`), and
      there is no position-document, staff-number-config, appointment-letter or labour-law entity
      anywhere. **Two Training rows were false and are now `DONE`** (see lane 4), and one was
      understated. Leave numbering is confirmed a max+1 prefix scan at `LeaveService.cs:1004`, and
      `searchCandidates` confirmed to have zero `.tsx` callers.
- [x] Add the three lane-1 endpoints to a C2 build checklist so their boxes tick themselves.
      **Done**: `SeparationsController` (29 of 31 wired) and `EmployeeCompetencyController`
      (3 of 4) are now enumerated checklists.

**Done when:** ~~the queue reads 3 BUILD, and no controller-level disposition names an endpoint that
no longer exists.~~ ✅ **Both hold as of 2026-08-31.**

---

## Lane 1 — Close the coverage queue · ✅ **DONE 2026-08-31** · 31 assertions ×2

**The queue reads 0 BUILD.** The regenerated ledger no longer prints the category at all: 234
flagged, 96 INTENTIONAL / 126 FALSE / 12 DONE. Harness: `hr-separation/run-finishplan-lane1.mjs`,
31 assertions, green on two consecutive runs.

- [x] **`POST api/hr/separations/retirements/sweep`** and
- [x] **`POST api/hr/separations/contract-expiries/sweep`** — ⚠ **the gap was four times bigger
      than the queue could show.** The plan said "a client method with no screen caller". In fact
      the **entire separation reminder surface had no screen at all**: all six endpoints were
      mentioned by exactly one file in the whole frontend, the service that defines them. Three of
      the six are GETs, which instrument 01 skips outright; `reminders/run` matched its own service
      definition and counted as *wired*; only the two sweeps surfaced, and only because of the
      query-string artefact. **Three separate blind spots hiding one missing screen.**
      `/administration/hr/separation/reminders` now serves all six, and is the last reminder family
      to get the console every other one already had.
- [x] **`DELETE api/employee-competencies/{}`** — client method plus an Admin-gated affordance on
      `/hr/competencies/assess`. It **ticked itself** out of the queue on the next instrument run.

**Three things this lane established that the plan did not know:**

1. ⚠ **The sweeps RAISE separations, and the API has no delete for one.** There is no
   `[HttpDelete("{id:guid}")]` on `SeparationsController` — only settlement lines can be removed.
   So the screen shows the upcoming lists first, marks who already has a separation, states how
   many are genuinely new, and puts each sweep behind a confirmation that names the horizon and
   says the raise cannot be undone. **Learned the hard way**: probing the endpoints raised a live
   record at a 10-year horizon (against a harness fixture employee; soft-deleted afterwards,
   stamped `probe-cleanup-lane1-sweep`).
2. ⚠ **The horizon is the whole screen.** At the default 90 days both lists look empty on this
   data, which reads as "broken" rather than "nothing due yet"; at 3,650 days the same endpoints
   return 12 retirements and 10 expiries. The control is not a refinement, it is the feature.
3. ⚠ **The administrator who may delete an assessment cannot create one.** `admin` has no linked
   employee record and the assessor is taken from the token, so assessing 400s for them while the
   Admin-tier delete succeeds. The harness records both halves.

⚠ **The two sweeps still read as flagged and always will.** Instrument 01 needs a literal first
argument and the query must be interpolated, so a computed URL is invisible to it — the same blind
spot that reads `EmployeesController` as 73/81 unwired. Splitting the path from the query was tried
and changed nothing; both are dispositioned `DONE` with the evidence. **Do not re-fix the call site
for coverage's sake** — the client comment says so too.

## Lane 2 — Decisions owed · a memo plus a ½ slice · blocks lanes 3 and 8

**Send the memo before starting lane 3.** But do NOT treat the whole lane as a wait: eight of the
questions below are better answered by shipping a defensible default that TDC can change on a
screen, and that half can start today.

### 2a. The six that become settings, not questions · ✅ **DONE 2026-08-31** · 30 assertions ×2

⚠ **It was eight, and two of them did not survive contact with the code.** Both are back in 2d as
genuine questions:

| Withdrawn | Why it is not a setting |
| --- | --- |
| `LongServiceBasis` | The basis is `HrPolicyCalculations.CompletedYears(employee.DateEmployed, …)`. "Continuous from `DateEmployed`" versus cumulative-with-breaks versus a recognised-service date are **three different computations**, not three values of one. A setting would have had one branch implemented. |
| `GrievanceInternalDocsVisibleToParties` | There is **no visibility mechanism in employee relations at all** — nothing withholds HR's interpretation or the investigation report from anybody today. The default "true, as built" was accurate only because no rule exists. The setting would have had nothing to switch. |

Both are the D-29 trap: a control that enforces nothing creates false assurance, which is worse
than an honest open question. Recorded rather than shipped.

`CompanyHrPolicySettings` already exists: one row per tenant, coded defaults returned when no row
exists, and a screen at `/administration/hr/settings/policy`. `GrievanceRungChaseDays` is the
precedent — area 9c pulled the 5-day rung clock out of a `const` for exactly this reason, so TDC's
answer would cost a settings edit rather than a deploy.

⚠ **Every default below is what the system does today**, so conversion changes no behaviour. What
it changes is that the answer stops being a blocker.

| Question | Setting | Default | Hardcoded today at |
| --- | --- | --- | --- |
| Written-query window | `WrittenQueryHours` | 48 | `DisciplineProcessDeadlines.cs:31` |
| Employee's response window | `QueryResponseWindowHours` | 72 | `:55` — its own comment already says TDC may want working days |
| Investigation window | `InvestigationDays` | 28 | `:34` |
| Reminders stop after | `DisciplineBacklogHorizonDays` | 90 | `DisciplineReminderService.cs:115` |
| Exit-pay daily rate | `SettlementDaysPerYear` | 365, as built | `SeparationService.cs:1826` |
| Attendance denominator | `AttendanceRateIncludesApprovedLeave` | true, as built | `AttendanceDashboardService.cs:38` |

⚠ **One that must NOT become a setting.** The natural-justice gate — a decision blocked until the
employee has been heard — stays fixed. Making due process an option means the first time it is
switched off is the first unfair-dismissal claim. It was only ever "for confirmation" anyway.

⚠ The settlement already stores `DailyRateBasis` as a string on the row, so switching the divisor
later does not rewrite settlements already computed. **Built so the divisor and the sentence come
from the same number**, so the words on a settlement can never describe a basis other than the one
that produced the figure beside them.

**What shipped:** the six settings on `CompanyHrPolicySettings`, both DTO halves, both mapper
halves, the `HasData` seed, migration `20260831232612_AddHrPolicyDeadlineSettings`, the settings
screen with its own card, and — the part that makes them real — **every consumer rewired to read the
policy instead of a constant**. Harness `hr-separation/run-finishplan-lane2a.mjs`, 30 assertions,
green twice. It asserts movement, not storage: widening the query window pushes the case clock by
**exactly** 192 hours, widening the answer window pushes the natural-justice gate by **exactly**
648, and the settlement divisor reproduces **TDC's own worked example** — GHS 197.2603/day at 365
against GHS 272.7273 at 264, the 38% spread the open-questions document quotes, with each
settlement recording its own divisor in words and neither naming the other's.

⚠ **The migration took three attempts and each failure hid the next.** (1) The scaffold refused
outright: the `HasData` seed needs a value for every new non-nullable property — its own comment had
warned of this. (2) The generated body used `defaultValue: 0` and repaired only the seeded row by
id, which would have left any other tenant on `WrittenQueryHours = 0` (every case instantly in
breach) and `SettlementDaysPerYear = 0` (**a divide-by-zero in the daily rate**). (3) Its
`UpdateData` then failed at `database update` — that operation resolves column types from the
migration's TARGET MODEL, and the fast EF build strips every `*.Designer.cs` **and** the snapshot.
**A data operation in a migration in this repository must be raw SQL**, or it works in Release and
breaks in Debug.

⚠ **The constants were in more places than the plan knew.** FR-HR-178's investigation window turned
out to have **five** call sites, not one: the case advisory, the overdue queue, the reminder sweep's
cutoff, the sweep's own due-date arithmetic, and the interface doc that described it. The
written-query clock had two. A rename of each constant to `Default*` was what surfaced them — the
compiler found what a grep had missed.

⚠ **The attendance rate is computed in THREE places, not two** — today's snapshot, the daily trend,
and the chronic-absentee ranking. The class comment said two. All three now take the flag from a
single read in `GetDashboardAsync`: a headline rate that counted leave while the risk list beside it
did not would rank people by a rule the page does not state.

⚠ **`DailyRateBasis` is not like the other seven, and it is the one to hold.** The four candidate
divisors span **GHS 3,156.16 to GHS 4,363.64 on the same facts** — a 38% swing in what a leaver is
actually paid. Storing the basis in words makes an early settlement *auditable*; it does not make
it *right*. So ship the setting with the ÷365 default like the rest, and then either **do not run a
real final settlement until TDC answers**, or accept that anything settled meanwhile may need a
correction exercise. The other seven are windows and visibility flags — being wrong for a fortnight
costs nothing. This one pays money out of the door. `SeparationService.cs:1826` says the same thing
in its own remark: *"Do not change this quietly."*

### 2b. Two that were never questions

- [x] **Which awards accept a nomination from the nominee themselves** — `AwardType.AllowSelfNomination`
      is already a per-award flag. It needs sensible seeded values per category, not a decision.
- [x] **How long may a grievance sit at one rung** — already `GrievanceRungChaseDays`.

### 2c. The product call that blocks a build

- [ ] **D-13 — does HR administer employee insurance enrolment?** Seven writes (`policies`
      create/update/cancel/delete, `dependents` add/update/remove) have no HR screen; only the
      employee's self-service surface reads them. If enrolment arrives from payroll or the insurer,
      D-13 closes as `INTENTIONAL` and nothing is built. If HR administers it, this is a slice.
      ⚠ Consequence today: the insurance-claims path **cannot be tested at all**, because no policy
      exists to claim against.
      ⚠ Worth putting a recommendation in the memo rather than an open question: **build it**. The
      cost of being wrong is one unused screen; the cost of waiting is that claims stay untestable.

### 2d. The questions that genuinely need TDC

Tracked in [`HR-OPEN-QUESTIONS-FOR-TDC.md`](HR-OPEN-QUESTIONS-FOR-TDC.md); listed here so the plan
is self-contained. **Question 4 has been answered and is struck through.**

- [x] ~~**Will reporting lines and departmental heads be maintained?**~~ — **ANSWERED 2026-08-31.**
      A head of department is the employee named on `OrganizationUnits.HeadEmployeeId`; a supervisor
      is the line manager on `Employees.ManagerId`. Lane 7 was built to this answer. ⚠ The
      administrative dependency survives: TDC must populate the real hierarchy before the module is
      relied on — today's data is seeded test data, stamped `seed-hr-org-authority`.
- [ ] **No public holiday calendar is loaded** — an ops prerequisite, not a design question.
      ⚠ Consider seeding a Ghana statutory calendar as an editable default, the same spirit as 2a.
- [ ] **How does TDC actually pay for medical treatment?** — also a lane-8 money question.
- [ ] **What is the basis for a long-service award?** ⚠ **Withdrawn from 2a on 2026-08-31 — it is
      not a setting.** `CompletedYears(DateEmployed, asOf)` is continuous service; cumulative-with-
      breaks and a recognised-service date are different computations, so answering this is a build,
      not a configuration change. ⚠ Only **1** employee has 10+ years on
      record and **0** have 15/20/25; 38% have a `DateEmployed` at all. The answer may be a data
      question, not a policy one. (The *basis* becomes a setting under 2a; the *data* does not.)
- [ ] **Who may read HR's interpretation and the investigation report on a grievance?**
      ⚠ **Withdrawn from 2a on 2026-08-31 — it is not a setting.** Employee relations has **no
      visibility mechanism at all**: nothing withholds either document from anybody today. Whatever
      TDC answers has to be built before it can be configured.
- [ ] **Nobody holds the Internal Audit role** — an operational prerequisite; the separation
      pipeline has a review step with no possible reviewer. ⚠ Re-measured 2026-08-31: the role is
      **not in the database at all** — `AspNetRoles` has 47 rows and neither `TDC_INTERNAL_AUDIT`
      nor `TDC_MANAGING_DIRECTOR` is among them. It must be created before it can be granted.
- [ ] **Public certificate verification** — does TDC want outside parties to verify a training
      certificate without a login? The endpoint is built and rate-limited; no public page exists,
      and it would be the application's first unauthenticated page.
- [ ] **Attendance-rate denominator** — confirm the policy choice. (Becomes a setting under 2a.)
- [ ] Payroll-vs-direct-payment reimbursement (lane 8; governs two surfaces).
- [ ] Cost attribution — project and cost-centre dimensions (lane 8).
- [ ] For information: a disciplinary decision is blocked until the employee has been heard, and
      disciplinary reminders stop chasing after 90 days.

### 2e. The four `DECIDE` rows in the demo-feedback backlog

- [ ] Workflow step checklists are unused by any HR process — candidates are onboarding, separation
      clearance, probation. Adopt one or close the row.
- [ ] Compassionate leave set off against annual leave — there is no offset or advance concept
      anywhere in the module; this needs a design call before it is scoped.
- [ ] Training menu order differs from TDC's suggestion — the setup/operations split may be
      deliberate.
- [ ] Appraisal check-in link to company objectives — confirm it matches intent.

**Done when:** the eight settings are shipped with their defaults, every remaining row is answered
or explicitly deferred with a trigger, and the answers are written back into the ledger's section A
so they are never re-derived.

---

## Lane 3 — Employee Master feedback block · 4 slices · the largest build left

Nineteen rows against the module's most-used entity. Grouped by migration boundary so each slice is
one schema change, one set of screens, one harness run.

### 3a — Employee record fields (one migration)

- [ ] Disability tick and description sit on `EmployeeDependent`, not `Employee` — **wrong entity,
      not merely absent**. Moving it is a migration plus a back-fill, not a new field.
- [ ] Hometown absent from the employee record. ✅ *verified 2026-08-31 — no `Hometown` anywhere in
      `Entities/HR/`.*
- [ ] Gender `Other` has no description field.
- [ ] Guarantor has no guaranteed amount and no photograph.
- [ ] Referees cannot carry a reference letter. ⚠ Goes through the controlled upload gate, not a
      `filePath` string — see 3c.
- [ ] Expatriate: no issue dates, no resident permit, no family members (`FamilyAccompanying` is a
      bare bool today).

### 3b — Reference data that should be a dimension (one migration + admin screens)

- [ ] `IdentificationType` has no expiry notification lead days — a per-type setting, and the
      reminder engine has nothing to read.
- [ ] Certification bodies are free text (`EmployeeSkill.CertifyingBody`) — should be a lookup.
- [ ] Qualification level is not a dimension. ⚠ `QualificationType` is a **category**, not an
      academic level — and area 17's closure slice already proved that enum is wider than the TS
      union claimed. Read the enum before designing the ladder.
- [ ] Staff number auto/manual is behaviour, not configuration — no mode flag, no prefix, no format.
      ⚠ Build it on `INumberSequenceService`, not a max+1 scan: this module has met the
      prefix-scan defect **at least eight times**, and the platform mechanism is atomic per tenant.

### 3c — The document surface (controlled upload gate)

- [ ] **No employee document attachments exist at all.** ✅ *verified 2026-08-31 — there is no
      `EmployeeDocument` entity and `EmployeesController` has no upload route, while the gate is
      wired into 30+ other HR controllers.* This is the single largest functional hole left in the
      module: the most-used entity in HR cannot hold a contract, an ID scan or a certificate.
      New table, `HrAttachmentUpload` + `HrDocumentDownload`, a category registered in
      `SystemCleanScanRequired` **in the same commit** (D-11's half-job must not recur).
- [ ] No mandatory documents against a position — the checklist that makes 3c enforceable.
- [ ] No appointment letter templates — only email templates exist today.

### 3d — Guards, pickers and the rest

- [ ] **Probation dates stay editable after confirmation** — no confirmed-state guard on contract
      update. Same shape as D-03, which was closed by a service-level guard across 36 write paths;
      copy `RequireAuthorableJobDescriptionAsync`, do not put the rule on the screen.
- [ ] **Manager picker ignores `ReportsToPosition`.** ✅ *verified — `ReportsToPositionId` exists at
      `HREntities.cs:777` and is populated; the picker does not use it.*
- [ ] Exit interview questions are fixed fields, not a configurable question set, and carry no
      attachments.
- [ ] No labour-law checklist.
- [ ] No mass application of benefits to dependents. ⚠ This is a **bulk operation**, which is the
      excluded `docs/HR/` programme — build the single-record path here and record the bulk need
      there rather than inventing a second bulk pattern.

**Done when:** each slice's harness is green twice, the two greps return no uncalled service method,
and the section F rows are ticked in the ledger with the commit that closed them.

---

## Lane 4 — Leave · Training · Succession · Recruitment feedback · 2 slices

Thirteen feedback rows plus the four manpower-budget endpoints, mostly UI over endpoints that
already work. ⚠ Every one is `<unverified>` — several name client methods as dead code, and "no UI
caller" has been wrong before.

### Leave

- [ ] Adjustment form does not show the employee's balance.
- [ ] Reliever clashes are not visible on the plan.
- [ ] Free-text field still labelled "Reason", not "Remarks" — cosmetic.
- [ ] Leave request numbering still uses a max+1 scan → move to `INumberSequenceService`, seeded
      from the table's current maximum so it keeps issuing after the numbers already in the wild.
      **Assert create → delete → create**; a create-then-create pair passes on the broken code.

### Training

- [ ] **Bulk nomination has no UI** — ⚠ re-verified, and the row was *understated* rather than
      wrong. `setBulkResult` is declared at `NomineesPanel.tsx:63` with **no call site anywhere**,
      and there is no `bulkNominate` client method — but `bulkResult` **is rendered** at lines
      158-166, listing the created count and every skipped row with its reason. The result display
      was built and the action never was. Build the action; the display is waiting for it.
- [x] ~~Bulk completion has no UI and no client method~~ — **FALSE, struck 2026-08-31.**
      `BulkCompletionPanel.tsx` calls `trainingCompletionService.bulkRecord` (line 89) and is
      mounted at `schedules/[id]/page.tsx:364` with a readOnly guard for cancelled schedules.
- [x] ~~Nominee availability check never shown~~ — **FALSE, struck 2026-08-31.** Wired at
      `NomineesPanel.tsx:314` onto `checkAvailability`, which renders the conflicts. Closure lane 2
      built it. ⚠ **The generator already held a `DONE` for this controller while this row and the
      endpoint disposition beside it both still said `BUILD`** — two dispositions on one controller
      disagreeing, and the ledger rendered both. That is the argument for lane 0's re-verification
      existing at all.
- [ ] No "Training Activities" grouped screen.
- [ ] Mentoring still inside the Training menu — wants its own nav section.
- [ ] Certificate does not gate completion — a per-program flag.

### Succession

- [ ] Criteria candidate search has no screen — `successionSearchService.searchCandidates` has no
      caller.
- [ ] Candidate age and service-years-left are not displayed, though the API returns both.

### Recruitment / manpower budgets

- [ ] Menu still reads "Manpower Budgets" → "Manpower Recruitment Budget".
- [ ] **`PUT` and `DELETE api/JobAnalysis/budgets/{}`** have no caller.
- [ ] **`PUT` and `DELETE api/JobAnalysis/lines/{}`** have no caller. Surfaced when the whole
      JobAnalysis controller was enumerated and never scheduled: a manpower budget can be created
      and approved but **not edited or deleted**.

---

## Lane 5 — Section E · 2 slices · unblocked

**69 fields across 27 DTOs.** These endpoints *are* wired; the form omits fields, so the feature is
degraded rather than missing — the class an endpoint audit cannot see.

- [ ] **Classification pass first, endpoint by endpoint.** Section D's lesson applies exactly: of
      149 `REVIEW` endpoints only 65 were real. Some of these 69 fields are correctly omitted
      (server-computed, or set by a transition rather than a form). Produce a `BUILD` /
      `INTENTIONAL` verdict per field **before** any screen work, and put the reasons in the ledger.
- [ ] `UpdateAppraisalHRReviewDto` — **6 of 8**, including `AdjustedOverallScore` and
      `AdjustmentReason`. HR can adjust a score and cannot say why. Highest severity in the table.
- [ ] `Create`/`UpdateJobShortlistingCriteriaDto` — 6 each: `MatchMode`, `MatchStrategy`,
      `ComparisonOperator`, `RequiredValue`, and both required-entity FKs. The **entire matching
      mechanism** is unreachable from the form.
- [ ] `Create`/`UpdateStaffTravelVisaRequirementDto` — 6 and 5.
- [ ] The medical insurance plan and provider pairs — 4 and 3 each (contribution percentages,
      lifetime limit, claims portal).
- [ ] The remaining 21 DTOs — 1 or 2 fields each; batch by screen, not by DTO.

**Done when:** every field is either settable or carries a recorded reason, and instrument 03
re-runs clean against the classification.

---

## Lane 6 — Deferred area residues · 2 slices · partly blocked outside HR

Each was a deliberate deferral with a trigger, not an oversight.

### Buildable now

- [ ] **9c — `getCasesForSource` has no screen.** The reverse "this incident has ER cases" panel
      needs UI in SHE, performance and discipline — three closed areas. API and client method ready.
- [ ] **9c — `probe-slice10b.mjs` files a real concern on every run**, leaving probe rows in the
      live whistleblower inbox. Fix the harness, or clean up after it.
- [ ] **25 — portal feedback form**, which needs a `feedback/mine` read.
- [ ] **25 — `appraisalNotificationService` left with no consumers.** Wire it or delete it; a
      service with no caller is the shape closure lane 2 kept finding.
- [ ] **12 — the travel exchange rate is still caller-supplied.** `IExchangeRateService` is
      registered and callable at `ServiceCollectionExtensions.cs:796`. This is **not** deferred to
      the GL sweep: master data is read now, per the standing split.
- [ ] **D-02 — self-service invitation response is act-as-anyone.**
      `events/{id}/participants/respond` takes a `ParticipantId` on the HR-desk policy. Correct for
      the HR screens; needs a self-or-permission check **before** any `/me` calendar ships.
- [ ] **D-29 — the travel policy rules.** Not a screen job: the work is **enforcement**, and three
      things belong in it — the approval guard on rule writes, a filtered unique index on
      `(TenantId, PolicyId, RuleCode)` instead of the revive-on-re-add, and mapping
      `RuleType × ExpenseCategory` onto real booking and claim fields, which is a product decision.
      Until then the five endpoints stay withheld and are **not** gaps.

### Blocked outside HR — track, do not schedule

- [ ] **10 — project auto-trigger and the procurement/construction hard block**
      (FR-ENV-008/010/012/013): needs the Projects module and TDC's DR-09.
- [ ] **10 — FR-SHE-114/201 procurement routing.**
- [ ] **10 — SMS alerts** (FR-SHE-161): no gateway exists.
- [ ] **10 — SHE-specific roles** (DR-10).

### Owed to ops, not to engineering

- [ ] **TDC's holiday calendar** and the real leave-type catalogue (28 days permanent / 15 days
      contract, per the questionnaire). Seeding, and it blocks real use of leave.
- [ ] **The responder matrix is unconfigured — 0 of 48 org units** — which also leaves its
      TypeScript type the one interface in the module never validated against a live payload.
      ⚠ Now derivable: lane 7 populated `HeadEmployeeId` and `ManagerId`, so this could fall back to
      the hierarchy instead of resolving to nobody.

⚠ **Five seed gaps make parts of the module unverifiable, not merely unused.** Found while running
the suites; each is data, not code:

- [ ] **`TDC_INTERNAL_AUDIT` does not exist in the database.** 47 roles, and neither it nor
      `TDC_MANAGING_DIRECTOR` is among them. `hr-separation/run-slice8` and `run-slice10` cannot
      execute at all, so FR-HR-185 is **unverifiable end to end**, not just unused.
- [ ] **No leave types are seeded.**
- [ ] **`ReportDataSources` is empty** — awards slice 10 aborts on it.
- [ ] **`AppraisalCompetency` is empty** and is a *different store* from `/api/competencies`
      (which has rows). `hr-performance/run-sliceC` and `run-sliceE` abort on it.
- [ ] **The employee population is down to ~1,084**, and awards slices 0/0b are ground reports with
      stale hardcoded fixtures rather than pass/fail suites. Read this list before believing a red.

⚠ **Harness litter is a live hazard, not a tidiness issue.** `hr-discipline/run-slice5`'s cleanup
needed `HR.Attendance.Admin`, which its HR actor does not hold, so it 403'd on **every run since it
was written** and left a fixture public holiday in the tenant's only calendar each time — changing
attendance and leave calculations for everyone. Three had accumulated. Fixed and cleared; the shape
is worth watching for elsewhere.

---

## Lane 7 — Org-authority model · ✅ BUILT 2026-08-31 · 4 residues

Three spec requirements rested on one missing foundation: FR-HR-080 (issuing authority), FR-HR-181
(the grievance ladder), FR-HR-173 (area 8's establishment rule, downgraded to advisory for exactly
this reason). **TDC answered the question and the foundation was built to the answer.**

> A head of department is the employee named on `OrganizationUnits.HeadEmployeeId`. A supervisor is
> the line manager on `Employees.ManagerId`.

### What shipped

- [x] **`seed-hr-org-authority`** (`ca27765f`) — a head on every unit and a manager on every
      employee. ⚠ A **separate command**, never folded into `seed-hr-all`: that seeds TDC's real
      structure, and fabricated management lines behind it would look authoritative. It never
      overwrites, so it does nothing where a real hierarchy exists, and every row it writes is
      stamped `seed-hr-org-authority`.
- [x] **FR-HR-080 made real** (`f6599ea2`). `ResolveIssuingAuthorityAsync` reads the organisation:
      HR/SuperAdmin/Admin hold full authority; the head of the employee's own unit **or of any unit
      above it** holds head-of-department authority; nobody else holds any. The decision route is
      ungated, because headship is data and no permission policy can express it — and the
      surrounding routes (`approve-decision`, `reject-decision`, `awaiting-my-approval`) were
      already ungated for the same reason.
- [x] **The ten sanctions unblocked and built** (`8667ab0e`), which is what this foundation was
      holding.

### What the old plan said, and why it is wrong now

⚠ This lane used to read *"blocked on TDC question 4"*, quote **0 of 41** units with a head and
**3 of 562** employees with a manager, and warn that *"deriving authority from org structure
resolves to nobody for every employee, so a well-built feature would refuse all of them"*. All three
are now out of date. Measured after seeding: **41 of 41** units headed, **1,057 of 1,084** employees
with a manager, **0 cycles**, nobody self-managed.

⚠ It also listed *"FR-HR-080 still wants a head-of-department role — opening it up is a gate change,
not a rule change"* as outstanding. That gate change is made, and proven: a head of department
holding **no discipline permission at all** can decide for someone in their unit, is capped at
head-of-department actions, and a head of an unrelated unit is refused indistinguishably from a case
that does not exist. `probe-authority-gate.mjs`, 33 assertions.

### Residues

- [ ] **The data is seeded TEST data.** TDC must populate the real hierarchy before the module is
      relied on. Every seeded row carries `UpdatedBy = 'seed-hr-org-authority'`, so the synthetic
      lines are findable and removable. This is an administrative dependency, not a coding one —
      **say it out loud rather than let it be discovered at go-live.**
- [ ] **Employee placement is an artefact of the port and was deliberately not touched.**
      1,046 of 1,084 employees sit in a single unit, *Financial Accounts*; 14 in *General*; 24 in
      none. The 41 units are genuine TDC structure. So the hierarchy is coherent and exercises every
      rule, but only one branch is realistic. Redistributing a thousand people to make it look
      plausible would invent far more than it fixed — that belongs with the real employee data.
      ⚠ **The 24 in no unit have a consequence worth naming.** `ResolveIssuingAuthorityAsync`
      returns null when the subject has no `OrganizationUnitId`, so for those 24 nobody but
      HR/SuperAdmin/Admin can ever decide a case — refused indistinguishably from a case that does
      not exist. That is correct fail-closed behaviour, not a defect; it is a data dependency of
      the same kind as the residue above, and it disappears the moment those 24 are placed.
- [ ] **FR-HR-181 and FR-HR-173 can now be revisited.** Both were worked around for want of this
      foundation: area 9c's responder matrix resolves to nobody for 0 of 48 units, and area 8's
      establishment rule is still advisory. Neither is broken; both could now derive rather than
      refuse. **Re-measure before designing** — that rule has been paid for twice.
- [ ] **⚠ The harnesses have already polluted the measurement this lane tells you to re-run.**
      Measured 2026-08-31, after the seeding: the raw tables read **65 of 68 units headed** and
      **1,057 of 1,454 employees managed** — which looks like the seeding regressed, and has not.
      Filtering by provenance reproduces the figures above exactly:

      | | |
      | --- | ---: |
      | Seeder ran at | `2026-08-31 19:20:56` |
      | Employees present then | **1,084**, of which **1,057** now carry a manager |
      | Employees created since, all unmanaged | **370** — every one an `A9Ver Actor…` harness actor |
      | Units named `Probe%` | **27**, and the 3 unheaded units are all probe fixtures with no staff |

      So the real structure is **41 of 41 headed** and the fixtures account for every apparent gap.
      **Exclude `Name LIKE 'Probe%'` and `CreatedAt > @seededAt` before quoting any number from
      this data**, or the next re-measure files a defect against a system that is working. Same
      shape as `probe-slice10b.mjs` in lane 6, one layer more consequential: this residue
      contaminates the exact query that decides whether FR-HR-181 and FR-HR-173 can derive.

---

## Lane 8 — HR ↔ Finance GL posting sweep · 5+ slices · the long pole

The one workstream deliberately deferred module-wide: HR does not post to GL per area; **one sweep
does it after the whole module is built**. That condition is now met.

Register: [`HR-FINANCE-INTEGRATION-BACKLOG.md`](HR-FINANCE-INTEGRATION-BACKLOG.md) · entity map:
[`HR-FINANCE-ENTITY-SWEEP.md`](HR/HR-FINANCE-ENTITY-SWEEP.md) · mechanism:
`docs/Finance/finance-integration-contract-catalogue.md`.

### 8a — Prerequisites

- [ ] **Back-fill the five closed areas** into the register — Leave, Compensation, Training,
      Medical, SHE. ⚠ **Medical is the priority**: it has a live claim → approve → **pay** path, so
      it is the closest analogue to travel 12.1 and the two must post the same way. Without this
      the sweep is a re-survey.
- [ ] **Raise FIN-INT-011** ("SH Fund, PF, ESB and fuel allocation") with the Finance owner — the
      one catalogue entry that is HR/payroll-shaped and has **no assigned owner**. Coordination is
      required before anyone designs against it.
- [ ] **Open the budget-commitment conversation.** No contract exists for HR's four budget surfaces
      (`ManpowerBudget`, `TrainingBudget`, `AwardBudget`, `StaffTravelBudget`), which today each
      track `BudgetAmount`/`SpentAmount` themselves with no reserve → consume → release. FIN-INT-015
      is Available but Procurement-specific; reusing its shape is a **new, Planned-status
      conversation**, not something HR wires up unilaterally.
- [ ] Settle the **three-way training double-count**: area 7's training budget, succession
      development activities, and `ManpowerBudget.TrainingBudget`.
- [ ] Decide who writes `ManpowerBudget.ActualSpent` and `.Variance` — nothing does today.
- [ ] Decide whether a manpower budget carries a currency at all, and if so which.
- [ ] Decide whether an approved asset surcharge is an employee receivable, a payroll deduction, or
      both in sequence.

### 8b — The adapters

- [ ] Decide the treatment **once**, then apply it across every row in the register — travel claims
      and advances (an outstanding advance is an **employee receivable** living only in HR, in no
      trial balance and no ageing), medical claim payments, separation final settlement, awards,
      asset surcharges, succession development spend.
- [ ] Each adapter ships with the **reusable consumer-contract tests**
      (`FinanceConsumerContractAssertions.ShouldSatisfyPostingContract`): happy path,
      retry/idempotency, Finance-failure-does-not-mark-posted, cross-tenant, closed period,
      AR/AP visibility. Resolve accounts from Finance configuration, never hard-code; deterministic
      idempotency key; persist `PostingEventId`/`JournalEntryId` back onto the HR source record.

### 8c — Not HR's to execute

- [ ] **Payroll's GL posting must migrate off `IJournalEntryService` onto `IFinancePostingEngine`.**
      `PostPayrollJournalAsync` creates and posts a Finance journal directly, which the Finance
      owner's 2026-08-31 message forbids in terms — so this is a **compliance fix, not an
      improvement**. ⚠ **Payroll belongs to another developer and HR integrates read-only.** Raise
      it with the payroll owner and the Finance owner; do not fix it from HR.

**Done when:** every money event in the register either posts through `IFinancePostingEngine` with
its contract tests, or carries a recorded decision not to.

---

## Lane 9 — Hand-offs · file and acknowledge · not HR's to fix

Real defects HR found in other teams' code, plus two platform items. Recorded so the module can be
declared finished without pretending these are closed.

- [ ] **22 cross-module defects** in
      [`CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`](CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md). The
      severe ones, in order: **#14/#15** — the workflow platform's pending feeds die mid-body once
      non-empty, and a generic-surface approval consumes the approval while stranding the record
      (**every module on the engine is affected**); **#3** conditional routing never routes; **#11**
      bare `[Authorize]` behind the external-user allowlist, with procurement exposed *today*;
      **#9** Projects' maintenance follow-through throws for every tenant; **#10** maintenance
      numbers collide within one second behind unique indexes.
- [ ] **The EF migration chain cannot build the database from scratch** — no migration ever CREATEs
      `Employees`, so a fresh environment cannot be deployed with `dotnet ef database update`.
      `rebuild-db` works around it. A squashed baseline generated from the current model is separate
      platform work, and it is not HR-caused.
- [ ] **A full-project `tsc --noEmit` crashes** (TypeScript 5.9.2, "Debug Failure. No error for last
      overload signature") on a clean tree, so **every frontend type error in every module is
      invisible**. Slices are checked against scoped tsconfigs meanwhile. Nobody has found the
      offending file; cross-module defect #20.

---

## Definition of done for the HR module

The module is finished, excluding the `docs/HR/` programme, when all seven hold:

1. The coverage queue reads **0 BUILD** and every `INTENTIONAL` carries a reason in section D2.
2. Section E is classified field by field and every `BUILD` field is settable.
3. Section F is closed — every row built, or `DECIDE`d with an answer written back.
4. Every deferred residue is either built or explicitly owned by someone outside HR, with a trigger.
5. ✅ The org-authority question has an answer and the module's behaviour matches it (lane 7, 2026-08-31). Remaining: TDC populates the real hierarchy in place of the seeded test data.
6. Every money event posts through `IFinancePostingEngine` with contract tests, or carries a
   recorded decision not to.
7. The hand-offs in lane 9 are filed and acknowledged by their owners.
