# HR Module Port — Changes Made to Other Modules' Code

**Purpose:** the HR module port (from the standalone `HRApi` solution) changed some HR entity shapes.
A small number of files **outside the HR module** referenced those shapes and had to be adjusted to keep
the solution compiling. This document is the complete, git-verified inventory so the owning developers
can review, correct, or re-do each change themselves.

Every change site is tagged in code with a searchable marker:

```bash
grep -rn "HR-MODULE-PORT" src/
```

**Nothing here changes another module's behaviour by design.** Changes were kept as small and
mechanical as possible. The two items marked ⚠ need the owning team's input.

---

## 1. The underlying HR model changes that forced all of this

| HR model change (from HRApi) | Old shape | New shape |
|---|---|---|
| ID cards | `EmployeeIdentificationCard.DocumentType` (free-text `string`) | `IdentificationTypeId` + `IdentificationType` lookup entity |
| Leave dates | `LeaveRequest.StartDate/EndDate` = `DateTime` | `DateOnly` |
| Positions | `EmployeePosition.DepartmentId` | **required** `OrganizationUnitId` + `OrganizationLevelId` |
| Employee shift | `Employee.Shift` (single FK) | `ShiftAssignment` / `EmployeeWorkSchedule` (richer attendance model) |

---

## 2. Inventory

### A. Shared infrastructure — additive only, no behaviour change

| File | Change | Diff | Risk |
|---|---|---:|---|
| `src/ErpSystem.Core/Interfaces/IUnitOfWork.cs` | **Added** `ExecuteInTransactionAsync(Func<CancellationToken,Task>, …)` and `ClearChangeTracker()` | +14 −0 | None — additive; existing implementers/callers unaffected |
| `src/ErpSystem.Data/UnitOfWork.cs` | Implementations of the two methods above | +23 −0 | None — additive |
| `src/ErpSystem.Core/Entities/EmailTemplate.cs` | **Added** `EventKey` (`string?`, max 100) and `IsSystemDefault` (`bool`) | +22 −0 | None to existing flows. **Note:** adds 2 columns to the shared `EmailTemplates` table (covered by the HR migration). RHEMA's `EmailTemplateService` / email core were **not** touched. |
| `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs` | **Added one call**: `services.AddHrModuleServices();` | +7 −0 | None. All 621 HR registrations live in `HrModuleServiceRegistration.cs`. Called *before* the existing HR block so those explicit registrations still win. |

> No action needed — informational. Nothing existing was modified or removed.

---

### B. Shared `ApplicationDbContext` — restructure (looks big, is contained)

| File | Change | Diff |
|---|---|---:|
| `src/ErpSystem.Data/ApplicationDbContext.cs` | class → `partial`; `ConfigureHREntities(builder)` call replaced with `ConfigureHrModule(builder)` + `ConfigureRhemaPerformanceEntities(builder)`; the old ~1,550-line `ConfigureHREntities` slimmed to the retained appraisal/performance model only; 6 stale HR `DbSet`s removed | +12 −1305 |

**Why the −1305 looks alarming but isn't:** HR configuration was **moved**, not deleted. It now lives in
the new `ApplicationDbContext.HR.cs` partial (417 HR DbSets + HR Fluent config). **No other module's
configuration was touched** — Finance, Inventory, Maintenance, Workflow, EHC, Procurement config is
untouched. The benefit: future HR re-syncs rewrite only `ApplicationDbContext.HR.cs`, so HR work no
longer creates merge conflicts in the shared DbContext.

---

### C. Maintenance / Fleet team — 1 functional line per file

`FleetAssignmentService.cs`, `FleetHealthService.cs`, `FleetTripService.cs` (+7 −1 each, 6 of the 7 added
lines are the explanatory comment):

```diff
- c.DocumentType.ToLower().Contains("driver"))
+ c.IdentificationType.Name.ToLower().Contains("driver"))
```

**Why:** `EmployeeIdentificationCard` no longer has a free-text `DocumentType` string; it now references
the `IdentificationType` lookup entity.

⚠ **VERIFY (Fleet team):** this is behaviour-preserving **only if the licence identification type is
named with "driver"** (e.g. "Driver's License"). That is a data/seeding assumption — please confirm, or
replace the match with an explicit `IdentificationTypeId` check, which would be more robust than a
string match.

Also note: any *other* Fleet code that expects a `DocumentType` string must move to the
`IdentificationType` relationship.

---

### D. Projects team — type conversion only

`src/ErpSystem.Core/Services/Projects/ProjectServices.cs` (+12 −4), in `GetApprovedLeaveMetricsAsync`:

```diff
+ var periodStartDate = DateOnly.FromDateTime(periodStart);
+ var periodEndDate   = DateOnly.FromDateTime(periodEnd);
- && x.StartDate <= periodEnd
- && x.EndDate   >= periodStart);
+ && x.StartDate <= periodEndDate
+ && x.EndDate   >= periodStartDate);

- var overlapStart = x.StartDate > periodStart ? x.StartDate : periodStart;
- var overlapEnd   = x.EndDate   < periodEnd   ? x.EndDate   : periodEnd;
+ var leaveStart   = x.StartDate.ToDateTime(TimeOnly.MinValue);
+ var leaveEnd     = x.EndDate.ToDateTime(TimeOnly.MinValue);
+ var overlapStart = leaveStart > periodStart ? leaveStart : periodStart;
+ var overlapEnd   = leaveEnd   < periodEnd   ? leaveEnd   : periodEnd;
```

**Why:** `LeaveRequest.StartDate/EndDate` are now `DateOnly` (were `DateTime`).
**Risk:** low — the query predicate and the overlap/capacity arithmetic are unchanged; only the types are
converted. Worth a quick review by the Projects team, but no logic was altered.

---

### E. ⚠ Maintenance team — seeders now incomplete (ACTION NEEDED)

| File | Change | Diff |
|---|---|---:|
| `src/ErpSystem.Data/Seeders/MaintenanceE2ETestSeeder.cs` | Removed 4 × `DepartmentId = …` on `EmployeePosition` | +28 −4 |
| `src/ErpSystem.Api/Data/SimpleMaintenanceSeeder.cs` | Removed 3 × `DepartmentId = …` on `EmployeePosition` | +9 −3 |

```diff
- DepartmentId = maintenanceDeptId,
+ // [HR-MODULE-PORT] ACTION NEEDED (see comment in file)
```

**Why:** `EmployeePosition` no longer has `DepartmentId`; positions now attach to the org structure via
the **required** `OrganizationUnitId` + `OrganizationLevelId`.

⚠ **These seeders will now fail their FK at run time.** Both seeders create `Department` rows but no
`OrganizationLevel` / `OrganizationUnit` rows, so the positions they create have no valid org anchor.

**I deliberately did not invent the org structure** for the Maintenance module's test data. The
Maintenance team should decide the intended shape and seed an `OrganizationLevel` + `OrganizationUnit`,
then reference them on these positions. The removed lines and required fields are documented inline at
each site.

---

### F. ⚠⚠ HIGH — silent behaviour change affecting Maintenance technicians

**No file was edited for this one — it is a consequence of the `Employee` entity swap, and it will not
show up as a compile error. It must be handled before the Maintenance module is used.**

`Employee.CanBeAssignedToMaintenance` changed from a **computed** property to a **stored column**:

```csharp
// OLD (RHEMA) - computed, always correct, zero configuration
[NotMapped]
public bool CanBeAssignedToMaintenance => IsActive && !IsDeleted &&
    (StaffStatus == StaffStatus.Active || StaffStatus == StaffStatus.Probation) &&
    Department?.DepartmentType == DepartmentType.Maintenance;

// NEW (HRApi) - a plain stored bool, defaults to FALSE
public bool CanBeAssignedToMaintenance { get; set; }
```

**Impact:** the flag now defaults to `false` for every employee, so all technician-selection paths return
**empty results** until the column is populated:

| Consumer | Effect |
|---|---|
| `EmployeeService.cs:533` — `MaintenanceTechniciansOnly` filter | returns nothing |
| `EmployeeService.cs:2341 / 2347 / 2364` — technician lookups | return nothing |
| `Controllers/EmployeesController.cs` → `GET /employees/maintenance-available` | returns empty list |
| `MaintenanceStaffScheduleService.cs:48` — scheduling guard | blocks every assignment |

And the Maintenance **frontend actively depends on these endpoints**:
`frontend/src/services/maintenanceApiService.ts` (`/employees/maintenance-available`, `/employees`),
`frontend/src/services/maintenanceDataService.ts`, `frontend/src/components/admin/UserEmployeeLinks.tsx`.

**Net effect if unaddressed: Maintenance technician pickers silently come back empty.**

**Recommended fix (needs Maintenance + HR agreement):** keep HRApi's stored column (HR services assign
it, so it cannot go back to a get-only computed property) and **backfill it in the HR migration** to
reproduce the old rule:

```sql
UPDATE e SET e.CanBeAssignedToMaintenance = 1
FROM Employees e
JOIN Departments d ON d.Id = e.DepartmentId
WHERE e.IsDeleted = 0 AND e.IsActive = 1
  AND e.StaffStatus IN (/* Active, Probation */)
  AND d.DepartmentType = /* Maintenance */;
```

Going forward the flag becomes explicitly managed (set on the employee record) rather than derived.

---

### G. Other Maintenance dependencies on replaced HR code — worth a regression pass

These compile cleanly (interfaces match), but the **implementations behind them were replaced** with
HRApi's versions, so behaviour could differ (eager-loading, filtering, tenant scoping):

- **8 Maintenance services** consume HR repositories whose implementations changed
  (`IEmployeeRepository`, `IDepartmentRepository`, `ISectionRepository`, `IEmployeeSkillRepository`,
  `IEmployeePositionRepository`): `JobCardService`, `MaintenanceExpenseService`,
  `MaintenanceNotificationService`, `MaintenanceStaffScheduleService`, `QualityControlService`,
  `WorkOrderLaborService`, `WorkOrderSchedulingService`, `WorkOrderService`.
- **10 Maintenance services** reference the `Employee` entity directly (whose shape changed), including
  `TechnicianService`, `TechnicianSchedulingService` and the Fleet services.

**Suggested:** a short regression pass on technician assignment, work-order labour and job cards once the
migration is applied.

---

## 3. How to hand any of this back

Each file can be independently reverted and re-done by its owner:

```bash
# see exactly what changed in one file
git diff -- src/ErpSystem.Core/Services/Maintenance/Fleet/FleetHealthService.cs

# revert a single file to its pre-port state
git checkout -- src/ErpSystem.Core/Services/Maintenance/Fleet/FleetHealthService.cs
```

**Important:** reverting a file in sections **C, D or E** will break the build, because the old code
references HR members that no longer exist. The practical options are:

1. **Keep the mechanical fix (recommended)** — the solution stays green; owners review the tagged spots
   and adjust if they disagree with the approach. Only the two ⚠ items genuinely need their input.
2. **Owner re-does it** — revert the file and have the owner reimplement against the new HR model in the
   same change, so the build is never left red.

Sections **A** and **B** are additive/structural and need no action; they are listed for transparency.

---

## 4. Summary for a stand-up

- **11 files** outside HR were edited; **9 of them are additive, structural, or a one-line type fix.**
- **Items needing owner input, by priority:**
  1. ⚠⚠ **(F) `CanBeAssignedToMaintenance` became a stored column** — Maintenance technician lookups will
     silently return empty until it is backfilled. **No compile error; highest-risk item.**
  2. ⚠ **(E) Two Maintenance seeders** now create `EmployeePosition` rows without the required org
     unit/level — they will fail their FK at run time.
  3. ⚠ **(C) Fleet driver-licence match** now relies on `IdentificationType.Name` containing "driver" —
     confirm the data, or switch to an explicit `IdentificationTypeId`.
  4. **(G)** Regression pass on Maintenance services that use the replaced HR repositories/entity.
- Everything edited is greppable via `HR-MODULE-PORT`. Items **F** and **G** involve **no edited file**,
  so they are *only* documented here — they cannot be found by grepping.
- Full rationale: `HR_MODULE_PORT_PLAN.md` → "Cross-module touchpoints".
