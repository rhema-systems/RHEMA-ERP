# Fixed Asset Revaluation and Impairment Foundation - PR Summary

> Update (2026-08-09): the impairment-reversal and posted-valuation-correction exclusions recorded in this historical foundation summary are now resolved by `docs/fixed-asset-valuation-correction-foundation.md`.

Date: 2026-07-08

## Scope

Implemented Fixed Assets Batch 21A only: revaluation and impairment foundation, category account mappings, valuation snapshots, posting-engine journals, NBV/book-value updates, prospective straight-line depreciation interaction, Finance audit events, diagnostics, and focused tests.

Transfers, disposals, frontend UI, broad fixed asset reporting, data migration/sign-off, print/export, depreciation reversal/correction, capitalization reversal/adjustment, additional depreciation methods, impairment reversal, and workflow routing were not started.

## Definition Of Done

- [x] Backend build impact verified through isolated test assembly build.
- [x] Frontend build/type-check impact: no frontend files were intentionally changed in this batch.
- [x] Tests added and focused Batch 21A suite passes.
- [x] Migration added.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Accounting impact documented.
- [x] Revaluation/impairment behavior documented.
- [x] Audit-event verification documented.
- [x] Limitations register updated with resolved/split limitation IDs.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/FixedAssets/AssetValuation.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAssetCategory.cs`
- `src/ErpSystem.Core/DTOs/Finance/AssetValuationDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/AssetValuationService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetCategoryService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetDepreciationService.cs`
- `src/ErpSystem.Api/Controllers/Finance/FixedAssetCategoriesController.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260708100000_AddFixedAssetRevaluationImpairmentFoundation.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetRevaluationImpairmentFoundationTests.cs`
- `docs/fixed-asset-revaluation-impairment-foundation.md`
- `docs/fixed-asset-revaluation-impairment-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Migration

Added `20260708100000_AddFixedAssetRevaluationImpairmentFoundation`.

Schema impact:

- Adds revaluation/impairment account mappings to `FixedAssetCategories`.
- Adds accounting book, fiscal period, accounting date, valuation snapshots, adjustment fields, posting references, status/idempotency, and failure metadata to `AssetValuations`.
- Adds valuation indexes and foreign keys for book, period, journal entry, posting event, and account mappings.

Rollback:

- Safe only before production valuation postings.
- After valuation postings exist, rollback would remove valuation subledger-to-GL links and account mappings required for audit and reconciliation.

## Tests

Focused suite: `FixedAssetRevaluationImpairmentFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetRevaluationImpairment`.

| Scenario | Test method |
|---|---|
| Revaluation cannot run before capitalization | `RevaluationCannotRunBeforeCapitalization` |
| Impairment cannot run before capitalization | `ImpairmentCannotRunBeforeCapitalization` |
| Disposed asset cannot be revalued or impaired | `DisposedAssetCannotBeRevaluedOrImpaired` |
| Revaluation increase posts through posting engine and correct Dr/Cr | `RevaluationIncreasePostsThroughPostingEngine_WithExpectedDebitCredit` |
| Revaluation decrease posts through posting engine and correct Dr/Cr | `RevaluationDecreasePostsThroughPostingEngine_WithExpectedDebitCredit` |
| Revaluation decrease uses existing surplus before loss | `RevaluationDecreaseUsesExistingSurplusBeforeLoss` |
| Impairment loss posts through posting engine and correct Dr/Cr | `ImpairmentLossPostsThroughPostingEngine_WithExpectedDebitCredit` |
| Missing revaluation surplus account blocks posting | `MissingRevaluationSurplusAccountBlocksPosting` |
| Missing impairment loss account blocks posting | `MissingImpairmentLossAccountBlocksPosting` |
| Cross-tenant revaluation account rejected | `CrossTenantRevaluationAccountRejected` |
| Cross-tenant asset rejected | `CrossTenantAssetRejected` |
| Closed-period revaluation and impairment rejected through posting engine | `ClosedPeriodValuationRejectedThroughPostingEngine` |
| Duplicate valuation post is idempotent | `DuplicateValuationPostIsIdempotent` |
| Acquisition cost is not mutated and book value updates after valuation | `AcquisitionCostIsNotMutatedByValuationAndBookValueUpdates` |
| Later depreciation uses adjusted carrying amount prospectively | `LaterDepreciationUsesAdjustedCarryingAmountProspectively` |
| Prior posted depreciation schedules are not rewritten | `PriorPostedDepreciationSchedulesAreNotRewritten` |
| Foreign-currency acquisition snapshots remain unchanged | `ForeignCurrencyAcquisitionSnapshotsRemainUnchanged` |
| Valuation audit events are emitted | `ValuationAuditEventsAreEmitted` |
| Impairment reversal is rejected until supported | `ImpairmentReversalIsRejectedUntilSupported` |

Results:

- Isolated test assembly build: passed with `0` warnings and `0` errors.
- Focused fixed asset revaluation/impairment suite: `20/20` passed.
- Finance go-live regression slice: `284/284` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- Valuation queries scope fixed assets, categories, book values, fiscal periods, and account mappings by current Finance `TenantId`.
- Cross-tenant asset lookup fails as not found.
- Cross-tenant account mapping is rejected before posting.
- Posting requests pass `SourceDocumentTenantId`.
- Valuation records, journal entries, account transactions, and posting events store tenant context.

## Accounting Impact

- Revaluation increase posts Dr asset cost/carrying account / Cr revaluation surplus.
- Revaluation decrease posts Cr asset cost/carrying account and debits existing surplus first, then revaluation loss for excess decreases.
- Impairment loss posts Dr impairment loss / Cr accumulated impairment.
- Valuation postings use `IFinancePostingEngine`.
- Acquisition cost, capitalization records, foreign-currency acquisition snapshots, and prior depreciation schedules are not mutated.
- Book-value NBV updates after posting and remains controlled subledger/read-side state reconciled to posted GL.

## Supported Behavior

- Supported: revaluation increases, revaluation decreases, and impairment losses for capitalized, non-disposed assets.
- Explicitly rejected: impairment reversal. See `FIN-LIM-0035`.

## Depreciation Interaction

Straight-line depreciation now supports prospective depreciation after valuation changes. Unadjusted assets keep the original straight-line charge capped at residual value; assets whose NBV was changed by revaluation/impairment use adjusted NBV less residual over remaining useful life.

## Workflow And Permission Behavior

- Uses existing Finance authorization and audit infrastructure.
- Does not introduce a parallel approval system.
- Revaluation/impairment workflow routing remains tracked as `FIN-LIM-0036`.

## Audit Event Verification

Implemented/emitted where applicable:

- `Finance.FixedAsset.RevaluationCalculated`
- `Finance.FixedAsset.RevaluationPosted`
- `Finance.FixedAsset.RevaluationPostingFailed`
- `Finance.FixedAsset.ImpairmentCalculated`
- `Finance.FixedAsset.ImpairmentPosted`
- `Finance.FixedAsset.ImpairmentPostingFailed`
- `Finance.FixedAsset.ValuationConfigurationUsed`
- `Finance.FixedAsset.ValuationBlockedClosedPeriod`

Constants are also available for valuation batch created, impairment reversed, account mapping changed, cross-tenant rejected, and posted-edit rejected events for later workflow/correction batches.

## Limitations Register

- `FIN-LIM-0024`: resolved for Batch 21A revaluation and impairment posting foundation.
- `FIN-LIM-0035`: impairment reversal remains open.
- `FIN-LIM-0036`: revaluation/impairment workflow routing remains open.
- `FIN-LIM-0037`: posted valuation correction/reversal/supersession remains open.
- `FIN-LIM-0025`: fixed asset transfers remain open.
- `FIN-LIM-0026`: fixed asset disposals remain open.
- `FIN-LIM-0027`: fixed asset reporting/reconciliation remains open.

None blocks the next fixed-asset lifecycle batch; all remain final go-live controls unless explicitly accepted.

## Rollback Considerations

Rollback before use is straightforward through the migration down path. After valuation postings exist, rollback would remove valuation account mappings and subledger/journal/posting-event references, so accountant-approved export/reconciliation would be required before rollback in any production-like environment.
