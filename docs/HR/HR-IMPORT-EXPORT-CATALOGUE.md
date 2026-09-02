# HR & SHE Import/Export Catalogue — Which Entities Need It, and How to Build It

**Generated:** 2026-08-31
**Purpose:** A comprehensive sweep of every HR and Safety/SHE entity, identifying which ones need
a bulk **import** (load many records from a file) and/or **export** (extract entity data to a
file) capability, what already exists, and how the missing ones should be built.

---

## 0. Relationship to the other docs in this folder

| Document | How it relates |
|---|---|
| [`HR-REPORTS-CATALOGUE.md`](HR-REPORTS-CATALOGUE.md) | Overlaps on **export**: a "report" and a raw data "export" are often the same button. Where a gap is already tracked there as a missing report, this document doesn't re-litigate it — it adds the *import* side, which that document doesn't cover, and calls out exports that are pure data extracts rather than formatted reports. |
| [`HR-BULK-OPERATIONS-CATALOGUE.md`](HR-BULK-OPERATIONS-CATALOGUE.md) | Different kind of "bulk": that document is about bulk **actions** (approve/reject many existing records at once). This document is about bulk **data** (create/update many records from a file, or extract many records to one). Don't conflate the two — a bulk-approve endpoint and a bulk-import endpoint solve different problems even when both operate on "many rows at once." |
| [`HR-CROSS-MODULE-PATTERNS-SWEEP.md`](HR-CROSS-MODULE-PATTERNS-SWEEP.md) | Already named Finance's `BulkImportAssets` pattern (Excel + dry-run + row-level errors) as worth copying for Inventory-style asset lifecycle work. This document applies that same recommendation more broadly, as the one pattern HR should standardize its imports on. |
| [`HR-UAT-DEMO-PRESENTATION-PLAN.md`](HR-UAT-DEMO-PRESENTATION-PLAN.md) | §6 of that document names the single biggest data-readiness caveat found this session — most organisational units have no assigned head. §3 below explains why a proper bulk-import/correction tool for org structure is the direct fix for that, not just a data-entry request to TDC. |

---

## ⚠ Vetting pass, 2026-09-02 — corrections

| Where | Verdict | Corrected fact |
|---|---|---|
| §1 — "no bulk import for the core `Employee` entity **at all**" | Right about bulk, wrong about "at all". | A **single-record** import exists: `POST api/employees/import` (`EmployeesController.cs:162`, `EmployeeAdminPolicy`) → `EmployeeService.ImportEmployeeAsync` → `StaffNumberService.AcceptImportedAsync`. No frontend caller. **This is the seam an Employee bulk import must be built on** — see §4.1's staff-number rule, which the earlier text did not know about. |
| §2 — Asset Register Report "Export (CSV/Excel)" | **WRONG.** | Print only (`window.print()`); no CSV or XLSX anywhere in `hr/assets/report/page.tsx`. Backend register read: `GET api/assets/reports/register`. |
| §2 — `StaffAttendancePayrollExportsController` "export" | Nuance. | `POST export` creates an export **record** (`ExportReference`, `TargetSystem`, `TotalEmployees`, `Status`) — **no file is produced**. It is a hand-off ledger, not an export. |
| §2 — `JobApplicationController` `shortlist/export` | Confirmed — and it is the **only** HR controller that emits a CSV. | |
| §3.3 — `PublicHoliday` "notably absent" | **WRONG.** | `PublicHoliday` exists (`StaffAttendanceEntities.cs:1365`, with `HolidayCalendarId`, `DateFrom/DateTo`, `Recurring`, `AttractsHolidayPay`, `HolidayPayMultiplier`) with `HolidayCalendarsController` (`api/holiday-calendars`: per-holiday CRUD, `holidays/by-year/{year}`, `holidays/range`, `recurring`), plus a separate `PayrollHoliday` in the payroll module. No bulk load — that gap stands. TDC has no calendar loaded: `docs/HR-OPEN-QUESTIONS-FOR-TDC.md` §3. |
| §3.4 — biometric feed | Confirmed, with the precise state. | `StaffAttendanceDevice` (`LastSyncDate`, `PendingSyncCount`) + `StaffAttendanceDevicesController` (`external/{externalDeviceId}`, `overdue-sync`, `{id}/sync` — a bookkeeping stamp, not a data pull); `EmployeeBiometric` + enrol/revoke. `POST api/staff-attendance-logs/punch` takes the actor from the **token's** EmployeeId — a self-service punch, not a device feed. No device-authenticated ingestion endpoint. "Verify, don't build" stands. |
| §3.10 — "SHE has no import and no export anywhere" | Confirmed for file I/O; nuance. | SHE does produce **report artefacts**: `POST audits/{id}/issue-report`, `GET environmental/reviews/{id}/clearance-report`, monthly-report list/get/generate/submit with two frontend pages. Reports exist, exports don't. |
| §4.3 — `DataExportTools.tsx` | Confirmed mock. | Mounted only by `app/administration/reports/page.tsx` — an **Administration** screen, not an HR one; HR did not build it and should not be the one to relabel it without the platform owner. |
| §2 — existing bulk-load mechanisms omitted | Add. | `Program.cs` CLI commands `seed-hr-all` (`HrSeedOrchestrator`), `seed-hr-demo` (`HrDemoSeedOrchestrator`), `seed-hr-org-authority`, wrapped by `scripts/New-UatDatabase.ps1`. **All bypass the service layer** — which is exactly why the staff-number counter hazard in §4.1 exists. `TdcDemoPersonaSeeder` links 8 demo logins to existing employees; it loads no employees. |
| §2 — other I/O the table missed | Add. | `POST api/hr/payroll/runs/{runId}/oracle-report` (the Oracle report executor, payroll dev's); `POST api/hr/separations/clearance-templates/seed-defaults`; client-side CSV in `components/hr/payroll/PayrollGridExportButton.tsx`; screens `hr/attendance/payroll-exports` and `hr/employees/payroll-reconciliation` (a read, not an import UI). |

---

## 1. Executive summary

**HR has five real import/export capabilities today (four bulk, one single-record), and they're inconsistent with each other:**
Attendance has a genuine staged bulk-import (upload → review → process, with row-level status).
Payroll has an employee-reconciliation import (matches legacy IDs to HR employees). Two exports
exist: a payroll-handoff export for finalized attendance, and a CSV export of a recruitment
shortlist. The Asset Register Report and the legacy Oracle payroll reports both export to
CSV/Excel from the frontend. **Each of these was built independently, with its own row-status
enum and its own error-reporting shape — there is no shared import/export infrastructure in HR**,
unlike Finance, which has one well-built reference pattern (`BulkImportAssets`: Excel upload,
dry-run mode, row-level field errors, a reusable `BulkImportResultDto`).

**Three gaps stand out as genuinely significant, not just "nice to have":**

1. **There is no bulk import for the core `Employee` entity** — only a single-record
   `POST api/employees/import` with no screen. No onboarding-a-whole-workforce tool, and no
   correction tool for a partial legacy migration. This matters because
   this session's own research already found live evidence of incomplete migrated data (a
   measured finding that only a small fraction of employees had a salary on record at one point).
   A proper import/correction tool is the direct fix for exactly that kind of gap — not a
   one-off script, a real, repeatable, dry-run-capable tool.
2. **Safety/SHE has zero import or export capability**, despite being a 60+ entity sub-module
   with real compliance stakes (incident registers, audit trails, contractor compliance records)
   that regulators and auditors will eventually want extracted, and that a safety officer
   onboarding a new site's worth of contractors, hazards, or PPE stock will want to bulk-load.
3. **There is no tenant-level data export.** A `DataExportTools` component exists in the frontend
   but is confirmed **mock data only** — it renders as if the feature exists without a backend
   behind it. There is no GDPR/data-subject-access-request export, no backup-style full extract,
   and no migration-out capability anywhere in HR.

---

## 2. What already exists (do not rebuild these)

| Capability | Entity | Direction | Where | Notes |
|---|---|---|---|---|
| Single-record employee import | `Employee` | Import (one row per call) | `POST api/employees/import` (`EmployeesController.cs:162`, Admin-tier) → `EmployeeService.ImportEmployeeAsync` → `StaffNumberService.AcceptImportedAsync` | Accepts a supplied staff number verbatim and advances the tenant's number sequence past it. No screen. **The seam a bulk Employee import must wrap** — see §4.1 (added 2026-09-02) |
| Bulk attendance import | `StaffBulkAttendanceImport` | Import | `StaffBulkAttendanceImportsController` (`api/staff-bulk-attendance-imports`) + screens `hr/attendance/imports[/new,/[id]]` | The most mature import in HR: upload → stage → review → process, with per-row status |
| Payroll employee reconciliation import | `PayrollImportBatch` | Import | `PayrollController` (`imports/employee-reconciliation`) | Matches legacy employee numbers/IDs; tracks matched/unmatched/duplicate rows |
| Job offer benefit import | `JobOfferBenefit` | Import (in-memory, not file-based) | `JobOfferController` | Copies benefits from a position's grade into an offer — not a bulk-file import, listed here only so it isn't mistaken for one |
| Legacy file migration | Documents (not data records) | Import | `HrLegacyFileMigrationController` | Moves old files into the central DMS; dry-run supported; SuperAdmin/cross-tenant scope. Out of this document's scope (files, not entity data) but the dry-run pattern is worth copying |
| Attendance-to-payroll hand-off | `StaffAttendancePayrollExport` | Hand-off **record** (no file) | `StaffAttendancePayrollExportsController` (`POST export`) | Finalized attendance for a closed pay period, with an audit trail and status tracking — it records *that* a hand-off happened; nothing is written to disk (corrected 2026-09-02) |
| Recruitment shortlist export | N/A (query result) | Export (CSV) | `JobApplicationController` (`shortlist/export`) | Candidate name/email/phone/score/status for a vacancy — **the only CSV emitted by any HR controller** |
| Asset Register Report | `CompanyAsset` | Print only | `frontend/src/app/hr/assets/report/page.tsx` + `GET api/assets/reports/register` | `window.print()`; **no CSV/Excel** (corrected 2026-09-02). Already covered in `HR-REPORTS-CATALOGUE.md` |
| Legacy payroll reports | Various payroll entities | Export (CSV/Excel, client-side XLSX) | `frontend/src/app/reports/hr/page.tsx` ← `POST api/hr/payroll/runs/{runId}/oracle-report` | Payroll developer's; already covered in `HR-REPORTS-CATALOGUE.md` |
| Dev/UAT bulk load | Whole HR dataset | Import (CLI, bypasses services) | `Program.cs` commands `seed-hr-all`, `seed-hr-demo`, `seed-hr-org-authority`; `scripts/New-UatDatabase.ps1` | Not a product feature — but it is how every current database was populated, and it is why the staff-number counter can be behind the data (§4.1) |

**The one pattern worth standardizing on:** Finance's `IFixedAssetService.ImportAssetsFromExcelAsync(stream, fileName, dryRun)` — Excel upload, a `dryRun` flag that validates without persisting, and a `BulkImportResultDto` with row-level field errors (row number, field name, error message). HR's own two imports (Attendance, Payroll reconciliation) each built a bespoke version of the same idea with their own status enum — a good sign the concept is right, worth consolidating on one shared DTO shape going forward rather than a third bespoke version for the next entity.

---

## 3. Master list — import/export needs by sub-module

Legend — **Priority**: 🔴 high · 🟡 medium · 🟢 low/optional.

### 3.1 Core Employee & Organization

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `Employee` | **Yes** — onboarding a whole workforce, or correcting a partial legacy migration | **Yes** — full employee register/master extract | 🔲 Neither exists | 🔴 | The single biggest gap in this whole document (see §5.1) |
| `EmployeeBankDetail` | Yes — bulk correction of bank details at migration | No (sensitive; individual correction only, see masking guidance in `HR-REPORTS-CATALOGUE.md`) | 🔲 | 🟡 | Import only, deliberately no bulk export of bank numbers |
| `EmployeeDependent`, `EmployeeEmergencyContact` | Yes — migration/onboarding bulk load | No | 🔲 | 🟡 | |
| `EmployeeQualification`, `EmployeeWorkHistory`, `EmployeeSkill` | Yes — migration bulk load | No | 🔲 | 🟢 | |
| `OrganizationUnit` (incl. head assignment) | **Yes** — bulk load/correct the org structure, especially unit heads | Yes — full org-structure export for review | 🔲 | 🔴 | Directly fixes the org-unit-head data gap already flagged in the UAT demo plan §6 |
| `Position`, `JobDescription` | Yes — initial establishment bulk load | Yes — establishment register export | 🔲 | 🟡 | Ties to `ManpowerBudget`/establishment enforcement already discussed in the Finance sweep |
| `JobFamily`, `JobSubFamily`, `CareerLevel`, `StaffLevel` | Yes — reference-data bulk load | No | 🔲 | 🟢 | |

### 3.2 Compensation & Payroll

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `SalaryGrade`, `SalaryLevel`, `SalaryNotch` | Yes — annual pay-scale revision is inherently a bulk operation | No | 🔲 | 🟡 | |
| `PayComponent`, `PositionPayComponent` | ✅ Partially covered — Payroll's existing bulk-save endpoints (bonus rules/exceptions, tax reliefs, component exceptions, promotion arrears, overtime summaries) already handle several of these as DTO-array bulk saves | Export of the resolved emolument package per employee | Bulk save ✅, export 🔲 | 🟢 | See `HR-BULK-OPERATIONS-CATALOGUE.md` §2 for the existing bulk-save list |
| Payroll opening balances (loans, contributions) | ✅ Already covered — `contribution-opening-balances/bulk` exists | No | ✅ | — | Confirmed existing, no action needed |
| Payroll employee reconciliation | ✅ Already covered | — | ✅ | — | |
| Full payroll register / journal summary | — | ✅ Already covered (Oracle report set) | ✅ | — | |

### 3.3 Leave

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `LeaveType`, `LeaveCategoryAllocation`, `LeaveAccrualPolicy` | Yes — initial policy bulk setup | No | 🔲 | 🟢 | |
| `LeaveBalance` (opening balances) | **Yes** — carrying over every employee's leave balance at cutover is inherently a bulk, one-time-critical operation | Yes — same gap as the missing Leave Balance Report in `HR-REPORTS-CATALOGUE.md` §3.3 | 🔲 | 🔴 | Get this wrong once at go-live and it's wrong for every employee, permanently, unless corrected — treat as migration-critical |
| `LeaveRequest`/register | No (transactional, created live) | Yes — same as the missing Leave Register in `HR-REPORTS-CATALOGUE.md` §3.3 | 🔲 | 🟡 | |
| `PublicHoliday` (+ `HolidayCalendar`) | Yes — annual calendar bulk load, a common gap in ERP go-lives | Yes — the year's calendar for staff | Entity + per-holiday CRUD exist (`api/holiday-calendars`); no bulk load, no export | 🟡 | Corrected 2026-09-02 — it exists. TDC has **no calendar loaded** (`HR-OPEN-QUESTIONS-FOR-TDC.md` §3), which makes the bulk load the go-live blocker, not the entity. Payroll keeps a separate `PayrollHoliday` — settle which is authoritative before loading both |

### 3.4 Attendance

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `StaffAttendanceLog`/`StaffDailyAttendance` | ✅ Already covered (the staged bulk-import) | Yes — a general attendance register export distinct from the payroll handoff export, for HR's own analysis | Import ✅, export 🔲 | 🟡 | |
| Biometric device feed | Confirmed 2026-09-02: `StaffAttendanceDevice` is registered with `LastSyncDate`/`PendingSyncCount` and `{id}/sync` is a bookkeeping stamp, not a pull; `EmployeeBiometric` enrol/revoke exists; `POST api/staff-attendance-logs/punch` takes the actor from the **token**, so it is a self-service punch, not a device feed. **No device-authenticated ingestion endpoint exists** — confirm with whoever owns the physical devices how data is meant to arrive before building | — | ⚠ needs TDC confirmation | 🟡 | Do not build a webhook speculatively; if one is needed it must authenticate the device, not a user |

### 3.5 Benefits & Medical

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `EmployeeBenefitEnrollment`, `EmployeeMedicalInsurancePolicy` (opening enrollment) | **Yes** — same migration-critical shape as Leave opening balances | Yes — insurer reconciliation / open-enrollment audit export | 🔲 | 🔴 | |
| `HealthcareFacility`, `MedicalInsuranceProvider`, `MedicalInsurancePlan` | Yes — a health-insurer's provider network can run to hundreds of facilities, a natural bulk-load candidate | No | 🔲 | 🟡 | |
| `MedicalExpenseClaim` register | No | Yes — same gap as the missing Medical Claims Report in `HR-REPORTS-CATALOGUE.md` §3.7 | 🔲 | 🔴 | Shares priority with the Finance sweep's flagged medical-claims back-fill |

### 3.6 Assets

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `AssetType` | Yes — initial catalogue bulk load | No | 🔲 | 🟢 | |
| `CompanyAsset` | **Yes** — cataloguing an existing asset estate at go-live is exactly the scenario Finance's `BulkImportAssets` was built for; copy that pattern directly rather than reinventing it | ✅ Already covered (Asset Register Report) | Import 🔲, export ✅ | 🔴 | The clearest single "just copy the existing Finance pattern" opportunity in this whole document |

### 3.7 Training

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `TrainingProgram`, `TrainingVendor` | Yes — course/vendor catalogue bulk load | No | 🔲 | 🟢 | |
| Training completion records | ✅ Partially covered — bulk *record* completion exists (`HR-BULK-OPERATIONS-CATALOGUE.md` §2) | Yes — compliance-audit export of who completed mandatory training and when | Import ✅, export 🔲 | 🟡 | |

### 3.8 Awards, Discipline, Grievance, Separation

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `AwardType`, `AwardLevel` | Yes — reference data, low volume | No | 🔲 | 🟢 | |
| Discipline case register | No | Yes — legal/compliance audit-trail export, same gap as `HR-REPORTS-CATALOGUE.md` §3.10 | 🔲 | 🔴 | |
| Grievance case register | No | Yes — same gap as §3.13 there | 🔲 | 🟡 | |
| Separation / final settlement register | No | Yes — Finance/Audit hand-off, same gap as §3.12 there | 🔲 | 🔴 | |

### 3.9 Union / CBA

| Entity | Import? | Export? | Status | Priority | Notes |
|---|---|---|---|---|---|
| `Union`, `CollectiveBargainingAgreement` | Yes — reference data, low volume | No | 🔲 | 🟢 | |

### 3.10 Safety, Health & Environment (SHE) — confirmed zero capability today

Every one of the entities below is a genuine gap — SHE has **no file import and no file export
anywhere**, confirmed by direct search (re-confirmed 2026-09-02). It does produce report
*artefacts* — `POST api/safety/audits/{id}/issue-report`, `GET api/safety/environmental/reviews/{id}/clearance-report`,
and the auto-generated monthly environmental report with its own screens — so "SHE cannot
produce a document" is wrong; "SHE cannot get a register into a file" is right. Given the
module's own compliance nature (incident records, audit trails, contractor compliance), this is
worth treating as a coherent block of work, not scattered one-offs. Module overview:
`HR-SHE-INTEGRATION-AND-BOUNDARIES.md`.

| Entity | Import? | Export? | Priority | Notes |
|---|---|---|---|---|
| `SheIncidentType`, `SheInjuryType`, `SheBodyPart`, `SheCorrectiveActionTemplate`, `SheRegulatoryBody` | Yes — reference/lookup bulk setup | No | 🟢 | Cheap, low-risk first SHE import to build |
| `PpeType`, `JobRolePpeRequirement` | Yes — PPE catalogue and per-role requirement bulk load | No | 🟡 | |
| `SheContractor` | Yes — bulk-load an approved contractor register at go-live | Yes — contractor compliance register, ties to the ranking report already flagged in `HR-REPORTS-CATALOGUE.md` §3.17 | 🟡 | |
| `SafetyEquipment` | Yes — same shape as `CompanyAsset`, a physical register cataloguing exercise | No | 🟡 | Consider building alongside the Asset import (§3.6) since it's the same underlying problem |
| `SafetyIncident` register | No (transactional, created live, and each is individually investigated) | **Yes** — the incident register export already flagged as missing in `HR-REPORTS-CATALOGUE.md` §3.17 | 🔴 | |
| `SheAudit`/`SheAuditFinding` register | No | Yes — same gap, same document | 🔴 | |
| `SheEnvironmentalReview` register | No | Yes — same gap, same document | 🟡 | |
| `SheEnvironmentalPermit` register | Yes — bulk-load an existing permit portfolio at go-live | Yes — expiry/compliance register | 🟡 | |

---

## 4. The three biggest gaps, in detail

### 4.1 Employee master data — the most consequential gap

There is no way to bulk-load or bulk-correct the core `Employee` entity today. This matters for
two concrete reasons already surfaced elsewhere in this session's research, not hypothetically:

- **Go-live onboarding.** A tenant moving its whole workforce onto this system needs to load
  hundreds or thousands of employee records at once. Doing this one employee at a time through
  the create-employee screen is not a realistic path for a real deployment.
- **Correcting an incomplete legacy migration.** This session's own research already surfaced a
  measured data-completeness gap (only a fraction of employees carrying a salary value at one
  point in the data). A proper import tool — built on the dry-run/row-level-error pattern already
  proven in Finance's `BulkImportAssets` — is the direct, repeatable way to both load and *correct*
  this kind of gap, rather than a one-off manual data-fix script that leaves no audit trail and
  can't be safely re-run.

**Recommended shape:** Excel/CSV upload, a `dryRun` flag that validates (duplicate employee
numbers, invalid FK references to department/position/location, malformed dates) without writing,
and a row-level result showing exactly which rows would fail and why — the same shape Finance
already uses, not a new design.

**The staff-number rule the importer must honour (added 2026-09-02).** Staff numbers are issued
by a configurable register (`StaffNumberService`, lane 3b): under an auto-generating rule the
ordinary create path **refuses** a supplied number (`ResolveForCreateAsync`), and the unique index
on `(TenantId, EmployeeNumber)` is unfiltered, so soft-deleted leavers still occupy their numbers.
`AcceptImportedAsync` is the sanctioned exception — it takes the supplied number verbatim and
advances the counter past it (`AdvanceToAtLeastAsync`), best-effort, after the row commits. A bulk
importer therefore must go **row by row through `ImportEmployeeAsync`** (or call
`AcceptImportedAsync` per row) and finish with `ReconcileCounterAsync`; the repair endpoints are
`GET api/reference-dimensions/staff-number-formats/{id}/counter` and `POST …/counter/reconcile`.
Any load that bypasses this — SQL, the `seed-hr-*` CLI commands, a restore — leaves the counter
behind the data, and the first real hire collides. `IsOnPayroll` (lane 3f) is the other field with
a rule: off-payroll rows must carry an `OffPayrollReason` and no salary block, or the service
refuses them.

### 4.2 Safety/SHE — a compliance-heavy module with no bulk tooling at all

Sixty-plus entities, real regulatory stakes (incident submissions to authorities, audit trails,
contractor compliance), and genuinely zero import or export capability. The two highest-value
starting points: **the incident/audit/environmental-review register exports** (§3.10 — these
registers already exist as filterable APIs per `HR-REPORTS-CATALOGUE.md` §3.17, so exporting them
is largely "add a CSV button to something that already works," not new engineering), and **a
`SheContractor`/`SafetyEquipment` bulk import** for a site's initial safety-asset and
approved-contractor cataloguing exercise, which is the same shape of problem as `CompanyAsset`
and can plausibly reuse the same tooling once it exists.

### 4.3 No tenant-level data export

`DataExportTools.tsx` in the frontend renders as though a "export all my data" feature exists —
confirmed to be **mock data with no backend behind it**, mounted only by the **Administration**
reports page (`app/administration/reports/page.tsx`), so it is a platform screen rather than an HR
one; relabelling it is the platform owner's call, raised here because HR data is what it implies
it would export. This is worth flagging clearly rather
than leaving it looking functional: anyone who clicks it today gets an illusion of a feature, not
an error that says it isn't built yet. Three real reasons to eventually build the backend for it:
a data-subject-access-request under data-protection obligations (already flagged as an open
question in `HR-REPORTS-CATALOGUE.md` §5.12), a genuine backup/disaster-recovery need, and a
migration-out capability if a tenant ever needs to leave the platform. None of these are urgent
engineering, but the mock UI should either get a backend or be clearly labelled as a preview,
not left as-is.

---

## 5. Cross-cutting considerations for building these

1. **Standardize on one import result shape.** A shared `HrBulkImportResultDto` (total rows,
   succeeded, failed, and a `List<HrImportRowError>` with row number, field, and message) —
   modelled directly on Finance's `BulkImportResultDto` — should replace each entity inventing its
   own status enum, the way Attendance and Payroll's imports currently do independently of each
   other.
2. **Dry-run before commit, always.** Every import in this document that touches core records
   (Employee, Org structure, opening balances) should support a `dryRun` flag that reports what
   would happen without writing anything — this is not optional for anything migration-critical.
3. **Referential integrity is the actual hard part.** An employee import references department,
   position, manager, location; an org-structure import references parent units and heads (who
   are themselves employees). Validate FK references exist and belong to the same tenant before
   writing, and refuse the whole row rather than writing a partially-valid record.
4. **Downloadable templates.** Every import should ship a downloadable template (column headers,
   one example row, valid enum values) — this is cheap to build and is the single biggest reducer
   of "what format does this expect" support requests.
5. **Security and PII on export.** Bank details, medical data, disciplinary records, and salary
   figures should never be casually exportable in bulk without the same masking/permission
   discipline already recommended in `HR-REPORTS-CATALOGUE.md` §5 — an export is a bigger data
   exposure than a report because it typically leaves the application as a file.
6. **Audit the import/export act itself**, not just the resulting record changes — who ran an
   import, what file, how many rows succeeded/failed; who ran an export, what filter, how many
   records. This is the same semantic-audit-service gap already flagged in
   `HR-CROSS-MODULE-PATTERNS-SWEEP.md` §3.4, and import/export is exactly the kind of event that
   generic per-row audit logging doesn't capture well on its own.
7. **Go through the service, never the table** *(added 2026-09-02)*. Every HR aggregate has
   rules that live in its service and nowhere else: the staff-number register (§4.1), the
   payroll-membership gate (`IsOnPayroll`), tenant stamping (the DbContext auto-stamp is inert —
   ported services set `TenantId` explicitly), soft-delete-aware number generators, and the
   workflow adapters. An importer that writes rows directly reproduces the exact class of defect
   the `seed-hr-*` commands already cause (a counter behind its data). Loop the create/import
   service per row; accept the throughput cost.
8. **Distinguish "opening balance" imports as their own category.** Leave balances, benefit
   enrollments, and payroll opening balances aren't ongoing operational imports — they're one-time
   (or rare, per-cutover) migration events that need to be gotten right once and are unusually
   painful to correct after the fact if wrong. Treat them with the highest validation rigor in
   this document, even though their ongoing usage frequency is low.

---

## 6. Suggested build priority

1. **Standardize the shared import-result DTO** (§5.1) before building anything else — every
   subsequent item in this list benefits from it existing first.
2. **`Employee` bulk import**, with dry-run, on the Finance `BulkImportAssets` pattern (§4.1) —
   highest-consequence gap in the whole document.
3. **`OrganizationUnit` bulk import/correction**, specifically enabling bulk head-assignment —
   directly resolves the single biggest cross-cutting data-readiness caveat already found in
   `HR-UAT-DEMO-PRESENTATION-PLAN.md` §6.
4. **`LeaveBalance` and benefit/medical-enrollment opening-balance imports** — migration-critical,
   get these right once rather than correcting them piecemeal after go-live.
5. **`CompanyAsset` bulk import** — directly reuses the Finance pattern with the least adaptation
   of anything in this list.
6. **SHE incident/audit/environmental-review register exports** — cheapest real win in the whole
   document, since the underlying filterable APIs already exist per `HR-REPORTS-CATALOGUE.md` §3.17.
7. **`SheContractor`/`SafetyEquipment` bulk import** — same shape as the asset import, build once
   the pattern above exists.
8. Everything else in §3, roughly in the priority order shown per sub-module.
9. **Resolve the `DataExportTools` mock-vs-real gap** — either build the backend or relabel the UI
   so it stops implying a capability that doesn't exist.
