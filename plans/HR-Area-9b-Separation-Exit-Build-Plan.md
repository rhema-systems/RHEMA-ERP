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

➡ **Slice 9b, next:** wire discipline into this pipeline (decision D1) and repair the **29 orphans**.
Split from slice 9 on purpose — a data repair should run against a mechanism already proven, not
alongside one being built.

⚠ **Owed, and deliberately not in slice 3: workflow-engine wiring.** The decision rule lives in the
service because the engine cannot express the procedural split. That is fine for the API, but W1
says never build a bespoke HR approval **UI** — so the engine must be wired before the screens
land in slice 11, giving the MD one inbox rather than a separate place to sign exits. Recorded here
so it is not discovered at UI time.
