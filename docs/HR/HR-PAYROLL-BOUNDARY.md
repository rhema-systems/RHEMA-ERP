# HR ↔ Payroll — Ownership Boundary and the Three Bridges

**Created:** 2026-09-02, from decisions taken between 2026-08-02 and 2026-09-02 and re-verified
against the code. **Why this document exists:** the other documents in this folder treat payroll
as an HR sub-area (the Finance sweep's rows 12–21, the reports catalogue's Oracle set, the bulk
catalogue's seven payroll endpoints). It is not. **Payroll is another developer's module.** Every
recommendation in this folder that names a payroll file is a request to that developer, not a
task for HR.

---

## 1. The rule

**Decided by the user 2026-08-02.** HR integrates with payroll read-only and never modifies it.
When HR needs payroll data, HR writes a reader and *pulls*; nothing is pushed from payroll's
save path; no hook is added to payroll's code. If payroll does not expose what HR needs, HR
raises it with the payroll owner — the `docs/HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md` shape
(what is broken · what was proven · what it blocks · what a fix needs).

**Off-limits files** (HR does not edit these, not even to add an attribute):

| Backend | Frontend |
|---|---|
| `src/ErpSystem.Api/Services/HR/PayrollService.cs` | `frontend/src/services/payrollService.ts` |
| `src/ErpSystem.Api/Controllers/HR/PayrollController.cs` | `frontend/src/app/hr/payroll/**` |
| `src/ErpSystem.Core/Entities/HR/Payroll/PayrollEntities.cs` | `frontend/src/app/administration/hr/payroll/**` |
| | `frontend/src/app/reports/hr/page.tsx` (the Oracle report page) |

Known pattern differences in the payroll frontend (its own `fetch` wrapper instead of
`apiService`, a duplicated `PagedResult<T>`, a 9,700-line monolithic setup page) are **recorded,
not scheduled** — no refactor.

**What this means for the other documents here.** Anything that reads "migrate Payroll's
journal posting to `IFinancePostingEngine`", "add `RowVersion` to `PayrollLoan`", "fix the
`PayrollExchangeRate` parallel FX table", "gate `PayrollController`'s 87 bare actions" (cross-module
defect #11), or "the Oracle report page" is the payroll owner's work. HR's job is to have raised it
with evidence.

---

## 2. The three bridges HR owns

All three sit on the HR side, pull from payroll, and are the **only** code that reads payroll
tables. Each was built after the same discovery: two parallel, unconnected table sets modelling
the same thing.

### 2.1 Salary structure — payroll owns grades, HR mirrors them (built 2026-08-02)

- **The parallel sets:** payroll `PayrollGrade`/`PayrollGradeNotch` (2-tier, legacy string codes,
  edited at Administration → HR → Pay & Benefits → Payroll Setup → Grades Setup) vs HR `SalaryGrade`/`SalaryLevel`/
  `SalaryNotch` (3-tier, Guid keys). Every HR FK — position, salary assignment, staff movements,
  recruitment offers, benefit policies, job architecture — points at the HR set, and
  `EmolumentService.GetMonthlyBasicPayAsync` reads `SalaryNotch.SalaryAmount` for basic pay.
- **The bridge:** `SalaryStructureProjectionService` (`src/ErpSystem.Core/Services/HR/`) projects
  payroll → HR one way. No migration, no schema change, HR FKs untouched.
- **Triggers:** reconcile-on-read inside `SalaryStructureService`'s grade/level/notch reads, plus
  `POST api/hr/salary-grades/sync`. A new payroll grade appears in HR on the next HR read, not
  instantly. Accepted.
- **Load-bearing rules:** only projection-owned rows are deactivated (provenance marker
  `"Defined in Payroll"` in `SalaryGrade.Description` — `TdcOrganogramSeeder` creates M1–M5/S1–S3
  grades that positions reference, and without the marker check the projection would deactivate
  them all); withdrawn rows are **deactivated, never deleted** (deleting breaks live HR FKs);
  notches are keyed on the parsed notch number and **never renumbered** (`SalaryNotch` has no
  text field for the payroll code, so renumbering would silently repoint an FK at a different
  amount); duplicate payroll codes are reported and skipped.
- **Writes refused:** `Salary{Grades,Levels,Notches}Controller` keep their GETs; every write
  returns **409** pointing at the payroll screen. Frontend `services/hr/salary-grade.service.ts`
  and `types/hr/salary.ts` are read-only by design.

### 2.2 Pay components — payroll owns the master, HR keeps four attributes (built 2026-08-04)

- **The bridge:** `PayComponentProjectionService`. `ApplyPayrollOwnedFields` is the single method
  that writes a mirrored row; nothing else in the projection touches it.
- **The split is partial, deliberately.** Payroll owns code, name, type, calculation basis,
  default amount, taxability and the active flag. HR keeps:

  | HR-owned field | Why payroll cannot supply it |
  |---|---|
  | `IsPensionable` | no payroll equivalent (SSNIT treatment) |
  | `StatutoryTreatment` | no payroll equivalent |
  | `AffectsGrossPay` | seeded once from `IncludeInGross`, then HR's |
  | `EffectiveFrom` / `EffectiveTo` | **payroll has no effective dating at all** |

  The last row is the reason: `EmolumentService.ComputeEffectiveComponentsAsync` and the leave
  encashment rate filter on the effective window, so a synthesised "today" would silently drop
  components from historical calculations. New mirrors get **2000-01-01**, not the sync date.
- **Mapping:** payroll has 5 component types, HR 3. `EmployeeContribution` → `Deduction`;
  `EmployerContribution` → **skipped with a warning** (employer cost, not money off the payslip).
  Payroll keeps a fixed figure in `Amount` and a percentage in `Rate`; HR has one field whose
  meaning follows the basis.
- **Write rules are provenance-conditional, not blanket:** create → always 409; update/deactivate
  → 409 **only** for mirrored rows (`EmolumentDataSeeder` creates six HR-native components —
  HOUSING, TRANSPORT, MEDICAL, RESP, PAYE, PENSION — payroll has never heard of; a blanket refusal
  would have frozen PENSION's 5.5% rate); `PATCH /hr-attributes` is the one edit allowed on a
  mirrored row. Verified: HR attributes survive a payroll-side rename + rate change + sync.

### 2.3 Payroll membership — two facts, two owners (built 2026-09-02, lane 3f)

- **The requirement:** not every employee is paid through the payroll run, so a tick decides
  whether the salary block is captured at all — and, by the user's call, whether the grade/notch
  placement is captured too.
- **Two facts:** `Employee.IsOnPayroll` / `OffPayrollReason` / `OffPayrollNote` is HR's *should*;
  `PayrollEmployeeProfile.PayrollActive` is payroll's *is*. Disagreements are **reported, not
  reconciled**: `GET api/hr/Employees/{id}/payroll-status` and
  `GET api/hr/Employees/payroll-reconciliation` (`AwaitingPayrollSetup`, `InactiveInPayroll`,
  `StillActiveInPayroll`, `NoPayBasis`), screen `hr/employees/payroll-reconciliation`.
- **The bridge:** `PayrollMembershipService` (`IPayrollMembershipService`) is the **only** place HR
  reaches into payroll. `EnsurePayrollProfileAsync` calls payroll's `UpsertEmployeeProfileAsync`
  **create-only** (the upsert is a replace-set over payment methods/components; re-sending it
  would clobber payroll's own configuration), never deactivates, and is best-effort after the HR
  commit.
- **Rules in `EmployeeService`:** off-payroll + salary/switch = refused with a message, not
  dropped (`ValidatePayrollMembership`); off requires a reason; on→off clears figures and closes
  open salary assignments; off→on clears the reason and enrols only when the payload says so.
  `RequireOnPayrollAsync` gates salary-assignment create/update. Also gated: `StaffMovementService`
  (movements carrying a salary or grade refuse with 422), `EmolumentService.GetMonthlyBasicPayAsync`
  (0 off payroll), `HrLetterRequestService` (salary letter refused), `JobHireService` (flag from
  the offer). **Deliberately not enforced:** on payroll does not require a salary server-side —
  the hire path creates employees before the pay basis is known; the API reports `NoPayBasis`.
- ⚠ **Cross-module defect #23 — payroll's upsert cannot create a NEW profile at all** (an FK cycle
  profile ↔ `DefaultPaymentMethod` inside one insert; proven with and without a supplied payment
  method). So the bridge tries the upsert and, on failure, `CreateMinimalProfileDirectlyAsync`
  writes profile + basis + one bank method in two saves after `_unitOfWork.ClearTrackedChanges()`
  (the failed upsert leaves its Added graph in the scoped tracker). Logged as
  `defect #23 fallback` every time. **Remove the fallback when payroll fixes the upsert.** The
  hand-off is `docs/HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md`.

---

## 3. What HR reads from payroll, and where

| HR reader | Payroll surface | Purpose |
|---|---|---|
| `SalaryStructureProjectionService` | `PayrollGrade`, `PayrollGradeNotch` | Grade mirror (§2.1) |
| `PayComponentProjectionService` | payroll component master | Component mirror (§2.2) |
| `PayrollMembershipService` | `PayrollEmployeeProfile`, `UpsertEmployeeProfileAsync` | Membership (§2.3) |
| `PayrollMembershipService.UpdateMonthlyBasicAsync` (round 3, lane S) | `UpsertEmployeeProfileAsync`, read-modify-write of the full profile | **The one write of a payroll FIGURE from HR**: an APPROVED salary change request sets payroll's monthly basic. Every payment method and component is carried through with its id (the upsert is a replace-set) and the profile is re-read; a changed method set or a basis that did not take is reported as a failure, never success. Ask (d) in the round-3 plan § 6: payroll to confirm the upsert is lossless on a round trip. |
| `AssetsController` `surcharges/payroll-deductions`, `payroll/rental-deductions` | — (HR *exposes* these for payroll to read; neither writes a deduction) | Asset recovery projections |
| `StaffAttendancePayrollExport` | — (a hand-off *record*, no file) | Attendance → payroll period |
| `PayrollReportSnapshot` / payslip snapshots | payroll's own | Read by the ESS portal's payslip view |

Payroll's own reach into other modules: `PayrollEntities.cs:1900 JournalEntryId` → Finance
(the legacy `IJournalEntryService` path, Finance sweep row 15), and `PayrollHoliday` — a second
holiday calendar beside HR's `PublicHoliday`.

---

## 4. Items in this folder that are the payroll owner's, not HR's

| Document / row | What it asks for | Status |
|---|---|---|
| Finance sweep row 15, §3.1, §5 item 1; quick reference | Migrate `PostPayrollJournalAsync` from `IJournalEntryService` to `IFinancePostingEngine` (FIN-INT-001) — now a stated compliance gap per the Finance owner's governance message | **To raise** with the payroll owner; not raised as of 2026-09-02 |
| Finance sweep row 85 | `PayrollExchangeRate` duplicates Finance's `ExchangeRate` master | To raise |
| Finance sweep row 86 / FIN-INT-011 | `PayrollContributionOpeningBalance`/`Transaction` is the SH Fund / PF / ESB sub-ledger the ownerless contract describes | To raise, with the Finance owner in the loop |
| Finance sweep §1 | `EnsurePayrollFinanceAccountsAsync` hard-codes `"GHS"` | To raise |
| Patterns sweep §3.2 / §8 item 2 | `RowVersion` on `PayrollLoan`/`PayrollSalaryAdvance` | To raise |
| Patterns sweep §4.2 | A payroll-period lock (Open/Closed/Locked) | To raise as a design suggestion |
| Bulk catalogue §2 | Seven `api/hr/payroll/.../bulk` endpoints are all-or-nothing replace-sets | Recorded; no ask |
| Reports catalogue §2.1 | The Oracle report set and `reports/hr/page.tsx` | Payroll's; HR's reports hub links to it |
| Cross-module defect **#11** | `PayrollController`'s 87 bare `[Authorize]` actions (`api/hr/payroll`) — open to any internal user; area 26 must not ship until answered | Recorded in `docs/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md` |
| Cross-module defect **#23** | Profile upsert cannot create | Hand-off written 2026-09-02 |
| Import/export catalogue §3.3 | `PayrollHoliday` vs HR `PublicHoliday` — which is authoritative | To decide with the payroll owner before loading a calendar |

---

## 5. Recorded, not fixed

- `Employee.Salary` is **monthly** to `EmolumentService` and the membership bridge, but the salary
  letter prints it as `AnnualSalary`.
- A grade placement must be posted at midnight today or an as-of-date read does not see it until
  tomorrow (harness lesson, lane 3f).
- The `hr-w3-permissions` slice-11 runner reads 133/142 on a database where "Samuel Accounts"
  (an estate fixture) is the first paged employee — its own fixture drift, not a regression.

See also: `HR-FINANCE-ENTITY-SWEEP.md` (rows 12–21, 69, 85–87), `HR-MODULE-INTEGRATION-MAP.md`
row 30, `docs/HR-FINISH-PLAN.md` § Lane 3f.
