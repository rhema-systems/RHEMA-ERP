# HR-Port Data Migration Runbook

The HR module port (`20260720181131_AddHRModule`, `20260720193903_AddHRPerformanceModule`) is a large
schema **redesign**, not a rename. Several legacy tables/columns are dropped or replaced by
differently-shaped structures, and several new **required** relationships are introduced.

To guarantee no data is ever silently lost, those migrations contain **fail-safe guards**:

- On a **fresh / dev database** (the legacy tables are empty), every guard is a **no-op** and the
  migration applies normally.
- On a **populated pre-port database**, a guard that detects real data **halts the migration**
  (`THROW`, which rolls the migration transaction back) with a message pointing here, instead of
  dropping the data or failing on a foreign-key violation.

`Database.MigrateAsync()` runs at startup, so if you hit one of these halts the app will fail to start
until the affected data is migrated by hand and the guard condition is cleared. This is intentional.

---

## What is auto-handled (no action needed)

These are backfilled automatically by the migrations (no-op when empty):

| Item | Migration | Behaviour |
|---|---|---|
| `AttendanceRecords` → `StaffAttendanceRecords` | AddHRModule | Rows copied (status string → enum int) before the drop. |
| `EmployeePositions.OrganizationUnitId` / `OrganizationLevelId` | AddHRModule | Orphaned positions are pointed at a created/reused per-tenant **"Unassigned" (code `UNASSIGNED`)** OrganizationStructure → Level → Unit. Re-assign to real units afterwards. |
| `Employees.CanBeAssignedToMaintenance` | AddHRModule | Set `true` for employees whose `Department.Code = 'MAINT'` (reproduces the old computed rule). |
| `LeaveTypes.IsActive`, `LeaveSubTypes.IsActive`, `KpiDefinitions.IsActive`, `AppraisalGradeDefinitions.IsActive` | both | Set `true` for all pre-existing rows (they were implicitly active before the flag existed). |

---

## What halts the migration (manual action required)

If your database holds data in any of the following, the migration halts. Migrate the data into the
replacement schema, then remove the source rows (or the columns' data) so the guard passes, and re-run.

### `20260720193903_AddHRPerformanceModule`
| Legacy source | Replacement target | Notes |
|---|---|---|
| `KpiEvaluationRecords` | `AppraisalKpiEvaluationSnapshots` | Re-parented from `EmployeeKpiTargetId` to `AppraisalCriterionScoreSnapshotId` — needs a join/mapping to resolve the new parent. `EvidenceLinks` shrinks 4000 → 1000. |
| `EmployeeKpiTargets` | `EmployeeGoals` | Model redesigned. |
| `AppraisalCriterias` | `PerformanceAppraisalCriterionConfigs` / `AppraisalCompetencies` | Split across two tables. |
| `PositionCriteriaMappings` | `PerformanceAppraisalCriterionConfigs` | |
| `MappingGradeRanges` | new `*GradeRanges` tables | Verify score value types. |
| `PerformanceAppraisals` (any rows) | — | `AppealOutcome` dropped (now `AppraisalAppeals`); required `AppraisalCycleId` + `AppraisalTemplateId` added with no legacy source. Create the cycle/template and assign appraisals. |
| `EvaluatorEvaluations.EvaluationDate` | — | No target column. Preserve externally if needed. |

### `20260720181131_AddHRModule`
| Legacy source | Replacement / notes |
|---|---|
| `Shifts` | `ShiftDefinition` requires a `WorkSchedule` (no legacy source). Create schedules/definitions and re-map. |
| `EmployeeShiftPreferences` | Concept removed — no replacement. Export if you need the history. |
| `WorkStations` (any rows) | `DepartmentId`/`StationType` dropped; required `CountryId` added. Re-map. |
| `PublicHolidays` (any rows) | Required `HolidayCalendarId` added (new empty `HolidayCalendars`). Create a calendar and assign. |
| `ShiftAssignments.StartDate` | Dropped — preserve if needed. |
| `LeaveRequests.ApprovalDate` / `ApprovalNotes` | Dropped — preserve approval history if needed. |
| `LeavePlans.DepartmentId` | Dropped. |
| `LeaveBalances.AdjustmentReason` | Dropped. |
| `EmployeePositions.MinSalary` / `MaxSalary` / `Requirements` / `Responsibilities` | Dropped — no target column in the new model. |
| `EmployeeIdentificationCards.DocumentType` / `IssuingAuthority` | Dropped. |
| `EmployeeDependents.IsEmergencyContact` / `IsStudentDependent` | Dropped. |

**Intentionally dropped without a guard (data deliberately superseded):**
`LeaveTypes.ApplicableToDepartments/…EmploymentTypes/…Genders/…Positions` (normalised onto lookup
tables) and `EmployeePositions.SectionId`/`UnitId` (replaced by the organization-structure anchoring).

---

## Recommended path for a real populated upgrade
1. Restore a copy of the production database to a staging environment.
2. Apply migrations there; note which guard(s) halt.
3. For each halted item, run a bespoke `INSERT … SELECT` into the replacement table (see the mapping
   above), then clear the source rows/columns.
4. Re-run migrations until they complete cleanly.
5. Capture the bespoke SQL as a one-off pre-migration script to run against production before deploy.
