# Finance Batch 3 PR Summary

Date: 2026-07-04

## Scope

Batch 3 implements tenant-isolation diagnostics and fixes for Finance data paths using the existing `TenantId` model. It does not introduce a new tenancy model, posting engine, approval system, tax engine, FX engine, or fixed asset accounting behavior.

## Tenant-Isolation Map

| Area | Enforcement added or verified |
| --- | --- |
| Tenant context | Added `FinanceTenantGuard.GetRequiredFinanceTenantId()` so Finance paths fail closed when authenticated claims do not contain a valid tenant. |
| GL and journals | Tenant-scoped journal entry reads, posting preparation, reversal, approval status updates, account lookups, account balances, period close checks, and report calculations. |
| AP and supplier flows | Tenant guard applied to AP reports, vendor invoices, vendor payments, finance purchase orders, purchase receipts, supplier returns, and subledger posting source lookups. |
| AR and customer flows | Tenant guard applied to AR reports, customers, invoices, payments, allocations, and subledger posting source lookups. |
| Cash and bank | Added `TenantId` to bank accounts, cash transactions, bank statements, statement lines, reconciliations, and reconciliation matches; scoped services and workflow facts by tenant. |
| Tax | Removed hardcoded/default tenant usage in tax rule controller and applied guarded tenant context to tax configuration/calculation services. |
| FX | Applied guarded tenant context to currencies, exchange rates, and revaluation services. |
| Fixed assets | Applied guarded tenant context across fixed asset services and fixed asset category controller lookups. |
| Budget and unit accounting | Added tenant-scoped scenario, return, entry, fiscal-period, account, unit-account, allocation, and ratio lookups. |
| Segments | Applied guarded tenant context to segment structures, values, lookup values, and account combination generation. |
| Workflow approvals | Tenant-scoped pending approvals, finance workflow fact resolution, approved/rejected outcome updates, and vendor invoice finalization. |
| Diagnostics | Enabled static diagnostics for default tenant fallbacks and direct `FindAsync` in Finance services/controllers. |

## Files Changed

- `src/ErpSystem.Api/Services/Finance/FinanceTenantGuard.cs`
- `src/ErpSystem.Core/Interfaces/ICurrentUserService.cs`
- `src/ErpSystem.Core/Entities/Finance/BankAccount.cs`
- `src/ErpSystem.Core/Entities/Finance/CashTransaction.cs`
- `src/ErpSystem.Core/Entities/Finance/BankStatement.cs`
- `src/ErpSystem.Core/Entities/Finance/BankReconciliation.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260704100000_AddCashBankTenantIsolation.cs`
- `src/ErpSystem.Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceApprovalsController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinancePurchaseOrderController.cs`
- `src/ErpSystem.Api/Controllers/Finance/FixedAssetCategoriesController.cs`
- `src/ErpSystem.Api/Controllers/Finance/JournalEntryController.cs`
- `src/ErpSystem.Api/Controllers/Finance/SupplierReturnsController.cs`
- `src/ErpSystem.Api/Controllers/Finance/TaxRuleController.cs`
- `src/ErpSystem.Api/Services/Finance/AP/*`
- `src/ErpSystem.Api/Services/Finance/AR/*`
- `src/ErpSystem.Api/Services/Finance/Budget/BudgetService.cs`
- `src/ErpSystem.Api/Services/Finance/Cash/*`
- `src/ErpSystem.Api/Services/Finance/Fiscal/FiscalPeriodService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/*`
- `src/ErpSystem.Api/Services/Finance/GL/*`
- `src/ErpSystem.Api/Services/Finance/MultiCurrency/*`
- `src/ErpSystem.Api/Services/Finance/Segments/*`
- `src/ErpSystem.Api/Services/Finance/Settings/*`
- `src/ErpSystem.Api/Services/Finance/Taxation/*`
- `src/ErpSystem.Api/Services/Finance/UnitAccounting/*`
- `tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FinanceTenantGuardTests.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BankAccountTenantIsolationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`

## Definition Of Done

- [x] Finance tenant context fails closed through `FinanceTenantGuard`.
- [x] Default tenant fallbacks removed from Finance services/controllers.
- [x] Cash/bank persistence entities now carry `TenantId`.
- [x] Cross-tenant GL, bank, statement, reconciliation, budget, allocation, and unit-account references guarded in changed paths.
- [x] Workflow approval outcome updates are tenant-scoped.
- [x] Static diagnostics enabled for default tenant fallback and direct `FindAsync`.
- [x] Automated tests added and passing.
- [x] Backend build completed using isolated output.
- [x] Database migration added.
- [x] No posting engine, tax, FX, or fixed asset accounting implementation started.

## Build Status

Passed:

```powershell
dotnet build src/ErpSystem.Api/ErpSystem.Api.csproj --no-restore -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ -v:minimal /clp:ErrorsOnly
```

Note: default output build failed because a running `ErpSystem.Api` process locked existing `bin\Debug\net8.0` DLLs. The isolated build passed.

## Tests Added Or Updated

Passed:

```powershell
dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build -c Debug -p:BaseOutputPath=C:\tmp\rhema-build\bin\ --filter "Batch=FinanceGoLive-3|Batch=FinanceGoLive-3-Diagnostic" -v:minimal
```

Result: 9 passed, 0 failed, 0 skipped.

Coverage added:

- tenant guard claim/fallback behavior,
- bank account tenant filtering,
- cross-tenant GL account rejection for bank account creation,
- cash/bank entity `TenantId` reflection check,
- source diagnostic for Finance default tenant fallbacks and direct `FindAsync`.

## Migration Impact

Added `20260704100000_AddCashBankTenantIsolation`:

- Adds non-null `TenantId` to `BankAccounts`, `CashTransaction`, `BankStatement`, `BankStatementLine`, `BankReconciliation`, and `ReconciliationMatch`.
- Backfills existing rows to the seeded default tenant ID.
- Adds tenant indexes and composite tenant indexes used by cash/bank queries.
- Adds a unique index on `(TenantId, AccountNumber)` for bank accounts and `(TenantId, TransactionNumber)` for cash transactions.

Existing production data must be checked for duplicate bank account numbers or cash transaction numbers before applying this migration.

Preflight diagnostic added:

- `docs/sql/finance-batch3-cash-bank-tenant-preflight.sql`

This script must be run against production-like data before applying the migration. Sections for duplicate bank account numbers and duplicate cash transaction numbers must return zero rows, and cash/bank ownership rows must be reviewed for tenant assignment before migration sign-off.

## API Impact

Finance endpoints now fail with an exception if authenticated claims are present but no valid tenant claim exists. This is intentional for Finance. Non-Finance legacy fallback behavior in `CurrentUserService` remains unchanged.

Cross-tenant source document IDs, GL account IDs, bank account IDs, statement line IDs, reconciliation IDs, fiscal period IDs, segment IDs, and unit-account IDs are now rejected or excluded in the changed Finance paths.

## Frontend Impact

No Batch 3 frontend changes were made. Frontend build/type-check was not run.

## Tenant-Isolation Verification

- Static source diagnostic found no Finance `DefaultTenantId`, tenant `Guid.Empty` fallback, or direct `FindAsync` usage after fixes.
- Behavioral tests verified same-tenant bank account reads still work and cross-tenant bank account reads are excluded.
- Behavioral test verified bank account creation rejects another tenant's GL account.
- Workflow approval processing resolves and mutates finance entities using the approval tenant.

## Accounting Impact

Batch 3 does not change accounting calculations. It reduces accounting risk by preventing cross-tenant contamination of cash/bank, AP, AR, GL, tax, FX, fixed asset, budget, unit-accounting, and workflow data paths. This is a prerequisite for the posting engine and immutable GL source-of-truth work.

## Rollback Considerations

Rollback requires reverting code and running the migration `Down` path to drop the new cash/bank tenant indexes and columns. If production rows have already been backfilled to the default tenant, rollback removes that tenant attribution from cash/bank tables.

## Risks And Known Limitations

- Existing accounting data backfilled to the default tenant still requires the dedicated migration/cleanup phase for tenant assignment validation and accountant sign-off.
- Role grants remain global rather than tenant-specific; Batch 2 handler enforces tenant access, but role assignment design needs a separate security follow-up if tenant-specific roles are required.
- Default tenant fallback still exists in generic `CurrentUserService` for legacy non-Finance code. Finance paths use `FinanceTenantGuard`.
- Cash/bank unique indexes can fail migration if legacy duplicate account or transaction numbers exist under the default tenant backfill.
- Full tenant verification for future posting engine tables remains part of the posting-engine batches.

## Recommended Next Batch

Proceed to the posting-engine foundation batch only after reviewing Batch 3 migration readiness against existing production data. The next implementation should centralize Finance posting paths and idempotency while preserving posted GL as the source of truth.
