# AP/AR Settlement Read Model and Aging/Control Reconciliation PR Summary

## Summary

This batch adds a tenant-scoped, rebuildable AP/AR settlement read model and updates AP/AR aging/control reconciliation to derive outstanding balances from posted accounting facts instead of mutable operational paid/credited fields.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/SubledgerSettlementReadModel.cs`
- `src/ErpSystem.Core/DTOs/Finance/SubledgerSettlementDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ISubledgerSettlementReadModelService.cs`
- `src/ErpSystem.Api/Services/Finance/SubledgerSettlementReadModelService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/ApReportsService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/ArReportsService.cs`
- `src/ErpSystem.Api/Controllers/Finance/ApControllersConsolidated.cs`
- `src/ErpSystem.Api/Controllers/Finance/ArControllersConsolidated.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Core/DTOs/Finance/AccountsPayableDtos.cs`
- `src/ErpSystem.Core/DTOs/AR/ReportDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IApReportsService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IArReportsService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260708133000_AddSubledgerSettlementReadModels.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/SubledgerSettlementReadModelFoundationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/ap-ar-settlement-read-model-foundation.md`
- `docs/finance-reporting-foundation.md`
- `docs/finance-go-live-limitations-register.md`

## Migrations

Added `20260708133000_AddSubledgerSettlementReadModels` for:

- `SubledgerSettlementBalances`
- `SubledgerSettlementApplications`
- tenant/source indexes, posting-event/journal indexes, and FK links to tenants, journals, posting events, and FX realized settlements.

Rollback drops the read-model tables only. Posted source documents, posted GL, posting events, and settlement allocations remain intact.

## Limitations Register

- `FIN-LIM-0001`: resolved for AP/AR settlement read model, aging, and control reconciliation.
- `FIN-LIM-0045`: added for unapplied payments, unapplied receipts, and advances where production scope requires aging/statement treatment.
- `FIN-LIM-0013`: remains open for compatibility customer credit-note workflow semantics.
- `FIN-LIM-0009`, `FIN-LIM-0010`, `FIN-LIM-0012`: remain open for posted AP payment, AR receipt, and credit-note reversal/correction accounting.

## Accounting Impact

- No new accounting journals are created by the read-model rebuild.
- AP/AR aging now uses posted invoices, posted payments/receipts, posted allocations, posted credit notes, posted FX settlement links, and posting-event/journal references.
- AP control reconciliation compares read-model AP outstanding to posted AP control GL movement.
- AR control reconciliation compares read-model AR outstanding to posted AR control GL movement.
- Mutable operational paid/credited fields are retained only as drift diagnostics.

## Tenant Isolation

- Rebuild, balances, aging, and control reconciliation are scoped by current Finance tenant.
- Source documents, settlement applications, posting events, journals, and control-account lines must share the same tenant.
- Cross-tenant allocations are excluded and diagnosed.
- Report filters retain supplier/customer tenant validation through existing service paths.

## Tests

Focused tests added under `Batch=FinanceGoLive-SubledgerSettlementReadModel`:

- 18 settlement read-model tests covering posted/unposted documents, partial/full AP and AR settlements, WHT/VAT withholding, AR credit notes, FX settlement links, cross-tenant allocation diagnostics, aging read-model usage, AP/AR control reconciliation, missing source journal diagnostics, idempotent rebuild, and audit events.

Results:

- `dotnet build tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-restore -v:minimal /m:1 /nodeReuse:false`: passed with existing warnings.
- `dotnet test tests\ErpSystem.Api.Tests\ErpSystem.Api.Tests.csproj --no-build --filter "Batch=FinanceGoLive-SubledgerSettlementReadModel" -v:minimal`: `18/18` passed.

## Rollback Considerations

Rollback can remove the read-model tables and service endpoints. AP/AR posting, GL journals, allocations, and posting events are unchanged. AP/AR aging would fall back to prior behavior only if the service is unregistered, but that is not go-live preferred.

## Safe To Proceed

Safe to proceed to broader reporting/export, workflow hardening, or migration/sign-off work after the Finance go-live regression slice passes. Export/sign-off should account for `FIN-LIM-0045` if unapplied advances are in production scope.
