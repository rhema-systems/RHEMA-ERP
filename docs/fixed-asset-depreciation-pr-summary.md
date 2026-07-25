# Fixed Asset Depreciation Foundation - PR Summary

Date: 2026-07-07

## Scope

Implemented Fixed Assets Batch 20 only: straight-line depreciation foundation, depreciation run/header model, schedule line snapshots, posting-engine depreciation journals, accumulated depreciation/NBV updates, audit events, diagnostics, and focused tests.

Revaluation, impairment, transfers, disposals, frontend UI, broad fixed asset reporting, data migration/sign-off, print/export, capitalization reversal, and depreciation reversal were not started.

## Definition Of Done

- [x] Backend build impact verified through isolated test assembly build.
- [x] Frontend build/type-check impact: no frontend files were intentionally changed in this batch.
- [x] Tests added and focused Batch 20 suite passes.
- [x] Migration added.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Accounting impact documented.
- [x] Supported depreciation method documented.
- [x] Audit-event verification documented.
- [x] Limitations register updated with resolved/split limitation IDs.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAssetDepreciationRun.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/AssetDepreciationSchedule.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetDepreciationService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260707103000_AddFixedAssetDepreciationFoundation.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetDepreciationFoundationTests.cs`
- `docs/fixed-asset-depreciation-foundation.md`
- `docs/fixed-asset-depreciation-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Migration

Added `20260707103000_AddFixedAssetDepreciationFoundation`.

Schema impact:

- Adds `FixedAssetDepreciationRuns`.
- Adds run/posting references and calculation snapshots to `AssetDepreciationSchedules`.
- Adds tenant-aware indexes for depreciation run, journal, and posting-event reconciliation.

Rollback:

- Safe only before production depreciation postings.
- After use, rollback would remove depreciation run headers, posting-event references, and calculation snapshots required for audit and GL reconciliation.

## Tests

Focused suite: `FixedAssetDepreciationFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetDepreciation`.

| Scenario | Test method |
|---|---|
| Depreciation cannot run before capitalization | `DepreciationCannotRunBeforeCapitalization` |
| Depreciation cannot run before activation/placed-in-service | `DepreciationCannotRunBeforeActivationOrPlacedInServiceDate` |
| Straight-line schedule generation without book update | `StraightLineScheduleGeneration_ShouldBeDeterministicAndNotUpdateBookValueUntilPosted` |
| Deterministic rounding | `DepreciationAmountRoundingIsDeterministic` |
| Residual value cannot exceed cost | `ResidualValueCannotExceedAssetCost` |
| Useful life must be positive | `UsefulLifeMustBePositive` |
| Schedule total does not exceed depreciable amount | `ScheduleTotalDoesNotExceedDepreciableAmount` |
| Depreciation posts through posting engine and correct Dr/Cr | `DepreciationPostsThroughPostingEngine_WithExpectedDebitAndCredit` |
| Missing depreciation expense account blocks posting | `MissingDepreciationExpenseAccountBlocksPosting` |
| Missing accumulated depreciation account blocks posting | `MissingAccumulatedDepreciationAccountBlocksPosting` |
| Cross-tenant depreciation account rejected | `CrossTenantDepreciationAccountRejected` |
| Closed-period depreciation rejected and audited | `ClosedPeriodDepreciationPostingRejectedAndAudited` |
| Duplicate period returns existing run without duplicate posting | `DuplicateDepreciationPeriodReturnsExistingRunWithoutDuplicatePosting` |
| Accumulated depreciation and NBV update after posting | `AccumulatedDepreciationAndNbvUpdateAfterPosting` |
| Later assumption change does not rewrite posted depreciation | `LaterDepreciationAssumptionChangeDoesNotRewritePostedDepreciation` |
| Depreciation run creates audit events | `DepreciationRunCreatesAuditEvents` |
| Foreign-currency asset uses functional capitalized cost | `ForeignCurrencyAssetDepreciationUsesFunctionalCapitalizedCostNotCurrentExchangeRate` |
| Depreciation does not mutate acquisition cost | `DepreciationDoesNotMutateAssetAcquisitionCost` |

Results:

- Isolated test assembly build: passed with `0` warnings and `0` errors.
- Focused fixed asset depreciation suite: `18/18` passed.
- Legacy fixed asset depreciation service test: `1/1` passed.
- Finance go-live regression slice: `264/264` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- Depreciation queries scope fixed assets, book values, schedules, runs, categories, fiscal periods, and accounts by current Finance `TenantId`.
- Cross-tenant depreciation account mappings are rejected before posting.
- Posting requests pass `SourceDocumentTenantId`.
- Run headers and schedule lines store tenant ID.

## Accounting Impact

- Depreciation posts Dr depreciation expense / Cr accumulated depreciation through `IFinancePostingEngine`.
- Posted depreciation schedule lines store journal and posting-event references.
- Asset book values update accumulated depreciation and NBV after posting.
- Asset acquisition cost is not mutated.
- Projected schedules do not update book values or GL.

## Supported Depreciation Methods

- Supported: straight-line.
- Rejected/deferred: declining balance, double-declining balance, sum-of-years-digits, units-of-production, and none. See `FIN-LIM-0031`.

## Workflow And Permission Behavior

- Uses existing Finance authorization and audit infrastructure.
- Does not introduce a parallel approval system.
- Depreciation workflow approval routing remains tracked as `FIN-LIM-0032`.

## Audit Event Verification

Implemented/emitted where applicable:

- `Finance.FixedAsset.DepreciationRunCreated`
- `Finance.FixedAsset.DepreciationCalculated`
- `Finance.FixedAsset.DepreciationPosted`
- `Finance.FixedAsset.DepreciationPostingFailed`
- `Finance.FixedAsset.DepreciationBlockedClosedPeriod`
- `Finance.FixedAsset.DepreciationAccountMappingInvalid`

Constants are also available for policy configured, schedule generated, run reversed, and configuration changed events for later workflow/reversal batches.

## Limitations Register

- `FIN-LIM-0023`: resolved for straight-line depreciation foundation.
- `FIN-LIM-0031`: additional depreciation methods remain open.
- `FIN-LIM-0032`: depreciation workflow approval routing remains open.
- `FIN-LIM-0033`: depreciation reversal/correction remains open.
- `FIN-LIM-0034`: period-close depreciation completeness integration remains open.

None blocks the next fixed-asset lifecycle batch; all remain final go-live controls unless explicitly accepted.

## Rollback Considerations

Rollback before use is straightforward through the migration down path. After depreciation postings exist, rollback would remove run headers and line snapshots that link the fixed asset subledger to posted GL, so accountant-approved export/reconciliation would be required before rollback in any production-like environment.
