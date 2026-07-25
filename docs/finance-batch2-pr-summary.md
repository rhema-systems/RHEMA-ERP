# Finance Batch 2 PR Summary

Date: 2026-07-03

## Scope

Batch 2 implements action-level Finance authorization using the existing ERP permission and role infrastructure. It does not introduce a new permission system, posting path, workflow engine, tax engine, FX logic, or fixed asset logic.

## Files Changed

- `src/ErpSystem.Shared/FinancePermissions.cs`
- `src/ErpSystem.Api/Authorization/PermissionRequirement.cs`
- `src/ErpSystem.Api/Authorization/PermissionAuthorizationHandler.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionAuthorizationConvention.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Api/Services/DatabaseSeedingService.cs`
- `tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs`
- `tests/ErpSystem.Api.Tests/Authorization/FinancePermissionAuthorizationHandlerTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/finance-permission-matrix.md`
- `docs/finance-batch2-pr-summary.md`

## Implementation Summary

- Added a central Finance permission catalog in `FinancePermissions`.
- Registered each Finance permission as an ASP.NET authorization policy.
- Added a tenant-aware permission authorization handler that checks:
  - authenticated identity,
  - valid selected `TenantId`,
  - user tenant access,
  - super admin tenant scope,
  - role permissions through existing `RolePermission` records.
- Added a Finance MVC convention that applies authorization policies to Finance controller actions from a central map.
- Mapped Finance controllers and workflow-facing actions to granular permissions for read, write, approve, post, reverse, reconcile, export, close, reopen, tax, FX, fixed asset, and migration operations.
- Updated database seeding to seed the Finance permission matrix and assign Finance permissions to existing ERP roles.
- Added authorization tests for baseline denial, permission grant, cross-tenant denial, workflow approval denial, report/export denial, admin tenant scope, and controller action coverage.

## Definition Of Done

- [x] Finance permission matrix documented.
- [x] Existing permission/role/claim infrastructure inspected and reused.
- [x] No parallel permission system introduced.
- [x] Finance controller actions mapped to granular permission policies.
- [x] Finance workflow-facing actions mapped to workflow permissions.
- [x] Tenant-aware permission handler implemented.
- [x] Super admin behavior is explicit and tenant-scoped.
- [x] Automated tests added or updated.
- [x] Backend build completed.
- [x] Targeted authorization tests completed.
- [x] No posting, tax, FX, or fixed asset implementation started.

## Build Status

Backend build passed:

```powershell
dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore "-p:BaseOutputPath=C:/tmp/finance-batch2-api-bin/" -p:UseSharedCompilation=false -nr:false
```

Result: passed with existing warnings; no errors.

## Frontend Impact

No frontend files were changed in Batch 2. Frontend build or type-check was not run because this batch only changed backend authorization, seed data, tests, and documentation.

## Tests Added Or Updated

Targeted tests passed:

```powershell
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --filter "FullyQualifiedName~FinanceControllerSecurityTests|FullyQualifiedName~FinancePermissionAuthorizationHandlerTests" --no-restore "-p:BaseOutputPath=C:/Users/Akwas/Documents/DEV WORK/RHEMA ERP/RHEMA-ERP/.codex-tmp/finance-batch2-tests-bin/" -p:UseSharedCompilation=false -nr:false -m:1
```

Result: 25 passed, 1 skipped, 0 failed.

The skipped diagnostic remains the Batch 3 tenant-isolation diagnostic for tenantless service lookups.

## Migration Impact

No database schema migration was added.

Seed data was updated so Finance permissions and role-permission assignments can be created through the existing seeding flow. The seeding update deduplicates permission names before applying changes.

## API Impact

Finance endpoints now require granular Finance permission policies in addition to authentication. Clients using accounts that previously relied only on authentication may now receive `403 Forbidden` until appropriate Finance permissions are assigned through existing roles.

## Tenant-Isolation Verification

The new permission handler denies access when:

- the authenticated principal has no selected tenant claim,
- the tenant claim is invalid,
- the user does not belong to the selected tenant,
- a super admin attempts access outside an active tenant relationship.

Batch 2 does not claim full Finance data isolation. Query-level and service-level tenant enforcement remains the scope of Batch 3.

## Accounting Impact

Batch 2 reduces operational accounting risk by enforcing segregation of duties at the API boundary. Sensitive actions such as journal approval, posting, reversal, AP payment processing, AR payment receipt, reconciliation, report export, period close/reopen, tax setup, FX revaluation, fixed asset disposal, and migration adjustments now have distinct permission gates.

No accounting calculations, posting rules, ledger behavior, or source-of-truth behavior were changed.

## Rollback Considerations

Rollback is code-only unless seed data has already been applied in an environment.

If rolled back after seeding, extra Finance permission rows and role-permission rows may remain in the database. They are inert without matching authorization policies, but can be cleaned up with a controlled data script if required.

## Risks And Known Limitations

- Existing users may need role assignment review before testing Finance workflows because endpoints now enforce stricter permissions.
- Identity role assignment is not tenant-specific. The authorization handler confirms tenant access, but role grants are still global for the user.
- Some existing controller-level permission checks remain alongside the centralized policy layer.
- Shared platform workflow endpoints are not globally treated as Finance endpoints; Finance workflow-specific controller actions are covered.
- Finance audit trail semantics remain in the explicit Batch 7A audit foundation batch.
- Frontend permission-aware button visibility is not part of this batch.

## Recommended Next Batch

Proceed to Batch 3: tenant isolation diagnostics and fixes. This should verify every Finance query, command, report, workflow lookup, posting preparation path, and migration diagnostic is constrained by `TenantId` before posting-engine work begins.
