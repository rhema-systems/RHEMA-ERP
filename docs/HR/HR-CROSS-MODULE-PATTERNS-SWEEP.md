# Cross-Module Patterns Sweep — Reusable Practices for HR

**Generated:** 2026-08-31
**Scope:** Finance, Procurement, Inventory and Projects — the four most built-out modules in
this ERP — reviewed for engineering and business patterns that the HR module
(`src/ErpSystem.Core/Entities/HR/**`) has not yet adopted, or has adopted only partially.
**Purpose:** A companion to the HR↔Finance sweep — where that document asks *"which HR entity
carries money and where should it eventually post,"* this document asks *"what has the rest of
the system already solved that HR is re-solving badly, or not solving at all."*

---

## 0. How this relates to the other HR docs in this folder

| Document | Question it answers |
|---|---|
| [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) | Which HR entities carry money, and what Finance surface should they reach? |
| [`HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`](HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md) | One-page briefing distilling the Finance sweep + backlog for a new session. |
| **This document** | What reusable engineering/business patterns exist elsewhere in the codebase that HR should copy, and where is HR's own version of something (concurrency control, workflow adapters, notifications) thinner than the rest of the system? |

Several findings below **overlap** with the Finance sweep's open decisions (e.g. the budget
commitment lifecycle, external-payee modelling) — where that happens, this document says so
explicitly rather than repeating the analysis.

**Methodology note:** every specific claim below (file existence, entity/field names, adapter
coverage counts) was checked directly against the repository after the initial research pass;
a small number of over-stated claims from that pass (e.g. exact workflow-adapter coverage) were
corrected against actual file counts. Where a recommendation is a *suggested new entity*, it is
labelled as such — it does not exist in the codebase today.

---

## 1. Executive summary

**What HR already does as well as, or in line with, the rest of the system:**
- Multi-tenancy (`TenantEntity` + query filters) — complete, no gaps found.
- Background/scheduled-job infrastructure (`BackgroundService` + manual-run endpoints) — HR's 7+
  reminder engines follow the same pattern as Finance/Procurement/Maintenance.
- Controlled document upload / central DMS (`IHrControlledDocumentService`) — HR is in fact one
  of the **heaviest adopters** in the whole codebase: confirmed wired into 30+ HR controllers
  (Assets, Awards, Leaves, Separations, Grievances, Discipline, Medical, Succession, etc.).
- The workflow engine (`WorkflowInstanceId` + `IWorkflowStatusAdapter`) — HR has **15 dedicated
  adapter files** (Leave, Assets, Discipline, Separation, Movement, Probation, PIP, Performance,
  Attendance, Requisition, JobOffer, JobArchitecture, Proposal, StaffTravel, Succession) — broader
  coverage than the first research pass suggested.

**What is genuinely thin or missing, confirmed by direct file inspection:**
1. **Reminder engines run, but notify nobody.** Confirmed: none of the ~9 HR reminder
   background services or their underlying sweep services reference `INotificationService` or
   `IEmailService` anywhere. Probation expiries, separation deadlines, asset returns and
   disciplinary case deadlines are computed and logged, never sent.
2. **Optimistic concurrency (`RowVersion`/`[Timestamp]`) is rare.** Confirmed count: **12
   entities** across the whole HR module carry a `RowVersion` (5 in Training, 5 in Recruitment, 1
   in Position Vacancy, 1 in Staff Requisition). Payroll, Leave, Benefits, Medical, Assets,
   Discipline, Separation, Awards, and **Safety/SHE (confirmed 2026-08-31: zero of its 60+
   entities have a `RowVersion`, including approval-bearing ones like `ShePermitToWork` and
   `SheRiskAssessment`)** — the highest-traffic, multi-editor entities — have **none**.
3. **No HR-specific semantic audit trail.** The generic EF interceptor logs every HR entity
   change automatically (this part works), but unlike Finance's `IFinanceAuditService` (used in
   20+ Finance services), HR has no equivalent to record *why* a salary changed, a disciplinary
   fine was imposed, or a bank detail was corrected in business terms.
4. **HR doesn't use Finance's reporting/export framework — it has three of its own instead.**
   Finance's `IFinanceReportExportService` (backed by
   `docs/backend-reporting-export-presentation-foundation.md`) is a real, working framework HR
   does not consume. That much stands; corrected on 2026-08-31: HR's own payroll reports
   (BankSchedule, TaxSchedule, PensionSchedule, Payslip) are **not** unimplemented enum values —
   they have real service methods and a working Oracle-crosswalk-driven frontend. The actual gap
   is that HR has three uncoordinated reporting patterns of its own with no single reports hub.
   Full inventory and recommendation: [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md).
5. **Budget entities with no commitment lifecycle.** `ManpowerBudget`, `TrainingBudget`,
   `AwardBudget`, `StaffTravelBudget` each track `BudgetAmount`/`SpentAmount` themselves with no
   reserve→consume→release mechanism — Procurement's `IFinanceBudgetCommitmentService` (confirmed
   registered and callable) is the exact, already-proven template for this.
6. **Caller-supplied exchange rates instead of Finance's rate master.** Confirmed:
   `IExchangeRateService` is registered and callable (`ServiceCollectionExtensions.cs:796`);
   HR's Travel module still accepts a rate from the caller rather than resolving one from it.
7. **Asset lifecycle patterns (adjustment, transfer, reservation, physical count) are all more
   mature in Inventory** than HR's equivalent Assets entities, which is unsurprising (Inventory
   is older) but means HR is rebuilding weaker versions of things Inventory already solved.
8. **Projects' own code comments already point at two of these gaps** — see §5.1 and §5.2 below,
   which HR's own source comments cite as the pattern to copy.

---

## 2. Master table

Legend — **Priority**: 🔴 high-leverage/high-risk · 🟡 moderate · 🟢 nice-to-have.

| # | Pattern | Origin | HR status today | Priority |
|---|---|---|---|---|
| 1 | Reminder → notification wiring | Generic (`INotificationService`) | 🔲 Reminders sweep and log; **nothing sends** | 🔴 |
| 2 | Optimistic concurrency (`RowVersion`) | Procurement/Quantity Survey (~95% coverage) | 🔲 ~12 of 180+ HR entities (~7%) | 🔴 |
| 3 | Budget commitment lifecycle (reserve→consume→release) | Procurement (`IFinanceBudgetCommitmentService`, FIN-INT-015) | 🔲 `ManpowerBudget`/`TrainingBudget`/`AwardBudget`/`StaffTravelBudget` are self-tracked only | 🔴 |
| 4 | Exchange-rate resolution from Finance master | Finance (`IExchangeRateService`) | 🔲 Travel accepts a caller-supplied rate | 🔴 |
| 5 | Semantic audit trail for sensitive changes | Finance (`IFinanceAuditService`) | 🔲 Only the generic EF interceptor; no HR-specific business-meaning log | 🟡 |
| 6 | Reporting/export framework | Finance (`IFinanceReportExportService`) | ❓ HR has three of its own reporting patterns (Oracle payroll reports, `HrAwardsReportCatalogue` system reports, dashboards) but doesn't use Finance's — see [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) | 🟡 |
| 7 | Adjustment/write-off with GL posting | Inventory (`IInventoryAdjustmentFinancePostingService`) | 🔲 `AssetSurcharge.WaiverReason` has no accounting treatment | 🟡 (shared with Finance sweep decision #2) |
| 8 | Reservation/allocation state machine | Inventory (`InventoryAllocation`) | 🔲 `AssetRequisition` has no formal reserve→assign→expire lifecycle | 🟡 |
| 9 | Physical count / cycle-count reconciliation | Inventory (`PhysicalCount`, ABC classification) | 🔲 No periodic asset-register verification process exists | 🟡 |
| 10 | Multi-location transfer with in-transit + discrepancy handling | Inventory (`InventoryTransfer`) | 🔲 `AssetTransfer` is a binary Draft→Approved→Completed with no in-transit/discrepancy state | 🟡 |
| 11 | Landed-cost allocation | Inventory (`LandedCostService`, `Calculate Landed cost.txt`) | 🔲 `CompanyAsset.PurchaseCost` is a flat field; freight/duty/installation untracked | 🟢 |
| 12 | Cross-module "push" link (asset ↔ maintenance work order) | Projects (`ProjectAssetLink` + `MaintenanceFollowThrough`, ~432 lines) | 🟡 HR's own code comments already name this as the template for a not-yet-built `AssetAdmission` | 🔴 (already acknowledged in-code) |
| 13 | Budget revision + actual-cost feed | Projects (`ProjectBudgetRevision`, committed/forecast/actual) | 🔲 Same gap as row 3/5 in the Finance sweep — no HR budget entity has a real actuals feed | 🟡 (shared with Finance sweep decision #4) |
| 14 | Percentage/FTE resource allocation with substitution tracking | Projects (`ProjectResourceAllocation`) | 🔲 HR's `TeamMember`/`StaffMovement` model binary membership, no % allocation or backfill chain | 🟢 |
| 15 | Phase-gate workflow (blocking readiness rules) | Projects (`ProjectPhase` + `ProjectStageGateRule`) | 🔲 `AppraisalCycle` uses flat date fields per phase, no blocking-rule evaluation | 🟢 |
| 16 | Billable timesheet distinct from attendance | Projects (`ProjectTimesheetEntry`) | ⚪ Not applicable to most of HR, but relevant if any HR/consulting staff bill time to projects | 🟢 |
| 17 | Quantitative risk/issue scoring (probability × impact) | Projects (`ProjectRisk`/`ProjectIssue`) | 🔲 `StaffGrievance`/`StaffDisciplinaryAction` have status and severity, but no comparable exposure score | 🟢 |
| 18 | Unified document-versioning model | Projects (`ProjectDrawing`: number + revision + supersedes + status) | 🔲 `JobDescription` uses a numeric version, `HrPolicyDocument` uses a label + supersedes link — two inconsistent schemes | 🟢 |
| 19 | Accounting-period lock (Open→Closed→Locked) | Finance (`FiscalPeriod`) | 🔲 No equivalent "payroll period lock" preventing backdated edits after a run is finalized | 🟡 |
| 20 | Bank-statement reconciliation matching | Finance (`BankReconciliationEngine`) | 🔲 Payroll's bank-disbursement schedule is generated but never reconciled against actual bank confirmation | 🟢 |
| 21 | AP/AR settlement read-model (aging, outstanding balance) | Finance (`SubledgerSettlementBalance`) | 🔲 HR's employee receivables (loans, advances, surcharges, travel advances, service bonds) are raw balance fields with no aging/reconciliation view | 🟡 (shared with Finance sweep decision #2) |
| 22 | Unified external-party/payee model | Finance/Procurement (`Supplier`/`BusinessPartner`) | 🔲 `TrainingVendor`, `MedicalInsuranceProvider`, `HealthcareFacility` each duplicate their own bank-detail fields | 🟡 (shared with Finance sweep decision #7) |

---

## 3. Cross-cutting infrastructure findings (detail)

### 3.1 Reminders sweep but do not notify — 🔴 highest-confidence gap found

HR runs at least nine reminder/background sweep services: Probation, Separation, Asset,
Discipline, Staff Movement, Staff Travel, SHE, plus identity reconciliation and CV-upload ticket
sweeping. Each follows the correct shared pattern — a `BackgroundService` registered in
`ServiceCollectionExtensions.cs`, a tenant-scoped `RunSweepForTenantAsync`, a dispatch-log entity,
and a manual "run now" controller endpoint. **This part is done well and consistently.**

What's missing: a repo-wide search for `INotificationService`/`IEmailService` inside every
`*Reminder*.cs` file under `src/ErpSystem.Core/Services/HR/` and every
`*ReminderBackgroundService.cs` under `src/ErpSystem.Api/Services/HR/` returns **zero matches**.
The sweep computes who should be reminded and writes a row to a dispatch log; nothing downstream
turns that row into an email, SMS, or in-app notification. Probation deadlines, separation
notice/clearance deadlines, asset-return due dates, and disciplinary case deadlines are therefore
silent today.

**Fix shape:** inject `INotificationService` (already used elsewhere, e.g. recruitment's
`ApplicationPipelineService`, `JobInterviewService`) into each reminder service and call it when a
dispatch-log row is created, using HR-specific templates.

### 3.2 Optimistic concurrency is the rarest pattern in HR

Confirmed by direct search: **12 entities** in `src/ErpSystem.Core/Entities/HR/**` carry
`[Timestamp] public byte[] RowVersion`:

| File | Entities with `RowVersion` |
|---|---|
| `TrainingEntities.cs` | 5 |
| `RecruitmentEntities.cs` | 5 (includes `JobVacancy`) |
| `PositionVacancyEntities.cs` | 1 (`PositionVacancy`) |
| `StaffRequisitionEntities.cs` | 1 |

By contrast, Procurement and Quantity Survey entities carry it on the large majority of their
mutable entities. **Every high-traffic, multi-editor HR surface has none**: `LeaveRequest`,
`EmployeeBenefitEnrollment`, `PayrollLoan`/`PayrollSalaryAdvance`, `EmployeeSeparation`,
`StaffDisciplinaryAction`, `EmployeeAward`, `MedicalExpenseClaim`, `EmployeeProfileChangeRequest`.
Two HR officers editing the same leave request, disciplinary case, or benefit enrollment
concurrently will silently overwrite one another rather than getting a conflict.

**Fix shape:** add `[Timestamp] public byte[] RowVersion` to the entities above (a small, safe,
additive migration), and have the corresponding update DTOs/endpoints require and check it,
mirroring how Recruitment/Training already do it.

### 3.3 Workflow engine — broader coverage than first assumed, but real gaps remain

Fifteen HR-specific adapter files exist under `src/ErpSystem.Core/Services/Workflow/`:
`HrLeaveWorkflowStatusAdapters.cs`, `HrAssetsWorkflowStatusAdapters.cs`,
`HrDisciplineWorkflowStatusAdapters.cs`, `HrSeparationWorkflowStatusAdapters.cs`,
`HrMovementWorkflowStatusAdapters.cs`, `HrProbationWorkflowStatusAdapters.cs`,
`HrPipWorkflowStatusAdapters.cs`, `HrPerformanceWorkflowStatusAdapters.cs`,
`HrAttendanceWorkflowStatusAdapters.cs`, `HrRequisitionWorkflowStatusAdapters.cs`,
`HrJobOfferWorkflowStatusAdapters.cs`, `HrJobArchitectureWorkflowStatusAdapters.cs`,
`HrProposalWorkflowStatusAdapters.cs`, `HrStaffTravelWorkflowStatusAdapters.cs`,
`HrSuccessionWorkflowStatusAdapters.cs`. This is **materially better coverage** than an initial
scan suggested — Performance Improvement Plans, Staff Travel and Disciplinary cases are already
wired, contrary to a first-pass claim that they were not.

**Confirmed real gap:** no `HrMedical*WorkflowStatusAdapter` or `HrBenefit*WorkflowStatusAdapter`
exists anywhere in the codebase. `MedicalExpenseClaim`'s create→approve→pay path (already flagged
in the Finance sweep as the priority back-fill) and `EmployeeBenefitEnrollment`'s approval both
use bare `ApprovedById`/`ApprovedDate` pairs rather than the shared workflow engine.

**A third confirmed instance, found 2026-08-31: Safety/SHE.** None of its 60+ entities carry a
`WorkflowInstanceId`, and no `HrShe*WorkflowStatusAdapter` exists. Permit-to-work approval is a
bare two-step `IssuedById`/`IssuedDate` then `ApprovedById`/`ApprovedDate` pair with no state
machine; risk-assessment and environmental-review approvals follow the same bare-FK shape. Same
gap, same fix, third area to apply it to.

### 3.4 Audit logging — automatic baseline exists, semantic layer does not

The EF Core change interceptor behind the generic `AuditLog` table applies to every
`TenantEntity`, so raw row-level history exists for all HR entities already — this is not a gap.
What Finance has and HR does not is a **semantic** layer: `IFinanceAuditService`, used in 20+
Finance services, records business-meaningful events (`"Finance.AccountingPeriod.Closed"`,
`"Finance.JournalEntry.ReversalCreated"`) with structured before/after values. HR has no
equivalent (`IHrAuditService` does not exist), so an auditor can see that an `Employee` row
changed but not *that a salary was adjusted, by whom, and why* in one readable record.

### 3.5 Reporting/export framework — HR doesn't use Finance's, but has its own (corrected 2026-08-31)

`IFinanceReportExportService` (confirmed, backing `FinanceReportExportsController.cs`) is a real,
working shared framework: it exports Trial Balance, Aging, Ledger and other reports as CSV/PDF
using the *same* backend logic as the on-screen report, and logs a `Finance.Report.Exported`
audit event. **HR does not use it — that part stands.** The original claim here, that
`PayrollReportType.BankSchedule`/`.TaxSchedule`/`.PensionSchedule`/`.Payslip` had "no service
implementing them," was **too strong** and is corrected: `PayrollService.cs` has real methods
(`GetRunSummaryReportAsync`, `GeneratePayslipSnapshotsAsync`) and a working Oracle-RDF-driven
frontend page (`frontend/src/app/reports/hr/page.tsx`) covering 23 payroll reports end to end.
HR in fact has **three different, independently-evolved reporting patterns of its own** — none of
them Finance's. See [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) for the full inventory,
the gaps that remain, and which of HR's own patterns to use for each.


### 3.6 Controlled document upload / central DMS — HR's best-adopted pattern

Confirmed via direct search: `IHrControlledDocumentService` is wired into over 30 HR controllers
— Assets, Awards, Leaves, Separations, Grievances, Discipline, Medical (claims, insurance,
self-service), Succession, Candidate, Job Offer/Posting/Vacancy, Requisitions, Staff Travel,
Movements, Policies, Announcements, Oaths of Secrecy, Pre-Employment Checks, My Profile, and
more. This is comprehensive and is arguably the pattern other modules should point to as the
reference, not the other way round.

### 3.7 Multi-tenancy & background-job infrastructure — complete, no action needed

Both are used consistently and correctly across HR. Recorded here only so a future sweep does not
re-check them.

---

## 4. Finance-originated patterns

### 4.1 Exchange-rate resolution (🔴 priority — overlaps Finance sweep)

`IExchangeRateService` is registered (`ServiceCollectionExtensions.cs:796`) and callable today,
resolving a rate for a currency pair/date/rate-type from Finance's own master. The Finance sweep
already flagged (row 50) that HR's Travel module accepts a caller-supplied exchange rate instead
of reading this master — this is the master-data "read now, never duplicate" rule from
`HR-FINANCE-INTEGRATION-BACKLOG.md` being violated in one specific place. **This is not part of
the deferred GL-posting sweep** — reading master data was never deferred, so this can be fixed
independently and immediately, unlike the GL-posting questions.

### 4.2 Accounting period lock (Open → Closed → Locked)

Finance's `FiscalPeriod` carries three independent flags (`IsOpen`/`IsClosed`/`IsLocked`) and the
posting engine refuses to post into a closed/locked period, with a validation gate that checks
for unposted journals, unreconciled banks, etc. before allowing a close. **Suggested (not yet
built) HR analogue:** a payroll-run/period lock so that once a payroll run is finalized, salary
component edits, loan/advance changes, and benefit-enrollment changes within that period are
rejected rather than silently altering a run that has already been reported or paid. The same
three-state shape (Open/Closed/Locked) maps directly.

### 4.3 Bank reconciliation matching

Finance's `BankReconciliationEngine` matches bank-statement lines against internal cash
transactions (exact match on amount+reference, fuzzy match on amount+date window, everything else
flagged for manual review). **Suggested HR analogue:** payroll's bank disbursement schedule is
generated (`PayrollReportType.BankSchedule`) but never checked against what the bank actually
paid — there is no mechanism today to detect a bounced/returned salary payment. This would reuse
the same matching engine rather than building a parallel one.

### 4.4 AP/AR settlement read-model (aging & outstanding balance)

Finance's `SubledgerSettlementBalance`/`SubledgerSettlementApplication` give a materialized,
queryable view of what's outstanding per invoice, with an application/allocation history. HR has
five different "employee owes the organisation money" surfaces (loans, salary advances, travel
advances, asset surcharges, training service bonds) each modelled as a bare running balance with
no shared aging or reconciliation view. **This is the same gap already recorded as Finance-sweep
decision #2** (should these become Finance AR records) — noted here because the read-model
pattern, if Finance AR is chosen, already exists and does not need to be invented.

### 4.5 Reversal / correction patterns

`IFinancePostingEngine.GetReversalPlanAsync(postingEventId, reason, reversalDate)` is a first-class
operation — correcting a posted transaction is a designed capability, not an afterthought. Once
HR/Payroll's journal posting is migrated onto `IFinancePostingEngine` (per the Finance sweep's
top recommendation), corrections to a posted payroll run, a paid award, or a settled benefit claim
should use this same reversal mechanism rather than a bespoke "undo" built in HR.

---

## 5. Procurement-originated patterns

### 5.1 Budget commitment lifecycle (🔴 priority — overlaps Finance sweep)

`IFinanceBudgetCommitmentService` (confirmed implemented at
`src/ErpSystem.Api/Services/Finance/Budget/FinanceBudgetCommitmentService.cs`, and consumed by
Procurement per contract FIN-INT-015) implements a clean, already-proven lifecycle: draft
requisition → reserve on approval → amend to an absolute target, never a blind delta → release on
rejection/cancellation → reduce the remaining commitment only after the corresponding Finance
posting succeeds, so receipts and invoices never double-count the same spend.

HR's four self-tracked budget entities — `ManpowerBudget`, `TrainingBudget`, `AwardBudget`,
`StaffTravelBudget` — each reinvent a much weaker version of this (a `BudgetAmount` and a
`SpentAmount`/`ReservedAmount` pair with no reservation record, no idempotency key, and — per the
Finance sweep — no actuals feed at all for `ManpowerBudget`). **This is the single most
directly-reusable pattern in this whole document**: the service interface already exists, is
already tenant-safe and idempotent, and Procurement's consumption sequence (draft→reserve,
amend→resize, cancel→release, receipt→consume-by-posted-actual) is a template HR's four budget
surfaces could follow with minimal adaptation, once the "three-way training double-count" and
"who writes ManpowerBudget.ActualSpent" questions from the Finance sweep are settled.

### 5.2 Segregation-of-duties guard pattern

Procurement's access-control model enforces "requestor ≠ approver" as a first-class, reusable
rule rather than a convention developers have to remember. HR's approval chains (surcharge
approval, disciplinary sanction, separation approval) rely on role-based policy gates
(`HrPermissions.*Policy`) but do not appear to enforce a comparable "the person who raised this
cannot also approve it" rule at the service layer. Worth checking against `AssetSurcharge`
specifically, since HR is the *creditor* there — the one place a self-approved recovery would be
most damaging.

---

## 6. Inventory-originated patterns

All four of these apply most directly to HR's **Assets** sub-module (`AssetsEntities.cs`), which
is structurally the closest thing in HR to Inventory's stock register — a register of items,
custody/location, and money recovery.

### 6.1 Adjustment/write-off with GL posting

`IInventoryAdjustmentFinancePostingService` posts stock write-offs through
`IFinancePostingEngine` only **after** an approval gate, with a fixed reason-code list (Damage,
Loss, Theft, Expired, Write-off, etc.). `AssetSurcharge.WaiverReason` is HR's equivalent event
(forgiving a receivable) and today has **no GL treatment at all** — already flagged in the
Finance sweep (decision area, row 41) as unmodelled. Inventory's pattern — decouple the write-off
*decision* from its *posting*, and post only on approval — is the direct template once the sweep
assigns an owner.

### 6.2 Reservation / allocation state machine

Inventory's `InventoryAllocation` is a proper reserve→partially-fulfil→consume→expire state
machine with an idempotency key (so a retried request never double-allocates the same unit) and
an atomic lock around contested reservations (e.g. the last unit of a scarce item). HR's
`AssetRequisition` has no equivalent — fulfilment is effectively "assign whichever asset is
available," with no formal hold once a requisition is approved, and no timeout/escalation if a
requisition goes unfulfilled.

### 6.3 Physical count / cycle-count reconciliation

Inventory schedules periodic counts by ABC classification (high-value items counted more often),
supports "blind" counts (the counter doesn't see the expected quantity, reducing bias), freezes
movement during the count window, and escalates variances above a threshold through a multi-level
sign-off (Stores → Finance → Audit). **HR has no equivalent process for verifying its own asset
register** — no scheduled reconciliation of "what the register says is assigned" against "what
physically exists," which is the natural detection point for assets that should have triggered a
surcharge or a written-off status but never did.

### 6.4 Multi-location transfer with in-transit + discrepancy handling

Inventory's transfer workflow has a real in-transit state (stock leaves the source before it
lands at the destination) and a structured discrepancy entity (damaged/short/lost-in-transit,
with evidence and a resolution outcome). HR's `AssetTransfer` is a binary
Draft→Approved→Completed record with no dispatch/receipt confirmation step and no discrepancy
capture — if an asset transferred between employees or locations goes missing in the process,
there is no structured way to record that today (it would likely become an ad-hoc `AssetSurcharge`
with no link back to the transfer that caused it).

### 6.5 Landed-cost allocation (lower priority)

Inventory's landed-cost mechanism spreads shared shipping/duty/handling costs across multiple
items proportionally (by value, quantity, weight or volume) before computing each item's true
unit cost — worked example preserved in `Calculate Landed cost.txt` at the repo root. HR's
`CompanyAsset.PurchaseCost` is a flat field with no way to attribute import duty, freight or
installation cost when several assets arrive in one shipment/invoice. Lower priority than the
rest of this section because it only matters for HR-created assets bought in bulk — assets linked
to Finance's `FixedAsset` register already get correct capitalization from Finance.

---

## 7. Projects-originated patterns

### 7.1 Cross-module "push" link — HR's own code already names this pattern

This is the one item in this whole document that HR's own source code already flags. The comment
in `CompanyAsset.MaintenanceAssetId` (`AssetsEntities.cs`) reads, in part:

> *"The live precedent is Projects, not Finance. `ProjectAssetLink` carries this same column
> beside a `CompanyAssetId` and a `JobCardId`, has a real picker behind it (the project Access
> tab), and `ProjectService.MaintenanceFollowThrough` raises job cards and work orders on it —
> 432 lines of exactly the push slice 9b needs, worth copying rather than reinventing. Finance's
> `FixedAsset.MaintenanceAssetId` is the same shape but has never carried a value on any row,
> because no screen renders an input for it."*

In other words: **HR has already identified this as the pattern to copy** for its planned
`AssetAdmission` push into the Maintenance module (raising job cards/work orders when an
HR-held asset needs servicing), and has already ruled out copying Finance's version of the same
idea because Finance's is unused. This document surfaces it here so it is not lost between
sessions — `ProjectService.MaintenanceFollowThrough.cs` is the concrete file to read before
building `AssetAdmission`.

### 7.2 Budget revision + real actual-cost feed

Projects' `ProjectBudgetRevision`/`ProjectForecastVersion` separate **approved budget** from
**committed cost** (approved-but-unpaid) from **actual cost** (from posted Finance transactions)
from **forecast** (committed + projected remainder), with threshold-based warnings (e.g. 75%/90%
consumed). This is precisely the shape the Finance sweep already said `ManpowerBudget` is
missing (`ActualSpent`/`Variance` have no writer anywhere). Projects' implementation is evidence
that this is a solved problem elsewhere in the codebase, not a new design HR would have to invent
— the actuals genuinely can be synced from Finance postings, because Projects already does it.

### 7.3 Percentage/FTE resource allocation with substitution tracking

Projects' `ProjectResourceAllocation` supports partial allocation (50% to this initiative), soft
vs. hard booking, skill-matching, and an explicit substitution chain (`SourceAllocationId` /
`ReplacementAllocationId` + reason) for backfilling someone on leave. HR's `TeamMember` models
binary org-structure membership and `StaffMovement` models a full-time transfer — neither
supports "this employee is 30% seconded to a cross-functional initiative for six weeks," which is
a real HR scenario (secondments, project-based dotted-line reporting) with no first-class model
today. Lower priority than the 🔴 items, but worth knowing the pattern exists if HR ever needs it.

### 7.4 Phase-gate workflow with blocking readiness rules

Projects' `ProjectPhase` + `ProjectStageGateRule` model formally blocks advancing to the next
phase until configured conditions are met (e.g. "90% of BOQ items complete"), with an explicit
override-with-reason escape hatch. HR's `AppraisalCycle` instead carries a long flat list of date
fields per phase (`GoalSettingOpenDate`, `GoalSettingDeadline`, `SelfEvaluationOpenDate`, …) with
no mechanism to actually block calibration from starting before, say, 80% of self-evaluations are
in. This is a real gap but a lower-priority one — the current model works, it's just manual.

### 7.5 Quantitative risk/issue scoring

Projects scores risks as Probability × Impact = Exposure, with a small catalogue of response
strategies (Monitor/Mitigate/Avoid/Transfer). HR's `StaffGrievance`/`StaffDisciplinaryAction`
case-tracking is arguably richer in procedural detail (investigation, hearing, appeal
sub-entities) but has no comparable way to say "this case is higher-priority than that one" other
than reading the free text. Lowest priority in this document — genuinely optional, not a
correctness gap.

### 7.6 Unified document-versioning model

Projects' `ProjectDrawing` combines a stable document number, a free-text revision label (A, B,
P1…), a `SupersedesDrawingId` link, and an explicit lifecycle status (Draft/ForReview/Approved/
Superseded) in one consistent shape. HR has **two different, incompatible versioning schemes**
today: `JobDescription.VersionNumber` (a bare integer) and `HrPolicyDocument.VersionLabel` +
`.SupersedesPolicyId` (a label plus a supersedes link, but no numeric version and no explicit
"under review" status). If a third HR document type ever needs versioning, it would be worth
converging on one shape rather than inventing a third scheme.

---

## 8. Suggested priority order

Ranked by (a) risk if left unaddressed, (b) how directly reusable the existing pattern is, and
(c) how many rows in this document or the Finance sweep it closes at once:

1. **Wire the 9 reminder engines to `INotificationService`.** Self-contained, no design decisions
   needed, closes a real and currently-silent operational gap.
2. **Add `RowVersion` to the highest-traffic HR entities** (Leave, Payroll loans/advances,
   Benefits, Medical claims, Separation, Discipline, Awards). Additive migration, low risk, closes
   a genuine data-loss exposure.
3. **Resolve exchange rates from `IExchangeRateService`** in Travel instead of accepting a
   caller-supplied rate — this is a master-data fix, not part of the deferred GL-posting sweep, so
   it can proceed independently and immediately.
4. **Adopt `IFinanceBudgetCommitmentService`'s lifecycle** for `ManpowerBudget`/`TrainingBudget`/
   `AwardBudget`/`StaffTravelBudget` — highest-leverage reuse in this document, but sequence it
   *after* the Finance sweep's training/manpower budget-ownership decision (§4 of the Finance
   sweep), or the wrong entity gets wired first.
5. **Build `AssetAdmission` by copying `ProjectService.MaintenanceFollowThrough.cs`** — HR's own
   code comments already call this out; there is no design work left to do, only porting.
6. **Add a semantic `IHrAuditService`**, modelled on `IFinanceAuditService`, for salary changes,
   bank-detail changes, disciplinary decisions and benefit approvals.
7. **Build HR's payroll report exports on `IFinanceReportExportService`** rather than inventing a
   bespoke generator, once the reports are actually built.
8. Everything in §6 (Inventory-style asset adjustment/reservation/count/transfer maturity) and
   §7.3–§7.6 (Projects-style resource allocation, phase-gates, risk scoring, document versioning)
   — genuinely useful, but lower urgency; revisit once the 🔴 items above are closed.
