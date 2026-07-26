# Finance Audit Hardening PR Summary

## Scope

Small hardening step after Finance Audit Trail Foundation and before AP posting migration. No AP, AR, cash/bank, tax, FX, fixed asset, reporting, frontend, or schema work was started.

## Definition Of Done

- [x] Manual journal create source mutation and Finance audit event persist through the same `DbContext.SaveChanges` unit when `IFinanceAuditService` is configured.
- [x] Manual journal update source mutation and Finance audit event persist through the same `DbContext.SaveChanges` unit when `IFinanceAuditService` is configured.
- [x] Manual journal delete source mutation and Finance audit event persist through the same `DbContext.SaveChanges` unit when `IFinanceAuditService` is configured.
- [x] Generic audit reads are tenant-scoped.
- [x] Optimized audit endpoint cache keys are tenant-scoped.
- [x] Tests added for audit atomicity and generic audit exposure.
- [x] No migrations added.

## Files Changed

- `src/ErpSystem.Api/Services/Finance/GL/JournalEntryService.cs`
  - Added `PersistJournalMutationWithAuditAsync`.
  - Manual journal create/update/delete now save through the Finance audit write path when `IFinanceAuditService` is available, avoiding source-save-then-audit-save partial success.
- `src/ErpSystem.Core/Services/LogService.cs`
  - Tenant-scoped all generic audit read methods.
  - Tenant-scoped old audit cleanup.
- `src/ErpSystem.Api/Controllers/Optimized/OptimizedAuditLogController.cs`
  - Added current tenant context to optimized audit list/detail/stats queries.
  - Added tenant-specific cache keys.
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceAuditFoundationTests.cs`
  - Added audit hardening tests.
- `docs/finance-audit-hardening-pr-summary.md`
  - This summary.

## Generic Audit Endpoint Hardening

The following generic audit paths were hardened:

- `AuditLogController` via `IAuditLogService`
- `OptimizedAuditLogController`
- `SecurityController` was inspected and already used `SecurityService`, which filters `AuditLog` rows by `TenantId`.

`AuditLogService` now requires tenant context for audit reads and only returns records where `AuditLog.TenantId` matches the current tenant. `OptimizedAuditLogController` now applies the same tenant filter directly because it queries `IOptimizedGenericRepository<AuditLog>` rather than going through `IAuditLogService`.

## Test Coverage

Added tests for:

- manual journal create audit failure does not persist source mutation
- manual journal update audit failure does not persist source mutation
- manual journal delete audit failure does not persist source mutation
- generic audit endpoint excludes another tenant's Finance audit records
- generic audit endpoint returns same-tenant Finance audit record by ID

Regression filters still pass for:

- Finance Audit Foundation
- Batch 4 posting engine
- Batch 5 journal lifecycle

## Build And Test Results

- `dotnet build tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed, 1636 warnings, 0 errors.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceAuditFoundation|Batch=FinanceAuditHardening" -v:minimal`
  - Passed, 11/11.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceGoLive-4|Batch=FinanceGoLive-5" -v:minimal`
  - Passed, 19/19.
- `dotnet build src/ErpSystem.Data/ErpSystem.Data.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed, 0 warnings, 0 errors.
- `dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -maxcpucount:1 -v:minimal /clp:ErrorsOnly`
  - Passed, 0 warnings, 0 errors.

## Migrations

None.

## Rollback Considerations

Rollback is code-only:

- restore `JournalEntryService` create/update/delete to explicit source `SaveChanges` before audit logging
- remove tenant filters from `AuditLogService` and `OptimizedAuditLogController`
- remove the added hardening tests

No database rollback is required.

## Remaining Limitations

- Manual journal post/reversal audit remains covered by the posting engine transaction path.
- Other Finance workflows still need their own audit calls when they are migrated in later batches.
- Platform-wide cross-tenant audit review is no longer available through these generic endpoints without tenant context. If product requirements need platform-wide audit review, it should be implemented as an explicit platform-only endpoint with clear tenant filters and Finance permission controls.
