# HR Reports Catalogue — What to Build, How to Build It, What to Watch For

**Generated:** 2026-08-31
**Purpose:** A single reference listing every report the HR module needs (existing and missing),
how each should be built/displayed given the patterns already proven elsewhere in this codebase,
and the cross-cutting features/considerations that apply to all of them.

---

## 0. Relationship to the other docs in this folder

| Document | Focus |
|---|---|
| [`HR-FINANCE-ENTITY-SWEEP.md`](HR-FINANCE-ENTITY-SWEEP.md) | Which HR entities carry money and how they should post to Finance. |
| [`HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md`](HR-FINANCE-INTEGRATION-QUICK-REFERENCE.md) | One-page Finance-integration briefing. |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md) | Engineering/business patterns from other modules HR hasn't adopted (concurrency, notifications, budget commitment, asset lifecycle). |
| [`HR-BULK-OPERATIONS-CATALOGUE.md`](HR-BULK-OPERATIONS-CATALOGUE.md) | Which single-item HR actions need a bulk/multi-select equivalent. |
| **This document** | Reporting specifically: what reports must exist, how they should be built/displayed, and what to watch for. |

**One correction this document makes to the cross-module sweep:** that document's §3.5 said HR's
payroll report types (`BankSchedule`/`TaxSchedule`/`PensionSchedule`) have "no service
implementing them" and "zero adoption" of any reporting framework. Direct inspection for this
document found that claim **too strong** — HR has its own separate, working reporting
implementation for Payroll (Oracle RDF crosswalk-driven, with real backend methods and a full
frontend page). What remains true is narrower: HR does not use *Finance's* shared
`IFinanceReportExportService`, and has three **different, uncoordinated** reporting patterns of
its own rather than one. §4 below has the corrected picture and the recommendation.

---

## ⚠ Vetting pass, 2026-09-02 — corrections

Every route, service name and "missing" verdict was re-checked against the controllers and the
`frontend/src/app/hr` tree. Corrections are applied inline; this is the audit trail.

| Where | Verdict | Corrected fact |
|---|---|---|
| §1/§2.3/§5.9 — Asset Register Report "CSV built inline" / "CSV/Excel export" | **WRONG.** | `hr/assets/report/page.tsx` has `window.print()` and `print:hidden` only — no CSV, no XLSX. It does have a real backend, `GET api/assets/reports/register` (`AssetsController.cs:447`), which the doc called "bespoke frontend". The import/export catalogue repeated the error and is corrected too. |
| §1 item 3 / §2.4 — "Thirteen dashboards" | Undercount. | The table lists 14; at least a dozen more dashboard/analytics reads exist (see the addendum under §2.4). |
| §2.4 — `PipDashboardService`, `RecruitmentDashboardService` | **Do not exist.** | `PipDashboardController.cs:21-22` computes directly from `IPerformanceImprovementPlanService` + `ApplicationDbContext`; `RecruitmentDashboardController.cs:127-131` composes five services, and `RecruitmentAnalyticsService` backs `GET analytics`. |
| §2.4 — `/hr/safety/performance/analytics` "(SHE monthly environmental)" | Wrong backend. | Backed by `ShePerformanceController` (`kpis/departmental`, `kpis/contractor-ranking`, `kpis/hazard-heatmap`), not the monthly environmental report. |
| §1 — "23 Oracle reports" | Nuance. | The crosswalk lists 23; `reports/hr/page.tsx` carries 21 distinct `REP3_` codes. The data source is `POST api/hr/payroll/runs/{runId}/oracle-report` (`PayrollController.cs:403`) — payroll owner's file. |
| §2.2/§4.1 — "`HrAwardsReportCatalogue` is HR's most reusable pattern, used exactly once" | Nuance that *strengthens* the recommendation. | It is a **platform** pattern: Inventory, Procurement (×2) and Quantity Survey each have a `*ReportCatalogue.cs` with their own `system://tdc/<module>/` prefix. HR has one definition; the mechanism is proven across four modules. |
| §3 — several 🔲 "missing entirely" rows | **Overstated.** Registers exist as filterable APIs/screens; what is missing is export. | Leave balances (`GET api/leaves/balances`, screen `hr/leave/balances`); Attendance register (`StaffDailyAttendanceController`, `StaffMonthlyAttendanceSummariesController`, screens `hr/attendance/{daily,summaries,overtime}`); **Discipline case register** (`GET api/discipline/cases` + status/severity/offense/date filters, `with/outstanding-fine`, fines `fines/outstanding`, screen `hr/discipline/queues`); **Grievance register** (a literally-named `GET api/hr/employee-relations/register`, paged); Separation register (`GET api/hr/separations`, `retirements/upcoming`, `contract-expiries/upcoming`); Awards register (`GET api/awards` + paged + `pending/presentations`, screen `hr/awards/results`); Training certificate expiry (`training-completions/certificates/expiring`, `employee-certificates/expiring`); Asset insurance expiry (`assets/insurance/{expiring,expired,undated}`, screen `hr/assets/insurance`); Travel advances (`advances/employee/{id}/outstanding`, `advances/overdue-settlements`). Statuses corrected to 🟡 below. Still genuinely 🔲: Headcount (only `stats/*` counts), Leave *request* register (no all-requests list on `LeavesController`), Benefits enrollment register (only by-employee/by-policy), asset **warranty** expiry, CBA coverage. |
| §3.17 — "9 filter routes", `/api/she/performance/...` | Undercount; wrong prefix. | `SafetyIncidentController` has **12** filtered GETs (status, severity, category, date-range, location, employee, for-employee, mine, requiring-investigation, open, lost-time, reportable-pending) + `number/{n}`. **Nothing is under `/api/she/`** — all 28 SHE controllers use `api/safety/*`; contractor ranking is `GET api/safety/performance/kpis/contractor-ranking`. Audits can *issue a report* (`POST audits/{id}/issue-report`) and reviews have a *clearance report* (`GET environmental/reviews/{id}/clearance-report`) — report artefacts exist; file exports do not. |
| §4.6 — "extend `/reports/hr`" | Nuance. | A second landing already exists: `frontend/src/app/reports/human-resources/page.tsx` is a `ModuleReportLandingPage` whose only item links to `/reports/hr`. The hub shell exists and is empty — fill it, don't create a third. |
| §4.5 — `FinanceReportAutomationService` recurrence Daily…Annually | Not verified. | Recipients and `NotifyRecipientsAsync` confirmed; the exact recurrence value list was not found on that service (the only `Recurrence` enum located is `RecurringJournal.cs`). Check before relying on it. |
| Throughout — Payroll reports | Ownership. | Everything under `PayrollController`/`PayrollService`/`reports/hr/page.tsx` is the payroll developer's. HR adds nothing there; see `HR-PAYROLL-BOUNDARY.md`. |

---

## 1. Executive summary

**HR already has more reporting than a first glance suggests**, spread across three genuinely
different, independently-evolved patterns:

1. **Legacy Oracle payroll reports** — 18 core reports + a 5-report bonus family (23 total),
   fully catalogued in [`docs/HR_PAYROLL_ORACLE_REPORTS_CROSSWALK.md`](../HR_PAYROLL_ORACLE_REPORTS_CROSSWALK.md)
   and rendered end-to-end in [`frontend/src/app/reports/hr/page.tsx`](../../frontend/src/app/reports/hr/page.tsx)
   (CSV/Excel via the `XLSX` library, PDF via `GeneratePdf()`). Backed by
   `PayrollReportType` (`RunSummary`/`Payslip`/`BankSchedule`/`TaxSchedule`/`PensionSchedule`/
   `JournalSummary`) and real service methods (`GetRunSummaryReportAsync`,
   `GeneratePayslipSnapshotsAsync`) in `PayrollService.cs`, plus an endpoint that executes the
   original Oracle RDF report directly.
2. **A modern, reusable "system report" pattern** — `HrAwardsReportCatalogue.cs` defines a
   report (`long-service-eligibility`) as *data*: a code, a parameterized query
   (`system://tdc/hr-awards/long-service-eligibility`), a column list and a tag set, seeded into a
   per-tenant `Reports` table and executed through the shared `ISystemReportProvider` — never
   arbitrary SQL. **This is the most reusable pattern found in the whole HR module and it is
   currently used exactly once.**
3. **Thirteen operational dashboards** (Attendance, Performance/analytics, Medical, Orientation,
   Safety/SHE ×2, Training ×2, Recruitment, PIP, Succession, Travel, Separation/analytics,
   Employee Relations/analytics) — each a dedicated backend `*DashboardService` + DTO consumed by
   a matching frontend page. This pattern is **consistently and successfully applied** across HR.
4. Two more bespoke, one-off report pages: the **Asset Register Report**
   ([`frontend/src/app/hr/assets/report/page.tsx`](../../frontend/src/app/hr/assets/report/page.tsx),
   print-optimised via `window.print()` — **no CSV**, corrected 2026-09-02 — backed by
   `GET api/assets/reports/register`) and the **EEO diversity-compliance report**
   ([`EeoReportPanel.tsx`](../../frontend/src/components/hr/recruitment/EeoReportPanel.tsx),
   read-only, per-vacancy).

**What's missing** is not reporting infrastructure — it's **coverage**. Several HR areas have a
dashboard (aggregated, interactive) but no formal exportable/printable *register* report, and
several areas have neither. §3 lists every gap found, organized by HR sub-domain.

**The one structural decision this document recommends:** stop adding a fourth pattern. Every new
report should be built as either (a) a **system report** (§4.1 — the `HrAwardsReportCatalogue`
shape) for register/list/compliance reports, (b) a **dashboard service** (§4.2) for interactive
analytics that already has a home, or (c) a **snapshot document** (§4.3) for fixed-layout
printables like payslips and letters. New payroll statutory reports should keep following the
Oracle-crosswalk pattern **only** because those formats are externally mandated (GRA/SSF) — not
because it's the preferred pattern for anything new.

---

## 2. Inventory of reports that already exist (do not rebuild these)

### 2.1 Legacy Oracle payroll reports (23) — full crosswalk in `HR_PAYROLL_ORACLE_REPORTS_CROSSWALK.md`

| Oracle ID | Report | Variants |
|---|---|---|
| REP3_001 | Payroll Register | All / Detail / Dept / Section / Bank / Negative |
| REP3_035 | Bank Advice | Employee Bank, Branch, Summary, Regional, Arrears |
| REP3_003 | Pay Slip Printing | Company-specific layouts (PAYSLIP_EIC, PAYSLIP_GBC, PAYSLIP_SLTF) |
| REP3_006 | Monthly PAYE | Monthly PAYE, GRA Monthly PAYE |
| REP3_007 | SSF Report | Summary, 1st Tier, 2nd Tier, 1st Tier New, Tier 3, Format B |
| REP3_002 | Payroll Analysis (Month) | Department, Region, Individual |
| REP3_009 | Bank Advice by Employer Bank | Cedi-Cedi, Cedi-Dollar, Dollar-Cedi, Dollar-Dollar |
| REP3_010 | Allowances & Deductions Schedule | All, Allowances, Deductions, Advance, Loan Repayment, Loan Interest |
| REP3_011 | Cash List | — |
| REP3_016 | Journal Report | Summary, Detail |
| REP3_019 | Overtime Report | — |
| REP3_034 | Annual Tax Returns | Income Tax Form, Annual Return, Tax Register, Tax Certificate, By Location |
| REP3_305 | Staff Lists | Department, Category, Gender |
| REP3_036 | Loan Statement | Individual, Facility, Loan Type, Detailed |
| REP3_029/028/030/031/032 | Bonus family | Register, Slip, Tax Report, Bank Advice, Cash List |

### 2.2 Modern "system report" pattern (1)
- **Long-service award eligibility** (FR-HR-113) — `HrAwardsReportCatalogue.cs`.

### 2.3 Bespoke report pages (2)
- **Asset Register Report** — filters (Type/Unit/Location/Status/As-of date), breakdowns by
  status/type/condition/unit/location, watchlist counts, print-optimised CSS classes. Print only;
  no CSV (corrected 2026-09-02). Backend: `GET api/assets/reports/register`.
- **EEO diversity-compliance report** — gender/age/internal-vs-external breakdown per vacancy at
  every pipeline stage (all applicants → shortlisted → rejected → hired), read-only.

### 2.4 Dashboards (13) — interactive, not exportable registers

| Dashboard | Backend service | Frontend route |
|---|---|---|
| Attendance | `AttendanceDashboardService` | `/hr/attendance` |
| Performance analytics | `HRCycleDashboardQueryService` | `/hr/performance/analytics` |
| PIP (org-wide) | `PipDashboardController` computes inline (no service) | `/hr/performance/pip` |
| Medical | `MedicalDashboardService` | `/hr/medical/dashboard` |
| Orientation | `OrientationDashboardService` | `/hr/orientation/dashboard` |
| Safety/SHE | `SheDashboardService` | `/hr/safety/dashboard` |
| Safety performance | `ShePerformanceController` (`kpis/departmental`, `kpis/contractor-ranking`, `kpis/hazard-heatmap`) | `/hr/safety/performance/analytics` |

**Correction, 2026-08-31:** SHE is not merely a dashboard the way this table implies. A follow-up
field-level pass found real register endpoints (incident, audit, environmental review) and one
fully-automated report (`SheMonthlyEnvironmentalReport`) — see the corrected §3.17.
| Training analytics | `TrainingDashboardService` | `/hr/training/analytics` |
| Training compliance | `TrainingDashboardService` | `/hr/training/compliance` |
| Recruitment | `RecruitmentDashboardController` composes five services; `RecruitmentAnalyticsService` behind `GET analytics` | `/hr/recruitment/dashboard` |
| Succession | `SuccessionPlanController` `GET dashboard` | `/hr/succession/dashboard` |
| Travel | `StaffTravelRequestsController` dashboard read | `/hr/travel/dashboard` |
| Separation analytics | `SeparationsController` analytics reads | `/hr/separations/analytics` |
| Employee Relations analytics | `StaffGrievancesController` + `EmployeeRelationsAnalyticsService` | `/hr/employee-relations/analytics` |

**Dashboards/analytics the table above omits (added 2026-09-02):** the HR home page itself is a
metric-tile dashboard (`hr/page.tsx`) fed by Discipline `GET api/discipline/cases/dashboard` and
Movements `GET api/staff-movements/dashboard`; Talent pool (`TalentPoolController`,
`hr/recruitment/talent-pool`); Job architecture (`JobAnalysisController` → `hr/job-descriptions`
and `/gaps`); Company/Unit goal dashboards; Employee stats (`EmployeesController` `stats/*`);
Position-vacancy stats; Performance trend (`PerformanceAnalyticsController`); Leave compliance
(`hr/leave/compliance`); SHE sustainability KPIs (`hr/safety/environmental/sustainability`), SHE
corrective-action summary, and the SHE monthly-report pages
(`hr/safety/environmental/monthly-reports`). Portal dashboards exist for candidates, employees
and consultant clients. The pattern is applied more widely than the count suggests; the gap in §3
is still export/print, not aggregation.

---

## 3. Master list of reports HR needs, by sub-domain

Legend — **Status**: ✅ exists · 🟡 dashboard exists but no formal register/export · 🔲 missing
entirely. **Sensitivity**: 🟢 general HR access · 🟡 manager/HR-restricted · 🔴 HR-admin/Finance
only (bank details, salary, disciplinary, medical).

### 3.1 Organization & headcount

| Report | Purpose | Suggested filters | Status | Sensitivity |
|---|---|---|---|---|
| **Headcount Report** | Employee count by department/grade/unit/status, snapshot or trend over time | As-of date, org unit, employment type, staff level | 🔲 | 🟢 |
| **Establishment vs. Actual Headcount** | `ExpectedHeadcount` (position/org-unit budgeted) vs. actual filled positions, ties to Manpower Budget (see Finance sweep row 5-7) | Org unit, fiscal year | 🔲 | 🟡 |
| **Organization Chart Export** | Printable reporting-line structure (distinct from the Job Analysis "Reporting Relationships" panel, which is deliberately not this) | Org unit, as-of date | 🔲 | 🟢 |
| **Staff List** | Name/position/department/contact roster | Department, category, gender (already an Oracle variant, REP3_305) | ✅ (Oracle) | 🟢 |

### 3.2 Payroll & compensation

Covered comprehensively by the 23 Oracle reports (§2.1) plus `RunSummary`/`JournalSummary`. Gaps:

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Payroll Journal → GL reconciliation report** | Ties payroll's `JournalSummary` output to what actually posted in Finance — becomes meaningful once Payroll migrates to `IFinancePostingEngine` (see Finance sweep row 15 / governance update) | 🔲 | 🔴 |
| **Pay Equity / Compa-ratio Report** | Salary distribution vs. `SalaryGrade` band (min/mid/max) by grade, department, gender — a standard compliance report this codebase's rich `SalaryGrade`/`SalaryLevel`/`SalaryNotch` model already supports | 🔲 | 🔴 |
| **Emolument Summary Register** | Roll-up of `EmployeeEmolumentSummaryDto` (basic + allowances − deductions) across the whole workforce, not just per-employee | 🔲 | 🔴 |

### 3.3 Leave

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Leave Register** | Leave taken/approved/pending by employee/type/period — `LeavesController` has only per-employee history and `pending-approvals/{managerId}`; no all-requests list | 🔲 | 🟡 |
| **Leave Balance Report** | Entitlement/carried-over/used/available days by employee, org unit — `GET api/leaves/balances`, `employee/{id}/balances`, screen `hr/leave/balances`; no export (corrected 2026-09-02) | 🟡 | 🟡 |
| **Leave Encashment Register** | `LeaveEncashment` payouts — ties to the Finance sweep's leave-encashment posting gap (row 23) | 🔲 | 🔴 |
| **Leave Liability Report** | Aggregate `LeaveBalance.EncashedDays`/unused-day value — the balance-sheet-adjacent figure the Finance sweep flagged as unmodelled (row 24) | 🔲 | 🔴 |

### 3.4 Attendance & time

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Daily/Monthly Attendance Register** | Presence, lateness, absence, overtime by employee/department/period — `StaffDailyAttendanceController` (paged/date/status/overtime/late) and `StaffMonthlyAttendanceSummariesController` (year-month, pay-period) expose it; screens `hr/attendance/{daily,summaries,overtime}`; no export (corrected 2026-09-02) | 🟡 | 🟡 |
| **Overtime Report** | Hours, approval status, cost implication — Oracle REP3_019 exists for Payroll's overtime; a live-system equivalent tied to `StaffDailyAttendance.OvertimeHours` does not | 🔲 | 🟡 |
| **Exception/Regularization Report** | Outstanding `StaffAttendanceRegularization` requests, aging | 🔲 | 🟡 |

### 3.5 Recruitment

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Recruitment Pipeline Report (exportable)** | Same data as the dashboard, formal register for board/audit packs | 🟡 | 🟢 |
| **Time-to-Hire / Time-to-Shortlist Report** | SLA compliance using `JobVacancy.TimeToShortlistDays`/`ShortlistingSlaBreached` | 🟡 (dashboard only) | 🟢 |
| **EEO Report (export)** | Currently read-only/no export (§2.3) — add CSV/PDF | 🟡 | 🔴 |
| **Cost-per-Hire Report** | Ties recruitment cost fields (Finance sweep rows 8-9) to actual hires | 🔲 | 🟡 |

### 3.6 Performance & appraisal

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Appraisal Completion Register** | Who has/hasn't completed each phase (self-eval, manager-eval, calibration) by cycle | 🟡 (analytics dashboard, no register) | 🟡 |
| **Ratings Distribution Report** | Score/rating distribution by department — the standard "forced curve" compliance check | 🔲 | 🔴 |
| **Goal Achievement Report** | `EmployeeGoal` completion rates — feeds Awards' performance-triggered nomination logic (already cross-referenced in the Awards entity) | 🔲 | 🟡 |
| **PIP Register** | Cases, stage, outcome — dashboard exists, no formal register | 🟡 | 🔴 |

### 3.7 Benefits & Medical

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Benefits Enrollment Register** | Who is enrolled in what, contribution split, coverage utilization | 🔲 | 🔴 |
| **Medical Claims Report** | Claims by status/facility/provider/period — dashboard exists, no formal exportable register (priority: this is the same claim-payment path the Finance sweep flagged as the priority back-fill) | 🟡 | 🔴 |
| **Insurance Utilization Report** | `EmployeeMedicalInsurancePolicy.UtilizedAmount` vs. plan limits, by plan/provider | 🔲 | 🔴 |
| **NHIS/Statutory Health Report** | If TDC requires NHIS-related reporting — flagged, not found in codebase today | 🔲 | 🔴 |

### 3.8 Awards & recognition

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Long-Service Eligibility** | Already built (§2.2) — the reference pattern | ✅ | 🟢 |
| **Awards Register** | All awards conferred, level, value, payment status — `GET api/awards` (+ paged, `employee/{id}`, `pending/presentations`), screen `hr/awards/results`; no export (corrected 2026-09-02) | 🟡 | 🟡 |
| **Award Budget Utilization Report** | `AwardBudget` spent/reserved/available by type/year — same shape as the manpower/training budget gap in the Finance sweep | 🔲 | 🟡 |

### 3.9 Assets

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Asset Register Report** | Already built (§2.3) | ✅ | 🟢 |
| **Asset Surcharge Register** | Outstanding/recovered/waived surcharges by employee — an employee-receivable register, same shape as the Finance sweep's AR read-model gap | 🔲 | 🔴 |
| **Insurance Expiry Report** | `assets/insurance/{expiring,expired,undated}` + screen `hr/assets/insurance` exist; no export (corrected 2026-09-02) | 🟡 | 🟡 |
| **Warranty Expiry Report** | No endpoint (only a comment at `AssetsController.cs:1100`) — feeds `AssetReminder`, should also be a pull-able register | 🔲 | 🟡 |

### 3.10 Discipline

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Discipline Case Register** | All cases, offense, action, status, outcome — `GET api/discipline/cases` with status/severity/offense/date-range filters and `with/outstanding-fine`; screen `hr/discipline/queues`; no export (corrected 2026-09-02 — "nothing found" was wrong) | 🟡 | 🔴 |
| **Fine Recovery Report** | `StaffDisciplineFine` assessed/paid — `fines/outstanding` exists on the sub-entity controller; no register/export; fines have no Finance treatment either (Finance sweep row 42) | 🟡 | 🔴 |

### 3.11 Training & development

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Training Completion Report** | Dashboard exists (analytics + compliance); certificate-expiry lists exist (`training-completions/certificates/expiring`, `employee-certificates/expiring`, plus orientation and SHE training equivalents); no export (corrected 2026-09-02) | 🟡 | 🟡 |
| **Training Budget Utilization Report** | `TrainingBudget`/`TrainingBudgetTransaction` — same three-way-double-count caveat as the Finance sweep (§3.5 of that doc) | 🔲 | 🟡 |
| **Service Bond Register** | Outstanding `TrainingServiceBond` obligations, at-risk-of-forfeit employees | 🔲 | 🔴 |

### 3.12 Separation & exit

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Separation/Exit Register** | `GET api/hr/separations` (paged), `retirements/upcoming`, `contract-expiries/upcoming`, per-record `settlement`/`clearance`; dashboard exists; no export | 🟡 | 🔴 |
| **Final Settlement Report** | `SeparationSettlementLine` payable/recoverable breakdown per leaver — same "largest single money event" the Finance sweep names | 🔲 | 🔴 |
| **Clearance Outstanding Report** | Employees with unresolved `SeparationClearanceItem` rows blocking exit | 🔲 | 🟡 |

### 3.13 Grievance & employee relations

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Grievance Register** | A literally-named paged register exists: `GET api/hr/employee-relations/register` (page size capped at 200); analytics dashboard too; no export (corrected 2026-09-02) | 🟡 | 🔴 |

### 3.14 Succession & talent

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Succession Readiness Report** | Dashboard exists; no formal register of positions/candidates/readiness by org unit | 🟡 | 🔴 |
| **Development Activity Cost Report** | `SuccessionDevelopmentActivity.EstimatedCost`/`.ActualCost` — same three-way training-budget overlap flagged in the Finance sweep | 🔲 | 🟡 |

### 3.15 Travel

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Travel Expense Register** | Dashboard exists (status); no formal claims/advances register — same reimbursement gap the Finance sweep names as unresolved | 🟡 | 🔴 |
| **Outstanding Travel Advance Report** | `advances/employee/{id}/outstanding`, `advances/overdue-settlements`, `advances/status/{status}` exist on `StaffTravelFinanceController`; no tenant-wide aging view or export (corrected 2026-09-02) | 🟡 | 🔴 |

### 3.16 Union / CBA

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **CBA Coverage Report** | Which employees/positions fall under which `CollectiveBargainingAgreement` | 🔲 | 🟡 |

### 3.17 Safety, Health & Environment (SHE) — corrected 2026-08-31, more mature than first assumed

Confirmed via a field-level pass: SHE has **88 entity classes** (all in `StaffSafetyEntities.cs`)
and 28 controllers, every one under `api/safety/*`, and already exposes real, filterable register
endpoints (`SafetyIncidentController` alone has 12 filtered reads — status, severity, category,
date-range, location, employee, for-employee, mine, requiring-investigation, open, lost-time,
reportable-pending — plus `number/{n}`). This is ahead of most other HR sub-domains in
list/register coverage. What it lacks is **file export**; it does already produce *report
artefacts* (audit issue-report, environmental clearance report, the monthly environmental report).
See `HR-SHE-INTEGRATION-AND-BOUNDARIES.md` for the module as a whole.

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **Incident Register (export)** | The incident register already exists as a filterable API (`GET api/safety/incidents` + 11 filtered routes) with no CSV/PDF export — same gap as the Asset Register | 🟡 API exists, no export | 🔴 |
| **SHE Audit Register (export)** | `SheAuditController` exposes the audit/finding trail and `POST audits/{id}/issue-report` produces the audit report artefact; no register export | 🟡 API + report artefact, no export | 🟡 |
| **Environmental Review Register (export)** | `SheEnvironmentalReviewController`; `GET environmental/reviews/{id}/clearance-report` exists per review; no register export | 🟡 API + clearance report, no export | 🟡 |
| **Contractor SHE Ranking Report** | `GET api/safety/performance/kpis/contractor-ranking` (corrected 2026-09-02 — not `/api/she/`) already computes and ranks by compliance score, non-compliance count, pre-qualification score — a genuinely complete report, just not exportable/printable for a board pack | 🟡 API exists, no export | 🟡 |
| **Monthly Environmental Report** | `SheMonthlyEnvironmentalReport` is auto-generated monthly (obligations compliance %, waste/recycling rate, incidents, audit findings, sustainability cost savings) — the closest thing in all of HR to a fully-automated Pattern-A-style report already working end to end | ✅ Built, auto-generated | 🟡 |
| **Incident Insurance Claim Report** | `SafetyIncident.ClaimAmount`/`.AmountPaid` (added to `HR-FINANCE-ENTITY-SWEEP.md` row 60) has no reporting view of outstanding vs. paid claims | 🔲 | 🔴 |

### 3.18 Cross-HR / statutory (Ghana-specific)

| Report | Purpose | Status | Sensitivity |
|---|---|---|---|
| **SSNIT Contribution Report** | Not found in codebase (only "SSF" appears — confirm with TDC whether SSF and SSNIT are used interchangeably in this context, or whether a separate statutory report is required) | 🔲 (needs TDC confirmation) | 🔴 |
| **Ghana Labour Department Returns** | Not found in codebase — flagged as a likely statutory requirement to confirm with TDC | 🔲 (needs TDC confirmation) | 🔴 |
| **Diversity & Inclusion Report (org-wide)** | EEO report exists per-vacancy only; an org-wide, periodic version (gender/age/disability distribution across the whole workforce) does not | 🔲 | 🟡 |

---

## 4. How reports should be built and displayed

### 4.1 Pattern A — System report (register/list/compliance reports): **the recommended default**

Model: `HrAwardsReportCatalogue.cs` + `ISystemReportProvider` (executed by
`Api/Services/Reports/HrAwardsReportService.cs`, seeded by migration
`20260821233000_TDC0703HrAwardsReportCatalogue`). *(Added 2026-09-02.)* This is a **platform**
pattern, not an HR invention: `Inventory/InventoryStatutoryReportCatalogue.cs`,
`Procurement/ProcurementStatutoryReportCatalogue.cs`, `Procurement/AuditComplianceReportCatalogue.cs`
and `QuantitySurvey/QuantitySurveyStatutoryReportCatalogue.cs` each define reports the same way
under their own `system://tdc/<module>/` prefix. Copy one of those when adding the second HR
catalogue; the seeding migration is part of the pattern.

```csharp
public sealed record HrAwardsSystemReportDefinition(
    string Code,             // stable identifier, e.g. "leave-register"
    string Name,
    string Description,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<string> Tags)
{
    public string Query => QueryPrefix + Code;   // resolves to a parameterized, pre-approved query
}
```

**Why this is the right default for most of §3's gaps:** the report is *data*, not code — a row
in a per-tenant `Reports` table with a name, a parameterized query, a column list and tags. It is
executed through the shared `ISystemReportProvider`, which **never accepts arbitrary SQL**, so
adding "Leave Register" or "Discipline Case Register" is a seeding exercise, not a new controller
and a new frontend page each time. This is also the only one of HR's three patterns already proven
to generalize (it exists for exactly one report today, but nothing about its shape is
Awards-specific).

**When to use it:** any report in §3 that is a filtered, columnar list of records — Leave
Register, Discipline Case Register, Asset Surcharge Register, Benefits Enrollment Register, Awards
Register, etc. This should be the default choice unless a report is genuinely interactive
(→ 4.2) or a fixed printable per-person document (→ 4.3).

### 4.2 Pattern B — Dashboard service (interactive analytics): keep using it, it works

Model: any of the 13 existing `*DashboardService` + DTO pairs.

**When to use it:** aggregated, chart-friendly, drill-in analytics where the audience wants trends
and KPIs rather than a row-per-record export — e.g. the existing Recruitment/Training/Safety
dashboards. **Do not** use this pattern for something that's really a register report someone
wants to export and file (that's 4.1) — the two have been kept separate correctly so far
(e.g. Recruitment has both a dashboard *and*, per §3.5, should gain an exportable pipeline
register alongside it).

### 4.3 Pattern C — Snapshot document (fixed-layout, per-person printables)

Model: `PayrollReportSnapshot` (`SnapshotJson` + `GeneratePdf()`), already used for Payslips and
Award letters.

**When to use it:** anything that is fundamentally a *document* rather than a *report* — a
payslip, an award certificate, an HR letter, a final settlement statement handed to a leaving
employee. The snapshot is taken once and re-rendered identically later (audit trail: what the
person was actually shown), rather than recomputed live like a register report would be.

### 4.4 What NOT to do: the legacy Oracle pattern is a closed set, not a template

The 23 Oracle-crosswalk payroll reports (§2.1) exist because their **exact layout is externally
mandated** (GRA/SSF forms, bank-file formats) and because migrating a working legacy report
faithfully was the goal. That is a legitimate reason for a report to look hand-built and
XLSX-library-driven in the frontend. **It is not a reason to build the next report that way.**
Any *new* statutory report (e.g. an SSNIT or Labour Department return, §3.17) should still be
implemented as Pattern A (4.1) wherever the target format allows a parameterized query, reserving
the fully bespoke frontend/XLSX approach for cases where the output must byte-for-byte match an
externally-imposed template.

### 4.5 Where Finance's `IFinanceReportExportService` fits (and doesn't)

Finance's framework (CSV-only, hand-built-per-report-type, audit-logged via
`Finance.Report.Exported`, with a genuine scheduling/automation layer —
`FinanceReportAutomationService`, recurrence Daily/Weekly/Monthly/Quarterly/Annually, email
delivery to named recipients) is **not** something HR should adopt wholesale — it solves Finance's
specific problem (many report *types* against one well-understood GL/subledger data model) with a
pattern HR's own Pattern A already does more cheaply for HR's shape of problem (many report
*instances* of a similar list/register shape). **The one piece worth borrowing directly is the
scheduling/automation layer** — HR has no equivalent today, and "email me the Leave Register every
Monday morning" is a real, recurring HR need with no mechanism to satisfy it. If/when HR needs
scheduled report delivery, evaluate reusing `FinanceReportAutomationService`'s
schedule/recurrence/recipient model (possibly via a shared, module-agnostic scheduling service)
rather than building a second one from scratch.

### 4.6 Consolidate discovery: one HR Reports Hub

Today a user has to already know that payroll reports live at `/reports/hr`, the asset register
lives at `/hr/assets/report`, and everything else is a dashboard scattered across `/hr/<area>/...`.
**Recommendation:** fill the hub shell that already exists — `frontend/src/app/reports/human-resources/page.tsx`
is a `ModuleReportLandingPage` (`components/reports/module-report-landings.ts:87-92`) whose only
item links to `/reports/hr`, the payroll-only page (which is the payroll developer's; leave it).
Add every report in §3 to that landing, grouped by the same sub-domain headings used there, so
"where do I find X" has one answer. This is a navigation/UX change, not a new backend pattern.
*(Corrected 2026-09-02 — the earlier text would have created a third landing.)*

---

## 5. Cross-cutting features every report needs

1. **Single source of truth.** A report must read through the same service/query the on-screen
   feature already uses — never recompute totals independently (Finance's stated principle,
   equally valid for HR: a Leave Register that disagrees with the Leave Balance screen is worse
   than no report).
2. **Tenant scoping.** Automatic via `TenantEntity` query filters — already consistent across HR
   (confirmed in the cross-module sweep), just don't bypass it with `IgnoreQueryFilters()` in a
   report path without a specific, audited reason.
3. **Permission gating per report, not per module.** Reuse the `HrPermissions.*ReadPolicy` /
   `*AdminPolicy` convention already established; sensitive reports (🔴 in §3) need a **tighter**
   policy than the module's general read policy — e.g. a Salary/Compa-ratio report should not be
   visible to every holder of `HrPermissions.EmployeeReadPolicy`.
4. **Field-level masking for sensitive columns.** Bank account numbers, national ID/SSN, medical
   detail — mirror the pattern already used in `ProfileChangeRequestDto.BankAccountMasked`
   (last-four-digits masking) rather than inventing a new convention per report.
5. **Filters that match how HR actually asks the question.** At minimum: as-of/date-range, org
   unit (with descendant-unit inclusion, given the multi-level `OrganizationUnit` hierarchy),
   employment status, and — for anything touching money — currency. Avoid a single "employee
   picker" as the only filter; most of §3's reports are read by a manager about their unit or by
   HR/Finance across the whole tenant, rarely about one named person.
6. **Export formats matched to audience.** CSV for anything that feeds another system or a
   spreadsheet analysis; PDF for anything printed or filed (board packs, statutory submissions,
   employee-facing documents); Excel only where multi-sheet/pivot-friendly output is genuinely
   useful (the Oracle payroll reports already do this appropriately).
7. **Audit the act of reporting, not just the data.** Every report run should log who ran it,
   with what filters, how many rows, and in what format — mirroring Finance's
   `Finance.Report.Exported`/`.Printed` audit events. This matters more in HR than Finance for the
   🔴-tier reports (salary, medical, disciplinary) precisely because they are personal data.
8. **Large datasets need a plan.** Finance's own framework loads everything into memory
   synchronously with no streaming or pagination — acceptable for GL-sized datasets, riskier for
   an all-tenant, all-time Attendance or Leave register that could be materially larger. Decide a
   sensible default filter (e.g. current fiscal year, current org unit) rather than defaulting to
   "everything," and consider a background/async export path for anything that can't be bounded.
9. **Print layout conventions.** The Asset Register Report's `print:hidden` CSS class pattern
   (hide filters/buttons, keep only the report body on print) is worth reusing verbatim rather
   than reinventing per report.
10. **Statutory report formats will change.** GRA/SSF/SSNIT formats are externally controlled;
    keep the Oracle-crosswalk reports' external-template mapping explicit and versioned (as the
    crosswalk doc already does) so a future format change is a data/template update, not a code
    change hunting through report-generation logic.
11. **Retention & artifact lifecycle.** If scheduled/emailed reports are built (per §4.5), decide
    a retention period for generated artifacts and who can retrieve historical runs — this is
    unresolved even in Finance's own automation layer per the research for this document (no
    explicit retention policy was found).
12. **Data-protection review for anything with PII leaving the system.** Any report exported as a
    file (especially emailed ones) is data leaving the application's access-control boundary.
    Flag for TDC/legal input on whatever Ghana's data-protection obligations require for exported
    personal data (this codebase does not appear to have settled this anywhere yet) — treat it as
    an open question alongside the others already tracked in `docs/HR-OPEN-QUESTIONS-FOR-TDC.md`.

---

## 6. Suggested build priority

1. **Compliance/statutory gaps with no existing coverage at all:** Discipline Case Register,
   Benefits Enrollment Register, Medical Claims Register (export), Leave Encashment Register —
   all 🔴-tier, all currently invisible to audit.
2. **Registers for areas that already have a dashboard** (cheapest to add — the aggregation logic
   already exists, only the row-level export is missing): Separation/Exit Register, Grievance
   Register, Succession Readiness Report, Travel Expense Register, PIP Register, Appraisal
   Completion Register.
3. **Operational basics with no equivalent anywhere:** Leave Register, Leave Balance Report,
   Attendance Register, Headcount Report — high-usage, low-controversy, good first candidates for
   proving out Pattern A (§4.1) beyond its one existing use.
4. **Money-adjacent registers that double as Finance-sweep back-fill evidence:** Asset Surcharge
   Register, Training Budget Utilization, Award Budget Utilization, Outstanding Travel Advance
   Report — building these surfaces the same balances the Finance sweep already flagged as
   unreconciled, so they are useful even before the Finance-posting questions are settled.
5. **Everything else in §3**, roughly in the order listed per sub-domain.
6. **Only after the above:** the Reports Hub consolidation (§4.6) and any scheduled-delivery
   capability (§4.5) — both are navigation/convenience improvements, not coverage gaps.
