# Fixed Asset Acquisition And Capitalization - PR Summary

Date: 2026-07-07

## Scope

Implemented Fixed Assets Batch 19 only: tenant-scoped fixed asset creation, AP invoice asset capitalization, direct capitalization guardrails, category/account validation, asset register and book-value posting links, capitalization audit events, currency snapshots, diagnostics, and focused tests.

Depreciation, revaluation, impairment, transfers, disposals, frontend UI, data migration/sign-off, export/print, and broad fixed asset reporting were not started.

## Definition Of Done

- [x] Backend build impact verified through isolated test assembly build.
- [x] Frontend build/type-check impact: no frontend files were intentionally changed in this batch.
- [x] Tests added and focused Batch 19 suite passes.
- [x] Migration added.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Accounting impact documented.
- [x] Tax and FX assumptions documented.
- [x] Limitations register updated with fixed asset lifecycle follow-up IDs.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/AccountsPayable.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAsset.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAssetBookValue.cs`
- `src/ErpSystem.Core/DTOs/Finance/AccountsPayableDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFixedAssetService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Controllers/Finance/FixedAssetsController.cs`
- `src/ErpSystem.Api/Services/Finance/AP/VendorInvoiceService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetCategoryService.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260706183000_AddFixedAssetCapitalizationFoundation.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetCapitalizationFoundationTests.cs`
- `docs/fixed-asset-acquisition-capitalization-foundation.md`
- `docs/fixed-asset-acquisition-capitalization-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Migration

Added `20260706183000_AddFixedAssetCapitalizationFoundation`.

Schema impact:

- Adds fixed asset linkage and capitalization journal/posting references to AP invoice lines.
- Adds source document, source line, journal, posting event, transaction currency, functional currency, exchange-rate snapshot, and capitalization timestamp fields to fixed assets.
- Adds capitalization/source references to fixed asset book values.
- Adds tenant-aware indexes and foreign keys for asset/posting lookup.

Rollback:

- Safe only before production fixed asset capitalization postings.
- After use, rollback would remove source-document, journal, posting-event, and currency snapshot links required for audit and GL reconciliation.

## Tests

Focused suite: `FixedAssetCapitalizationFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetCapitalization`.

| Scenario | Test method |
|---|---|
| Draft asset creation is tenant-scoped and audited | `CreateDraftFixedAsset_ShouldBeTenantScopedAndAudited` |
| AP asset line posts to asset account and not expense | `ApInvoiceCapitalizableLine_ShouldPostAssetCostThroughPostingEngineAndNotExpense` |
| AP capitalization retry does not duplicate register transactions | `DuplicateApInvoiceCapitalizationRetry_ShouldNotDuplicateAssetRegisterTransactions` |
| Cross-tenant fixed asset reference rejected | `CrossTenantFixedAssetReference_ShouldBeRejectedBeforePosting` |
| Cross-tenant category account rejected | `CrossTenantAssetCategoryAccount_ShouldBeRejected` |
| Missing category cost account blocks direct capitalization | `CategoryMissingCostAccount_ShouldBlockDirectCapitalization` |
| Recoverable tax is not capitalized | `RecoverableTaxOnApAssetLine_ShouldNotBeCapitalized` |
| Non-recoverable tax is capitalized when configured | `NonRecoverableTaxOnApAssetLine_ShouldBeCapitalizedWhenTaxConfigMarksItNonRecoverable` |
| Foreign-currency AP asset acquisition preserves rate snapshot | `ForeignCurrencyApAssetAcquisition_ShouldPreserveCurrencyAndRateSnapshot` |
| Closed-period direct capitalization rejected through posting engine | `DirectCapitalizationIntoClosedPeriod_ShouldBeRejectedByPostingEngineAndAudited` |
| Direct capitalization without credit/AUC account is guarded | `DirectCapitalizationWithoutClearingAccountOrCreditAccount_ShouldBeGuarded` |
| Capitalized asset rejects destructive cost edit | `CapitalizedAsset_ShouldRejectDestructiveCostEdit` |
| Activation requires capitalization and preserves placed-in-service date | `AssetCannotActivateBeforeCapitalization_ButActivationAfterCapitalizationPreservesPlacedInServiceDate` |

Results:

- Isolated test assembly build: passed with `0` warnings and `0` errors.
- Focused fixed asset capitalization suite: `13/13` passed.
- Finance go-live regression slice: `246/246` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- Fixed asset queries and mutations use the current Finance tenant ID.
- AP fixed asset lines require same-tenant asset references.
- Asset categories require same-tenant GL account mappings.
- Direct capitalization resolves debit and credit accounts only within the current tenant.
- Posting requests pass source document tenant ID to `IFinancePostingEngine`.
- Cross-tenant fixed asset and account scenarios are covered by focused tests.

## Accounting Impact

- AP fixed asset lines debit the configured asset cost account and credit AP control through the central posting engine.
- AP fixed asset lines do not also post to expense.
- Direct capitalization debits the asset cost account and credits an explicit clearing/credit account or configured AUC/CIP account.
- Register/book values link back to the posted journal and posting event.
- Posted GL remains the accounting source of truth.

## Tax Treatment

- Recoverable input tax is not capitalized.
- Non-recoverable tax is capitalized when the configured tax treatment marks it non-recoverable.
- Full fixed asset statutory reporting remains out of scope.

## FX Assumptions

- Fixed asset register cost is stored in tenant functional currency.
- Foreign-currency AP asset acquisitions preserve transaction currency and exchange-rate snapshots from posting.
- Later exchange-rate edits do not mutate asset cost or the posted capitalization journal.
- Asset FX revaluation/translation remains out of scope.

## Workflow And Permission Behavior

- Uses existing Finance authorization and audit infrastructure.
- Does not introduce a parallel approval system.
- Direct capitalization is permission-controlled and audited.
- Workflow-engine routing for direct capitalization approval remains tracked as `FIN-LIM-0029`.

## Limitations Register

- `FIN-LIM-0016`: narrowed by Batch 19.
- `FIN-LIM-0023`: depreciation remains open.
- `FIN-LIM-0024`: revaluation/impairment remains open.
- `FIN-LIM-0025`: transfers remain open.
- `FIN-LIM-0026`: disposals remain open.
- `FIN-LIM-0027`: reporting/reconciliation remains open.
- `FIN-LIM-0028`: procurement/GRV-origin capitalization remains open and guarded.
- `FIN-LIM-0029`: direct capitalization workflow routing remains open.
- `FIN-LIM-0030`: resolved by the controlled capitalization reversal/correction follow-up in `docs/fixed-asset-capitalization-reversal-foundation.md`.

The limitations still marked open do not block the depreciation batch; they remain final go-live controls unless explicitly accepted.

## Rollback Considerations

Rollback before use is straightforward through the migration down path. After capitalization postings exist, rollback would remove the link between AP lines, assets, book values, posted journals, posting events, and currency snapshots. Accountant-approved export and reconciliation would be required before rollback in any production-like environment.
