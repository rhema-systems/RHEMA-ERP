# Cross-Module Patterns Sweep — Reusable Practices for HR

**Generated:** 2026-08-31
**Scope:** Finance, Procurement, Inventory and Projects — the four most built-out modules in
this ERP — reviewed for engineering and business patterns that the HR module
(`src/ErpSystem.Core/Entities/HR/**`) has not yet adopted, or has adopted only partially.
**Purpose:** A companion to the HR↔Finance sweep — where that document asks *"which HR entity
carries money and where should it eventually post,"* this document asks *"what has the rest of
the system already solved that HR is re-solving badly, or not solving at all."*

---

## 0. How this relates to the other HR docs in `docs/HR/`

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

## ⚠ Vetting pass, 2026-09-02 — corrections to the 2026-08-31 text

Every file-level claim in this document was re-verified against the working tree on 2026-09-02.
The corrections below are applied inline in the sections they affect; this block is the audit
trail so nobody has to diff the two versions.

| # | Original claim | Verdict | Corrected fact |
|---|---|---|---|
| 1 | "Reminder engines run but notify nobody" (§1 item 1, §3.1, master row 1, §8 item 1) | **WRONG in conclusion.** The grep for `INotificationService`/`IEmailService` inside reminder files is literally true, but that is not how HR reminders deliver. | **Five of eight** reminder engines (Asset, Discipline, StaffMovement, StaffTravel, SHE) publish `EntityActivityEvent` on `IAppEventBus` → `EntityActivityNotificationTopicHandler` → `NotificationTopicPublisher` writes `Notification` rows (in-app/email/SMS) → `NotificationDispatcherBackgroundService` calls `INotificationService.SendPendingNotificationsAsync()`. **Three are genuinely silent**: Probation, Separation, IdentificationExpiry write `*ReminderDispatchLog` rows that nothing reads. Lane 1 (2026-08-31) proved discipline queued 10 reminders on its first real scheduled run. |
| 2 | "~12 of 180+ HR entities (~7%) carry RowVersion"; "Procurement/QS ~95%" | **Counts right, denominators wrong.** | 12 is correct. HR has **596** `TenantEntity` classes excluding Payroll (648 including) → **≈2%**. Procurement is 65/213 (31%), Quantity Survey 24/69 (35%). |
| 3 | "HR's Travel module accepts a caller-supplied exchange rate" (§1 item 6, row 4, §4.1, §8 item 3) | **STALE — fixed in area 12.** | `StaffTravelFinanceService.cs:714` resolves the rate through `StaffTravelCurrencyBridge : HrCurrencyBridge` (`IExchangeRateService.GetCurrentRateAsync`, refuses when no rate is published); the create/update DTOs deliberately carry no rate. **The surviving caller-supplied rate is `StaffRequisitionCost.ExchangeRate`** (`StaffRequisitionDTOs.cs:401/427`, default 1, mapped straight through, summed at `StaffRequisitionService.cs:632`). |
| 4 | "`IFinanceBudgetCommitmentService` … consumed by Procurement per FIN-INT-015" | **NUANCE.** | No Procurement code references that service; its consumers are Finance AP (`VendorInvoiceService`, `FinancePostingEngine`). Procurement keeps its own `ProcurementBudgetCommitment` + `IProcurementBudgetReservationStore`. The catalogue still lists FIN-INT-015 as Procurement→Finance Available — the *lifecycle* is the template, the consumer is Finance's own AP path. |
| 5 | "HR does not appear to enforce requester ≠ approver" (§5.2) | **HALF WRONG.** | HR does enforce it in several services: `StaffRequisitionService.cs:385`, `JobApplicationService.cs:1870` (shortlist submitter), `StaffGrievanceService.cs:993` ("cannot decide your own case"), and `EmployeeGoalService.cs:351` records a closed self-approval hole. The `AssetSurcharge` question is answered: `AssetSurchargeService.cs:814-820` `RequireNotTheSubject` blocks the *charged employee*; there is **no** raised-by ≠ approver check, approval then goes through the workflow engine. |
| 6 | "`AssetTransfer` is a binary Draft→Approved→Completed" (row 10, §6.4) | **WRONG on the enum, right on the substance.** | `HRAssetTransferStatus` has seven values incl. `InTransit` — but `InTransit` is never assigned anywhere (only a guard at `AssetsServices.cs:3750`), and the entity has no dispatch/receipt/discrepancy fields. It is a dead-letter state. |
| 7 | "`AssetAdmission` … not-yet-built push into Maintenance" (row 12, §7.1, §8 item 5) | **WRONG — built.** | `AssetsServices.cs:2270 SendForMaintenanceAsync` → `IAssetAdmissionService.CreateAdmissionAsync`, stored on `AssetMaintenance.MaintenanceAdmissionId`, endpoint `POST api/assets/{id}/send-for-maintenance` (`AssetsController.cs:511`). Admission was chosen *because* the work-order path is blocked: cross-module defect **#9** — `ProjectService.MaintenanceFollowThrough.cs` throws for every tenant because `MaintenanceType`/`PriorityLevel`/`WorkOrderType` have 0 rows. Copying that file is therefore **not** "no design work left" — it inherits a dependency on three empty lookup tables. |
| 8 | Fix shape in §3.1 cites `ApplicationPipelineService`/`JobInterviewService` as `INotificationService` users | **WRONG.** | Neither injects it; they use `IEmailService` + `ITemplatedEmailService`. **No HR service injects `INotificationService` directly.** The house pattern is the `IAppEventBus` topic pipeline the five delivering engines already use. |
| 9 | §3.3 uses "no `WorkflowInstanceId`" as the test for "not on the engine" | **Weak test.** | Only 7 HR entities carry `WorkflowInstanceId` (Leave ×3, JobVacancy, EmployeeSeparation, StaffRequisition, TrainingNomination). Assets, Discipline, Movement, PIP, Probation etc. are wired through the `IWorkflowStatusAdapter.EntityTypes` registry with no such column. The conclusion for Medical/Benefits/SHE still holds (no adapter exists), but the adapter registry is the wiring, not the column. |
| 10 | "`IHrControlledDocumentService` wired into over 30 HR controllers" | CONFIRMED with a nuance. | 35 controllers + 2 static helpers. The 28 SHE controllers do **not** use it — they go through `ISheControlledDocumentService`. |
| 11 | `JobDescription.VersionNumber` "(JobArchitecture)" (§7.6) | Location wrong. | It lives in `JobAnalysisEntities.cs:31`. |
| 12 | "§1: 7+ reminder engines" vs "§3.1: at least nine" | Internally inconsistent. | There are **eight**: Asset, Discipline, IdentificationExpiry, Probation, Separation, SHE, StaffMovement, StaffTravel — all unconditional `AddHostedService` (`ServiceCollectionExtensions.cs:2297-2338`); SHE hourly, the rest daily. `PublicCvUploadTicketSweeper` is a cleanup job; there is no "identity reconciliation sweep". |

Everything not listed above was confirmed as written (15 adapter files, no `IHrAuditService`,
`IFinanceAuditService` in 41 Finance services, `FiscalPeriod` flags, `MaintenanceFollowThrough.cs`
at exactly 432 lines with the `AssetsEntities.cs:136-140` comment verbatim, `AppraisalCycle` flat
dates, `HrPolicyDocument` versioning fields).

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
1. **Three of eight reminder engines write a dispatch log that nothing reads** *(corrected
   2026-09-02)*. Asset, Discipline, StaffMovement, StaffTravel and SHE deliver through the
   `IAppEventBus` → notification-topic → `NotificationDispatcherBackgroundService` pipeline.
   **Probation, Separation and IdentificationExpiry do not** — they write `*ReminderDispatchLog`
   rows, inject no bus, and no consumer exists. Probation expiries (FR-HR-140), separation
   notice/clearance/retirement deadlines (FR-HR-111) and identification-document expiries are
   therefore silent today; asset returns and disciplinary deadlines are not.
2. **Optimistic concurrency (`RowVersion`/`[Timestamp]`) is rare.** Confirmed count: **12
   entities** across the whole HR module carry a `RowVersion` (5 in Training, 5 in Recruitment, 1
   in Position Vacancy, 1 in Staff Requisition) — out of **596** HR `TenantEntity` classes
   (≈2%; 648 counting Payroll). Payroll, Leave, Benefits, Medical, Assets,
   Discipline, Separation, Awards, and **Safety/SHE (confirmed 2026-08-31: zero of its 88
   entity classes in `StaffSafetyEntities.cs` have a `RowVersion`, including approval-bearing ones
   like `ShePermitToWork` and `SheRiskAssessment`)** — the highest-traffic, multi-editor entities —
   have **none**.
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
   Full inventory and recommendation: [`HR-REPORTS-CATALOGUE.md`](../catalogues/HR-REPORTS-CATALOGUE.md).
5. **Budget entities with no commitment lifecycle.** `ManpowerBudget`, `TrainingBudget`,
   `AwardBudget`, `StaffTravelBudget` each track `BudgetAmount`/`SpentAmount` themselves with no
   reserve→consume→release mechanism — Procurement's `IFinanceBudgetCommitmentService` (confirmed
   registered and callable) is the exact, already-proven template for this.
6. **One caller-supplied exchange rate survives — on staff-requisition cost lines** *(corrected
   2026-09-02)*. `IExchangeRateService` is registered and callable
   (`ServiceCollectionExtensions.cs:796`), and Travel now resolves its rate from it through
   `HrCurrencyBridge` (so do Assets, Surcharges, Letters, Separation and Succession).
   `StaffRequisitionCost.ExchangeRate` is still taken from the caller (default 1) and summed into
   the requisition's recruitment cost without ever consulting Finance. Payroll's own
   `PayrollExchangeRate` table is a parallel FX master too, but that module is another
   developer's (see `HR-PAYROLL-BOUNDARY.md`).
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
| 1 | Reminder → notification wiring | HR's own `IAppEventBus` topic pipeline (five engines already use it) | 🟡 5 of 8 engines deliver; **Probation, Separation, IdentificationExpiry log only** | 🔴 |
| 2 | Optimistic concurrency (`RowVersion`) | Procurement 31% / Quantity Survey 35% coverage | 🔲 12 of 596 HR entities (≈2%) | 🔴 |
| 3 | Budget commitment lifecycle (reserve→consume→release) | Finance (`IFinanceBudgetCommitmentService`, FIN-INT-015 — consumed by Finance AP; Procurement keeps its own reservation store) | 🔲 `ManpowerBudget`/`TrainingBudget`/`AwardBudget`/`StaffTravelBudget` are self-tracked only | 🔴 |
| 4 | Exchange-rate resolution from Finance master | Finance (`IExchangeRateService` via `HrCurrencyBridge`) | ✅ Travel/Assets/Letters/Separation/Succession, and Recruitment costs since 2026-09-10 (round 2b, R7) | ✅ |
| 5 | Semantic audit trail for sensitive changes | Finance (`IFinanceAuditService`) | 🔲 Only the generic EF interceptor; no HR-specific business-meaning log | 🟡 |
| 6 | Reporting/export framework | Finance (`IFinanceReportExportService`) | ❓ HR has three of its own reporting patterns (Oracle payroll reports, `HrAwardsReportCatalogue` system reports, dashboards) but doesn't use Finance's — see [`HR-REPORTS-CATALOGUE.md`](../catalogues/HR-REPORTS-CATALOGUE.md) | 🟡 |
| 7 | Adjustment/write-off with GL posting | Inventory (`IInventoryAdjustmentFinancePostingService`) | 🔲 `AssetSurcharge.WaiverReason` has no accounting treatment | 🟡 (shared with Finance sweep decision #2) |
| 8 | Reservation/allocation state machine | Inventory (`InventoryAllocation`) | 🔲 `AssetRequisition` has no formal reserve→assign→expire lifecycle | 🟡 |
| 9 | Physical count / cycle-count reconciliation | Inventory (`PhysicalCount`, ABC classification) | 🔲 No periodic asset-register verification process exists | 🟡 |
| 10 | Multi-location transfer with in-transit + discrepancy handling | Inventory (`InventoryTransfer`) | 🔲 `HRAssetTransferStatus` has an `InTransit` value **nothing ever sets**, and no dispatch/receipt/discrepancy capture | 🟡 |
| 11 | Landed-cost allocation | Inventory (`LandedCostService`, `Calculate Landed cost.txt`) | 🔲 `CompanyAsset.PurchaseCost` is a flat field; freight/duty/installation untracked | 🟢 |
| 12 | Cross-module "push" link (asset ↔ maintenance) | Projects (`ProjectAssetLink` + `MaintenanceFollowThrough`, 432 lines) | ✅ **Built** as `SendForMaintenanceAsync` → `AssetAdmission` (`POST api/assets/{id}/send-for-maintenance`). The **work-order** leg is what is blocked — cross-module defect #9 (three empty Maintenance lookup tables make `MaintenanceFollowThrough` throw for every tenant) | 🟡 (blocked outside HR) |
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

### 3.1 Three reminder engines sweep but do not notify — 🔴 (corrected 2026-09-02)

HR runs **eight** reminder sweep services, each a Core sweep + an Api `BackgroundService` host
pair: Asset, Discipline, IdentificationExpiry, Probation, Separation, SHE, StaffMovement,
StaffTravel. All eight are unconditional `AddHostedService` registrations
(`ServiceCollectionExtensions.cs:2297-2338`; SHE runs hourly, the rest daily), tenant-scoped, with
a dispatch-log entity and a manual "run now" endpoint. **This part is done well and consistently.**
(`PublicCvUploadTicketSweeper` is a cleanup job, not a reminder.)

**How delivery actually works — the pattern to copy.** The five engines that deliver do **not**
inject `INotificationService`; no HR service does. They publish an `EntityActivityEvent` on
`IAppEventBus` (`AssetReminderService.cs:165`, `DisciplineReminderService.cs:376`,
`StaffMovementReminderService.cs:219`, `StaffTravelReminderService.cs:139`,
`SheReminderService.cs:288`). `Services/Notifications/EntityActivityNotificationTopicHandler.cs`
routes the event to `NotificationTopicPublisher` (`:505-540`), which writes `Notification` rows
for the topic's channels (in-app / email / SMS); `Api/Services/NotificationDispatcherBackgroundService.cs:122`
then calls `INotificationService.SendPendingNotificationsAsync()`. Each engine self-heals its
`NotificationTopic` rows (`EnsureTopicsAsync`). Lane 1 (2026-08-31) proved this end to end:
discipline queued 10 reminders on its first real scheduled run.

**What's actually missing.** `ProbationReminderService`, `SeparationReminderService` and
`IdentificationExpiryReminderService` write `*ReminderDispatchLog` rows and stop. They inject no
bus, publish nothing, and nothing consumes those logs. `SeparationReminderService.cs`'s own header
says "delivery is the notification engine's, as it is for the other five" — but it never hands
anything to that engine. Probation expiry warnings (FR-HR-140), separation notice / clearance /
retirement / contract-expiry alerts (FR-HR-111) and identification-document expiries are silent.

**Fix shape:** give the three silent sweeps the same `IAppEventBus` publish + `EnsureTopicsAsync`
the other five have, with their own topic codes. Do **not** inject `INotificationService` into a
sweep — that bypasses the topic/channel configuration the dispatcher already honours. A second
step is the manager-facing digest discussed in `HR-BULK-OPERATIONS-CATALOGUE.md` §4.7.

### 3.2 Optimistic concurrency is the rarest pattern in HR

Confirmed by direct search: **12 entities** in `src/ErpSystem.Core/Entities/HR/**` carry
`[Timestamp] public byte[] RowVersion`:

| File | Entities with `RowVersion` |
|---|---|
| `TrainingEntities.cs` | 5 |
| `RecruitmentEntities.cs` | 5 (includes `JobVacancy`) |
| `PositionVacancyEntities.cs` | 1 (`PositionVacancy`) |
| `StaffRequisitionEntities.cs` | 1 |

That is 12 of **596** HR `TenantEntity` classes (648 with Payroll) — about 2%. By contrast,
Procurement carries it on 65 of 213 entities (31%) and Quantity Survey on 24 of 69 (35%) — not
"most", but on the mutable, multi-editor ones. **Every high-traffic, multi-editor HR surface has
none**: `LeaveRequest`,
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

**How to tell whether a family is on the engine** *(added 2026-09-02)*: check the adapter
registry, not the entity. Only 7 HR entities carry a `WorkflowInstanceId` column (three Leave
entities, `JobVacancy`, `EmployeeSeparation`, `StaffRequisition`, `TrainingNomination`); Assets,
Discipline, Movements, PIP, Probation and the rest are wired purely through
`IWorkflowStatusAdapter.EntityTypes`. The absence of the column proves nothing; the absence of an
adapter does. The full plug-in recipe and its four traps are in
`HR-WORKFLOW-ENGINE-INTEGRATION.md`.

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
them Finance's. See [`HR-REPORTS-CATALOGUE.md`](../catalogues/HR-REPORTS-CATALOGUE.md) for the full inventory,
the gaps that remain, and which of HR's own patterns to use for each.


### 3.6 Controlled document upload / central DMS — HR's best-adopted pattern

Confirmed via direct search: `IHrControlledDocumentService` is wired into 35 HR controllers (plus
two static upload helpers) — Assets, Awards, Leaves, Separations, Grievances, Discipline, Medical
(claims, insurance, self-service), Succession, Candidate, Job Offer/Posting/Vacancy, Requisitions,
Staff Travel, Movements, Policies, Announcements, Oaths of Secrecy, Pre-Employment Checks, My
Profile, and more. The 28 SHE controllers use their own `ISheControlledDocumentService` for the
same gate. This is comprehensive and is arguably the pattern other modules should point to as the
reference, not the other way round.

### 3.7 Multi-tenancy & background-job infrastructure — complete, no action needed

Both are used consistently and correctly across HR. Recorded here only so a future sweep does not
re-check them.

---

## 4. Finance-originated patterns

### 4.1 Exchange-rate resolution (🟡 — corrected 2026-09-02; Travel is done)

`IExchangeRateService` is registered (`ServiceCollectionExtensions.cs:796`) and callable today,
resolving a rate for a currency pair/date/rate-type from Finance's own master. HR reaches it
through `HrCurrencyBridge` (`Services/HR/HrCurrencyBridge.cs`, injecting `ICurrencyService` +
`IExchangeRateService`; `GetRateToBaseAsync` refuses when no rate is published). Travel's
`StaffTravelFinanceService.cs:714` uses it for every claim line, and the bridge is also used by
`AssetsServices`, `AssetSurchargeService`, `HrLetterRequestService`, `SeparationService` and
`SuccessionPlanServices`. **The Finance sweep's row 50 is stale**; it has been corrected there.

**Where the master-data rule is still violated:** nowhere in HR since 2026-09-10 — `StaffRequisitionCost.ExchangeRate` was the last (round 2b, R7)
(`StaffRequisitionEntities.cs:170`, DTO default 1, mapped straight through at
`StaffRequisitionMappingExtensions.cs:306/320`, summed at `StaffRequisitionService.cs:632`).
`StaffRequisitionService` injects no currency or rate service. Fix = route it through
`HrCurrencyBridge` like Travel. **This is not part of the deferred GL-posting sweep** — reading
master data was never deferred, so it can be fixed independently and immediately.

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
`src/ErpSystem.Api/Services/Finance/Budget/FinanceBudgetCommitmentService.cs`; catalogued as
FIN-INT-015 — note that its actual consumers are Finance's own AP services (`VendorInvoiceService`,
`FinancePostingEngine`), while Procurement keeps a separate `ProcurementBudgetCommitment` entity
and `IProcurementBudgetReservationStore`) implements a clean, already-proven lifecycle: draft
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

Procurement's `IProcurementSodGuardService` is a generic guard: callers pass a
`ProhibitedActorUserIds` list (e.g. `EmergencyProcurementPlanService.Governance.cs:295` includes
`requisition.RequestedById`), so "requestor ≠ approver" is one reusable call rather than a
convention. *(Corrected 2026-09-02.)* HR **does** enforce the rule, but as one-off checks per
service: `StaffRequisitionService.cs:385` (`RequestedById == approvedByUserId` → refuse),
`JobApplicationService.cs:1870` (the shortlist submitter cannot approve it),
`StaffGrievanceService.cs:993` ("cannot decide your own case"), and `EmployeeGoalService.cs:351`
records a closed self-approval hole. Workflow-engine families get it from
`preventInitiatorApproval` on the definition — with the caveat in
`HR-WORKFLOW-ENGINE-INTEGRATION.md` that this is the wrong control when the record is *about* a
third party. **`AssetSurcharge`, the one place HR is the creditor:** `AssetSurchargeService.cs:814-820`
`RequireNotTheSubject` blocks the *charged employee* from acting; there is **no** raised-by ≠
approver check in the service, and approval then runs through the engine. The reusable-guard
shape is still worth copying so the rule is one call, not four re-implementations.

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
with evidence and a resolution outcome). HR's `HRAssetTransferStatus` enum does declare
`InTransit` (seven values: Draft, Pending, Approved, InTransit, Completed, Rejected, Cancelled),
but **nothing ever assigns it** — the only reference is a guard at `AssetsServices.cs:3750` — and
the entity has no dispatch/receipt confirmation fields and no discrepancy capture. In practice the
record still runs Draft→Approved→Completed; if an asset transferred between employees or
locations goes missing in the process,
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

**Status, corrected 2026-09-02: the push is built, on the admission leg.**
`AssetsServices.cs:2270 SendForMaintenanceAsync` calls Maintenance's
`IAssetAdmissionService.CreateAdmissionAsync` and stores the result on
`AssetMaintenance.MaintenanceAdmissionId` (a deliberately non-FK column, `AssetsEntities.cs:578-594`);
the endpoint is `POST api/assets/{id}/send-for-maintenance` (`AssetsController.cs:511`). The
service's XML doc explains why admission rather than a work order: the **work-order/job-card leg
is blocked outside HR** — cross-module defect **#9** in `docs/HR/integration/CROSS-MODULE-DEFECTS-FOR-FINALIZATION.md`.
`ProjectService.MaintenanceFollowThrough.cs` resolves `MaintenanceType`, `PriorityLevel` and
`WorkOrderType` by name and throws when none exists; the reference database has 0 rows in all
three, so Projects' own follow-through has never succeeded on any tenant either. Copying that
file is therefore not "porting with no design work left" — it inherits the same dependency. The
fix is Maintenance's (seed the three lookups; the code expects "Corrective", "Medium", "High",
"Standard"), after which HR can add the work-order leg beside the admission one.

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
today: `JobDescription.VersionNumber` (a bare integer, `JobAnalysisEntities.cs:31`) and `HrPolicyDocument.VersionLabel` +
`.SupersedesPolicyId` (a label plus a supersedes link, but no numeric version and no explicit
"under review" status). If a third HR document type ever needs versioning, it would be worth
converging on one shape rather than inventing a third scheme.

---

## 8. Suggested priority order

Ranked by (a) risk if left unaddressed, (b) how directly reusable the existing pattern is, and
(c) how many rows in this document or the Finance sweep it closes at once:

1. **Give the three silent reminder engines (Probation, Separation, IdentificationExpiry) the
   `IAppEventBus` publish the other five already have.** Self-contained, the pattern is in the
   same folder, closes FR-HR-111/140's currently-silent alerts. *(Corrected 2026-09-02 — was "wire
   9 engines to `INotificationService`", which is the wrong count and the wrong mechanism.)*
2. **Add `RowVersion` to the highest-traffic HR entities** (Leave, Benefits, Medical claims,
   Separation, Discipline, Awards, and the SHE approval-bearing ones). Additive migration, low
   risk, closes a genuine data-loss exposure. Payroll loans/advances are the payroll owner's
   (see `HR-PAYROLL-BOUNDARY.md`) — raise, don't edit.
3. ~~**Route `StaffRequisitionCost.ExchangeRate` through `HrCurrencyBridge`**~~ **DONE 2026-09-10
   (round 2b, R7)** — and the payee is now a Procurement `Supplier`, read through `api/hr/suppliers`
   (row 22's "unified payee" is closer by one entity).
4. **Adopt `IFinanceBudgetCommitmentService`'s lifecycle** for `ManpowerBudget`/`TrainingBudget`/
   `AwardBudget`/`StaffTravelBudget` — highest-leverage reuse in this document, but sequence it
   *after* the Finance sweep's training/manpower budget-ownership decision (§4 of the Finance
   sweep), or the wrong entity gets wired first.
5. **Add the work-order leg to the existing send-for-maintenance push once cross-module defect #9
   is fixed** — the admission leg is built; the work-order leg waits on Maintenance seeding three
   lookup tables. Nothing for HR to do until then except keep #9 on the finalization list.
6. **Add a semantic `IHrAuditService`**, modelled on `IFinanceAuditService`, for salary changes,
   bank-detail changes, disciplinary decisions and benefit approvals.
7. **Build HR's payroll report exports on `IFinanceReportExportService`** rather than inventing a
   bespoke generator, once the reports are actually built.
8. Everything in §6 (Inventory-style asset adjustment/reservation/count/transfer maturity) and
   §7.3–§7.6 (Projects-style resource allocation, phase-gates, risk scoring, document versioning)
   — genuinely useful, but lower urgency; revisit once the 🔴 items above are closed.
