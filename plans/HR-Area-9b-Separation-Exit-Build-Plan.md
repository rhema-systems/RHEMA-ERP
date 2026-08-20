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
| 3 | Clearance checklist + the gate | Configurable items, departmental sign-off, `CanTerminate` refusing an incomplete clearance — **FR-HR-091, FR-HR-183** |
| 4 | Approval chain | Workflow engine wiring, MD signature, procedural auto-approval — **FR-HR-092** |
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
