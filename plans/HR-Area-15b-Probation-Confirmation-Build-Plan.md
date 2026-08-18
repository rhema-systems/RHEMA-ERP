# HR Area 15b — Probation & Confirmation: Build Plan

**Started 2026-08-18.** Numbered **15b**, not 14: this area's requirements live in FRD
§A1.4 *"Onboarding, Probation & Confirmation"*, whose onboarding half shipped as area 15
(closed 2026-08-13). `14` stays reserved for Awards & Recognition. Probation is the part of
A1.4 that area 15 did not build.

---

## 1. How to use this document

Section 3 is measured ground truth, not intention — every claim there was checked against the
source or the database on 2026-08-18 and is cited. Section 5 holds decisions that need the user.
Section 8 is the slice plan; section 9 is the log, appended as slices land. Read 3 before
touching code: this area is **not greenfield, and not a resurrection either** — it is a live
skeleton with three writers and no readers.

---

## 2. Status at a glance

| | |
|---|---|
| Backend surface | `ProbationController`, 18 endpoints, `api/probations` |
| Service | `ProbationService` (357 lines), 3 repositories, 3 entities |
| Frontend | **none** — zero references to `api/probations` anywhere in `frontend/src` |
| Gate | bare `[Authorize]` — any authenticated user |
| Harness | none |
| Data | **0 probation rows**, 0 reviews, 0 extensions |
| FRD backing | **18 mentions**, 3 requirements, all priority **M**: FR-HR-031, FR-HR-032, FR-HR-140 |
| Also owed from §A1.4 | FR-HR-030 (oath of secrecy) — zero implementation anywhere |

The FRD is unusually specific here, which is rare and worth exploiting:

- **FR-HR-031** — probation is **6 months senior / 3 months junior**, applied *by staff category*.
- **FR-HR-032** — **within the 5th month** of a 6-month probation, route a probation form to the
  **head** to confirm retention, and **generate a confirmation letter**.
- **FR-HR-140** — notify **employee, supervisor and HR** ahead of probation expiry; support
  **confirmation, extension or termination** as the outcome.
- The notification matrix (row "Probation confirmation") states the chain exactly:
  **System (month 5) → Head confirms → HR issues confirmation letter.**

---

## 3. Ground truth — measured 2026-08-18

### 3.1 The surface, and what it is missing

18 endpoints: 6 reads, create, delete, extend/confirm/terminate, and 7 review endpoints.
There is **no plain list and no paged list** — a register screen has to be assembled from
`/active` and `/status/{status}`. Every other closed area got a paged register.

### 3.2 ✅ The engine is wired and the data model is good

DI registration is present (`HrModuleServiceRegistration.cs`), all three repositories exist and
**every list query calls `.Include(p => p.Employee)`**, tenant scoping follows the RHEMA explicit
convention ([[hr-tenancy-stamping-gap]]), the ownership helpers (`GetOwnedProbationAsync`,
`GetOwnedReviewAsync`) exist and are used, and the schema is clean — no duplicate shadow FK
columns (`ProbationPeriods` has exactly 20 columns, all expected). The entity model is genuinely
well designed: `ProbationPeriod` + `ProbationExtension` (per-extension audit) + `ProbationReview`
(3 ratings, strengths, improvements, reviewer comments, employee response, recommendation,
second reviewer, employee acknowledgement, HR approval, signed document).

This is a **harden-and-complete** area, not a resurrection. The problem is that almost none of
that model can be reached.

### 3.3 ⚠ Area 6 already creates probation rows — this area inherits data

`JobOfferHireService.cs:1502` — on hire confirmation, when the offer carries
`ProbationPeriodMonths > 0`, a `ProbationPeriod` is created alongside the contract detail,
`Active`, with `OriginalEndDate = CurrentEndDate = startDate.AddMonths(n)`. So the *first* writer
of this store is closed area 6, and it works. Same shape as separation inheriting from area 9
([[hr-deferred-modules]]).

### 3.4 ⚠ Area 5 is a second writer, and it bypasses this area's service entirely

`Services/HR/Appraisal/Handlers/ProbationHandlers.cs` — `ConfirmProbationHandler` and
`ExtendProbationHandler` resolve the appraised employee's active probation from an appraisal
outcome recommendation and mutate it directly: status → `Completed`, or `CurrentEndDate +=
AppraisalSettings.ProbationExtensionMonths` (default 3). Live, built with area 5.

**So probation status has three writers — hire, appraisal handlers, and `ProbationService` — and
no shared code path.** Any rule this area adds to `ConfirmAsync` (employee record update,
confirmation letter, letter numbering) is skipped by the appraisal route unless the two are
converged. This is [[hr-succession-area-survey]]'s fourth lesson, visible *before* it costs
anything: the fix must land in one place both callers reach.

### 3.5 ⚠⚠ The whole *content* of a probation review has no writer

The review entity carries 15 substantive fields. The only write paths are:

- `AddReviewAsync` — schedules a review (number, date, reviewer, second reviewer).
- `UpdateReviewAsync` — writes **`ScheduledDate` and `SecondReviewerId`. That is all.** Every
  other field in the payload is silently discarded; the endpoint returns 200 and the DTO.
- `CompleteReviewAsync` — sets `Status = Completed`. Nothing else. Not even `ActualDate`.

So the three ratings, strengths, areas for improvement, reviewer comments, employee response,
recommendation, proposed extension months, acknowledgement and HR approval are **unreachable by
any request**. A review can only ever be scheduled and then marked complete while empty.

And the DTOs for the missing half already exist, unused, never referenced:
`SubmitProbationReviewDto`, `AcknowledgeProbationReviewDto`, `ApproveProbationReviewDto`,
`ConfirmProbationOutcomeDto`, `ExtendProbationPeriodDto`. Five DTOs, no service method, no
endpoint — [[hr-dead-path-defects]], and the [[hr-movements-area-survey]] "no writer anywhere"
shape at its largest so far.

⚠ `ApproveProbationReviewDto.HrApprovedById` is **caller-declared**. When it is wired, the actor
comes from the token, not the payload — [[hr-travel-area-survey]].

### 3.6 ⚠ Two extension paths; the live one loses the audit trail

`ProbationService` has both:

- `ExtendAsync` — **the one the endpoint calls.** Moves `CurrentEndDate`, increments
  `ExtensionCount`, and **overwrites `OutcomeNotes` with the extension reason**. Writes **no
  `ProbationExtension` row**.
- `RecordExtensionAsync` — validates the gap is a full calendar month, writes the audit row with
  previous/new dates, reason, actor and comments, then advances the period. **Unreachable.**
- `GetExtensionsAsync` — reads the audit trail. **Unreachable.**

So the entity built to be the extension audit trail can only ever be empty, and the live path
corrupts the outcome-notes field it shares with confirm/terminate. `ExtensionCount` will
therefore disagree with the (empty) extension history — an internal contradiction the harness can
assert directly.

### 3.7 ⚠ `GET employee/{id}` declares an array and returns one object

`ProbationController.cs:35` declares `ActionResult<IEnumerable<ProbationPeriodSummaryDto>>`;
`IProbationService.GetByEmployeeIdAsync` returns `ProbationPeriodDto?` and the service returns
the single active period (or the most recent). It compiles because `Ok(object)` converts to any
`ActionResult<T>`. A TypeScript type written from the endpoint signature would be fiction that
type-checks, and `.map` on the response would throw — precisely
[[hr-travel-area-survey]]'s lesson. The repository already returns the full history ordered by
start date, and the entity comment says an employee may accumulate several.

### 3.8 ⚠ Four read defects that return 200 and are wrong

| Read | Wrong because |
|---|---|
| `GET {id}`, and the create/extend/confirm responses | generic `GetByIdAsync` has no `.Include`, so `EmployeeName`/`EmployeeNumber` are **blank strings** — while the same fields are populated in every list |
| every list (`/active`, `/status/{s}`, `/ending-within`) | no `.Include(p => p.Reviews)`, so `ReviewCount` is **0 for every row**, always — the register's review column is a lie |
| `GET {probationId}/reviews` | includes `ReviewedBy` only, so `SecondReviewerName` and `HrApprovedByName` are always blank |
| `GET reviews/reviewer/{id}` | omits `ReviewedBy` entirely, so `ReviewedByName` is blank in the reviewer's own queue |

All four are [[hr-ported-list-read-bugs]] shapes, all null-guarded, so nothing 500s.

### 3.9 ✅ FR-HR-031's data exists and already encodes the rule

Measured on the DEFAULT tenant, 2026-08-18:

- `StaffLevels`: **Management Staff (MGT, rank 1), Senior Staff (SNR, rank 2), Junior Staff
  (JNR, rank 3)** — exactly the FRD's categories, plus one test row.
- `EmployeePositions` (the position master, 146 live): **124 have a `StaffLevelId`** (85%),
  **123 have `ProbationPeriodMonths`**.
- And the values already match the spec: **JNR → 3 months (47 positions), SNR → 6 months (60,
  one null), MGT → 6 months (16)**.

This is the opposite of the `ExpectedHeadcount` and org-head situations: the data is maintained
and it agrees with the requirement. FR-HR-031 can be **enforced and validated** rather than
invented, and a position that disagrees is detectable.

Today nothing does this: duration comes from `offer.ProbationPeriodMonths ?? position.
ProbationPeriodMonths` with no category rule and no validation, and
`CompanyHrPolicySettings.DefaultProbationMonths` (6) / `ProbationEndLeadDays` (30) are
unreferenced by any probation code.

### 3.10 ⚠⚠ FR-HR-032's routing target does not exist in the data — the fourth encounter

FR-HR-032 and the notification matrix both say the month-5 form goes to **the head**.
Re-measured today, on a workforce that has nearly doubled since [[hr-deferred-modules]] recorded
these numbers:

| | 2026-08-16 | **2026-08-18** |
|---|---|---|
| `OrganizationUnits.HeadEmployeeId` populated | 0 of 41 | **0 of 41** |
| `Employees.ManagerId` populated | 175 of 1,210 (14%) | **175 of 2,351 (7%)** |
| `Employees.OrganizationUnitId` populated | 1,186 of 1,210 | **2,327 of 2,351 (99%)** |

It has got **worse**: the manager count did not move while the workforce grew. So "route to the
head" resolves to nobody for **93%** of employees, and FR-HR-140's "notify the supervisor" has
the same hole. This is the same missing foundation that already cost a mid-slice redesign in area
8 (FR-HR-173) and left area 9's `MinimumAuthority` rule correct but inert.

[[hr-deferred-modules]] says explicitly: **raise this rather than working around it a fourth
time.** Hence decision D-2 below — this is the one thing in the area I will not decide alone.

### 3.11 ⚠⚠ Confirmation never reaches the employee record, in either direction

`Employee` carries `StaffStatus`, `ConfirmationDate` ("Date probation was passed") and a computed
`IsOnProbation => StaffStatus == StaffStatus.Probation`. Measured today:

- **All 2,351 live employees are `StaffStatus = Active`.** Not one is `Probation`.
- **Zero employees have a `ConfirmationDate`.**
- `grep` finds **no writer of `Employee.ConfirmationDate` anywhere in HR**, and nothing that
  **clears** `StaffStatus = Probation` on confirmation.

⚠ **Corrected 2026-08-18 while building slice 5.** An earlier draft of this section said nothing
set the flag in *either* direction. That was wrong: `JobHireService.cs:1463` already stamps
`StaffStatus.Probation` when the offer carries probation months. The hire half works. What is
missing is the **clearing** half and `ConfirmationDate` — which is the more damaging half anyway,
because a flag that is set and never cleared is worse than one that is never set. The 2,351-Active
measurement reflects a workforce loaded by migration rather than hired through the pipeline, not a
broken hire path.

So the flag is set and never released, and the consequence is live and measurable:
`EmployeeBenefitEnrollmentService.cs:1090` refuses enrollment when
`!policy.AvailableDuringProbation && employee.IsOnProbation`, and
`BenefitEnterpriseDataSeeder.cs:144` seeds a policy with `AvailableDuringProbation = false`.
Because `IsOnProbation` is never true, **that policy's probation restriction has never once
fired** — a benefit rule that cannot work.

⚠ Giving this field teeth is the [[hr-travel-area-survey]] lesson in advance: the moment hire
sets `Probation` and confirm clears it, the benefit gate starts biting, and every neighbour that
assumed `IsOnProbation == false` becomes a defect. Grep the readers *before* flipping it, and
assert the benefit consequence — not the flag — in the harness.

### 3.12 ✅ The letter pattern already exists

`OfferLetterService` (343 lines) renders an HR-editable named template through
`ITemplatedEmailService` to HTML. FR-HR-032's confirmation letter is a sibling of it, not new
machinery. FR-HR-033's appointment letter is the same service, already built for offers.

### 3.13 ✅ Four reminder-engine precedents to copy

SHE, movements, discipline and travel each ship a reminder engine with a manual queue, a run log
and — the seam that makes it testable — a server-stamped **`preview?asOf=`**
([[hr-discipline-area-survey]]). FR-HR-032 (month 5) and FR-HR-140 (ahead of expiry) are one
engine with two rules, and `ending-within` + `ProbationEndLeadDays` are already the right seams.

### 3.14 ⚠ A required FK with almost no data

`ProbationPeriods.ContractDetailId` is **NOT NULL**, and there are **4 live
`EmployeeContractDetails` rows** for 2,351 employees. The hire path is fine — it creates the
contract in the same transaction. But an HR-initiated create (backfilling probation for an
existing employee, which is the only way to get data into this area today) has nothing to point
at for 2,347 employees. The create form cannot be built without answering this — decision D-3.

### 3.15 ⚠ Every not-found is a 400 whose message is thrown away

`GetOwnedProbationAsync` and `GetOwnedReviewAsync` throw `ArgumentException`, and
`GlobalExceptionHandlingMiddleware.cs:138` maps that to **400** and **replaces the detail with the
fixed string "Invalid argument provided."** Two consequences:

- A missing probation record is indistinguishable from a malformed payload — both are a 400 saying
  nothing. Succession hit this and added its own domain exception for exactly this reason
  (`SuccessionValidationException`, with the comment naming the problem).
- Worse, the one carefully-worded rule in the service goes the same way:
  `RecordExtensionAsync`'s *"New end date (x) must be later than the current end date (y)"* is an
  `ArgumentException`, so the caller sees "Invalid argument provided." **A rule that fires
  correctly but cannot explain itself is still a defect** — the user is left on a disabled screen
  with no idea what to do.

### 3.16 FR-HR-030 — oath of secrecy has no implementation at all

`grep -ri oath src` returns only Estate's land-acquisition witness oaths. FR-HR-030 is
Mandatory and sits in §A1.4 beside probation. Area 15 closed without it. It is small — an
onboarding checklist item with a date, an acknowledgement and a document link — and it belongs
here only because A1.4 is what this area finishes.

---

## 4. Scope and boundaries

**In.** Probation periods and their lifecycle; probation reviews and their content; extensions
with audit; confirmation (record + letter); the month-5 routing and expiry notification engine;
the employee-record consequences of confirmation; FR-HR-031 category durations; FR-HR-030 oath
of secrecy.

**Out, deliberately.**

- **Termination of probation hands off, it does not compute.** Probation termination records the
  decision and stops; entitlement computation, clearance gating and the non-disciplinary exit
  routes stay with the deferred separation module ([[hr-deferred-modules]]). Same boundary area 9
  drew. Probation termination will *write a separation record and reference it*, nothing more.
- **Payroll.** Confirmation may change pay in reality; this area records the confirmation and
  registers the money event in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md`
  ([[hr-finance-integration-split]], [[payroll-ownership-boundary]]).
- **The appraisal route stays.** Area 5's probation recommendation handlers are not removed; they
  are converged onto this area's confirm/extend path (§3.4).

---

## 5. Decisions needed

**D-1 — Does probation confirmation go on the workflow engine?**
The FRD chain is two steps: *head confirms → HR issues the letter*. Single-step definitions
silently auto-approve ([[workflow-engine-integration]]), so a two-step chain is a genuine fit,
and areas 8, 9 and 13 all put their approvals there. **DECIDED 2026-08-18 — yes, the workflow
engine**, now unblocked: D-2 gives step 1 a resolvable approver (the named confirming authority
for the employee's organisation unit), so the chain is *authority confirms → HR issues the
letter*.

**D-2 — Who is "the head", given 0 of 41 units have one and 7% of employees have a manager?**
**DECIDED 2026-08-18 — option 1, the explicit assignment screen.** The options as put:
1. **Explicit assignment (recommended, and the recommendation [[hr-deferred-modules]] already
   says to put to TDC):** an admin screen where HR names the confirming authority per
   organisation unit. Small, works on day one, and pays for the fourth encounter once instead of
   again in the fifth.
2. **Resolve with a documented fallback ladder:** `Employee.ManagerId` → unit `HeadEmployeeId` →
   HR queue. Cheap, no new data, and honest — but for 93% of employees it *is* just "HR queue",
   so FR-HR-032's routing would be nominally implemented and effectively inert, which is the
   area-9 `MinimumAuthority` outcome again.
3. **Block the slice** until TDC confirms the org data will be maintained.

**D-3 — What does an HR-initiated probation point at, given `ContractDetailId` is required and
only 4 contract rows exist?**
**DECIDED 2026-08-18 — option 1, create the contract detail alongside the probation.** The
endpoint already exists to do it from outside (`POST /api/hr/Employees/{id}/contracts`), which is
what the harness fixtures use; the service will do it internally on the HR-initiated path.
1. **Create the contract detail as part of the probation create (recommended)** — mirrors what
   the hire path does, keeps the FK honest, and gives the 2,347 employees a contract row they
   should have anyway.
2. Make `ContractDetailId` nullable — a migration on another dev's shape, and it discards real
   information.
3. Restrict creation to the hire pipeline only — spec-clean but leaves the register permanently
   empty for the existing workforce, so nothing can be tested or demonstrated.

**D-4 — Is FR-HR-030 (oath of secrecy) in scope here, or logged back to area 15?**
**DECIDED 2026-08-18 — build it here**, as one small slice. It is Mandatory, it is in the same
FRD section, and area 15 is closed — logging it means it is owed by nobody.

**D-5 — Self-service reach.** Employee acknowledgement of a probation review is inherently the
employee's own act (the entity has `EmployeeAcknowledged`/`EmployeeResponse`), so unlike
succession — where assessment is *about* a candidate — this area does need a self path for that
one action, gated self-or-HR on the ownership helper ([[hr-area-authz-pattern]]). Everything
else HR/Management. **DECIDED 2026-08-18 — acknowledge-only self path**, following discipline,
and it arrives in slice 2 rather than slice 0 so the gate lands as one piece.

---

## 6. Findings register

| # | Finding | § |
|---|---|---|
| F1 | Area ungated — bare `[Authorize]`, any authenticated user | 3.2 |
| F2 | Review content has no writer: 12 of 15 fields unreachable | 3.5 |
| F3 | `UpdateReviewAsync` discards every field but two, returns 200 | 3.5 |
| F4 | `CompleteReviewAsync` completes an empty review, never sets `ActualDate` | 3.5 |
| F5 | 5 orphan DTOs (submit / acknowledge / approve / confirm-outcome / extend) | 3.5 |
| F6 | `ApproveProbationReviewDto.HrApprovedById` caller-declared | 3.5 |
| F7 | Live `ExtendAsync` writes no audit row and clobbers `OutcomeNotes` | 3.6 |
| F8 | `RecordExtensionAsync` + `GetExtensionsAsync` unreachable | 3.6 |
| F9 | `GET employee/{id}` declares an array, returns one object | 3.7 |
| F10 | `EmployeeName`/`Number` blank on `GET {id}` and all write responses | 3.8 |
| F11 | `ReviewCount` always 0 on every list | 3.8 |
| F12 | `SecondReviewerName` / `HrApprovedByName` always blank | 3.8 |
| F13 | `ReviewedByName` blank in the reviewer's own queue | 3.8 |
| F14 | No list and no paged list endpoint | 3.1 |
| F15 | FR-HR-031 category duration rule absent; policy settings unreferenced | 3.9 |
| F16 | FR-HR-032 month-5 routing absent; no confirmation letter | 2, 3.12 |
| F17 | FR-HR-140 expiry notification absent | 2 |
| F18 | Nothing clears `StaffStatus.Probation` on confirmation and nothing writes `ConfirmationDate`, so the seeded `AvailableDuringProbation = false` benefit rule would refuse a confirmed employee forever (hire sets the flag correctly — corrected 2026-08-18) | 3.11 |
| F19 | Three uncoordinated writers of probation status | 3.4 |
| F20 | FR-HR-030 oath of secrecy unimplemented | 3.16 |
| ~~F22~~ | **WITHDRAWN 2026-08-18.** I recorded that the benefit-eligibility refusal was silenced like F21, reasoning from the bare `InvalidOperationException` in `EnsureEligibleAsync`. Measured: it arrives as a **409 carrying its own message**, because `EmployeeBenefitEnrollmentsController` catches it before the global handler sees it. Reading the throw site is not reading the contract. | — |
| F21 | Not-found is a 400 with the message replaced by "Invalid argument provided."; the extension date rule is silenced the same way | 3.15 |

## 7. Cross-module seams

| Seam | State |
|---|---|
| Area 6 recruitment → creates the probation row on hire | ✅ works, keep |
| Area 5 performance → confirm/extend via appraisal outcome | ✅ works, must converge (F19) |
| Compensation → `BenefitPolicy.AvailableDuringProbation` | ⚠ dormant, will wake (F18) |
| Separation/exit (deferred) → probation termination | record + reference only |
| Payroll → pay change on confirmation | backlog entry only |
| Workflow engine → the two-step confirmation chain | D-1 |
| `ITemplatedEmailService` → confirmation letter | ✅ pattern exists |

## 8. Proposed slices

Backend slices each ship a `run-sliceN.mjs` harness in `dev-harness\hr-probation\`; UI slices
each ship a `run-sliceN-ui.mjs` payload probe ([[hr-travel-area-survey]]). Run in Staging with
the JWT key ([[hr-harness-run-environment]]).

| # | Slice | Closes |
|---|---|---|
| 0 | Gate the area onto `HR.Probation.*` (Read/Write/Admin), seed **before** the gates; acknowledge-only self path | F1, D-5 |
| 1 | Actor integrity + the read defects: array/object contract, Includes, `ReviewCount`, paged list, and not-found/rule messages that survive the middleware | F6, F9–F14, F21 |
| 2 | The review spine: submit / acknowledge / hr-approve; `CompleteReview` requires content | F2–F5 |
| 3 | Extensions: `/extend` onto `RecordExtensionAsync`, expose the audit trail, stop clobbering notes | F7, F8 |
| 4 | FR-HR-031 category durations, validated against `StaffLevel`, policy-settings fallback | F15 |
| 5 | Confirmation lands on the employee (`StaffStatus`, `ConfirmationDate`); converge all three writers; assert the **benefit** consequence | F18, F19 |
| 6 | Confirmation letter + FR-HR-033 check, on the `OfferLetterService` pattern | F16 |
| 7 | The reminder engine: month-5 routing + expiry notice, `preview?asOf=` seam | F16, F17 |
| 8 | Probation confirmation on the workflow engine (subject to D-1/D-2) | D-1 |
| 9 | FR-HR-030 oath of secrecy (subject to D-4) | F20 |
| 10 | UI: register + detail (reviews, extensions, outcome tabs) + create form — **built early enough to act as an actor audit** | — |
| 11 | UI: my-reviews queue, acknowledge screen, HR approval queue, dashboard | — |
| 12 | Content audit — every GET demanded **by id**, no conditional assertions | — |

## 9. Slice log

Harness: `D:\Rhema\TDC ERPS\dev-harness\hr-probation\`. Run the API in **Staging** with the JWT
key passed in ([[hr-harness-run-environment]]). Running totals: **38 / 50 / 61 / 40 / 32 = 221**.

### Slice 0 — gate the area (2026-08-18) — `run-slice0.mjs`, 38 assertions

`ProbationController` carried a bare `[Authorize]`. Now on a new `HR.Probation.Read/Write/Admin`
family, seeded from `HrPermissions.All` and mirrored by the verb-aware role fallback, so HR works
without a reseed.

**The ladder, and why it falls where it does.** HR keeps the record — open a probation, schedule
reviews, record them, issue the letter. The three *outcomes* (confirm, extend, terminate) and
deletion sit with **Admin**, because FR-HR-032 states the chain as *head confirms → HR issues the
confirmation letter*: confirmation is a management act, not record-keeping. Interim — slice 8 moves
the real check onto the workflow engine's named confirming authority (D-2), and only then may
these relax.

Everything the harness now refuses returned **200** before this slice for a plain Employee
account: the register of who is on probation, their performance/conduct/attitude ratings, the
reviewer's private comments, extending a probation, and confirming or terminating one.

### Slice 1 — the read and error contracts (2026-08-18) — `run-slice1.mjs`, 50 assertions

Written against measured behaviour, not inferred: `probe-reads.mjs` reproduced **16 failures**
against the live API first, every one as predicted from the source in §3.5–§3.8 and §3.15.

- **F9** `GET employee/{id}` declared `IEnumerable<>` and returned one object. It now returns the
  whole history, active first. `Ok(object)` converts to any `ActionResult<T>`, which is why this
  compiled — and why a TS type written from the endpoint's name would have compiled too, and
  thrown on `.map`.
- **F10** the create response and `GET {id}` resolved a **blank** employee name while the list
  beside them resolved it correctly. Write paths now re-read through a detail-loaded helper.
- **F11** `ReviewCount` was **0 on every list**, always. Every probation read now carries a
  **filtered** `.Include(p => p.Reviews.Where(r => !r.IsDeleted))` — both halves matter, since an
  unfiltered include would count soft-deleted reviews back in.
- **F12/F13** all four review reads now carry the *same* three actor navigations. And the reviewer
  queue now matches **second reviewers too**: matching only `ReviewedById` hid a second reviewer's
  own work from the one screen that exists to show it.
- **F14** a paged, filterable, searchable register — the area had no list endpoint at all.
- **F21, and worse than recorded.** Not-found threw `ArgumentException` → 400 with the detail
  replaced by "Invalid argument provided." But `InvalidOperationException` is flattened the same
  way ("The operation is not valid for the current state of the object."), so **every rule in the
  service was mute**: a missing probation, a duplicate active probation and the one carefully
  worded date rule were all one of two fixed strings. `ProbationWorkflowException` (one type, four
  reasons → 404/409/400, message preserved) fixes it — the medical shape.

### Slice 2 — the review spine (2026-08-18) — `run-slice2.mjs`, 61 assertions

The largest defect in the area. `ProbationReview` carries 15 substantive fields and **not one had
a writer anywhere in the solution**: `AddReview` wrote the schedule, `UpdateReview` wrote two
fields and dropped the rest of the payload behind a 200, `CompleteReview` set the status on an
empty review and not even `ActualDate`. The DTOs for the missing half were already in the
codebase, unused and unreferenced.

Now: `submit` (the reviewer's assessment, with a required recommendation), `acknowledge` (the
subject's own act), `hr-approve` (HR sign-off), and `complete` refusing a review that carries no
assessment.

⚠ **`ApproveProbationReviewDto.HrApprovedById` and `HrApprovalDate` were deleted, not ignored.**
Both were caller-declared, so a request could name someone else as the approver and back-date it.
Removing the fields is what stops them being wired back up.

⚠⚠ **The lesson of this slice, and it cost a build: a permission gate is the wrong tool when the
actor is defined by the record.** I gated `submit` on `HR.Probation.Write` while the service
required the caller to be the named reviewer. But a probation review is conducted by the
employee's **line manager** — FR-HR-032 routes the month-5 form to the head — who holds no HR
permission whatsoever. So the policy admitted only HR, the service refused HR, and **the action
was reachable by nobody at all.** This is the area-9 `MinimumAuthority` shape (a correct rule
nobody can reach), flagged twice in this very plan and reproduced anyway. `submit`, `complete`,
`acknowledge`, `reviews/mine` and the reviewer queue now take a plain `[Authorize]` and read
entitlement off the record.

⚠ Two consequences worth carrying:
1. **The class-level policy had to go.** Stacked `[Authorize]` attributes are ANDed, so exempting
   one action means carrying the policy on every *other* action. There is now no class-level
   default: a new endpoint with no gate is an **open** endpoint.
2. **The harness hid the question first.** My fixture named the HR actor as the review's *second
   reviewer*, so "HR cannot conduct the review" was testing nothing — HR was legitimately entitled
   on that record. It failed for the right reason and masked the real one. The positive assertion
   ("the line-manager reviewer, holding no HR permission, **can** submit") is what actually
   catches this class of error, and it is now in the file.

### Slice 3 — the extension audit trail (2026-08-18) — `run-slice3.mjs`, 40 assertions

The service shipped with two extension paths and the endpoint called the wrong one. `ExtendAsync`
moved the end date, incremented `ExtensionCount`, wrote **no audit row**, and overwrote
`OutcomeNotes` — the field confirm and terminate use to record their decision — with the extension
reason. `RecordExtensionAsync` and `GetExtensionsAsync`, which did it properly, were unreachable.
So the entity built to be the audit trail could only ever be empty, and `ExtensionCount` had
nothing to agree with by construction.

`/extend` now runs through the recording path and returns the audit row; `GET {id}/extensions`
exposes the trail. The harness asserts the chain is **continuous** — each row starting where the
last ended — because a gap is how an unrecorded extension would show itself, and that a *refused*
extension leaves nothing behind at all.

⚠ Harness debt this created: changing the `/extend` payload broke three earlier files that still
sent the old shape, and slice 2's new rule broke slice 0's "HR completes a review". Both were
caught by re-running every file, which is the point of doing so after each slice.

### Slices 4 and 5 — the category rule, and confirmation reaching the employee (2026-08-18) — `run-slice4.mjs`, 32 assertions

**Slice 4 — FR-HR-031.** Nothing applied a category rule: the length was whatever the caller sent,
and `CompanyHrPolicySettings.DefaultProbationMonths` was unreferenced by any probation code.
`DurationMonths` is now optional — omit it and the category length applies — and a value that
contradicts the category is refused for permanent staff, naming both numbers. Contract and
temporary staff are unbound, which the FRD says explicitly. `GET api/probations/policy/{employeeId}`
exposes the resolved length, its source, the staff level and FR-HR-140's lead days; it is what a
create form should read before rendering.

The rule is **read from the position master, not hard-coded**, because that data already encodes it
(JNR 3, SNR 6, MGT 6) and level codes are tenant-editable. The harness asserts that against the
**live** position master — FR-HR-031 is a claim about TDC's data, so proving it on fixtures would
prove nothing. If TDC wants the rule stated independently of positions, its home is a
`ProbationMonths` column on `StaffLevel`.

**Slice 5 — confirmation reaches the employee, and area 5 stops disagreeing.** Confirm now clears
`StaffStatus.Probation` and stamps `ConfirmationDate`; terminate releases the flag without a date.
Area 5's `ConfirmProbationHandler`/`ExtendProbationHandler` now delegate to `IProbationService`
instead of mutating the row, so the same probation confirmed two ways no longer ends in two
different states, and an appraisal-driven extension is audited like any other.

⚠⚠ **The lesson of these slices: `AsNoTracking` is a silent write-loss, and its two symptoms look
unrelated.** `EmployeeRepository.BaseQuery` is `AsNoTracking` by default, so
`GetByIdWithDetailsAsync` returns a **detached** employee. Mutating it did nothing at all, and
calling `UpdateAsync` on it threw *"another instance with the same key value is already being
tracked"* — because the probation reads `.Include(p => p.Employee)` and had tracked the same row.
One cause, two symptoms; and my first fix removed the `Update()` calls, which cured the 500 and
left the silent no-op. **A change that makes a failure quieter is not a fix.** Writes now use the
tracked generic `GetByIdAsync`; the detail load stays for read-only work.

⚠ **Three harness defects, and one of them was the assertion the slice exists for.**
`EmployeeDto` carries `staffStatus` but not `isOnProbation`/`confirmationDate` — those are on
`EmployeeDetailDto` behind `/details`, so the first run read `undefined` for both.
`availableDuringProbation` is nested on the `definition` block, not top level. And the reference
tenant holds **zero** benefit policies (the enterprise seeder never ran here), so the benefit
assertion was **skipping** — [[hr-succession-area-survey]]'s "a conditional assertion is a skipped
assertion wearing a tick", live. The run now mints its own restricted policy and asserts both
directions unconditionally: the same enrolment, same policy, same employee, **refused on probation
and accepted after confirmation**. That pair is the proof the flag means something; asserting the
flag alone would not have been.

⚠ **F22 withdrawn.** I recorded that the benefit refusal was silenced like F21, reasoning from the
bare `InvalidOperationException` at the throw site. Measured, it arrives as a 409 carrying its own
message, because the controller catches it before the global handler sees it. **Reading the throw
site is not reading the contract.**
