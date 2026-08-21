# HR Area 9b — Separation, Clearance & Exit: Build Plan

**Started 2026-08-20.** Numbered **9b**, not 14 or 19: this area's requirements live in FRD
§A1.10 *"Separation, Clearance & Exit"* and §3.A.2 *"Separation & Final Settlement"*. It is the
part of the exit story that area 9 (Discipline, closed 2026-08-17) deliberately handed off — the
same relationship 15b has to 15. `14` stays reserved for Awards & Recognition.

---

## 1. How to use this document

Section 3 is **measured ground truth**, not intention — every claim was checked against the source
or against the DEFAULT tenant database on 2026-08-20 and is cited. Section 5 holds the decisions
that need the user before the affected slices start. Section 6 is the slice plan; section 8 is the
log, appended as slices land.

Read section 3 before touching code. This area is **neither greenfield nor a resurrection**: a
termination path exists and runs, a retirement calculator exists and is correct, and a separation
checklist exists but is chained to a disciplinary action. What is missing is everything between
them.

---

## 2. Status at a glance

| | |
|---|---|
| Backend surface | `POST api/hr/Employees/{id}/terminate` + `/reinstate`; `StaffDisciplineSubEntityController` termination/separation sub-resources. **No separation controller, no clearance, no settlement.** |
| Services | `EmployeeService.TerminateEmployeeAsync` (real, ~50 lines); `HrPolicyCalculations` (retirement maths, correct); `StaffDisciplineTerminationService` |
| Frontend | none — zero separation/exit/clearance screens under `frontend/src/app/hr` |
| Gate | `EmployeesController` role gate only; no `HR.Separation.*` policies exist |
| Harness | none |
| FRD backing | **8 requirements, all priority M**: FR-HR-090, 091, 092, 093, 182, 183, 184, 185. Plus FR-HR-046, FR-HR-152, FR-HR-111, which land here. |
| Data | 3,693 employees, **0 ever terminated**; 29 disciplinary terminations recorded, **all 29 employees still Active** |

The FRD is specific here, and the specificity is the whole point of the area:

- **FR-HR-090** — manage terminations and designations. *(M, URD 5.4)*
- **FR-HR-091** — a **completed clearance form is required before** resignation/separation, and
  **then** entitlements are computed for payment. Clearance is a **gate**, not a record. *(M)*
- **FR-HR-092** — the **MD signs all terminations except procedural ones**, which **HR
  auto-approves per policy** (the FRD's own example: absence beyond 10 days). *(M)*
- **FR-HR-093** — retirement at **age 60, effective on the birthday**, with **advance alerts**. *(M)*
- **FR-HR-182** — support **all eight separation types it names**: resignation, voluntary
  retirement, compulsory retirement, medical retirement, death, termination, redundancy and summary
  dismissal — plus **contract expiry**, which FR-HR-111 requires alerts for and the existing enum
  already carries. *(M)*
- **FR-HR-183** — exit clearance runs across **outstanding loans, salary advances, company
  property, office equipment, duty-post keys, documents and payroll recoveries**. *(M)*
- **FR-HR-184** — final settlement = **unpaid salary + notice pay + leave encashment + benefits −
  deductions − recoveries − loans + pension-related payments**. *(M)*
- **FR-HR-185** — **Internal Audit reviews the final settlement before payment is released**. *(M)*
- **FR-HR-046** — leave accumulates near retirement and is encashed **on exit; no other encashment
  except on exit**. *(M)*
- **FR-HR-152** — leave encashment on separation is **capped at 56 days**. *(M)*
- **FR-HR-111** — 30-day advance alerts for due events including **retirement and contract expiry**. *(M)*

Chapter 12's process table states the chain outright: **Termination** — initiator HR/System,
*"Managing Director signs. Procedural terminations: HR auto-approves per policy"*. **Final
settlement** — initiator HR, *"Internal Audit reviews → payment released"*.

---

## 3. Ground truth — measured 2026-08-20

### 3.1 What exists and works

- **`EmployeeService.TerminateEmployeeAsync`** (`EmployeeService.cs:387`) is a real, non-trivial
  implementation: it sets `StaffStatus`/`IsActive`/`TerminationDate`/`Reason`/`Notes`, terminates
  every active `EmployeeContractDetail`, and closes the open `EmployeePositionHistory` row with
  `PositionChangeReason.Termination`. Reached from `POST api/hr/Employees/{id}/terminate`
  (`Controllers/HR/EmployeesController.cs:227`).
- **`HrPolicyCalculations`** (`Services/HR/HrPolicyCalculations.cs`) already computes
  `EffectiveRetirementAge`, `RetirementDate` (explicit `Employee.RetirementDate` else DOB + age),
  `ServiceYearsLeft` and `Age` — pure, DB-free, gender-override aware. **FR-HR-093's maths is
  already written and correct.** What is missing is anything that *acts* on it.
- **`CompanyHrPolicySettings`** (`Entities/HR/CompanyHrPolicySettings.cs`) already carries
  `CompulsoryRetirementAge = 60`, `VoluntaryRetirementAge = 55`, gender overrides,
  `DefaultResignationNoticeDays = 30`, `DefaultTerminationNoticeDays = 30`,
  `RetirementCountdownLeadDays = 365` and `ContractExpiryLeadDays = 60`. **1 row exists on
  DEFAULT.** The policy anchors for FR-HR-092/093 and for notice pay are already in place and
  tenant-scoped.
- **The exit checklist model is good** — `StaffDisciplineSeparation`
  (`StaffDisciplineEntities.cs:761`) covers exit interview (+ interviewer, date, notes), equipment
  returned (+ missing equipment), access revoked (+ by whom), final payroll processed, benefits
  terminated, and a checklist-complete flag. `StaffDisciplineTermination` (`:731`) covers rehire
  eligibility, restrictions and final paycheck. **Both are chained to a `DisciplinaryActionId`.**
- **Roles for the approval chain already exist** — `AspNetRoles` on this database contains
  `Managing Director`, `TDC_MANAGING_DIRECTOR` and `TDC_INTERNAL_AUDIT`, seeded by the
  procurement/finance work. **FR-HR-092 and FR-HR-185 are not blocked on the org-authority gap**
  ([[hr-deferred-modules]] §3) the way FR-HR-080 and FR-HR-181 are.
  ⚠ Note the **double spelling** — `Managing Director` *and* `TDC_MANAGING_DIRECTOR` — the same
  trap as `HR` / `HR User` ([[hr-role-name-mismatch]]). Cover both, once, in a constant.

### 3.2 The measured holes

**(a) The disciplinary termination never reaches the employee.** 29 `StaffDisciplineTerminations`
rows exist on DEFAULT. Every one of their employees is still `StaffStatus = Active`:

> ⚠ **CORRECTION, 2026-08-20 (slice 9b).** All 29 of those rows are **area-9 harness fixtures** —
> `A9V…` employee numbers, `A9Ver Actor…` names — and **zero are real employees**. The *defect* is
> real and the missing code was real: `RecordAsync` recorded a termination and nothing carried it
> into an exit. But the "29 dismissed people still on strength" framing used throughout this plan,
> and in several commit messages, implies production impact that does not exist. The check that
> settles it is one `WHERE EmployeeNumber NOT LIKE 'A9V%'` and it should have been run at survey
> time. **A join count is evidence about rows, not about people.** Read the paragraphs below with
> that correction applied.

```
emp_status_behind_disc_termination | 1 (Active) | 29
```

Nothing anywhere calls `TerminateEmployeeAsync` except the two employee controllers — `grep -rn
TerminateEmployeeAsync src` confirms no discipline caller. A person can be terminated for cause and
remain an active employee on the master record: in headcount, in the establishment counts area
17/18 just made load-bearing, and on every roster.

**(b) The exit path has never executed.** DEFAULT tenant, 3,693 non-deleted employees:

| Measure | Value |
|---|---|
| `StaffStatus = Active` | 3,640 |
| `StaffStatus = Probation` | 53 |
| `StaffStatus = Terminated` | **0** |
| `TerminationDate` populated | **0** |
| `RetirementDate` populated | **0** |
| `DateOfBirth` populated | 3,391 (92%) |
| `LeaveEncashments` rows | **0** |
| `EmployeeContractDetails` with an `EndDate` | **0** of 202 |

**(c) Nobody is anywhere near 60.** Employee ages on DEFAULT run **34 to 48, mean 36**. Not one
employee is within twelve years of retirement. This is the [[hr-job-architecture-area-survey]]
lesson arriving early — **check the data before designing around it** — and it settles three things
now rather than in slice 7:

- the retirement sweep will legitimately return **0 rows** on real data; that is correct behaviour,
  not a bug, and the harness must not assert non-empty against live data;
- every retirement assertion needs a **purpose-built fixture** (an employee stamped with a DOB ~60
  years back, or an explicit `RetirementDate`), exactly as area 15b did for probation;
- the same applies to contract expiry — 0 of 202 contracts carry an `EndDate`, so FR-HR-111's
  contract-expiry alert has no live subjects either.

**(c-bis) `DateEmployed` is populated on 227 of 3,699 employees — 6%.** Found by the slice-1
harness on 2026-08-20, and it is the same shape as (c) and as area 8's `ExpectedHeadcount`. The
rule refusing a separation dated before the employee joined is **correct and inert for 94% of the
workforce**: with no joining date there is nothing to compare against, so the create is allowed.
Keep the rule — it costs nothing and bites the moment the data exists — but do not describe it as a
safeguard, and give any harness that tests it a fixture that actually carries the date.

**(d) There is no separation record independent of discipline.** `EmployeeTerminationType`
(`HREnums.cs:2837`) carries ten members — InvoluntaryForCause, InvoluntaryPerformance,
InvoluntaryRedundancy, VoluntaryResignation, VoluntaryRetirement, MutualAgreement, ContractExpiry,
Death, SummaryDismissal, Other — and `TerminationReason` (`:2447`) nine more. **Only a disciplinary
case can reach any of them.** Resignation, retirement, contract expiry and death have enum members
and no route.

Two of FR-HR-182's eight named types had **no member at all**: *compulsory retirement* and *medical
retirement*, the enum offering only `VoluntaryRetirement`. A compulsory retirement therefore had to
be recorded as something it was not. Appended in slice 1 as `CompulsoryRetirement = 10` and
`MedicalRetirement = 11` — appended, never renumbered, per the convention documented on
`SummaryDismissal`.

**(e) No clearance concept exists anywhere.** Searching the HR controllers for "clearance" returns
only SHE environmental clearance and pre-employment police clearance. FR-HR-091's gate and
FR-HR-183's checklist have **zero** implementation.

**(f) No settlement concept exists.** Nothing computes notice pay, unpaid salary, encashment or
recoveries. `LeaveEncashment` (`LeaveEntities.cs:533`) has days/amount/status but **no 56-day cap
(FR-HR-152) and no exit-only rule (FR-HR-046)** — grep for `56` in `LeaveEncashmentService` returns
nothing.

**(g) No workflow entity type for exit.** `WorkflowEntityTypeCatalogService` HR entries are
`Employee`, `PayrollRun`, `PayrollPayslipEmail`, `PayrollSalaryAdvance`, `PayrollBonusSetup` and
`PayrollBackpaySetup` — plus a `LegalTerminationRecognition` belonging to Legal, **not HR**. Any
approval chain here needs the four-step registration in [[workflow-engine-integration]].

### 3.3 Defects in the existing path — fix in this area, do not leave

1. **`TerminationReason` is silently discarded when unparseable.** `EmployeeService.cs:404` —
   `Enum.TryParse<TerminationReason>(dto.TerminationReason, out var tr) ? tr : null`. A typo'd or
   unknown reason writes **null** and the caller still gets a 200. Knowing why people left is
   FR-HR-090's entire point.
2. **`CanTerminateEmployeeAsync` checks nothing.** `EmployeeService.cs:2332` returns `true` after
   only re-checking "already terminated", with the comment *"no active guarantor verification
   pending? (simplified)"*. This is the natural home for FR-HR-091's clearance gate.
3. **`ReinstateEmployeeAsync` erases history.** `EmployeeService.cs:449` nulls `TerminationDate`
   and `TerminationReason` and overwrites `TerminationNotes`, so the fact that someone was
   terminated and reinstated is unrecoverable — and the contracts and position history that
   `Terminate` closed are never reopened. An asymmetric write, the shape in
   [[ef-tracked-graph-write-traps]].
4. **Two termination entry points.** `Controllers/EmployeesController.cs:353` (legacy) and
   `Controllers/HR/EmployeesController.cs:227` both call it. `api/hr/Employees` is canonical; the
   legacy one must not become a way round the clearance gate.

---

## 4. Scope

**In scope:** the separation record and register for every FR-HR-182 type; resignation intake
with notice; the clearance gate and checklist; the MD/HR approval split; the final-settlement
statement and its Internal Audit review; retirement detection and advance alerts; the contract
expiry, death and redundancy routes; the employee-master effect; the exit interview; reminders;
screens; exit analytics.

**Out of scope, deliberately:**

- **GL posting of the settlement.** Per [[hr-finance-integration-split]], every money event is
  registered in `docs/HR-FINANCE-INTEGRATION-BACKLOG.md` and posted in the one comprehensive sweep
  after the whole HR module. Do **not** invent an HR-side posting mechanism here.
- **Computing payroll figures.** Per [[payroll-ownership-boundary]], payroll is another developer's
  module: HR reads gross/net, loans and advances read-only and never modifies them. See D2.
- **Asset return as an enforced gate.** HR Assets (area 16) is unbuilt. See D4.
- **The grievance / employee-relations expansion** ([[hr-deferred-modules]] §2) — unrelated.

---

## 5. Decisions — all settled with the user 2026-08-20

**D1 — the disciplinary route JOINS this pipeline. ✅ DECIDED.**
*Recommendation taken: join.* Area 9 keeps owning the disciplinary *decision*; when the outcome is
termination it creates the same `EmployeeSeparation` record every other route creates, so there is
**one** exit register, one clearance run and one settlement. The alternative is two parallel exit
stores that disagree — which is how 3.2(a) happened. Cost: linking the 29 existing
`StaffDisciplineTermination` rows, and a back-fill decision for them.

**D2 — HR builds the STATEMENT and computes only what it owns. ✅ DECIDED.**
*Recommendation taken.* HR assembles the **statement** — line items, plus the HR-owned amounts (leave
encashment days × rate, notice pay days, benefit terminations) — and **reads** payroll for unpaid
salary, loans, advances and recoveries. Where payroll exposes nothing, the line is captured
manually with its source named. HR never writes payroll and never posts to the GL. Confirm, or tell
me to keep HR to a checklist and hand the whole computation to payroll.

**D3 — MD signs everything; "procedural" is ABSENCE BEYOND 10 DAYS ONLY. ✅ DECIDED.**
The FRD's example is taken as the literal list: absence beyond 10 days auto-approves by HR, and
**everything else — resignation and retirement included — routes to the MD**. The list is
configurable, so widening it later is a settings change, not a code change.
*Recommendation taken:* the workflow engine, with a **separation-authorities configuration** the way area
15b did confirming authorities — an explicit admin screen naming who signs what — rather than
deriving authority from org structure, which is still blocked on data ([[hr-deferred-modules]] §3:
0 of 41 units have a head). Anchor the MD step on the existing roles, covering both spellings. And
confirm the **procedural** list: the FRD names only *"absence beyond 10 days"* as an example — is
that the whole list, or do resignation, retirement and contract expiry also auto-approve?

**D4 — What does clearance actually block on today (FR-HR-183)?** Loans and salary advances live in
payroll; company property and office equipment need HR Assets (area 16, unbuilt); duty-post keys and
documents have no store at all. **✅ DECIDED: the configurable checklist with mixed sources.** *Recommendation taken:* items
defined per tenant, each with an owning department and a sign-off — reading real counts where a
source exists and standing as a signed manual line where it does not. That keeps 9b from blocking
on area 16, and becomes an enforced gate for free when 16 lands.

**D5 — Retirement is built fully, against fixtures. ✅ DECIDED (assumed, not contested).** FR-HR-093
ships complete; the live sweep legitimately returns zero until real staff data is migrated (3.2c),
and the harness asserts that emptiness deliberately rather than treating it as a failure.

---

## 6. Slice plan

| # | Slice | Delivers |
|---|---|---|
| 0 | Gate + foundation | `HR.Separation.*` policies per [[hr-area-authz-pattern]]; role constants covering both MD spellings; DI/registration audit; workflow entity-type seeding |
| 1 | Separation record + register | `EmployeeSeparation` covering every FR-HR-182 type (two of them appended to the enum here); paged register, detail, create — **FR-HR-090, FR-HR-182** |
| 2 | Resignation intake | Notice period from policy settings, acceptance/rejection, last-working-day computation |
| 3 | Approval chain *(was slice 4)* | MD signature, procedural auto-approval, notice waiver / pay in lieu — **FR-HR-092** |
| 4 | Clearance checklist + the gate *(was slice 3)* | Configurable items, departmental sign-off, refusing an incomplete clearance — **FR-HR-091, FR-HR-183** |
| 5 | Final settlement statement | Line items, notice pay, encashment at the 56-day cap and the exit-only rule — **FR-HR-184, FR-HR-046, FR-HR-152** |
| 6 | Internal Audit review gate | Review before payment release, on `TDC_INTERNAL_AUDIT` — **FR-HR-185** |
| 7 | Retirement | Retirement date from policy, effective on the birthday; register + advance alerts — **FR-HR-093** |
| 8 | The other routes | Contract expiry, death, redundancy, medical retirement — each with what differs |
| 9 | Employee-master effect + defect fixes | Termination reaches the employee record from every route; the four defects in 3.3; the 29 orphans |
| 10 | Reminder sweep | Employee / supervisor / HR notifications — **FR-HR-111** for retirement and contract expiry |
| 11 | Screens | `/hr/separations` register / `new` / `[id]` with tabs; `/administration/hr/separation` for authorities and clearance templates |
| 12 | Exit analytics | Turnover, reasons, exit-interview themes |
| 13 | Content audit ×2 + closeout | Every GET returns real content — **run it twice** |

Each slice: build → harness in `dev-harness/hr-separation/` with real assertions, run in Staging
with the JWT key ([[hr-harness-run-environment]]) → stage → hand over the commit message.

---

## 7. Risks carried in from other areas

- **Fixtures, not live data** (3.2c). Ages 34–48 and zero contract end dates mean three features
  have no live subjects. Build the fixture first, assert against it, and assert the live sweep's
  emptiness deliberately.
- **Run the content audit twice** — area 17's audit found two of its three real defects only on the
  second run.
- **Build the create form early** — the area-12 lesson; a create form is an actor audit.
- **A UI-payload probe is not optional** — `tsc` cannot see an absent key or an empty-string date.
- **Stamp `TenantId` explicitly** on every new entity ([[hr-tenancy-stamping-gap]]).
- **A soft delete does not release a unique index** — the area-13 lesson, wherever an index here is
  unique.

---

## 8. Log

### Slices 0 + 1 — written 2026-08-20, awaiting first build

**Slice 0 — the gate and the foundation.** No endpoints of its own; verified by slice 1's harness.

- `HrPermissions`: `HR.Separation.Read/Write/Admin` plus the three policies, the category, the
  three descriptors, and Read + Write into `HrStaffGrants`. The seeder inserts missing permissions
  and missing role grants on every startup, so this lands on the existing database without a reseed.
- `Constants.Roles`: `ManagingDirector`, `TdcManagingDirector`, `ManagingDirectorAny` and
  `InternalAudit`. The MD role exists under **two** spellings in the reference database; the
  combined constant is what `[Authorize]` should name, never one literal.
- `ServiceCollectionExtensions`: the three policies registered on the usual ladder, with a comment
  recording what is deliberately *not* in the family — the FR-HR-092 signature and the FR-HR-185
  review are read off the record and anchored on the MD and Internal Audit roles, so
  `HR.Separation.Admin` must never confer them.

**Slice 1 — the exit register.**

- `EmployeeSeparation` (new `SeparationEntities.cs`) + `SeparationStatus` (11 members, ordered to
  the FRD's own sequence: clearance precedes settlement, audit review precedes payment).
- **Two FR-HR-182 types had no enum member at all** — `CompulsoryRetirement` and
  `MedicalRetirement` — the enum offering only `VoluntaryRetirement`, so a compulsory retirement had
  to be recorded as something it was not. Appended as 10 and 11 per the documented convention.
- `ApplicationDbContext.HR`: the DbSet, indexes, enum conversions, and **all four** employee
  navigations configured explicitly so no shadow FK columns are minted. The unique index on
  `(TenantId, SeparationNumber)` is filtered on `IsDeleted`.
- Migration `20260820090000_AddEmployeeSeparationRegister` — hand-written, every statement guarded,
  discovery attributes inline, not listed in `FastBuildMigrationMetadata`.
- DTOs, `ISeparationService` / `SeparationService`, `SeparationsController`
  (`api/hr/separations`), DI registration.
- Harness `dev-harness/hr-separation/` — `run-slice1.mjs`, ~60 assertions.

**Three decisions worth keeping:**

1. **Status is never taken from the client.** A separation starts as a draft and every move from
   there is its own endpoint with its own rule. FR-HR-091 makes clearance a gate on the settlement,
   and a settable status field walks straight past it.
2. **No class-level `[Authorize]` on the controller.** Slices 4 and 6 must open individual actions
   to the MD and to Internal Audit, and stacked `[Authorize]` attributes are ANDed — a class-level
   role gate would keep applying. The cost is stated at the top of the controller: a new endpoint
   with no attribute is an **open** endpoint.
3. **Separation numbers are never reused.** A global query filter hides soft-deleted rows and the
   unique index is filtered on `IsDeleted`, so both would have allowed a deleted number back. The
   sequence reads through the filters deliberately, and the harness asserts it.

⚠ **Not yet exercised, and owed:** nothing reaches a status past Draft/Cancelled until slice 4, so
the harness cannot yet test the "employee has already left" rule or `employeeRecordUpdated` being
true. Slice 9 owns both, together with the 29 orphans.

**First harness run, 2026-08-20 — 66 of 67, then 67 of 67 after one code fix.** What the two
failures were worth:

1. **The fixture sent `hireDate`; `CreateEmployeeDto` has `DateEmployed`.** The unknown key bound
   to nothing, the create still returned **201**, and all six fixtures came out with
   `DateEmployed = NULL`. The rule refusing a separation dated before the employee joined then had
   nothing to compare against, allowed the call, and the row it created poisoned the next three
   assertions with "already has a separation in progress". This is the area-12 lesson in a new
   costume — *a payload written from a field name is fiction* — and it is why the harness now
   asserts the fixture really carries the date before testing the rule that reads it. It also
   surfaced §3.2(c-bis): the rule is inert for 94% of the workforce.
2. **A refusal test that creates a row when its rule fails to fire must own its subject.** Four
   consecutive refusals shared one, so the first false pass cascaded into three misleading
   failures. Refusal tests are not read-only; treat them as writes.
3. **`[Required]` on the cancellation reason made that one endpoint explain itself worse than every
   other rule on the controller** — ModelState answered first with "One or more validation errors
   occurred." while the service had a sentence ready that says what to do. Attribute removed; the
   service owns the message, and every refusal on this controller is now one shape.

**Final state: 67 of 67, green on three consecutive runs.**

**The scratch-scaffold check, and what it cannot see.** Running `dotnet ef migrations add Scratch`
against the hand-written snapshot produced exactly one difference — a `DropIndex` for
`IX_EmployeeSeparations_TenantId`, which the hand-written block claimed and the model does not
create: **EF's foreign-key index convention skips the FK index when an existing index already leads
with that column**, and `UX_EmployeeSeparation_Tenant_Number` does. Note that `migrations add`
*rewrites* the snapshot as it runs, so EF corrected the file itself; the DropIndex was the report,
not the repair.

⚠ **But that check diffs the model against the SNAPSHOT — the database is a third party to it.**
Listing `sys.indexes` found the real gap: the hand-written migration never created the FK indexes
for `ApprovedById`, `CancelledById` and `InitiatedById`, so a model-built database had them and a
migrated one did not, and no amount of scaffolding would have said so. Three guarded `CreateIndex`
statements added to the migration, and the same SQL applied to the dev database. **A schema change
has three homes; verifying two of them is not verifying it.**

(Incidental: `sqlcmd` needs `-I`. Any `CREATE INDEX` on a table that already carries a filtered
index requires `QUOTED_IDENTIFIER ON`, which sqlcmd defaults off and EF defaults on.)

### Slice 2 — submission, notice arithmetic, the separation file. 2026-08-20, 43/43

Migration `20260820100000_AddSeparationSubmissionAndDocuments` — **scaffolded by the user**, body
rewritten here to guarded SQL, listed in `FastBuildMigrationMetadata`. `SubmittedOn` /
`SubmittedById` on the separation, and the `EmployeeSeparationDocuments` table.

- **`POST /{id}/submit`** takes Draft → PendingApproval and *derives* what follows: given a notice
  date and period it works out the last working day, and the effective date follows that. It
  refuses a resignation with no notice date, because a resignation is its letter and the notice
  clock starts there. Other routes carry no notice date by nature.
- **Notice required / served / short — derived on read, never stored.** The shortfall floors at
  zero: serving more notice than owed is a longer handover, not negative notice pay. This is the
  figure slice 5 turns into FR-HR-184 notice pay.
- **Submission decides nothing about who signs.** `IsProcedural` stays false and nothing is
  approved; FR-HR-092 is slice 4's, and the harness asserts submission does not pre-empt it.
- **One document table for the whole lifecycle**, through the controlled-upload gate under a new
  `hr-separation-documents` category in the always-scan set. Entitlement is checked *before*
  storage. `FilePath` is deliberately absent from the DTO.
- **A document can be removed only while the separation is a draft.** After submission the
  attachments are part of what was approved and what the settlement is computed against, so
  removing one would silently rewrite the record. Deleting the whole separation stays with an
  administrator.

⚠ **Moved out of this slice deliberately: the notice waiver and payment in lieu.** Both are
decisions taken at acceptance, and FR-HR-092 makes acceptance the MD's. Adding their columns here
would have left fields with no writer until slice 4 — the dormant-field trap that made
`ExpectedHeadcount` and `DateEmployed` worthless. **Slice 4 owns them.**

⚠ **The scaffold caught an ordering fault in slice 1.** The hand-written slice-1 migration was
given an *invented* timestamp, `20260820090000` (09:00); the real scaffold ran at 01:16, so
`20260820011658` sorted **before** it. EF applies in id order, so on any database holding neither,
slice 2 would have run first and altered `EmployeeSeparations` before it existed. Renamed slice 2
forward to `20260820100000` — it was unapplied, so no `__EFMigrationsHistory` surgery. **A
scaffolded timestamp is real; an invented one is a guess that can sort into the past.** The second
argument for the user's workflow, and a sharper one than the snapshot.

⚠ **A harness lesson: do not probe "not found" with `Guid.Empty`.** The controller rejects an
all-zeros id with a 400 argument guard before any entitlement check, so that assertion passed
against the wrong code path. Use a real-looking id that simply is not ours.

### Slices 3 and 4 SWAPPED, 2026-08-20 — approval before clearance

Clearance was to be slice 3, but the status ladder makes that unbuildable in the right order:
clearance begins at `Approved`, and until FR-HR-092's decision exists **nothing can reach that
state**. Building clearance first would have meant either a gate starting from `PendingApproval`
that slice 4 then had to tighten, or a rule no test could reach — the area-9 `MinimumAuthority`
shape, and the area-15b mistake, both already recorded in this plan as things not to repeat.

Nothing is lost by swapping: FR-HR-091 requires clearance before the *separation* and before
entitlements are computed, and Chapter 12's chain is MD signs → clearance → settlement → Internal
Audit → paid. Approval genuinely comes first.

### Approval role membership, measured 2026-08-20 — one reachable, one not

| Role | Holders |
|---|---|
| `Managing Director` | **6** |
| `TDC_MANAGING_DIRECTOR` | 0 |
| `TDC_INTERNAL_AUDIT` | **0** |
| `HR` | 1,394 |

✅ **FR-HR-092 is reachable.** Six people hold `Managing Director`, so the MD's signature is a rule
somebody can satisfy. Both spellings still get authorized together — only one is populated *today*,
and which one that is could change.

⚠ **FR-HR-185 is NOT reachable, and slice 6 must not pretend otherwise.** Nobody holds
`TDC_INTERNAL_AUDIT`, so on live data no settlement could ever be reviewed and therefore no
settlement could ever be paid. This is the fourth instance of the area's recurring shape — a
correct rule with no data behind it — but unlike retirement ages or `DateEmployed` the fix is an
**administrative act, not a data migration**: somebody must be granted the role. Build the feature,
mint the role in the harness so it is genuinely tested, and put the grant to TDC as an operational
prerequisite. Recorded in `docs/HR-OPEN-QUESTIONS-FOR-TDC.md`.

### How FR-HR-092's exception is expressed

The FRD's only example of a procedural termination is "absence beyond 10 days", and there is no
`TerminationReason` member for abandonment — so nothing in the existing model can distinguish one.
Rather than infer it, slice 3 states it: `EmployeeSeparation.AbsenceDays` records the absence, and
`CompanyHrPolicySettings.ProceduralAbsenceDays` (default **10**) is the threshold. At or above it
the separation is procedural and HR may approve; below it, or with no absence recorded at all, the
MD signs. Set the threshold to 0 to require the MD's signature on everything.

**Entitlement is read off the record, not from a permission family.** Approving is gated with a
plain `[Authorize]`, and the service decides: an MD-role caller may approve anything, an HR caller
only what is procedural, everyone else is refused. A permission gate cannot express "may approve
this one but not that one", and stacking role and policy attributes would AND them
([[hr-area-authz-pattern]] trap 1).

### Slice 3 log — 2026-08-20, 50/50 (slice 2 now 44, slice 1 still 67)

Migration `20260820102312_AddSeparationApprovalDecision` — scaffolded, rewritten guarded, listed.

⚠ **The scaffold wrote `defaultValue: 0` for `ProceduralAbsenceDays`.** That is the CLR default for
`int`, not a decision, and it would have set every already-provisioned tenant to a threshold of
zero. The guarded rewrite creates the column with `DEFAULT (10)` — the entity's value and the FRD's
stated policy. **Read what a scaffold chose for a non-nullable column; it defaults to the type, not
to your intent.**

⚠ **The MD could have signed a separation they were unable to open.** Approving is a plain
`[Authorize]`, but every read endpoint requires `HR.Separation.Read`, which the MD role does not
hold. Fixed by granting `Managing Director`, `TDC_MANAGING_DIRECTOR` and `TDC_INTERNAL_AUDIT` a
**Read-only** grant — Write would let them edit what they are about to sign, and the authority to
decide is not granted as a permission at all. This is the area-15b shape again (two correct rules
whose intersection is empty), caught because the harness asserts that everyone who *should* be able
to act can.

⚠ **And a drift that the fallback handler was hiding.** `HrPermissions.RoleGrants` feeds the
role-fallback handler; `DatabaseSeedingService.rolePermissionMap` is the actual seed, and it is a
**separate hardcoded dictionary**. Adding the three roles to `RoleGrants` alone made the MD's reads
work — through the fallback — while `RolePermissions` held no rows for them. That is exactly the
drift `HrPermissions` warns about in its own remarks. Both sides now list the three roles. **Verify
a new grant in `RolePermissions`, not by watching a request succeed.**

⚠ **Two harness bugs, both mine, both the same class:** a subject suffix that collided with an
actor's employee number, and a `setToken` placed *before* `mintSubject` — which logs in as the
subject it creates and leaves the harness holding a plain Employee token. Neither was a product
defect; both cost a run.

### Slice 4 log — exit clearance, 2026-08-20, 64/64

Migration `20260820111206_AddSeparationClearance` — scaffolded, rewritten guarded, listed. Two
tables: `SeparationClearanceTemplates` (the tenant's form) and `SeparationClearanceItems` (one
filled-in copy per separation), plus `ClearanceItemKind` / `ClearanceItemStatus`.

**The owning unit is `OrganizationUnit`, not `Department`** — corrected by the user mid-slice, and
the data agrees twice over: it is the HR module's placement entity, **3,810 of 3,834** employees
carry one against 3,320 with a department, and this tenant's seven departments belong to the estate
and procurement modules. Caught before scaffolding, so no migration was wasted. I had generalised
from `departmentId` appearing in my own employee-create payload instead of checking which entity HR
actually places staff in.

**41 organisation units, 0 with a `HeadEmployeeId`** — so the owning unit routes and labels, it
does not authorise. HR walks the form round; each line records both the unit's signatory
(`SignedOffBy`, free text, because that person is often not an ERP user) and the HR user who
entered it.

Three rules the harness pins down:

1. **An empty catalogue is refused.** A form with no lines completes the instant it begins — every
   mandatory item satisfied because there are none — and reports "cleared" having checked nothing.
   A configuration gap must look like one.
2. **Items snapshot their catalogue line, with no FK back to it.** The harness renames a template
   after the form is signed and asserts the signed line keeps its original name. A clearance form is
   evidence about one person's exit.
3. **An amount is refused on kinds that cannot carry money.** `OutstandingAmount` feeds FR-HR-184;
   a number the settlement will never read is worse than none, because somebody will believe it.
   Note that a **waived** loan still counts in `TotalOutstandingAmount` — waived here means *carried
   into the settlement*, not *forgiven*.

⚠ **One real defect, found by the harness's "says why" convention.** Restarting a clearance answered
*"This separation is ClearanceInProgress. Clearance begins once the separation has been approved"* —
self-contradictory, because it **is** approved. The status check ran before the already-started
check, and a separation in clearance is no longer `Approved`. **Order the questions so the most
specific one answers first.** A rule that cannot explain itself is a defect even when it refuses
correctly.

✅ **The slice-3 seed/fallback drift is closed and verified in SQL:** `Managing Director`,
`TDC_MANAGING_DIRECTOR` and `TDC_INTERNAL_AUDIT` each hold a real `HR.Separation.Read` row in
`RolePermissions`.

### Slice 5 — the final settlement (FR-HR-184). Design settled 2026-08-20

**The measurement that dictated the design:**

| Source | Rows | Verdict |
|---|---|---|
| Employees with a salary on file | **202 of 3,883 (5%)** | no daily rate for 95% |
| `LeaveBalances` | **0** | encashment cannot be valued either |
| `PayrollLoans` | 0 | and `PayrollEmployeeProfiles` is 0, so the join bridge is dead too |
| `PayrollSalaryAdvances` | 0 | joins on `EmployeeId` — readable, empty |
| `PayrollPayslipSnapshots` | 0 | no unpaid-salary source |
| **`StaffTravelAdvances`** | **54, ~GHS 156k gross** | HR-owned, real, unconnected to exit |

⚠ **A settlement that only computed would print 0.00 for almost everybody**, and somebody would
sign it. Zero and "we could not work it out" are different statements. So every line carries a
`SettlementLineComputation` — Computed / ManuallyEntered / **CannotCompute** — and an uncomputable
line holds **null**, not zero, and **blocks finalisation** until a person supplies the figure with
its source named. *(User's decision, 2026-08-20.)*

✅ **Travel advances are auto-populated into both clearance and the settlement** *(user's decision)*.
54 live advances with a `SettledAmount` field, built by area 12, and until now an employee could
leave owing one with nothing to notice.

**Currency comes from Finance, not HR** — raised by the user before scaffolding, and it caught a
hardcoded `"GHS"` in the entity that is correct on this tenant and a lie on any other. The rule:
HR's `CompanyHrPolicySettings.DefaultCurrencyCode` wins when set, **validated against Finance's
currency master**; preparation is refused (not silently swapped) when Finance does not hold that
code; Finance's base currency is the fallback when HR's setting is blank. Same read-only
relationship `StaffTravelCurrencyBridge` established for travel, and for the same reason — two
opinions about what money is worth is how a trip came to be worth one thing on a travel screen and
another on a financial report. ⚠ Note there are **three** default-currency settings in the system
(Finance base, HR policy, Procurement policy); HR's had no consumer until now.

⚠ **An assumption to put to TDC, not bury:** the daily rate is *monthly salary × 12 ÷ 365*, stated
in words on every computed line. A 30-day-month or working-day basis gives different money. Raised
in `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` **with the arithmetic worked through** — GHS 6,000/month over
16 days' notice is 3,156 calendar / 3,200 on a 30-day month / 4,364 on working days.

**Slice 5 landed 2026-08-20 — 75/75 twice; full area regression green (67 + 44 + 50 + 64 + 75 =
300 assertions).**

Built: `SeparationSettlements` + `SeparationSettlementLines`, prepared from `ClearanceCompleted`,
finalised into `SettlementUnderReview` for slice 6. Money events registered in
`docs/HR-FINANCE-INTEGRATION-BACKLOG.md`; **nothing posts to the GL.**

Rules the harness holds down:

- **Null is not zero.** An unvaluable line carries no amount and blocks finalisation. On live data
  the unpaid-salary and leave-encashment lines *always* land there, so this is the normal path, not
  a corner case.
- **An amount requires a named source.** FR-HR-185 puts Internal Audit in front of this statement;
  a figure nobody can trace is one they cannot check. Supplying amount + source flips a line to
  `ManuallyEntered` — a person vouched for it, not the system.
- **Two ways to clear a block:** value the line, or delete it because nothing is owed. Adding a line
  with no amount re-blocks — asserted, because that is how HR says "something is owed and I don't
  yet know how much".
- **Notice served in full produces no line at all**, not a zero one. A zero line is noise on a
  document somebody signs.
- **A finalised statement is immutable** — no additions, edits or deletions.
- **Cross-currency travel advances are left uncomputed rather than converted.** Finance owns
  conversion and its rates are known to be inverted (recorded on `StaffTravelCurrencyBridge` during
  area 12); converting here would have inherited that bug silently and priced somebody's recovery
  wrong.

⚠ **Three compile errors worth the lesson**, all from assuming instead of reading:
`StaffTravelAdvance` and `LeaveBalance` live in `.StaffTravel` / `.StaffLeave` sub-namespaces, not
the flat `Entities.HR` the file paths suggest; and `CurrencyDto` exposes `CurrencyCode`, not `Code`
— written from the entity's shape rather than the DTO's. The cheap version of the area-12 lesson:
the compiler caught all three before a harness run.

### Slice 6 — FR-HR-185, Internal Audit's review. 2026-08-20, 37/37

Migration `20260820124713_AddSettlementAuditReview`: `ReviewOutcome`, `ReviewedById`, `ReviewedOn`,
`ReviewNotes`, `ReturnCount`. Approve → `SettlementApproved`; return → `SettlementPending` with
findings required.

⚠ **The scaffold's enum default was wrong again, in a new way.** `defaultValue: 0` for
`ReviewOutcome` — but `SettlementReviewOutcome` starts at `NotReviewed = 1`, so **0 is not a member
of the enum at all**. Corrected to `DEFAULT (1)`, and verified in SQL afterwards: all six existing
settlements carry 1, none carry 0. Second enum-default correction in this area after
`ProceduralAbsenceDays`. **Read what a scaffold chose for every non-nullable column, and check
enums that do not start at zero.**

**The control is in different hands from the approval, deliberately, and the harness proves it:**
HR, the **Managing Director** and a tenant administrator all get 403 on the review. The MD signed
the separation and still cannot pass the money — a control the approver can also clear is not a
control.

**A return refuses the figures, not the exit.** The separation's approval stands untouched
(asserted); only the statement reopens. `ReturnCount` survives re-finalisation because `FinalisedOn`
is overwritten each round trip, and "how many times was this queried before it was paid" is what an
auditor asks later.

**Refactor that came with it:** settlement editability is now keyed on the **separation's status**,
not on `FinalisedOn`. Unlocking a returned statement by clearing that timestamp would have erased
the fact it had ever been finalised. Slice 5 re-run: still 75/75.

⚠ **The role has no holders.** `TDC_INTERNAL_AUDIT` has zero members on the live tenant, so this
harness mints the only holder that has ever existed. **A green run proves the code, not the
deployment** — until the role is granted, every settlement finalises and then sits unpaid. The
control holding rather than failing open is correct, and will read as a stuck queue to whoever meets
it first.

**Area regression after slice 6: 67 + 44 + 50 + 64 + 75 + 37 = 337 assertions, all green.**

### Slice 7 — FR-HR-093, retirement at 60. 2026-08-20, 46/46. **No migration.**

The projection computes entirely from data that already exists (date of birth + policy age), so
nothing new is stored. `HrPolicyCalculations` had the maths right since long before this area;
what was missing was anything that acted on it.

⚠ **A comment I wrote in slice 1 was false for five commits.** `CreateEmployeeSeparationDto`'s
`EffectiveDate` said *"for compulsory retirement the service computes it from the birthday and
refuses a contradicting value (FR-HR-093)"* — and `CompulsoryRetirement` appeared **nowhere** in the
service. Documentation written ahead of the code, describing behaviour that did not exist. Nothing
depended on it so nothing broke, but **a comment describing a rule the code does not enforce is
worse than no comment: the next reader stops checking.** It is true as of this slice.

**Built:** the retirement queue (due within a horizon, default `RetirementCountdownLeadDays` = 365,
plus those already past their date and still on strength — a backlog, not a projection), and a sweep
that raises compulsory retirements for everyone due.

- **The birthday is not a choice.** A supplied date that disagrees is **refused**, naming both, not
  silently overwritten — somebody who typed a date deserves to know it was wrong. Voluntary
  retirement keeps whatever date was agreed; it is elected, not applied.
- **The sweep stamps no actor.** `IsSystemInitiated = true`, `InitiatedById = null` — a birthday
  arriving is nobody's act, and naming whoever ran the sweep would be a lie the audit trail could
  not tell from a real one. Those two fields have sat on the entity since slice 1 waiting for this.
- **One missing date of birth does not stop the sweep** for everybody else; it is reported and
  skipped.

⚠ **The new rule broke three earlier harnesses, and that was the rule working.** Slices 1–3 each
raised a `CompulsoryRetirement` carrying the shared fixture's `effectiveDate: '2026-08-31'`, which
now contradicts the computed birthday (2050-01-01 for a 1990-born fixture) and is refused. **The
harnesses were asserting behaviour FR-HR-093 says is wrong** — fixed by passing
`effectiveDate: null`, which is the correct usage. Worth noticing that this only surfaced because
every slice is re-run as regression; a defect that only bites old callers is invisible otherwise.

**Every assertion runs against a purpose-built fixture, and that is the finding, not a shortcut.**
Ages on live data run 34–48, so the queue answers with nothing and will until staff records are
migrated. The harness asserts that emptiness deliberately — including that an employee in their
thirties and one with no date of birth both stay out — so nobody later mistakes "no rows" for a
broken query.

**Area regression after slice 7: 67 + 44 + 50 + 64 + 75 + 37 + 46 = 383 assertions, all green,
twice.**

### Slice 8 — the other exit routes. 2026-08-20, 40/40. **No migration.**

FR-HR-182 lists nine ways out; slices 1–7 gave each a route. This slice gives four of them what
makes them *different*, rather than nine labels on one process.

- **Contract expiry** gets its own queue and sweep, and the rule that it **ends on the contract's
  own date** — an exit on another date is a different separation, not a contract expiring.
- **Medical retirement** cannot be submitted without the medical report; **death** cannot be
  submitted without the certificate. Both assert something that ends an income, and both should be
  evidenced rather than ticked. The document categories have existed since slice 2; this is what
  makes them mean something. ⚠ Enforced at **submission, not creation** — HR opens the record when
  it hears, and the paperwork follows. Demanding it up front would keep exits out of the system
  until the certificate arrived, which is how records go missing.
- **Redundancy gets nothing extra, deliberately.** Notice applies; nothing else in the FRD
  distinguishes it, and inventing a rule the specification does not state would be worse than
  leaving it plain.

⚠ **Third instance of the same data shape: 0 of 202 active contracts carry an `EndDate`.** So the
contract-expiry queue is empty on live data and the date rule is inert for everybody — the rule
applies only where a contract end date exists, which today is nowhere. Same handling as retirement:
real rule, fixtures to prove it, emptiness asserted.

**An assertion added against over-reach:** a plain resignation still submits with **no documents at
all**. A rule applied too widely is as wrong as one applied too narrowly, and an evidence gate is
exactly the kind that leaks onto neighbouring routes.

⚠ **Caught on review, after the slice was "done": three enum members had never been raised by any
harness at all** — `SummaryDismissal`, `InvoluntaryPerformance` and `MutualAgreement`. Summary
dismissal is in **FR-HR-182's own list of eight** and carries real behaviour (dismissal *without
notice*, so zero days) that nothing verified. The question that found it was simply "does slice 8
account for all the routes?" — and the answer came from grepping the harnesses for each enum
member, not from re-reading the slice.

**Lesson: "every route has a code path" and "every route has an assertion" are different claims,
and only the second one survives somebody changing the code.** A per-type coverage grep is cheap and
belongs in the closing audit. Now 7 more assertions; every FR-HR-182 type is exercised somewhere.

**Area regression after slice 8: 67 + 44 + 50 + 64 + 75 + 37 + 46 + 47 = 430 assertions, green
twice.** The slice-7 prediction held — slice 1 raises a `ContractExpiry` with a fixed date and still
passes, because those fixtures have no contract for the rule to bite on.

### Slice 9 — the exit reaches the employee record. 2026-08-20, 36/36. **No migration.**

**The slice the area exists for.** `POST /{id}/complete`, from `SettlementApproved` only, applies
the exit to the employee master: status, termination date and reason, active contracts closed, the
open position-history row closed, and `EmployeeRecordUpdatedOn` stamped — the column that has sat on
the entity since slice 1 waiting for something to set it. **Recording an exit and applying it are
two different acts, and only the first had ever been built.**

`EmployeeService.ApplySeparationOutcomeAsync` is deliberately separate from
`TerminateEmployeeAsync`: the latter now *refuses* while a separation is in flight, so the former is
the one legitimate way through.

**The four slice-1 defects, fixed.** Two are behaviour changes for callers outside this area — only
three callers exist, all HR controllers:

1. **An unparseable `TerminationReason` now 400s** instead of writing NULL on a 200. The likeliest
   to affect an existing caller, and the reason knowing why somebody left is FR-HR-090's point.
2. **The direct terminate path refuses while a separation is in flight**, naming the separation and
   its status — using it would bypass clearance, the MD's signature and the settlement review.
   Cancelling the separation releases it again (asserted).
3. **Reinstatement no longer erases history**: the prior termination date, reason and notes are
   written into the notes before the fields are cleared.
4. **`CanTerminateEmployeeAsync` answers its own question** rather than returning `true` under a
   comment describing a check nobody wrote.

⚠ **Left deliberately, and documented in the code so it does not read as an oversight:**
reinstatement does **not** reopen the contracts termination closed. A reinstated employee needs a
new contract with its own start date; reviving the old one would make the record claim continuous
employment across a gap.

⚠ **A harness lesson worth more than the fix: 13 assertions failed reading `undefined`, and the
product was correct throughout.** `GET /api/hr/Employees/{id}` returns the **summary** DTO —
`staffStatus` and `isActive`, no termination fields at all — while `GET /{id}/details` carries them.
I read the wrong endpoint and briefly believed I had found a defect in my own slice. **Probe the
live response before diagnosing from source**; one curl settled what several minutes of reading
had not.

⚠ **Recorded, not changed:** `terminate` and `reinstate` answer `{ message: "Employee terminated" }`
and **discard the full DTO the service builds**, so a UI must make a second call to see what it just
did. A real wart of the "discarded DTO" family, but changing the response shape would break any
caller reading `.message`, and that endpoint is not this area's contract to change.

**Area regression after slice 9: 67 + 44 + 50 + 64 + 75 + 37 + 46 + 47 + 36 = 466 assertions, green
twice.**

➡ **Slice 9b:** wire discipline into this pipeline (decision D1) and repair the orphans. Split from
slice 9 on purpose — a data repair should run against a mechanism already proven, not alongside one
being built.

### Slice 9b — the disciplinary route joins the pipeline, and the orphan repair

⚠ **The 29 orphans are all test data.** The dry run named them: every one is an area-9 harness
fixture (`A9Ver Actor…`, `A9V…`), and a follow-up query confirmed **0 real employees** among them.
The missing propagation was genuine and is now built; the *impact* was not what this plan claimed
for eight slices. Recorded at §3.2(a) as a correction rather than quietly edited away, because the
mistake is more instructive than the number: **a join count tells you about rows; it takes one more
predicate to learn whether they are about people.**

**Built:** `StaffDisciplineTerminationService.RecordAsync` now raises the same `EmployeeSeparation`
every other route creates, and `POST /repair/disciplinary-orphans` (Admin, `dryRun=true` by
default) finds decisions that never produced an exit.

Three deliberate choices in the wiring:

- **After the save, and not fatal.** The disciplinary decision is recorded and must not roll back
  because the exit could not be opened. The service returns `null`, logs, and the repair picks it
  up. The only place in this area that swallows an exception, and the code says why.
- **No effective date is invented.** The disciplinary record carries no termination date field;
  fabricating one would put a made-up last day on somebody's employment record.
- **The repair raises exits; it does not terminate anybody.** Every one is a Draft that still goes
  through clearance, the MD's signature and the settlement review. A repair that skipped the
  controls would be a worse defect than the gap it closed.

⚠ **The harness caught the dry run lying, which is the finding of this slice.** It promised 29
raises and delivered 21: the area-9 fixtures reuse subjects, so two decisions against one person can
only ever produce one exit, and the dry run did not model the "already has an exit in flight" rule
the real run enforces. Both modes now count identically, including exits raised earlier in the same
run. **A dry run that misreports what will happen is worse than none, because it is believed** —
and being believed is the entire reason the endpoint defaults to `dryRun=true`.

⚠ **Verified through area 9's harness, not a reimplementation of it.** Reaching a recordable
termination means walking the natural-justice ladder — written query, acknowledgement, proposal,
and a decision confirmed by a **workflow-engine-assigned** approver. Four attempts at rebuilding
that here each fixed a different invented payload before I stopped. Running `hr-discipline`'s own
`run-slice4.mjs` instead: disciplinary terminations 29 → 30, separations from discipline 21 → 22,
newest row Draft / SummaryDismissal / no effective date. **When another area already has a green
harness that drives its ladder, use it as the driver rather than writing a worse one.**

⚠ **A vacuous green to be aware of.** With the 29 now repaired, `raisedCount === wouldRaise.length`
is `0 === 0` on this database. The meaningful run was the first one. Anyone re-verifying the raise
path needs fresh orphans — run area 9's harness against employees who already have an exit in
flight.

**Area regression after slice 9b: 67 + 44 + 50 + 64 + 75 + 37 + 46 + 47 + 36 + 13 = 479 assertions.**

### Slice 10 — the reminder sweep (FR-HR-111). 2026-08-20, 37/37

Migration `20260820163509_AddSeparationReminderEngine` — two tables, same shape as the five reminder
engines already in the system (SHE, movements, discipline, travel, probation). This is the sixth.

**Five kinds.** FR-HR-111's two — a retirement or contract expiry approaching with no separation
raised — plus three from the pipeline: a clearance with mandatory lines unanswered, a settlement
with Internal Audit, and **a settlement approved but never completed**.

⚠ **That last kind is why the slice is worth having.** Approved, paid, never completed means the
employee is *still recorded as active* with a finished exit behind them — the exact shape of the
defect this area was opened on. Slice 9b built a repair that runs after the fact; this makes the
same situation visible the next morning.

**The dedupe key is kind + subject + due date + escalation tier**, and the tier is in the key
deliberately: an item that ages into the next tier raises a **new** reminder rather than repeating a
stale one. Tiers: on the horizon (>30 days) → due this month → overdue → overdue by more than a
month with nobody acting.

**Two assertions that matter more than the count:**

- **Acting on the thing stops the reminder.** Completing the abandoned settlement removes it from
  the sweep; raising the retirement silences the retirement reminder. A reminder engine that nags
  after the work is done gets ignored, and then it is decorative.
- **The second sweep suppresses everything** — `suppressedAsDuplicate == candidatesFound`. If the
  dedupe key were wrong in any way, that is where it would show.

**The FR-HR-185 role gap, made audible.** With nobody holding `TDC_INTERNAL_AUDIT`, finalised
settlements accumulate `SettlementAwaitingReview` reminders and escalate through the tiers. The
control holding rather than failing open is correct; this is what stops it reading as a stuck queue.

**On FR-HR-111's "30 days":** the sweep uses the tenant's lead days (365 retirement, 60 contract
expiry), both wider than 30, so the requirement is met from policy rather than a second hard-coded
threshold that could drift from it.

**Area regression after slice 10: 67 + 44 + 50 + 64 + 75 + 37 + 46 + 47 + 36 + 13 + 37 = 516
assertions.**

⚠ **Owed, and deliberately not in slice 3: workflow-engine wiring.** The decision rule lives in the
service because the engine cannot express the procedural split. That is fine for the API, but W1
says never build a bespoke HR approval **UI** — so the engine must be wired before the screens
land in slice 11, giving the MD one inbox rather than a separate place to sign exits. Recorded here
so it is not discovered at UI time.

---

## Slice 12 — exit analytics, and the exit interview

**⚠ The exit interview is not an FRD requirement.** "Exit interview" appears nowhere in the
specification, and this area deliberately declined to invent it. It was added at the client's
explicit request during this slice. It is recorded here so that nobody later reads it as a
requirement traced to a paragraph that does not exist.

It is also what makes the "themes" half of the analytics possible. Without it, the only record of
why somebody left is the reason **the organisation** wrote down. `ExitInterviewReason` is therefore
a separate enum from `TerminationReason` on purpose: the organisation records "resignation", the
employee says whether it was the pay or the manager. Same exit, two different facts, and only the
second one tells anybody what to fix.

### Three shapes in the entity that the numbers depend on

- **Every rating is nullable and runs 1–5.** Null means the question was not asked; zero would mean
  the worst possible answer. A half-finished form must not read as a damning one.
- **`WasDeclined` is a first-class outcome.** People leave angry or at short notice. A record that
  can only express a completed interview forces either an invented one or a blank file.
- **Marking an interview declined clears every answer,** server-side and deliberately. A part-filled
  form later marked declined must not leave ratings behind to be averaged as though a real
  interview had produced them.

### The vacuous green this slice nearly shipped

⚠ **Worth keeping, because it is the failure mode the double audit exists to catch.** The first
version of `run-slice12.mjs` built its interview fixtures at **Approved** and guarded the average
assertions behind `if (average != null)`. The themes query counts only **Completed** separations,
dated by effective date — and the standard fixture body exits on 2026-08-31, which is in the
future. So every number came back zero, every average came back null, and the two assertions that
mattered most **never ran at all**. 42 of 42 passed and proved nothing about the averages.

The rebuilt fixtures are carried the whole way to Completed, pinned to a past effective date, and
the window is narrowed to that date. The shape is chosen so that **running the file twice leaves
every expected number unchanged** — duplicating identical data does not move a mean, a coverage
ratio, or a decline rate — which makes the repeat run a real test rather than a formality.

The sharp assertion: of two conducted interviews, only **one** is asked about management, and the
answer is **5**. If an unasked question were counted as zero the mean reads 2.50; as a 1, it reads
3.00. Only **5.00** means the average ran over the answers actually given. The same trick on the
yes/no answers — `wouldReturnPercent` must read 100, not 50.

**A consequence worth stating:** the pinned window must belong to this file alone. A first revision
of the payload probe leaked a fourth fixture onto the shared date, which would have broken the
whole-sets arithmetic and moved the averages on the next run. The probe now has its own date, and
the file says: if the fixture shape changes again, **move the date rather than patch the
arithmetic**.

### Two editorial decisions on the dashboard

- **Coverage before averages.** An average over four interviews out of ninety exits describes those
  four people. The panel leads with how many exits were actually interviewed, and hides the averages
  entirely when none were conducted.
- **Unvalued is not zero.** Settlements carrying lines nobody could value are counted *beside* the
  money, not folded into it: the total is then an understatement of a known size rather than a fact.

`CompletedButNotApplied` — this area's founding defect — is on the dashboard as a number and as a
red banner when it is non-zero.

### One frontend defect found and fixed in this slice

`apiService` returns `response.text()` when there is no JSON content-type, so a **204 arrives as an
empty string**, and `result ?? null` does not catch it — `''` is not nullish. `getExitInterview`
would have handed the screen `''` and rendered an interview that does not exist. The service now
tests for an object.

### Delivered

- `SeparationExitInterview` entity, `ExitInterviewReason` enum, migration
  `20260820175026_AddSeparationExitInterview` (guarded, listed), filtered unique index on
  `SeparationId`
- `GET`/`PUT {id}/exit-interview` — the GET answers **204, not 404**, when none exists: a 404 would
  say the separation was missing, which is a different and more alarming thing
- `GET analytics`, `GET analytics/exit-interviews`
- `/hr/separations/analytics` screen; exit-interview tab on the detail page, gated on Approved
- `run-slice12.mjs` — **62 assertions**, green on two consecutive runs

**Area regression after slice 12: 67 + 44 + 50 + 64 + 75 + 37 + 46 + 47 + 36 + 13 + 37 + 62 = 578
assertions**, all green. (Slice 11 was the screens slice and has no harness of its own — its
endpoints are covered by the slices that built them, and by the content audit in slice 13.)

---

## Slice 13 — the content audit

`run-audit.mjs`, **136 assertions**, green on two consecutive runs. All **14 GET endpoints** read
for CONTENT — every joined name, resolved enum and computed total checked field by field — with
**nothing left unproven**.

### The rule this file is built on

Area 11 shipped a harness green on every endpoint and then failed a post-hoc content audit 20 times
out of 37, because status-code assertions prove the **gate**, not the **feature**. A 200 carrying an
empty string where a name should be is still a 200. So this file reads every endpoint against a
record that genuinely has something to say — one separation carried the whole way, with documents,
an amount left owing, a settlement through Internal Audit, and an exit interview.

The second trap gets its own machinery: an endpoint returning `[]` passes `Array.isArray` forever.
Where a collection could legitimately be empty, the file records the read as **UNPROVEN** and prints
the list at the end, rather than passing it. Two reads started that way (the analytics window held
no completed exits) and were closed by adding a fixture that exits *inside* the default window.

### One real defect, found by the coverage grep

⚠ The per-type grep flagged `BenefitPayment` and `TaxDeduction` as having no assertion anywhere.
Probing them found that a manually added settlement line took `IsDeduction` **from the caller** and
never checked it against the category:

```
ACCEPTED  category=TaxDeduction isDeduction=false   netPayable 0 -> 5000
```

A line categorised as tax **increased** the leaver's final pay by its amount. The update path was
worse — `UpdateSettlementLineDto` carries no category at all, so a *system-generated* travel-advance
recovery could be flipped into an earning after the fact.

Fixed by deriving the permitted direction from the category (`DirectionOf` /
`RequireDirectionMatchesCategory`). `PensionRelated` is deliberately left to the caller: a pension
line can be a payout owed to the leaver or a contribution owed by them, and the category cannot say
which. No schema change.

### Also closed

- **`TravelAdvanceRecovery`** — the only cross-module generator, and never once exercised. Now
  proven end to end: two disbursed advances, one in the settlement currency and one in USD. The
  foreign one is **not converted** — it is recorded uncomputable with the figure in the text, and
  it **blocks finalisation**. Finance owns conversion, and its rates are known to be inverted.
- **`ClearanceItemStatus.NotApplicable`** — one of the three answers `IsSettled()` accepts, so it
  satisfies a *mandatory* line. Remove it from that list and nothing would have failed.
- **The clearance-kind → settlement-category mapping**, three reachable branches, each identified by
  a distinct amount so a crossed mapping shows as the wrong number and not merely the wrong label.
  The fourth, `PropertyRecovery`, is **unreachable**: only three kinds carry money, and a property
  line is refused an amount outright. The rule that makes it unreachable is asserted instead.

Remaining uncovered enum members are pick-list values with no behaviour: `EndOfInternship`,
`BenefitPayment`/`TaxDeduction` as *generated* categories (they exist only as manual lines, now
asserted), and `PropertyRecovery`.

### Four vacuous greens caught in the audit's own code

Worth recording, because every one of them **passed**:

1. **`isRecovery` — a field invented rather than read from the DTO.** Every line returned
   `undefined`, so deductions summed to zero and "net is earnings less recoveries" quietly reduced
   to "net is the sum". The field is `isDeduction`. *A field name guessed from a concept rather than
   read from the DTO is exactly the fiction that type-checks.*
2. **Even fixed, the fixture had cleared every clearance line**, so there was no deduction in it at
   all. One line is now deliberately left owing.
3. **The property-amount rejection was asserted after the settlement was prepared**, where it still
   returned 400 — but for an unrelated reason ("clearance lines can only be answered while clearance
   is in progress"). A bare status-code assertion would have passed while testing nothing.
4. **`get(path, {query})` — a second argument the function does not take.** Silently ignored, so the
   read returned an unfiltered default page. It broke slice 9b's register assertion (25+25 rows,
   none disciplinary, against a product that was correct) and was latent in the audit's own register
   read. `get()` in this harness takes a path only.

### And one harness fragility repaired

Slice 9b asked whether any row in the first 200 was disciplinary. That passed only while the
disciplinary exits stayed among the 200 most recent; later slices added enough fixtures to push them
off. It now **filters** instead of paging. Two related facts it pinned down while failing:
`IsDisciplinary` is `DisciplinaryActionId != null` — "raised by the discipline module", **not** "is
a dismissal", so a hand-raised dismissal is correctly unflagged — and the case id is a **detail**
field: `EmployeeSeparationListDto` has 18 properties, all of them assigned, and that is not one of
them.

### ⚠ The environment trap that made a correct build look broken

`dotnet run` ignores `ASPNETCORE_ENVIRONMENT`: `launchSettings.json` sets `Development` and wins.
Started that way, every refusal comes back **500 with a stack trace** instead of 403, and slice 3
failed four authorization assertions against code that was working perfectly. Run the built DLL, or
pass `--no-launch-profile`, and confirm `Hosting environment: Staging` in the log before trusting a
run. Staging also has no user-secrets, so the JWT key must be passed in.

**Area regression after slice 13: 67 + 44 + 50 + 64 + 75 + 37 + 46 + 47 + 36 + 18 + 37 + 62 + 136 =
719 assertions**, all green, twice.

---

## Slice 13 — the content audit

`run-audit.mjs`, **136 assertions**, green on two consecutive runs. Every one of the area's **14 GET
endpoints** is read against a record that really has something to say, and every field that should
have resolved is checked for having resolved. Status codes prove the gate; this proves the feature.

### The second trap: a green that proves nothing

An endpoint returning `[]` passes `Array.isArray` forever. So where a collection can legitimately be
empty, the audit records the read as **UNPROVEN** and prints the list at the end rather than passing
it. An audit that cannot tell "verified" from "did not fail" is not an audit.

Both initial unproven reads were then closed rather than left: the analytics window counts
*completed* exits by effective date, and slice 12's fixtures exit in June 2025 — outside the default
twelve months. A second fixture that completes *inside* the window makes `byRoute` and the interview
averages real.

### Three vacuous greens in my own audit, found and fixed

- **`isRecovery` was a field I invented rather than read from the DTO.** Every line returned
  `undefined`, deductions summed to zero, and "the net is earnings less recoveries" quietly reduced
  to "the net is the sum". It is `isDeduction`. *A field name guessed from a concept rather than
  read from the DTO is exactly the fiction that type-checks.*
- **Even fixed, the fixture cleared every clearance line**, so the settlement had no deduction at
  all and the arithmetic still proved nothing. One line is now deliberately left owing.
- **The property-line rejection was asserted after the settlement was prepared**, where it still
  returned 400 — but for an unrelated reason ("clearance lines can only be answered while clearance
  is in progress"). A bare status-code assertion would have passed on the wrong rule.

### The per-type coverage grep

Every enum member greped against every assertion in the harness directory. Most misses are
pick-list values with no behaviour and are left alone deliberately. Four carried behaviour:

- **`ClearanceItemStatus.NotApplicable`** is one of the three answers `IsSettled()` accepts, so it
  satisfies a *mandatory* clearance line. Remove it from that list and nothing would have failed.
- **The clearance-kind to settlement-category mapping** decides what a recovery is *called* on a
  final statement. Only one of its branches was exercised. Three are now, each by a distinct
  amount so a crossed mapping shows up as the wrong number, not merely the wrong label.
- **`PropertyRecovery` is unreachable** — only loans, salary advances and payroll recoveries carry
  money, and a property line is refused an amount outright. It is a defensive default, not a route.
  The honest test is the rule that makes it unreachable, which is what is asserted.
- **`TravelAdvanceRecovery`**, the one cross-module generator, had never been produced. It is now,
  including the branch where a USD advance on a GHS settlement is left **unvalued with the figure
  in the text** rather than converted at a rate this module invented — and blocks finalisation.

### The defect the audit found

`BenefitPayment` and `TaxDeduction` had no assertion anywhere. Probing them turned up a real one: a
manually added settlement line took `IsDeduction` **from the caller**, unchecked against its
category.

```
ACCEPTED  category=TaxDeduction isDeduction=false   netPayable 0 -> 5000
```

A line categorised as tax **raised the leaver's final pay by its amount** — they would have been
paid their PAYE. The update path was worse: `UpdateSettlementLineDto` carries no category, so a
*system-generated* travel-advance recovery could be flipped into an earning after the fact.

Fixed by deriving the permitted direction from the category. `PensionRelated` is deliberately left
to the caller — a pension line can be a payout owed to the leaver or a contribution owed by them,
and the category alone cannot say.

---

## Slice 14 — the notice decision becomes its own act

**The blocker the engine wiring hit.** W1 forbids a module-specific approval UI, so the FR-HR-092
approval belongs on the generic workflow engine — but that engine's approve action is
`onApprove(comments, checklistResponses, signature)`. This area's approval also settled the
**notice**: waive the balance, or pay it instead of working it. That is money, and there is nowhere
in a comment to put it.

*Decision taken with the user:* the notice decision becomes **its own act**, gated before the
settlement. Approval becomes a clean yes/no the engine can carry; the money decision stays explicit,
separately audited, and correctable without re-approving.

### Why a date, and not just the two bools that already existed

`IsNoticeWaived == false && IsNoticePaidInLieu == false` is **two different facts wearing the same
clothes**: "we looked, and neither applies" and "nobody has looked". FR-HR-184 turns on the
difference, because a settlement prepared against an unserved notice with no decision recorded does
not produce a *wrong figure* — it produces **no notice-pay line at all**, and a missing line on a
final statement is invisible in a way a wrong number is not.

So `NoticeDecisionOn` is the stamp that makes "neither" sayable, `RequiresNoticeDecision` says it
the way a screen needs to hear it, and `PrepareSettlementAsync` refuses while it is null and notice
was left unserved, naming the days owed. Both columns are nullable, which is also the back-fill
answer: every existing separation is correctly described as "no decision recorded".

### The deadlock the harness found on its first run

The gate refuses the settlement at `ClearanceCompleted`. The first version of the new rule allowed
the decision only at `PendingApproval` or `Approved`. So a separation that reached clearance still
undecided could **neither settle nor decide** — nothing could move it, ever. And clearance is
exactly where such a record naturally ends up, because nothing forces the decision earlier.

The real constraint was never a status: it is **before the settlement exists**, because notice pay
is computed when the settlement is prepared. The window now runs from `PendingApproval` through
`ClearanceCompleted` and closes on the settlement existing — with a second rule saying so out loud:
once prepared, amend the settlement *line*, not the decision behind it, or the statement ends up
disagreeing with the decision it was computed from.

**And the order of the two questions matters.** Asked second, the settlement check lost to the
status check, so a separation at `SettlementPending` answered "the decision is taken up until the
settlement is prepared" when the truthful answer was "it already was". `StartClearanceAsync` carries
the same note for the same reason: order the questions so the more specific one answers first.

### One diagnosis worth not repeating

Two 403 assertions failed as 500s and looked like authorization defects. They were the **run
environment**: `dotnet run` applies `launchSettings.json`, which overrides `ASPNETCORE_ENVIRONMENT`,
so the API came up in Development where the developer exception page turns every uncaught
`UnauthorizedAccessException` into a 500 with a stack trace. Controller-caught exceptions give clean
400s in any environment, which is why only the record-dependent authority checks failed and the rest
of the run looked healthy. `--no-launch-profile` plus the JWT key fixes it — both already recorded
in [[hr-harness-run-environment]], and not followed.

### Delivered

- `NoticeDecisionOn` / `NoticeDecidedById` on `EmployeeSeparation`; migration
  `20260820235451_AddSeparationNoticeDecision`, guarded and listed
- `POST {id}/notice-decision`, plain `[Authorize]` with `RequireDecisionAuthority` in the service —
  whoever may sign this separation may settle its notice
- `ApproveAsync` reduced to a yes/no plus a comment
- The settlement gate, and `RequiresNoticeDecision` on the detail DTO
- The detail screen's notice panel; slice 3 and slice 5's assertions moved with the rule
- `run-slice14.mjs` — **35 assertions**

**Area regression after slice 14: 67 + 44 + 58 + 64 + 75 + 37 + 46 + 47 + 36 + 18 + 37 + 62 + 35 +
136 (audit) = 762 assertions**, all green.

**Still owed:** the engine wiring itself (slice 15) — entity type, adapter, entity context, display
resolver, the definition, and swapping the detail page onto `<WorkflowApprovalActions>`. The blocker
that stopped it is now cleared.

---

## Slice 15 — FR-HR-092 moves onto the workflow engine

The last thing this area owed, recorded since slice 3. W1: never build a module-specific approval
UI. The Managing Director signs a great many things and should sign them all in one inbox.

The recipe held — entity type and seed, status adapter (auto-discovered), routing context, display
resolver, `entityTypeMapping.ts`, and the detail page swapped onto `<WorkflowApprovalActions>` with
a workflow tab. Three things were judgement rather than recipe:

**The adapter owns three states and nothing after.** `Draft → PendingApproval → Approved/Rejected`
is a single-writer approval lifecycle. Everything from `ClearanceInProgress` on — the clearance run,
the settlement, Internal Audit's review, completion — has several writers and records that somebody
*did the work*, not that anybody approved it. Those stay direct actions, which is why the
settlement-review buttons remain on the page.

**No new enum member.** `SeparationStatus` already carried `PendingApproval`, `Approved` and
`Rejected` meaning exactly what the engine's outcomes mean. Look before adding one.

**Two gates on approve, in this order.** `RequireDecisionAuthority` (FR-HR-092 — a rule about the
RECORD) runs first, so a refusal explains itself in terms of the separation rather than answering
the generic "you are not assigned as an approver", and so the rule holds even if the definition is
missing or wrong. The engine's `CanUserApproveAsync` (a rule about the STEP) runs second.

### ⚠ The engine cannot route per record — cross-module defect 3

FR-HR-092 wants a procedural absence termination signed by HR and every other exit by the Managing
Director. That is per-record routing, and it does not work:
`CreateStepsAndTransitionsAsync` stores a transition's condition as
`JsonSerializer.Serialize(transitionDto.Condition)` — the whole `WorkflowConditionDto` — into a
`string?` column, and `WorkflowEngine` then hands that JSON blob to an evaluator that expects a bare
expression. The condition is never seen, nothing errors, and the branch taken tracks **priority
alone**.

Measured both ways round:

| conditional branch priority | record | expected | actual |
|---|---|---|---|
| below the default | `isProcedural = true` | HR approval | Managing Director approval |
| above the default | `isProcedural = false` | Managing Director approval | HR approval |

Recorded in `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` §3 rather than fixed — the workflow
engine is shared infrastructure and four call sites would need changing together. The conditional
definition is preserved behind `publishSeparationDefinition`'s `useConditionalRouting` flag so
whoever fixes it has a ready test.

**The workaround, and its honest cost.** The definition names *both* authorities on one approval
step, and the service decides. The requirement is still met — no exit can be signed by the wrong
authority, which is what slice 15's 403 assertions hold down. What is lost is only the assignment:
an HR officer sees exits in their queue that the service will refuse them. That is the right way
round to be wrong. Naming the MD alone would be worse — the engine would then refuse HR the
procedural terminations FR-HR-092 says they may sign.

**And a detail worth keeping:** `AdvanceFromStepAsync` picks
`validTransitions.OrderByDescending(t => t.Priority).FirstOrDefault()` — **highest priority wins**,
not lowest. Undocumented, and it reads as "1st, 2nd" to anyone authoring a definition.

### Two defects the harness found in my own work

- **The hand-off happened before the save.** `SubmitAsync` computed `IsProcedural` and called the
  engine before persisting, so the engine's routing context read the record back with the old
  value. Fixed by saving the record's own facts before handing off, which is the right dependency
  regardless of the engine defect above.
- **`preventInitiatorApproval` was the wrong control for this entity, and I had set it true.** It
  guards "nobody approves their own request", where the initiator is the BENEFICIARY. An exit is
  raised by HR about somebody else — the initiator gains nothing, and the person with the real
  conflict is the SUBJECT, who is barred by role long before the engine is consulted. Left on, it
  silently rewrote FR-HR-092's "HR may approve a procedural absence termination" into "two HR
  officers must". Slice 3 caught it by failing. Now false, with the subject-side conflict asserted
  explicitly instead.

### Delivered

- `EmployeeSeparation` in the entity-type catalogue; seeded and verified (160 types registered)
- `EmployeeSeparationWorkflowStatusAdapter` — Pending → `PendingApproval`, Approved → `Approved`,
  Rejected → `Rejected` with the grounds kept, Recalled → `Draft` with the submission stamp cleared
- Routing context (`isProcedural`, type, absence days, and the two DTO-derived flags), display
  resolver naming the PERSON as well as the number, frontend entity-type mapping
- Service drives the engine on submit / approve / reject
- Detail page on `<WorkflowApprovalActions>` + workflow tab; the bespoke approve/refuse buttons gone
- `workflow-definition.mjs` and `run-slice15.mjs` — **35 assertions**, green on two runs

**Area regression after slice 15: 67 + 44 + 58 + 64 + 75 + 37 + 46 + 47 + 36 + 18 + 37 + 62 + 35 +
35 + 136 (audit) = 797 assertions**, all green.

**Nothing is owed on this area now.** The out-of-scope items keep their recorded owners: GL posting
waits for the module-wide Finance sweep, payroll computation belongs to another developer, asset
return needs area 16, and the grievance expansion is its own deferred module.
