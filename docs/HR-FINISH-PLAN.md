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
| **3** | Employee Master feedback block | ✅ **3a · 3a-ii · 3b · 3c · 3d’s buildable rows all done** | — | — (1 row needs TDC) |
| **4** | Leave · Training · Succession · Recruitment feedback | 15 | 2 slices | partly lane 2 |
| **5** | Section E — 5a ✅; **5b 47 of 49 fields built** | 2 fields | — | **blocked on D-13** |
| **6** | Deferred area residues | 13 | 2 slices | 4 are blocked outside HR |
| **7** | Org-authority model | ✅ built · 4 residues | — | — |
| **8** | HR ↔ Finance GL posting sweep | 7 prerequisites + the adapters | 5+ slices | lane 2, Finance owner, Payroll owner |
| **9** | Hand-offs — real, but not HR's to fix | 3 | file and acknowledge | other owners |

**Buildable by this team: roughly 9–10 slices.** Unblocked today: **3b, 3a-ii, lane 6** (and lane 4). Lanes 0, 1, 7, 2a, 3a, 3c, 5a, 5b and 3d’s schema-free rows are done.

### Critical path

**Where to start next (as of 2026-09-01, end of the section-E stretch).** Done: lanes 0, 1, 7, lane
2a's six settings, lane 3's 3a / 3c / 3d-buildable rows, and **all of lane 5** that is not blocked on
D-13 (47 of 49 fields). The coverage queue reads **0 BUILD**.

▶ **Lane 4 is DONE (2026-09-01): 54 + 32 + 33 assertions, each twice** (see § Lane 4 for the
row-by-row verification). Lane 6's buildable rows are closed — two of its
seven were stale, one was verified not live, and the four real ones are built. Lanes 3b and
**3a-ii** are closed — all four caller-supplied image paths are gone, and the seal and signature are
versioned instruments rather than settings fields.

⚠ **FOUND 2026-09-01 by lane 4's harness, and it is the lane's real finding: two Employee-FK actor
columns were being fed the LOGIN's user id by the screens.** `LeaveAdjustment.PerformedBy` and
`LeavePlan.PlannedBy` are required foreign keys to `Employees`. The adjustments screen, the plans
screen and the self-service planner all sent `user.id` — a value that is never an employee id — so
no adjustment or plan raised from any screen had ever been saved (both creates 500 on the old
build; `LeaveAdjustments` was empty). A third writer, the year-end forfeiture, posted `Guid.Empty`
into the same column, so that endpoint had never succeeded either. All three now stamp the actor
from the token; an unlinked account is refused with a message. **The lesson generalises the
discipline one: "the token's id reaching the service is not the actor being stored" — here the
actor never reached the service at all, and nothing static could see it because the write compiled,
the payload validated, and only the database said no.**

▶ **After lane 4 closes, the next unblocked work is lane 8a's prerequisites or the remaining 3d /
2c rows; lane 2's memo still gates the money decisions.**

⚠ **FOUND 2026-09-01 while running regression, NOT part of lane 3b:
`GET /api/JobAnalysis/descriptions/{id}/details` returned 500 for every one of the tenant's 46 job
descriptions** — a 30-second SQL command timeout. Thirteen sibling collections hang off a job
description and the read materialised their CARTESIAN PRODUCT in one query. Nothing in the code had
changed; the data grew into it, which is exactly why area 17/18 could close with its own suite green.
Fixed with `.AsSplitQuery()`, the house remedy already used 109 times in this codebase.
**The lesson: a suite that passed at closure does not stay passed — this class of defect arrives
with row counts, and nothing in the endpoint audit or the coverage instruments can see it coming.**

_(previous note)_ ▶ **finish lane 3b's UI.** Its whole backend landed 2026-09-01 and the expiry
sweep has its screen, but **five backend surfaces still have no caller**: the certifying-body and
qualification-level lookup screens, the two pickers that would use them, the staff-number settings
screen, and an import endpoint for `AcceptImportedAsync`.

⚠ The import gap is the one with teeth. `StaffNumberFormat` now issues numbers from
`INumberSequenceService`, and `AcceptImportedAsync` — which advances the counter past numbers
loaded from outside — **exists and is called by nothing**. Load TDC's real staff numbers before
that endpoint exists and the counter starts at 1 and re-issues numbers already in use. That is a
data-corruption hazard sitting behind a screen nobody has built yet, not a polish item.

**Pick one of these; all are unblocked and none needs TDC:**

| Next | Why | Needs a build? |
| --- | --- | --- |
| ~~**Lane 5b**~~ — section E is **as done as it can be** | **47 of 49 fields.** Criteria 34 ×2, class 3 **69 ×2**, visa 41 ×2. The last 2 (`BenefitTierId`) need an employee-policy editor, which is **D-13 — a deferral by decision, not a gap**. Nothing here is buildable without reopening that. → pick **3b**, **3d**, **3a-ii** or **lane 6** next. | — |
| ~~**Lane 3b**~~ — reference-data dimensions · ✅ **COMPLETE 2026-09-01** | All three dimensions and both pickers. 39 ×2 + 49 ×2 + 40 ×2. _(was: two of three closed)_ ** ID-type lead days + the expiry sweep and its screen ✅ (39 ×2); staff numbering — settings screen, counter panel and the import door — ✅ (49 ×2). **Owed: the certifying-body and qualification-level admin screens, and the two pickers that consume them** (a level picker on qualifications, a certifying-body picker on skills). Their backends and client methods already exist and have no caller. Both migrations applied. ⚠ One decision owed — see § 3b. | yes |
| ~~**Lane 3d**~~ — guards and pickers | ✅ **Both schema-free rows done 2026-09-01** (35 assertions ×2). What remains in 3d needs schema or TDC: the exit-interview question set, the labour-law checklist, and bulk benefit application (the excluded `docs/HR/` programme). | — |
| **Lane 6** — buildable residues | Travel's caller-supplied exchange rate onto Finance (`HrCurrencyBridge` now exists for exactly this), `getCasesForSource`, the portal feedback form. | some |

⚠ **Read `§ Standing rules` above before starting any of them** — they are the mistakes already paid
for once each, and this session repeated three of them anyway (see the note at the end of lane 3a).

~~Lane 0~~ ✅ → ~~lane 1~~ ✅ Lane 2's remaining half is a **memo, and it should still go out on day one** — but ⚠ **it does not gate lane 3**, which is what this line used to claim. Checked item by item on 2026-08-31: of lane 3's nineteen rows, **one** (the labour-law checklist) genuinely needs TDC before it can be built, three want TDC's *data* to fill a mechanism that can be built now, and fifteen need nothing at all. What the memo does gate is lane 8's money decisions and the four `DECIDE` rows. It no longer gates lane 7, which is built.

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

### 3a — Employee record fields · ✅ **DONE 2026-09-01** · 47 assertions ×2

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

**Done 2026-09-01.** Backend, screens and harness (`hr-employee-docs/run-lane3a.mjs`, 47
assertions, green twice; lane 3c re-run 47/47, no regression). Screens: the three new employee
fields, the guarantor surety + currency, the position's guarantor requirement, the compliance strip,
the expatriate permits, and a family panel behind a per-row action.

⚠ **Two decisions the user made, and the assertions that hold them.** Disability is DUPLICATED on
the employee, not moved from the dependant — the harness records the employee's flag as true and a
dependant created afterwards as false, so a later "tidy-up" that merges them fails. And the
guarantor requirement lives on the POSITION: requires 75,000, holds 50,000 → shortfall 25,000; add a
second guarantor at 25,000 → satisfied. **Sum, not maximum** — two guarantors at half each satisfy a
surety between them, which is what co-signing means.

⚠ **`api/hr/currencies` exists because of this slice.** `api/finance/currencies` is mapped to
`FinancePermissions.ViewFinance` by `FinancePermissionPolicyMap` — a convention map, invisible on
the controller — so an HR user gets a **403** and every HR currency picker fed from it renders
EMPTY. Found by the harness. Both new pickers had it, and **area 13's `DevelopmentPanel` has had it
since it shipped**, on top of reading `c.code` off Finance's DTO behind an `any` cast and rendering
"undefined — undefined". Granting HR `ViewFinance` would have opened ledgers and payments to
populate a dropdown; the new endpoint is a read-only `{code, name, symbol}` projection instead.

**Original probe notes.** 39 columns, one new table, migration
`20260901013629_AddExtraEmployeeMasterFields`. Verified live: `/details` carries the new fields, a
guarantor round-trips with `amountGuaranteedCurrencyCode`, an unknown currency (`XYZ`) is **refused
with 400 by Finance's master**, and the compliance read answers the guarantor question — required
75,000, held 50,000, **shortfall 25,000, not satisfied**.

⚠ **Two things this slice turned up that are not in the six rows.**

1. **`GET api/hr/Employees/{id}` is a SUMMARY** (`GetEmployeeSummaryByIdAsync` → `EmployeeDto`); the
   record is at `/{id}/details`. None of the new fields appear on the summary and none should. **The
   employee form must read `/details`** — binding it to the summary would render every new field
   blank and blank them on save, which is D-09/D-12 one layer further out.
2. ⚠ **A string field on `UpdateEmployeeDto` cannot be CLEARED.** The mapper's pre-existing idiom is
   `if (dto.X != null) e.X = dto.X`, so null means "not supplied" — sending `hometown: null` leaves
   the old value. Found by trying to revert a probe: `hasDisability` cleared (a bool is always
   written) and `hometown` did not. This is the ESTABLISHED convention for `Religion`, `Address` and
   the rest, so the new fields inherit it rather than diverge — but it means a typo in a hometown
   can be corrected and never removed. Changing it is a behavioural change across the whole employee
   update path and deserves its own deliberate slice, not a quiet exception for two fields.

### 3a-ii — The remaining caller-supplied image paths · **NEW, recorded 2026-09-01**

Found while gating the employee, dependant and guarantor photographs. Kept OUT of 3a deliberately:
they belong to other areas, and one of them deserves its own thinking rather than being swept in.

- [ ] **`ExternalAssociate.PicturePath`** — non-nullable, so a location is *expected* to be filled.
      <br>✅ **DONE 2026-09-01.** `PhotoFileUploadRecordId` (one column, no DMS — the JobCandidate
      avatar precedent, not the Employee personnel one), `POST|GET api/external-associates/{id}/photo`
      on its own controller, and the DTO sink closed. **The last of the four.**
- [x] **`JobCandidate.ProfilePhotoUrl`** — ✅ **DONE 2026-09-01.** Removed from
      `UpdateCandidatePortalProfileDto` and from `MapDtoToCandidate`; asserted in
      `hr-recruitment/slice-f` (**69**, was 66).
      <br>⚠ **The sharpest of the four: an EXTERNAL candidate wrote it**, and every profile save
      undid `UpdateProfilePhotoAsync`'s own cleanup — that method deliberately nulls the legacy URL
      because a gated photo has no public URL. Never served as a file (both downloads pass
      `legacyPath: null`), so a stored attacker-controlled string reaching HR's screens, not file
      read.
- [x] ⚠ **NOT IN THE ORIGINAL LIST, and the one that mattered most: `CreateEmployeeDto`,
      `UpdateEmployeeDto` and both dependant write DTOs still accepted `PicturePath`** — lane 3a's
      own comment claimed "nothing new writes it" and that was false. `DownloadEmployeePhoto` passes
      the stored value to the file server as `legacyPath`, so a caller could choose which file an
      employee's photo resolved to. ✅ **DONE 2026-09-01 · 11 assertions ×2**
      (`hr-probation/run-lane3aii.mjs`).
      <br>⚠ **State it accurately:** `IsServeableLegacyPath` rejects rooted paths and `..` and
      requires an allow-listed legacy root, so this was **never arbitrary file read** — it was a
      caller picking a file *within* those roots. Bounded, real, and not what the field is for.
      <br>⚠ `CreateEmployeeDependentDto.PicturePath` is **not** a sink: `IEmployeeDependentService`
      is implemented by nothing and injected nowhere. Dead interface, left alone.
- [x] ✅ **`CompanyProfile.SignatureImageUrl` and `CompanySealImageUrl` — DONE 2026-09-01 ·
      30 assertions ×2** (`hr-probation/run-lane3aii-seal.mjs`). New `CompanySealAssets` table,
      migration `HrImagePathGovernance`.
      <br>**USER DECISION: build it as recommended.** Replacing one is gated on `AdministerCompany`
      — **no new permission seeded**: that tier already excludes the HR role and is already described
      as holding the settings that move trust boundaries, which is exactly what a seal is. HR edits
      the letterhead; HR cannot replace the seal.
      <br>**Versioned, never overwritten.** Currency is derived from `RetiredOn is null`, close-then-
      insert, the `EmployeeSalaryAssignment` idiom — deliberately no `IsCurrent` flag, and no unique
      index on "one open row per kind" because the delete is soft. Withdraw-without-replace is a
      first-class action: a compromised seal must stop being used before a replacement exists.
      <br>⚠ **Sharper than this row assumed.** They were not merely stored — they were substituted
      as tokens into rendered offer and probation letters, so an arbitrary value became an image
      source in a document sent to a candidate.
      <br>⚠ **The gap that would have made the whole feature invisible:** after the suite was green,
      **no template referenced either token**. A tenant could upload a seal, see it "in force", and
      never see it on a document. Both built-in templates now render them (hidden when nothing is in
      force) and both tokens are declared in the catalogues. **A capability with no reader is the
      same defect as a client method with no caller — one level further out.**
      <br>⚠ **No public URL, by design.** A letter embeds the bytes as a data URI, and
      `GetCurrentAsDataUriAsync` refuses anything without a `Clean` scan verdict. Verified end to
      end: a rendered confirmation letter contains `data:image`.
      <br>_(the original note)_ **the one to think about before touching.** A company seal is what stamps a document as authentic; a caller-supplied
      path to one is not merely the usual sink, it is a path to an instrument of authority. Who may
      replace the seal, whether a change is audited, and whether the old image survives are product
      questions, not a gating exercise.

The pattern to copy is `EmployeeDocumentsController`'s photo endpoints: gate on the way in, three
DMS ids on the row, a token-bearing download that falls back to the legacy path so ported images
are not lost.

### 3b — Reference data that should be a dimension (one migration + admin screens)

Two migrations, not one: the dimensions (`HrReferenceDataDimensions`) and the sweep's run/dispatch
pair (`HrIdentificationExpirySweep`). Both are applied.

- [x] `IdentificationType` has no expiry notification lead days — ✅ **DONE 2026-09-01 ·
      39 assertions ×2** (`hr-probation/run-lane3b-sweep.mjs`). The column, the sweep that reads it
      (`IdentificationExpiryReminderService`, two tiers, deduped per card per deadline), the run and
      dispatch tables, its own `IdentificationExpiryController`, and the screen at
      `/hr/employees/identification-expiry`. The lead-days input is on the identification-type form
      and the list carries a **"Never warns"** column.
      <br>⚠ **Tiers move OWNERSHIP, not volume.** Inside the window the holder is asked to renew
      their own document; once it lapses it becomes HR's compliance gap. One row either way — a
      second row addressed to HR at tier 1 would double the log and make "how many cards are
      expiring" ambiguous. `RoutedToEmployeeId` is an ownership stamp, **not a visibility switch**:
      the read is policy-gated, so HR sees both tiers throughout.
      <br>⚠ **The sweep shipped with no reader.** `runs` and `log` were added before the screen,
      because a preview plus a run button leaves the dispatch log unreadable — and "it ran and
      found nothing" then looks identical to "it never ran", which is precisely how lane 1 found
      two nightly sweeps that had **never once executed**.
      <br>⚠ **Self-inflicted, caught before the screen:** `EmployeeName`/`EmployeeNumber` were
      declared on the item DTO and set by nothing. Fifth instance of that shape in this module. The
      join is LEFT on purpose — a card outliving its employee row is the case HR most needs.
- [x] Certification bodies are free text (`EmployeeSkill.CertifyingBody`) — ✅ **DONE 2026-09-01.**
      Admin screen at `/administration/hr/certifying-bodies`, picker on the employee skills tab.
      <br>The free-text column **stays beside** the lookup rather than being replaced: existing rows
      are free text, and a genuinely one-off certifier does not deserve a catalogue row. The list
      shows the catalogued name and falls back to the text.
- [x] Qualification level is not a dimension — ✅ **DONE 2026-09-01.** Admin screen at
      `/administration/hr/qualification-levels`, picker on the qualification MASTER form (the level
      lives on `Qualification`, not on `EmployeeQualification`), and a Level column on the
      catalogue list so the value can be read as well as written.
      <br>⚠ `QualificationType` is a **category**, not a rank; the two coexist and neither
      substitutes for the other.
- [x] **Both dimensions were unreachable, which is the finding.** `Qualification.QualificationLevelId`
      and `EmployeeSkill.CertifyingBodyId` existed as columns with FKs, indexes and entity docs — and
      appeared on **no DTO at all**. Settable nowhere, readable nowhere. The lookups' own CRUD was
      complete, so an audit counting endpoints saw nothing wrong. **40 assertions ×2**
      (`hr-probation/run-lane3b-lookups.mjs`); sections B and D exist to prove the value survives a
      round trip through the records the pickers write to.
      <br>⚠ Both resolved names are asserted on **every** read that returns them, including the
      employee DETAIL as well as the dedicated skills list — an uneven `.Include` is how the same row
      shows a certifier on one screen and a blank on another. Four repository reads and two employee
      include chains were corrected.
      <br>⚠ `CertifyingBodyId` is applied **unconditionally** in `Apply`, unlike every neighbour.
      Those treat null as "not supplied", so none of them can ever be CLEARED — tolerable for a text
      box, a trap for a picker.
- [x] Staff number auto/manual is behaviour, not configuration — ✅ **DONE 2026-09-01 (backend +
      UI) · 49 assertions ×2** (`hr-probation/run-lane3b-staffnumbers.mjs`).
      The settings screen is at `/administration/hr/settings/staff-numbering` (HR Settings → Staff
      Numbering): per-register rules with a **server-composed** live preview, and a counter panel on
      every auto rule. `POST api/hr/Employees/import` is the import door, wired to a "this person
      already has a staff number" choice on the employee create form.
      <br>⚠ **`AcceptImportedAsync` advanced the counter by LOOPING `NextAsync`** — 8,000 round
      trips for TDC's register, and a **silent give-up** after 10,000 steps, on exactly the loads
      that need it most. Replaced by `INumberSequenceService.AdvanceToAtLeastAsync`, one guarded
      forward-only update, plus `PeekAsync` so a screen can read the counter without moving it.
      <br>⚠ **`StaffNumberFormat.TryReadSequence` is an exact inverse of `Compose`**, replacing a
      "take the trailing digits" reader. It makes the **year part of the match**: counters for a
      rule that prints the year are per-year buckets, so `EMP/25/0417` no longer pushes the 2026
      counter forward for no reason.
      <br>⚠ **The counter read scans by the SHAPE of the number, not by employment type.** A staff
      number records which register somebody ENTERED by, so a converted employee keeps a number
      their current type would never produce — partitioning by `Employee.EmploymentType` would both
      miss numbers the rule can reissue and count numbers it cannot.
      <br>⚠ **A defect found on the way, in `EmployeeRepository.EmployeeNumberExistsAsync`:** it
      excluded soft-deleted rows twice over while `IX_Employee_Tenant_EmployeeNumber` is unfiltered,
      so reusing a leaver's number passed validation and then **500'd in SaveChanges** with the
      index name as the only clue. Lane 3d fixed the generator's scan; this was the half left
      behind, and it is the half every manual register and every import relies on.
      <br>⚠ **`GetQueryable().IgnoreQueryFilters()` is a NO-OP** — `GenericRepository.GetQueryable()`
      bakes an explicit `Where(e => !e.IsDeleted)` into the query, and no filter switch removes a
      written predicate. The first cut of the counter read used it and silently under-counted
      tombstones, which is the exact failure the read exists to prevent. Use
      `GetQueryableIncludingDeleted(predicate)`. **Caught only by the two assertions that read the
      count** — the sibling assertion on the same fact passed, because the repository fix uses raw
      `_dbSet` where `IgnoreQueryFilters` does work.
      <br>⚠ **Lane 3d's section D was testing a retired mechanism** and had four red assertions: it
      created employees with a BLANK number and asserted the generator issued one, which stopped
      being the contract when the absence of a rule became manual entry. Rewritten onto the same
      hazard where it actually lives now — a tombstoned number under a manual register — rather than
      deleted. Count held at 35.
      <br>⚠ **LIVE: the DEFAULT tenant has NO numbering rule**, so every blank-number create is
      refused. `hr-recruitment/setup.mjs` and three sibling probes create employees without a
      number, which takes **all seven recruitment suites** down at setup. Confirmed by running one,
      not by grep. See the decision row below.
      <br>_(original entry)_ ✅ **backend DONE 2026-09-01.**
      `StaffNumberFormat` is a per-register table on `INumberSequenceService`, keyed on
      `AppliesToEmploymentType` with `null` as the tenant default. Built configurable because TDC's
      own register already numbers permanent and contract staff differently.
      <br>⚠ **There is no auto/manual mode flag, deliberately.** The ABSENCE of a rule IS manual.
      A flag plus a table would be two sources of truth for one question, and the module has met
      that shape before.
      <br>**Still owed:** the settings screen with its live preview, and an endpoint for
      `AcceptImportedAsync` — which exists and has no caller, so a data load cannot yet accept
      numbers as given and advance the counter past them. Until it does, loading real staff numbers
      would leave the counter re-issuing numbers already in use.
      <br>⚠ Five sub-questions went to TDC (`HR-OPEN-QUESTIONS-FOR-TDC.md`), one of them a
      counter-seeding hazard.
- [x] **RESOLVED 2026-09-01 — the tenant stays unconfigured; the four harness sites now pass explicit
      numbers.** `hr-recruitment/setup.mjs` (per-employee counter) plus the three sibling probes.
      Verified by running `run-lane5b.mjs` — **34/34**, matching its recorded count exactly. Seeding a
      dev-tenant rule was rejected because it would quietly pre-decide a question already put to TDC.
      <br>⚠ `hr-recruitment/run.mjs` now gets past employee creation and stops on a **separate,
      pre-existing** prerequisite it names itself: no published StaffRequisition workflow definition
      (`run publish-requisition-definition.mjs`). Unrelated to numbering, and left alone.
      <br>_(the decision as it stood)_ **does the DEFAULT tenant get a numbering rule?** A tenant that has configured
      nothing gets manual entry everywhere, which is the designed and correct default. But this
      tenant has configured nothing, so seven recruitment suites fail at setup and a fresh install
      cannot add an employee from the UI without typing a number. Either seed one default rule (one
      row, through the new screen) or pass explicit numbers at the four recruitment create sites.
      ⚠ Seeding one picks a format on TDC's behalf while **that is one of the five questions already
      put to them**, so a dev-tenant rule would quietly become the assumed answer.

### 3c — The document surface · ✅ **DONE 2026-09-01** · 47 assertions ×2

- [x] **Employee document attachments** — three tables (`EmployeeDocumentTypes`,
      `EmployeeDocuments`, `PositionDocumentRequirements`), a controller, the Documents tab on
      employee detail, and the `hr-employee-documents` category declared AND registered in
      `SystemCleanScanRequired` in one edit. Harness `hr-employee-docs/run-lane3c.mjs`, 47
      assertions, green twice. Both standing greps clean: 15 of 15 client methods have a screen
      caller, 10 of 10 write routes have a client method.
- [x] **Mandatory documents against a position** — the requirements panel on the position edit
      screen, plus the compliance read that makes it answerable. It **reports and does not block**:
      area 8's FR-HR-173 was built as a hard block exactly as specified and refused nearly every
      movement, and refusing an appointment over a missing document stops the transaction that gets
      somebody able to supply it.
- [ ] **No appointment letter templates** — ⚠ **deliberately NOT in this slice.** Templating is a
      different feature from document storage, and it is one of the three lane-3 items whose
      *content* is TDC's. Building the engine now means guessing what a TDC appointment letter says.

**Three things this slice established:**

1. ⚠ **The vocabulary had to be a TABLE, not an enum.** An employee HOLDS documents and a position
   REQUIRES them, so both must speak one language; every other HR document vocabulary in the
   codebase is a compiled enum precisely because nothing else reads it.
2. ⚠ **The paged employee list does not return `positionId`** (probed — it comes back undefined),
   while the compliance read resolves it. A requirements editor keyed off a list row's position
   would be keyed off nothing. The D-09/D-12 shape, one layer further out.
3. ⚠ **Two defects the harness caught that reading could not.** `AddRequirementAsync` used
   `GetQueryable()`, which filters soft-deleted rows — so the revive branch was unreachable and
   every re-add silently inserted a duplicate, and it did **not** throw, because the unique index is
   filtered and a tombstone does not occupy the slot. And every refusal on the controller was mute:
   `GlobalExceptionHandlingMiddleware` maps `InvalidOperationException` to a fixed string and
   DISCARDS the message, so three carefully-worded rules reached nobody until a `ToClientError`
   helper was added. **The code looked right in all four cases.**

### 3d — Guards, pickers and the rest

- [x] **Probation dates stay editable after confirmation** — ✅ **DONE 2026-09-01 · 35 assertions ×2**
      (`hr-probation/run-lane3d.mjs`). Guarded in `EmployeeService.RequireUnconfirmedProbationAsync`,
      keyed off **`Employee.ConfirmationDate`** — the field `ProbationService.MarkEmployeeConfirmed`
      actually writes. `EmployeeContractDetail.ConfirmationDate` is a separate hand-typed copy;
      guarding it against itself would let the real confirmation be contradicted.
      <br>⚠ **The row was understated.** The two probation fields were also **settable and readable
      nowhere** — `EmployeeContractDetailDto` carried neither, so the edit form hardcoded them blank
      and a probation term could be recorded and never seen again. That is very likely why nobody
      noticed it could also be rewritten. **Instrument 03 is blind to this class**: it scans
      Create/Update DTOs, not read ones.
      <br>Only the two probation fields are gated — A9/A10 assert a confirmed employee can still be
      given a pay rise and an hours change.
- [x] **Manager picker ignores `ReportsToPosition`** — ✅ **DONE 2026-09-01.** The form now offers the
      holders of the position this one reports to (**121 of 174 positions carry a reporting line**,
      and it already drives the organogram), and keeps the free search, since a manager is not
      always the post-holder. A vacant supervising post says so in words rather than rendering an
      empty list. The `positionId` filter was verified to actually filter before anything was built
      on it — a picker fed by a broken filter renders empty and says nothing.

- [x] **The eight contract fields that reached no DTO** — ✅ **classified and half built 2026-09-01.**
      **Four exposed** on all three DTOs, the mapper, the service and the form: `CurrencyCode` (a
      salary was shown as a bare number and assumed GHS — now gated through `HrCurrencyBridge`, the
      same Finance master 3a used), `WorkSchedule`, `SpecialConditions`, `Notes`.
      **Four NOT exposed** — twins of live fields. ⛔ **`EffectiveDate`, `ContractEndDate` and
      `IsCurrent` are RESERVED by decision 2026-09-01: contract versioning is coming and the columns
      are being held for it. DO NOT DROP.** The ledger carries the three obligations that keeping
      them creates — chiefly that `IsCurrent` is hardcoded `true` and maintained by nothing, so it
      is wrong on every terminated contract today. `AnnualLeaveEntitlementDays` is **not** covered
      by that decision and is still open.
      <br>⚠ **A defect fell out of it.** The form's default was `employmentType: 'FullTime'`, which
      is **not an `EmploymentType` member** — it belongs to `WorkArrangementType`, the very field
      that was on the entity and on no DTO. The dialog opened with a value its own select could not
      show and the API could not parse, so an untouched Add failed.

- [x] **⚠ Employee creation was broken by any prior delete** — found by a probe, not looked for.
      `GenerateEmployeeNumberAsync` scanned `BaseQuery()`, which filters `!IsDeleted`, while
      `IX_Employee_Tenant_EmployeeNumber` is **unfiltered** and still holds tombstoned numbers. So
      soft-deleting the newest employee handed their number straight back to the next create, which
      the index rejected — **as a 500 from SaveChanges, on the most ordinary path in HR**, for the
      rest of the calendar year. Deterministic, not a race. The scan now ignores query filters so it
      agrees with the index. ⚠ It is still not atomic; the durable fix is `INumberSequenceService`,
      which is **lane 3b's** work.
      <br>✅ **Verified.** Assertions D1–D4 create with a generated number, soft-delete, then
      create again. They were **red against the unfixed build** — four failures naming the cause
      — and are green after it. ⚠ The **second** run is what proves it: it inherits the first
      run's tombstones, which is exactly the condition that broke it.
      <br>⚠ **The suite was green while this defect was live**, because every other create in it
      passes an explicit `employeeNumber` and never reaches the generator. A passing suite tests
      only what it exercises.
- [ ] Exit interview questions are fixed fields, not a configurable question set, and carry no
      attachments.
- [ ] No labour-law checklist.
- [ ] No mass application of benefits to dependents. ⚠ This is a **bulk operation**, which is the
      excluded `docs/HR/` programme — build the single-record path here and record the bulk need
      there rather than inventing a second bulk pattern.

**Done when:** each slice's harness is green twice, the two greps return no uncalled service method,
and the section F rows are ticked in the ledger with the commit that closed them.

---

## Lane 4 — Leave · Training · Succession · Recruitment feedback · ✅ **DONE 2026-09-01** · 54 + 32 + 33 assertions ×2

Thirteen feedback rows plus the four manpower-budget endpoints. **Verified row by row before
building, per the standing rule: 10 as described, 1 understated-then-wrong, 2 STALE.** Harness:
`dev-harness/hr-finish-lane4/` (three scripts, README there). Before-evidence against the old
build: adjustment create **500**, plan create **500**, completion verify passed with no certificate.
Green twice against the rebuilt API: leave **54**, training **32**, succession **33** (read-only).

⚠ **Source caveat.** The "five pre-port feedback documents" the ledger's section F cites are not
on disk under `D:\Rhema\TDC ERPS` (scanned every .docx/.md/.txt/.pdf there); the rows were
introduced in b08b577d without a citation. Two rows needed an interpretation, stated below as
assumptions — confirm them with TDC at the next demo.

### Leave

- [x] **Adjustment form shows the employee's balance** — a `BalancePreview` inside the form reads
      `GET employee/{id}/balances` for the chosen employee/type/year (the row the Balances screen
      reads) and previews the balance after the signed `days`. ⚠ **While building it: the form sent
      `performedBy: user.id`.** `PerformedBy` is a required Employee FK — see the finding above.
      Now stamped from the token on both create paths (`AddAdjustmentAsync` and the standalone
      one — the half-fix shape checked), DTO property removed, forfeiture writer fixed too.
- [x] **Reliever clashes are visible on the plan** — `LeavePlanDto.RelieverClashes` from three
      sources (the reliever's own plan, their own live leave request, another plan in the window
      naming them), on every plan read, batched per list. `GET hr/leave-plans/reliever-clashes`
      answers the form before the plan exists; the register shows a red badge with the reasons.
      Advisory, not a gate. Second reliever now editable on the desk form (it was silently nulled
      on every edit). `PlannedBy` had the same user-id defect; stamped from the token, kept on edit.
- [x] Free-text field labelled **"Remarks"** — the adjustment form, its column and its search
      placeholder (the entity's own remark said this was the intent).
- [x] **Leave request numbering on `INumberSequenceService`** (`LEAVE-REQ`, year-bucketed,
      `LV{year}{seq:D6}` preserved). Seeded once per tenant-year from the table's highest number
      **including deleted rows**, via `AdvanceToAtLeastAsync`. Harness asserts create → SQL
      soft-delete → create issues N+1 (no endpoint deletes a leave request). The old scan re-issued a
      deleted row's number and the retry loop recomputed the same collision.

### Training

- [x] **Bulk nomination dialog** — ⚠ the row was understated in one direction and wrong in the
      other: `bulkCreate` (client) and `POST training-nominations/bulk` existed; only the action
      was missing. "Nominate several": multi-employee badge picker, one type, one justification,
      an availability check across the batch, result rendered by the panel that had been waiting.
      ⚠ Seats are taken at APPROVAL, not at nomination, on both paths — the dialog says so.
- [x] ~~Bulk completion~~ / ~~Nominee availability check~~ — struck 2026-08-31, false.
- [x] **"Training Activities" grouped screen** — _assumption_: the desk twin of My Training. One
      employee's nominations, requests, completions, certificates and mandatory compliance on one
      screen (`/hr/training/activities`), every read a per-employee endpoint that already existed.
      Nav entry under Training, card on the Training landing.
- [x] **Mentoring is its own nav section** — top-level "Mentoring" (pairs + programmes), removed
      from the Training children; routes unchanged; card on the HR landing.
- [x] **Certificate gates completion** — _assumption_: a PASSED completion of a programme flagged
      `ProvidesCertificate` (the per-programme flag, which nothing had ever read) cannot be
      VERIFIED until an active certificate exists for the nomination; a failed completion and a
      programme without a certificate are not held; a revoked certificate does not count. The
      verify dialog says so up front (`programProvidesCertificate` on the DTO). Refusal surfaces as
      422 via the exception middleware, like "already verified".

### Succession

- [x] **Criteria candidate search screen** — `CandidateSearchPanel`, hosted standalone at
      `/hr/succession/candidate-search` (nav: Find Candidates) and inside a plan's Candidates tab
      locked to the plan's post and excluding its candidates; a result row pre-fills the nominate
      form. Payloads and shape probed first (33 assertions, all green on the current build).
- [x] **Candidate age and service-years-left displayed** — two columns on the plan's candidate
      table; the by-plan read already carried both.

### Recruitment / manpower budgets

- [x] Menu reads **"Manpower Recruitment Budgets"** — sidebar, HR landing, list and new-page titles.
- [x] ~~`PUT`/`DELETE api/JobAnalysis/budgets/{}` have no caller~~ — **STALE, struck 2026-09-01.**
      Wired in 2efd553c: the detail page calls `updateBudget` and `deleteBudget`; the ledger's own
      C2 checklist had ticked both.
- [x] ~~`PUT`/`DELETE api/JobAnalysis/lines/{}` have no caller~~ — **STALE, struck 2026-09-01.**
      Same commit: `updateBudgetLine` and `deleteBudgetLine` on the detail page.

**Done when:** the three harnesses are green twice against the rebuilt API, the regression trio
(`w3 slice5-leave`, `portal slice4`, `training run3`) still passes, the counts are recorded here
and in the harness README, and the two assumptions are put to TDC.

---

## Lane 5 — Section E · 5a ✅ · 5b: 47 of 49 fields built · 2 left (D-13)

**69 fields across 27 DTOs.** These endpoints *are* wired; the form omits fields, so the feature is
degraded rather than missing — the class an endpoint audit cannot see.

### 5a — The classification pass · ✅ **DONE 2026-09-01** · 49 `BUILD` · 20 `INTENTIONAL`

### 5b — The screens · **47 of 49 fields done 2026-09-01** (criteria + class 3 + visa + NHIS)

- [x] **Classification pass first, endpoint by endpoint.** **Done** — all 69 fields read against
      their mapper, their service and their screen; verdicts and reasons in
      [`HR-CLOSURE-LEDGER.md` § E2](HR-CLOSURE-LEDGER.md). Section D's lesson held at almost the
      same rate: **20 of 69 (29%) are correctly omitted**, and an input for any of them would have
      done nothing or fought a transition. No build, no migration, nothing owed by the user.

⚠ **Four rows below changed because of it. Read § E2 before building any of them.**

- [x] ~~`UpdateAppraisalHRReviewDto` — HR can adjust a score and cannot say why. Highest severity.~~
      **Wrong, in the other direction.** `Create`/`UpdateAppraisalHRReviewDto` reach **no
      controller at all** — their only consumers are two mapper methods nothing calls. HR cannot
      adjust the score at sign-off *at all*; `ApproveAppraisalDto` carries only `HRRemarks` and
      `ApproveAndFinalizeAsync` recalculates. The live adjustment path is the appeal
      (`ResolveAppealDto.AdjustedScore` → `PerformanceAppraisal.AdjustedScore`, reason in
      `ResolutionNotes`), which is arguably the correct design. **6 of the 8 fields are
      `INTENTIONAL`** (a transition stamps them); the other two are dead columns. → **a question
      for the user, not a build.**
- [x] **`Create`/`UpdateJobShortlistingCriteriaDto`** — ✅ **DONE 2026-09-01 · 34 assertions ×2**
      (`dev-harness/hr-recruitment/run-lane5b.mjs`). Every claim in the classification was probed
      against the live API first, and all four held: the panel's exact payload **400s** on
      `weight: null` into a non-nullable `int`; with a weight it stores `"type": 0, "typeName": "0"`
      and reaches the scoring switch's `default:` arm; `minimumScore`/`displayOrder` are discarded;
      and the full payload round-trips.
      <br>**Two backend defects the probe found that reading had not.** `ToEntity` never assigned
      `ComparisonOperator` — `ToDto` returned it and `UpdateEntity` assigned it, so a criterion
      created with an operator came back with none and the numeric arm fell to its `?? Between`
      default. And `RequireScorableCriterion` now refuses an undefined type on **both** create and
      update, because a screen-only fix leaves the hole open to every other caller; the controller
      catches it, since `GlobalExceptionHandlingMiddleware` discards `InvalidOperationException`
      messages.
      <br>**Frontend**: the type rewritten from probed payloads with the four real enums, the panel
      rebuilt around what each criterion type actually uses in the engine (read off
      `EvaluateCriterion`, not guessed), both catalogue pickers **probed HR-readable first** — 26
      skills, 186 qualifications — an edit path it never had, and a banner on any untyped row.
      <br>⚠ **A third finding, from the probe rather than the plan**: DELETE is on
      `RecruitmentAdminPolicy` while POST and PUT beside it are on Write, so the Remove button
      answered an HR user 403 with a toast explaining nothing. The panel now takes a separate
      `canRemove`.
      <br>⚠ **Blast radius: nil, and that is the tell.** Ten vacancies, **zero criteria** in the
      whole tenant — nobody had ever created one, because the Add button 400'd whenever the weight
      was left blank. The banner is defensive, not remedial. A feature with no rows is not
      evidence that it works.
      <br>⚠ `GET {id}/criteria/mandatory` stays without a client method: it is a filtered subset of
      the full read the panel already makes, which returns `isMandatory` on every row. Not a gap.
- [x] **`Create`/`UpdateStaffTravelVisaRequirementDto`** — ✅ **DONE 2026-09-01 · 41 assertions ×2**
      (`hr-travel/run-lane5b-visa.mjs`, **41 assertions**). Register screen built at `/hr/travel/visa-requirements` and
      linked from the sidebar; TS interface rewritten from a live response (six of nine declared
      fields existed on neither the entity nor either DTO).
      <br>⚠ **Three backend defects the probe found that reading had not.** A country pair could be
      recorded **twice with contradictory answers** — Ghana → UK as both `EmbassyVisa` and
      `VisaFree`, while `GetRequirementAsync` picks one with `FirstOrDefaultAsync`, so a traveller
      could be told no visa is needed by a register that also says an embassy visa is. Both writes
      returned **null country names** (fresh entity, no navigation loaded) while the lookup beside
      them resolved them. And the by-destination list had an **uneven `.Include`**, naming the
      passport and not the destination.
      <br>⚠ The uniqueness guard is in the SERVICE, not a unique index: the delete is soft, and this
      module has met "a soft delete does not release a unique index" nine times. Retire-then-re-add
      is asserted.
      <br>⚠ `getVisaRequirements()` called a two-parameter lookup route with **no parameters** and
      typed the result as an array — it returned `null` cast as `[]` on every call, and had no
      screen caller, which is why nothing noticed.
      <br>⚠ **The register is consulted, not just fillable.** The pair lookup was first left unwired
      behind a written disposition; that was wrong, because **reference data nobody reads does not
      get maintained** — a register with a data-entry screen and no reader is this lane's own failure
      one step along. `TravelCompliancePanel` now answers "what does this traveller's passport need
      here?" on the Visas card. The passport country is the issuing country of their Passport travel
      document, not a field on the employee; and ⚠ the travel-request LIST carries
      `destinationCountryName` but **not** `destinationCountryId`, so the panel is fed the detail
      record — the D-09/D-12 shape, met for the third time in this lane.
- [x] **Class 3 — ✅ DONE 2026-09-01 · 40 assertions ×2** (`hr-medical/run-lane5b-class3.mjs`).
      Provider (6), plan (8), facility (4 + the three more of an accreditation block the instrument
      could not see), clearance template (2 + the four more the add strip never sent). No schema, no
      backend change.
      <br>⚠ **The count was wrong and it was mine: 20, not 24.** `BenefitTierId` (×2) and
      `LinkedMedicalClaimId` (×2) are **not** "a live screen omits an input" — neither write has a
      form. HR has no employee-policy editor at all (**D-13**), and the NHIS screen never calls its
      own `create` client method. I checked that a *page* existed for each and stopped there; a
      screen that READS a collection is not one that WRITES it. Both pairs moved to class 4.
      <br>⚠ **Three TS unions were short of their C# enum** — `MedicalInsuranceProviderType` missing
      `Other`, `HealthFacilityType` missing `MentalHealthFacility` and `Other`. A row stored as any
      of them failed the form's own schema and **could not be edited at all**.
      <br>⚠ **A summary list behind an edit dialog blanks optional fields**, and this slice was about
      to add three. `ResourceCollectionTab` now takes an opt-in `loadForEdit`; an assertion holds the
      thin projection in place so the reason stays visible.
      <br>⚠ **The premium split was under-fixed first time.** 90/90 was accepted with a 201, and
      that was recorded as "a policy question". Only half of it was: *must a split total exactly
      100* is policy (a third party can fund the balance); *may it total MORE than 100* is
      arithmetic. `RequireCoherentContributionSplit` now refuses the over-100 case in the service,
      on create and update, with the exact-100 boundary asserted.

- [x] **The NHIS claim create form — ✅ DONE 2026-09-01**, folded into the class-3 suite (sections E
      and F). It was misfiled as a missing *field*; the screen had **no create form at all** and
      never called its own `create`, so `GetByLinkedMedicalClaimIdAsync` could only return empty.
      <br>⚠ **Building the edit path found `UpdateClaimAsync` had no state guard** — a claim already
      submitted, approved or paid could have its amounts and service date rewritten. The D-03 shape.
      Draft and Rejected stay editable; everything past Submitted does not.
      <br>⚠ **Then the two greps found the lifecycle had no middle.** `updateStatus` had **no screen
      caller**, so nothing could leave `Submitted`, and "Record payment" — offered only on Approved
      — was UI on a branch nothing could reach. **All five NHIS claims in the tenant were `Draft`**:
      the same tell as the shortlisting criteria, where an empty table meant *never worked* rather
      than *unused*. F1–F6 now walk Draft → Submitted → PartiallyApproved → Paid.
- [x] ~~The remaining 21 DTOs — 1 or 2 fields each.~~ **Classified.** 20 are `INTENTIONAL` and close
      here: a transition owns 12 (`ReviewedByHRId`, `ScoreAdjusted`, `IsAuthoritative`,
      `LastPromotionDate`, `TrophyIssued`…), and 8 cannot be persisted or are superseded —
      `PublishToIntranet` has no column, `IsStudentDependent`/`IsEmergencyContact` are DTO-only,
      `RevisedScore` is written and read by nothing, and `CreateSectionDto.SectionHeadId` belongs to
      `ISectionService`, **which no class implements**.

**Done when:** every field is either settable or carries a recorded reason, and instrument 03
re-runs clean against the classification. ⚠ 03 will keep reporting the 20 `INTENTIONAL` fields — it
matches identifiers, not intent. § E2 is the answer to it, not a number that will fall to zero.

**Two residues 5a leaves behind, neither of them lane 5's:**

- `BMIRecorded` should be **computed on save** from `heightCm`/`weightKg`, not asked for. A stored
  BMI that disagrees with the height and weight beside it is a defect waiting.
- `IsStudentDependent` is a real product gap — student status is how `MaxChildAge` dependant
  eligibility actually works — but it needs a column before it needs a form. Recorded in section F.

---

## Lane 6 — Deferred area residues · 2 slices · partly blocked outside HR

Each was a deliberate deferral with a trigger, not an oversight.

### Buildable now

- [x] **9c — `getCasesForSource` has no screen** — ✅ **DONE 2026-09-01.** One shared
      `LinkedErCasesPanel`, mounted above the tabs on the three HR-desk detail screens (SHE incident,
      PIP, disciplinary case). Deliberately NOT on `/me/...`: the read is ER-permission gated, and
      the subject of a disciplinary case must not be shown the grievances filed about it. A 403 for
      a user without the ER permission renders nothing rather than an error. The client method now
      has a caller (was 0). The safety detail page carries 2 pre-existing lint errors far from the
      mount, verified identical with the change stashed.
- [x] **9c — `probe-slice10b.mjs` files a real concern on every run** — ✅ **DONE 2026-09-01.**
      There is no delete or withdraw endpoint on a concern, by design (a concern raised is a record),
      so the write is now opt-in behind `PROBE_WRITE_CONCERN=1`. Proven: inbox count 0 before and
      after a run.
- [x] **25 — portal feedback form** — ✅ **DONE 2026-09-01 · 20 assertions ×2** (`hr-orientation/run-lane6-feedback.mjs`).
      ⚠ **The row was half stale and half worse than stated.** The *training* half was already
      closed in area 25 (dedupe + `feedback/mine` + the `/me/learning` render all exist). The
      *orientation* half had a FORM already — whose own copy said "sending again adds another
      response" — and nothing behind it: `SubmittedByEmployeeId` came from the PAYLOAD (anyone could
      file feedback in another name); nothing deduped; no own-feedback read; and **`IsAnonymous` hid
      nothing** — the read handed the submitter id to every caller who could reach the enrollment,
      so HR saw exactly who had ticked "anonymous". Now: submitter stamped from the token, one
      response per enrollment per person (409 with its own sentence), `GET employee-orientations/
      feedback/mine`, names withheld on anonymous rows from everyone but the author, and the form
      shows what was filed instead of offering itself again, with a real "send anonymously" choice.
- [x] **25 — `appraisalNotificationService` left with no consumers** — **STALE ROW, verified
      2026-09-01.** The frontend client was deleted in area 25 slice 14 (`appraisal.service.ts:576`
      records it), its reads folded into `me-portal.service`. The backend
      `IAppraisalNotificationService` has three consumers. Nothing to do.
- [x] **12 — the travel exchange rate is still caller-supplied** — ✅ **DONE 2026-09-01 ·
      `hr-travel/run-slice4.mjs` 37 ×2 (was 30), slice 6 23.**
      ⚠ **Half stale, and the half that was true was worse than the row said.** On the CREATE path
      the rate was already Finance's (`HrCurrencyBridge`) — only the XML doc still claimed otherwise.
      But **`UpdateClaimLineAsync` never called the same valuation**: `UpdateEntity` wrote
      `ExchangeRate` AND `AmountBaseCurrency` straight from the payload, and `RecomputeClaimTotalsAsync`
      summed the result — so a line could be added at the published rate and then EDITED to any rate
      and any base amount, and the claim total followed. The same half-fix shape as
      `EmployeeNumberExistsAsync`: fixed where it was noticed, not on every path that writes the
      field. Both derived fields are now gone from the write DTOs and derived on both paths.
- [ ] **D-02 — self-service invitation response is act-as-anyone** — **VERIFIED NOT LIVE
      2026-09-01, trigger kept.** The only caller of `participants/respond` is the HR-desk screen
      (`/hr/company-schedule/events/[id]`), and no `/me` events or invitations surface exists
      (`me-portal.service` has none). Correct for its one consumer; the self-or-permission check is
      still owed the day a `/me` calendar ships.
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

⚠ **A suite can DIE at a gate and keep its recorded count.** `hr-orientation/run.mjs` §5 deleted a
programme as the HR actor; delete became `HR.Orientation.Admin` in W3, so the 403 was uncaught and
the suite has died there on every run since — sections 5–8 had not executed for weeks while the
README still said 107. Found 2026-09-01 running it as lane 6 regression; the delete now runs as
admin and the suite is back to **107/107**. **Re-run old suites; a count in a README is a claim.**

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
