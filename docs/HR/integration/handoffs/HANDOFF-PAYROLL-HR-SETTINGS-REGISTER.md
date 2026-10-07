# Hand-off to the Payroll module owner — the HR ↔ Payroll settings register

**Raised by:** HR module work, 2026-09-17, at the end of the leave module's closure build.
**Status:** a register, not a change request. **Nothing in here has been built.**
**Severity:** no defect and no data loss. Four places where the same concept is modelled twice.

This is a self-contained report — nothing in it requires reading HR's plans or code. It exists
because the HR side has reached the point where it needs to say, precisely, which system is the
authority for each shared idea. Where the answer is already settled, this says so and asks for
nothing.

---

## 1. The part that already works, and should not change

**Payroll owns the pay-component master, and HR mirrors it.** That is correct, it is built, and
this register does not propose touching it.

`PayComponentProjectionService` **pulls** `PayrollComponent` into HR's `PayComponent`:

```
PayrollComponent.Code / Name        →  PayComponent.Code / Name
PayrollComponent.ComponentType      →  PayComponent.ComponentType
PayrollComponent.CalculationType    →  PayComponent.CalculationBasis
PayrollComponent.Amount | Rate      →  PayComponent.DefaultAmount
PayrollComponent.Taxable            →  PayComponent.IsTaxable
PayrollComponent.IncludeInGross     →  seeds AffectsGrossPay (on create only)
PayrollComponent.IsActive           →  PayComponent.IsActive
```

Payroll rows are read `AsNoTracking` and **never written**. Mirrored rows carry a provenance marker
in their description, so the projection only ever updates or deactivates its own rows — anything HR
defined before the bridge existed is left alone, which matters because `BenefitPolicy` holds live
foreign keys to them (as `LeaveTypeAllowance` did until leave settings audit 2 dropped it, 2026-09-27).

**What HR adds on top**, because payroll does not model it: effective dating, pensionability, the
contributes-to-gross flag, and the links from a component to a benefit policy. *(The links to a leave
type went with HR's encashment rate, 2026-09-27: Finance values leave cashed in.)*

> **So, to answer the question that prompted this register directly:** *allowances are defined in
> payroll.* HR does not maintain a competing list and should not start. What HR is missing is a
> **screen** for the leave-type → component link, which is an HR screen, not a payroll one.

---

## 2. ⚠ Four places where the same concept exists twice, with no bridge

This is the substance of the register. In each case both models are **live** — each has endpoints
on `PayrollController` and is maintained through them — and neither knows about the other.

### 2.1 Public holidays — two lists

| | HR | Payroll |
|---|---|---|
| Entity | `PublicHoliday` + `HolidayCalendar` | `PayrollHoliday` |
| Shape | named, date-range, belongs to a calendar, `IsActive`, `ObservanceType`, `SubstitutionDate`, `AttractsHolidayPay`, `HolidayPayMultiplier` | a flat `HolidayDate` + `Description` |
| Read by | leave day-counting, attendance, HR's statutory disciplinary clocks — all through `IHrWorkingDayCalculator` | `PayrollService` |

**Consequence:** the day leave does not charge and the day payroll pays a premium for are two
different records, maintained on two different screens, that can disagree without anything noticing.

⚠ **And one behaviour changed on the HR side on 2026-09-17** that narrows the assumption payroll may
be holding: a holiday whose `ObservanceType` is **Optional** is now treated by HR as a **working
day** — the office is open and leave taken on it is chargeable. Only *Mandatory* and *Substitute
Day* close the tenant. If payroll's holiday logic assumes every holiday row is a non-working day,
that assumption is now narrower than it was.

**HR's recommendation:** one list, and it should be HR's, because HR's carries the fields the
statutory clocks and leave both need. Bridge it the way components are already bridged — a pull, in
whichever direction you prefer to own. **The two pay fields (`AttractsHolidayPay`,
`HolidayPayMultiplier`) are already stored on HR's holiday and labelled on the form as payroll's to
apply** — HR stores them and reads neither.

**Business closures are days off too (added 2026-10-05, company-schedule final closure, lane 1d —
decision D-15c).** HR records the days the company, a site or an organisation unit is shut
(`BusinessClosure`), each with **"Staff are paid" (`IsPaidClosure`)**. Payroll has no counterpart and
reads none of them. Since lane 1 they are days off for leave and the statutory clocks, like a
holiday — a company-wide one for everybody, a site or unit one for the people it covers. A *partial*
closure is a working day.

- **What payroll needs to decide:** what an **unpaid** closure day is worth on a payslip. HR applies
  no pay rule, as with unpaid leave (§ 3, item 1).
- **Where to read it** — one place, for the employees and dates you ask about, with the closure, the
  pay flag and whether the day is worked:
  - in process: `IHrClosureCalendar.GetClosureDaysAsync(tenantId, employeeIds, from, to)`;
  - over HTTP: `GET /api/CompanySchedule/closures/employee-days?employeeIds=…&from=…&to=…`
    (`HR.Company.Read`; up to 500 employees and a year at a time).

  It is a pull: HR pushes nothing into payroll.

### 2.2 Non-working days — a configurable week, on one side only

| | HR | Payroll |
|---|---|---|
| Model | **Saturday and Sunday, hard-coded** in `HrWorkingDayCalculator` | `PayrollNonWorkingDay` — a day code, a description and an **`OvertimeRate`** |
| Read by | every leave and statutory-clock calculation | `PayrollService` |

**Consequence:** a tenant whose working week is not Monday–Friday can configure that in payroll, and
**HR will not see it**. Leave would charge a Saturday the company does not work, and the appeal
clocks would count it.

HR's calculator has a comment saying that if a tenant ever needs a different working week, the place
for it is a tenant policy flag — not the per-employee schedules. **Payroll already has that flag.**
It is the obvious candidate for the shared source.

### 2.3 ⚠ Leave entitlement — two rulebooks

This is the one with the largest potential to embarrass both modules.

| | HR | Payroll |
|---|---|---|
| Entity | `LeaveType` + `LeaveCategoryAllocation` | `PayrollLeaveSetup` + `PayrollLeaveSetupDetail` |
| Shape | leave type → sub-types → allocations **by staff level, effective-dated**, optionally scoped to a sub-type; plus eligibility rules and accrual policies | category / category-detail → **days**, by **service band** (`ServiceFrom` / `ServiceTo`), sequenced |
| Maintained at | Administration → HR → Leave Types | `PayrollController` leave-setup endpoints |

Both answer *"how many leave days does this person get"*, by different rules, from different
screens, and **neither reads the other**.

⚠ **I have not traced whether a pay run actually consumes `PayrollLeaveSetup`** — I can see the
endpoints and the table, not the downstream use. **That is the first question for you**, because the
answer changes what this is:

- if **nothing consumes it**, it is a legacy-import artefact and should be retired, and the
  `LegacyCompanyCode` column on it suggests that is likely;
- if **something consumes it**, the two rulebooks are live simultaneously and one of them is
  producing numbers the other contradicts.

**HR's position:** leave entitlement is HR's. HR's model is the richer one and it is what the whole
leave module computes against. If payroll needs an entitlement figure it should read HR's, not keep
its own.

### 2.4 Exchange rates — three candidates

`PayrollExchangeRate` exists, with its own pay-period-scoped rates. Finance has `ExchangeRate`, and
the standing HR↔Finance rule is that HR reads Finance's rather than keeping its own. That leaves
payroll as the only module with a private rate table. Worth one decision, not urgent.

---

## 3. What leave specifically needs from payroll

Items 1–3 are already written up in [`HANDOFF-PAYROLL-LEAVE.md`](HANDOFF-PAYROLL-LEAVE.md); they are
repeated here in one line each so this register is complete on its own. Item 4 is the same question
for the company schedule's closures, which leave now treats as days off (§ 2.1).

| # | What | Status |
|---|---|---|
| 1 | **Unpaid leave produces no deduction.** `LeaveType.IsPaid` is display-only. HR records who was away and for how many chargeable days; what a day of unpaid leave is worth is payroll's | open |
| 2 | **Holiday pay.** `AttractsHolidayPay` and `HolidayPayMultiplier` are stored by HR and read by nothing | open |
| 3 | ⚠ **The daily-rate basis.** Leave encashment uses *(basic + linked allowances) ÷ 22*; the separation settlement uses *monthly × 12 ÷ 365*. On GHS 6,000/month those differ by about **38%** | **blocked on TDC** — logged as **L-D7** |
| 4 | **Unpaid closure days produce no deduction.** `BusinessClosure.IsPaidClosure` is stored by HR and read by nothing on payroll's side; the read to use is in § 2.1 (added 2026-10-05, D-15c) | open |

---

## 4. What the rest of HR needs, for completeness

| Area | What | Status |
|---|---|---|
| **Employee profile** | `POST api/hr/payroll/employee-profiles` fails with SQL 547 for any employee payroll has no profile for, so a profile can never be created through the API or the screen. HR carries a temporary workaround that should be deleted once this is fixed | **open — cross-module defect #23**, written up in [`HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md`](HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md) |
| **Salary structure** | payroll owns grades and notches; HR mirrors them through a pulled projection | **works** |
| **Benefits and deductions** | the same component bridge as §1 | **works** |
| **Salary change requests** | an approved HR salary-change request applies to HR and to payroll | **works** |
| **Attendance → payroll** | `DaysOnLeave` on the monthly attendance summary is what the payroll export reads. ⚠ **As of 2026-09-17 it is finally populated** — approving leave now writes `OnLeave` attendance days. Before that it was zero for everybody unless a clerk hand-edited each day | **newly working — worth re-checking your export against it** |

---

## 5. The payroll settings surfaces that already exist

Listed so the HR side stops asking for things that are built. Each has endpoints on
`PayrollController`:

parameter sets · components and component rules · **tax bands, reliefs and employee reliefs** ·
pension schemes · overtime policy and ranges · loan policy · bonus policy, rules and exceptions ·
back-pay policy, rules and exceptions · journal mappings · code types and values · holidays ·
non-working days · exchange rates · bank branches · **leave setup** · company profile · business
units · company bankers · grades and notches · employee profiles · salary bases · payment methods ·
employee components · loans and schedules · salary advances · promotion arrears · timesheet
summaries · contribution opening balances and transactions · import batches.

**The statutory machinery is there.** Nothing in this register asks for new tax, pension or
contribution modelling.

---

## 6. What is blocked on TDC, not on either of us

| ID | Question |
|---|---|
| **L-D7** | One daily-rate basis or two? Leave encashment and the separation settlement currently use formulas about 38% apart |
| **L-D8** | Is in-service leave encashment permitted at all? FR-HR-046 says leave is encashed *"only on exit"*, and the module ships an in-service path |
| **L-D6** | What should an unpaid leave type actually do? |

All three are in [`HR-OPEN-QUESTIONS-FOR-TDC.md`](../../programme/HR-OPEN-QUESTIONS-FOR-TDC.md).

---

## 7. What HR is actually asking for

In order, and none of it is urgent:

1. **Answer the `PayrollLeaveSetup` question in §2.3** — is anything consuming it? That single answer
   decides whether there is a real conflict or a dead table.
2. **Confirm the boundary in §1** — payroll owns the component master, HR mirrors it, HR does not
   maintain a competing list. If you disagree, say so before HR's sign-off, because HR's plans
   record it as settled.
3. **Note the Optional-holiday change in §2.1**, which narrows an assumption your holiday logic may
   hold.
4. **Re-check your export against `DaysOnLeave`** (§4) — it used to be zero for everybody and is not
   any more.
5. **Decide, eventually, on one authority per concept** for holidays, the working week and leave
   entitlement. HR's recommendation is in each section; none of it needs doing this month.

**HR will not build a second master for anything in §2**, and will not write to payroll tables. If a
bridge is wanted, HR's preference is the pattern that already works: a **pull**, read-only on the
source side, with a provenance marker so mirrored rows are distinguishable from hand-made ones.

Contact: the HR module owner. Related: [`HANDOFF-PAYROLL-LEAVE.md`](HANDOFF-PAYROLL-LEAVE.md),
[`HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md`](HANDOFF-PAYROLL-EMPLOYEE-PROFILE-CREATE.md),
[`CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`](../CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md) entry #23.
