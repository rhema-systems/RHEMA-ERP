# Fixed Asset Disposal and Derecognition Foundation - PR Summary

Date: 2026-07-08

## Scope

Implemented Fixed Assets Batch 21C only: whole-asset sale/write-off disposal requests, workflow approval, posting-engine derecognition, accumulated depreciation/impairment clearing, disposal gain/loss posting, sale proceeds clearing-account treatment, tenant-safe validation, audit events, diagnostics, limitations-register updates, and focused tests.

Fixed asset reporting, frontend UI, data migration/sign-off, print/export, GL-impacting transfer reclassification, impairment reversal, valuation correction/supersession, depreciation reversal, capitalization reversal, and additional depreciation methods were not started.

## Definition Of Done

- [x] Backend build impact verified through isolated backend/test builds.
- [x] Frontend build/type-check impact: no frontend files were intentionally changed in this batch.
- [x] Tests added and focused Batch 21C suite passes.
- [x] Migration added.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Accounting impact documented.
- [x] Workflow and permission behavior documented.
- [x] Audit-event verification documented.
- [x] Limitations register updated with resolved/split limitation IDs.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/FixedAssets/AssetDisposal.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAssetCategory.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/AssetDisposalService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetCategoryService.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260708120000_AddFixedAssetDisposalFoundation.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetDisposalFoundationTests.cs`
- `docs/fixed-asset-disposal-derecognition-foundation.md`
- `docs/fixed-asset-disposal-derecognition-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Migration

Added `20260708120000_AddFixedAssetDisposalFoundation`.

Schema impact:

- Adds disposal proceeds clearing account mapping to `FixedAssetCategories`.
- Adds disposal accounting/book/fiscal-period snapshots to `AssetDisposals`.
- Adds proceeds currency/account, cost/depreciation/impairment/revaluation/NBV/gain-loss snapshots.
- Adds completion/posting/failure metadata, workflow instance, idempotency key, and posting event link.
- Adds indexes and foreign keys for idempotency, fiscal period, accounting book, proceeds account, journal entry, and posting event.

Rollback:

- Safe before disposal records are used.
- After disposal journals exist, rollback requires accountant-approved export/reconciliation because posted GL remains while disposal snapshot/back-reference columns would be removed.

## Tests

Focused suite: `FixedAssetDisposalFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetDisposals`.

| Scenario | Test method |
|---|---|
| Disposal cannot run before capitalization | `DisposalCannotRunBeforeCapitalization` |
| Already disposed asset cannot be disposed again | `AlreadyDisposedAssetCannotBeDisposedAgain` |
| Disposed asset cannot be transferred, depreciated, or valued | `DisposedAssetCannotBeTransferredDepreciatedOrRevalued` |
| Disposal before placed-in-service/capitalization date rejected | `DisposalBeforePlacedInServiceOrCapitalizationDateRejected` |
| Closed-period disposal rejected through posting engine | `DisposalIntoClosedPeriodRejectedThroughPostingEngine` |
| Write-off/no proceeds posts correct Dr/Cr | `WriteOffNoProceedsPostsCorrectDebitCredit` |
| Sale disposal with gain posts correct Dr/Cr | `SaleDisposalWithGainPostsCorrectDebitCredit` |
| Sale disposal with loss posts correct Dr/Cr | `SaleDisposalWithLossPostsCorrectDebitCredit` |
| Accumulated depreciation cleared and cost not mutated | `AccumulatedDepreciationIsClearedAndCostIsNotMutated` |
| Accumulated impairment cleared through derecognition lines | `AccumulatedImpairmentIsClearedThroughDerecognitionLines` |
| Prior depreciation schedules and valuations are not rewritten | `PriorDepreciationSchedulesAndValuationsAreNotRewritten` |
| Missing disposal mapping blocks posting | `MissingDisposalGainLossOrProceedsMappingBlocksPosting` |
| Cross-tenant disposal account rejected | `CrossTenantDisposalAccountRejected` |
| Duplicate disposal is idempotent/safe | `DuplicateDisposalIsIdempotentOrSafelyRejected` |
| Missing required depreciation blocks disposal | `DisposalWithMissingRequiredDepreciationIsRejected` |
| Revaluation surplus transfers within equity and never affects P&L | `RevaluationSurplusTransfersDirectlyToRetainedEarningsWithoutAffectingProfitAndLoss` |
| Foreign-currency proceeds rejected clearly | `ForeignCurrencyProceedsRejectedClearly` |
| Disposal audit events are emitted | `DisposalAuditEventsAreEmitted` |

Results:

- Backend API build: passed.
- Focused fixed asset disposal suite: `18/18` passed.
- Finance go-live regression slice: `316/316` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- Disposal queries scope by current Finance `TenantId`.
- Cross-tenant asset lookup fails as not found.
- Cross-tenant category/account mappings are rejected through same-tenant account resolution.
- Disposal posting request carries `SourceDocumentTenantId`.
- Posting engine validates all GL accounts and posting event references by tenant.
- Audit events are written through tenant-guarded `IFinanceAuditService`.

## Accounting Impact

- Supported disposal journals post through `IFinancePostingEngine`.
- Direct legacy disposal posting via `IJournalEntryService` was removed from `AssetDisposalService`.
- Whole-asset sale disposal posts proceeds to a configured clearing account and recognizes disposal gain/loss.
- Whole-asset write-off/no-proceeds disposal derecognizes carrying amount and recognizes write-off loss.
- Accumulated depreciation and accumulated impairment are cleared through derecognition lines.
- Acquisition cost, prior depreciation schedules, prior valuation/impairment records, and acquisition FX snapshots are not rewritten.
- Revaluation surplus is not recycled to profit or loss. The later `FIN-LIM-0041` slice now transfers the remaining asset-specific reserve directly to retained earnings in the same disposal journal.

## Supported Disposal Behavior

Supported:

- Whole-asset sale disposal with functional-currency proceeds to a same-tenant clearing account.
- Whole-asset write-off/no-proceeds disposal.
- Idempotent completion for already completed disposal records.
- Closed-period block through the posting engine.

Unsupported and tracked:

- Final/partial-period depreciation on disposal was subsequently resolved under `FIN-LIM-0039`; see `docs/fixed-asset-disposal-date-depreciation-foundation.md`.
- Disposal sale VAT/AR/cash integration: `FIN-LIM-0040`.
- Revaluation surplus equity transfer policy: resolved by the follow-up documented in `docs/fixed-asset-disposal-revaluation-surplus-policy-foundation.md`.
- Partial/component disposal was subsequently resolved under `FIN-LIM-0042`; see `docs/fixed-asset-partial-component-disposal-foundation.md`.
- Foreign-currency disposal proceeds: `FIN-LIM-0043`.

## Depreciation Interaction

Disposal still requires depreciation posted through the prior fiscal period. The subsequent `FIN-LIM-0039` slice calculates final depreciation through the disposal date and retains prior posted schedules as immutable history.

## Revaluation And Impairment Interaction

Disposal uses posted revaluation/impairment snapshots to compute carrying account amount and accumulated impairment. It does not mutate valuation records and does not post revaluation surplus to profit or loss.

## Transfer Interaction

Disposed or written-off assets cannot be transferred, depreciated, or revalued/impaired by the existing services. Transfer history is not rewritten by disposal.

## Tax And Proceeds Behavior

Sale proceeds are recorded to a configured clearing account. The batch does not create AR invoices, cash/bank receipts, VAT documents, tax reports, or statutory sale-tax outputs.

## FX Behavior

Disposal is measured in tenant functional currency. Foreign-currency proceeds are rejected clearly; acquisition FX snapshots remain immutable.

## Workflow And Permission Behavior

Disposal request/approval uses the existing workflow service:

- `StartApprovalWorkflowAsync("AssetDisposal", disposal.Id)`
- `CanUserApproveAsync("AssetDisposal", disposal.Id, userId)`
- `ProcessApprovalStepAsync("AssetDisposal", disposal.Id, userId, "Approve" | "Reject", comments)`

No parallel workflow system was introduced. Controller permission behavior remains under the existing Finance permission framework.

## Audit Event Verification

Implemented/emitted where applicable:

- `Finance.FixedAsset.DisposalRequested`
- `Finance.FixedAsset.DisposalApproved`
- `Finance.FixedAsset.DisposalRejected`
- `Finance.FixedAsset.DisposalCalculated`
- `Finance.FixedAsset.DisposalPosted`
- `Finance.FixedAsset.DisposalPostingFailed`
- `Finance.FixedAsset.WrittenOff`
- `Finance.FixedAsset.DisposalSaleProceedsRecorded`
- `Finance.FixedAsset.DisposalBlockedClosedPeriod`
- `Finance.FixedAsset.DisposalBlockedMissingDepreciation`
- `Finance.FixedAsset.DisposalBlockedInvalidTenantAccountProceeds`
- `Finance.FixedAsset.DisposalConfigurationUsed`

`Finance.FixedAsset.DisposalPostedEditRejected` is defined for future explicit edit endpoints or diagnostics; current service has no destructive edit endpoint for posted disposals.

## Limitations Register

- `FIN-LIM-0026`: resolved for whole-asset disposal/write-off foundation.
- `FIN-LIM-0039`: subsequently resolved for whole-asset disposal.
- `FIN-LIM-0040`: opened by this historical batch and subsequently resolved by `docs/fixed-asset-sale-settlement-foundation.md`.
- `FIN-LIM-0041`: resolved by the dedicated whole-asset disposal equity-transfer policy slice.
- `FIN-LIM-0042`: resolved by controlled proportional partial/component derecognition.
- `FIN-LIM-0043`: opened for foreign-currency disposal proceeds.
- `FIN-LIM-0027`: fixed asset reporting/reconciliation remains open.

No remaining disposal limitation blocks fixed asset reporting/reconciliation because unsupported paths either fail clearly or post to controlled clearing accounts.

## Safe To Proceed

Safe to proceed to fixed asset reporting/reconciliation. Reporting should reconcile cost, accumulated depreciation, impairment, disposals, additions, and NBV back to posted GL and disposal posting events.
