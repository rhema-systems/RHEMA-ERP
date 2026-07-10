# Fixed Asset Transfer and Custody/Segment Movement Foundation - PR Summary

Date: 2026-07-08

## Scope

Implemented Fixed Assets Batch 21B only: transfer model hardening, custody/location transfer history, prospective segment movement, workflow approval through the existing workflow service, transfer idempotency, transfer audit events, future depreciation segment propagation, diagnostics, and focused tests.

Disposals, sale proceeds, write-offs, fixed asset reporting, frontend UI, data migration/sign-off, print/export, impairment reversal, valuation correction/supersession, depreciation reversal, capitalization reversal, additional depreciation methods, and GL-impacting fixed asset reclassification were not started.

## Definition Of Done

- [x] Backend build impact verified through isolated test assembly build.
- [x] Frontend build/type-check impact: no frontend files were intentionally changed in this batch.
- [x] Tests added and focused Batch 21B suite passes.
- [x] Migration added.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Accounting impact documented.
- [x] Workflow and permission behavior documented.
- [x] Audit-event verification documented.
- [x] Limitations register updated with resolved/split limitation IDs.

## Files Changed

- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAsset.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/AssetTransfer.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Core/Enums/FixedAssetEnums.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/AssetTransferService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetDepreciationService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260708110000_AddFixedAssetTransferFoundation.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetTransferFoundationTests.cs`
- `docs/fixed-asset-transfer-custody-segment-foundation.md`
- `docs/fixed-asset-transfer-custody-segment-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Migration

Added `20260708110000_AddFixedAssetTransferFoundation`.

Schema impact:

- Adds current custodian and current segment fields to `FixedAssets`.
- Adds accounting date, fiscal period, from/to segment snapshots, workflow instance, journal/posting references, idempotency key, completion/posting/failure metadata, and failure reason to `AssetTransfers`.
- Adds transfer indexes and foreign keys for current custodian, current segment, from/to segment, fiscal period, journal entry, and posting event.

Rollback:

- Safe before production transfer records exist.
- After transfer records exist, rollback would remove transfer segment/custodian snapshots and current asset custody/segment fields, so asset custody history should be exported and reconciled before rollback.

## Tests

Focused suite: `FixedAssetTransferFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetTransfers`.

| Scenario | Test method |
|---|---|
| Transfer cannot run before capitalization | `TransferCannotRunBeforeCapitalization` |
| Disposed asset cannot be transferred | `DisposedAssetCannotBeTransferred` |
| Cross-tenant asset transfer rejected | `CrossTenantAssetTransferRejected` |
| Cross-tenant segment lookup rejected | `CrossTenantSegmentLookupRejected` |
| Cross-tenant custodian rejected | `CrossTenantCustodianRejected` |
| Custody/location transfer creates history but no GL journal | `CustodyLocationOnlyTransferCreatesHistoryButNoGlJournal` |
| GL reclassification transfer fails clearly | `GlReclassificationTransferFailsClearlyInsteadOfMisposting` |
| Duplicate transfer request returns existing transfer | `DuplicateTransferRequestReturnsExistingTransfer` |
| Completed transfer is idempotent and not re-mutated | `CompletedTransferCannotBeCompletedAgainWithMutation` |
| Transfer preserves acquisition cost, accumulated depreciation, and NBV | `TransferPreservesAcquisitionCostAccumulatedDepreciationAndNbv` |
| Transfer preserves impairment/revaluation history | `TransferPreservesImpairmentAndRevaluationHistory` |
| Future depreciation uses new segment after transfer | `FutureDepreciationUsesNewSegmentAfterTransfer` |
| Prior depreciation schedules are not rewritten | `PriorDepreciationSchedulesAreNotRewritten` |
| Transfer audit events are emitted | `TransferAuditEventsAreEmitted` |

Results:

- Isolated test assembly build during focused test run: passed.
- Focused fixed asset transfer suite: `14/14` passed.
- Finance go-live regression slice: `298/298` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- Transfer queries scope by current Finance `TenantId`.
- Cross-tenant asset lookup fails as not found.
- Cross-tenant custodian lookup is rejected.
- Cross-tenant segment lookup is rejected.
- Transfer audit events are written through tenant-guarded `IFinanceAuditService`.
- Future depreciation posting uses the same tenant-scoped asset current segment value.

## Accounting Impact

- Custody/location transfers do not post GL journals.
- Prospective segment movements do not reclassify existing GL balances and do not rewrite historical journals.
- Future depreciation posting lines carry the asset's current segment string.
- Acquisition cost, accumulated depreciation, NBV, impairment history, revaluation history, and prior depreciation schedules are preserved.
- GL-impacting fixed asset reclassification transfers are rejected and tracked as `FIN-LIM-0038`.

## Supported Transfer Behavior

- Supported: workflow-approved custody/location transfer.
- Supported: workflow-approved prospective segment movement for future depreciation allocation.
- Unsupported and rejected: GL-impacting reclassification transfer of asset cost, accumulated depreciation, impairment, or revaluation balances.

## Depreciation Interaction

Completed segment transfers update the asset's current segment. Later depreciation posting lines use that segment. Prior posted depreciation schedules are not rewritten.

## Revaluation And Impairment Interaction

Transfer completion does not mutate valuation records, revaluation surplus, impairment allowance, or asset NBV except for existing controlled book-value state already maintained by valuation/depreciation services.

## Workflow And Permission Behavior

The batch uses the existing workflow service for request/approve/reject:

- `StartApprovalWorkflowAsync("AssetTransfer", transfer.Id)`
- `CanUserApproveAsync("AssetTransfer", transfer.Id, userId)`
- `ProcessApprovalStepAsync("AssetTransfer", transfer.Id, userId, "Approve" | "Reject", comments)`

No parallel workflow or approval system was introduced.

## Audit Event Verification

Implemented/emitted where applicable:

- `Finance.FixedAsset.TransferRequested`
- `Finance.FixedAsset.TransferApproved`
- `Finance.FixedAsset.TransferRejected`
- `Finance.FixedAsset.Transferred`
- `Finance.FixedAsset.TransferPostingFailed`
- `Finance.FixedAsset.TransferBlockedInvalidTenantDimensionAccount`

Constants are also available for transfer posted, closed-period block, configuration-used, and posted-edit rejected events for a future GL reclassification transfer batch.

## Limitations Register

- `FIN-LIM-0025`: resolved for custody/location and prospective segment movement foundation.
- `FIN-LIM-0038`: opened for GL-impacting fixed asset reclassification transfers.
- `FIN-LIM-0026`: fixed asset disposals remain open.
- `FIN-LIM-0027`: fixed asset reporting/reconciliation remains open.
- `FIN-LIM-0035`: impairment reversal remains open.
- `FIN-LIM-0036`: revaluation/impairment workflow routing remains open.
- `FIN-LIM-0037`: posted valuation correction/reversal/supersession remains open.

No remaining transfer limitation blocks the disposal batch because unsupported GL reclassification transfer requests fail clearly.

## Rollback Considerations

Rollback before use is straightforward through the migration down path. After transfer records exist, rollback would remove custody/segment snapshots and current asset custody/segment fields, so rollback in a production-like environment requires accountant-approved export and reconciliation of the asset movement history.

## Safe To Proceed

Safe to proceed to fixed asset disposals, subject to keeping `FIN-LIM-0038` visible for final go-live if GL-impacting asset reclassification transfers are required in production.
