# Fixed Asset Transfer and Custody/Segment Movement Foundation

Date: 2026-07-08

Scope boundary: this batch implements Fixed Assets Batch 21B only: tenant-scoped transfer requests, custody/location transfer history, prospective segment movement, workflow approval through the existing workflow service, Finance audit events, diagnostics, and focused tests. It does not implement disposals, sale proceeds, fixed asset reporting, data migration/sign-off, frontend UI, depreciation reversal, capitalization reversal, valuation correction, or GL-impacting fixed asset reclassification journals.

> **Follow-up status (9 August 2026):** the historical Batch 21B boundary below remains useful context, but `FIN-LIM-0038` is now resolved by the controlled GL reclassification extension documented in `docs/fixed-asset-gl-reclassification-foundation.md`. It reuses this same transfer record/workflow and adds posting-engine journals, immutable balance/account snapshots, maker-checker permissions and the Asset Transfers UI.

## Architecture Decision

Posted GL remains the accounting source of truth. Custody/location transfers and prospective segment movements are subledger/control events and do not create GL journals unless a later dedicated GL reclassification transfer model is implemented.

`AssetTransfer` is the transfer history record. It stores from/to location, custodian, segment snapshots, status, workflow instance reference, idempotency key, and optional journal/posting references for future GL-impacting transfer types. Completed transfers update the asset's current location/custodian/segment for operational control and future depreciation allocation, but they do not mutate historical capitalization, depreciation, revaluation, impairment, or journal records.

## Files And Modules

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
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetTransferFoundationTests.cs`

## Supported Transfer Types

Custody/location transfer:

- Creates an approved transfer history record.
- Updates current asset location/custodian on completion.
- Creates an `AssetTransaction` history row with zero accounting amount.
- Does not create GL journal lines.

Prospective segment movement:

- Creates an approved transfer history record with from/to segment snapshots.
- Updates current asset segment on completion.
- Future depreciation posting lines use the current segment string.
- Prior depreciation schedules and posted journals are not rewritten.

GL reclassification transfer:

- Explicitly rejected in this batch.
- Tracked as `FIN-LIM-0038`.
- No journal is created for unsupported reclassification attempts.

## Validation Rules

Transfer requests validate:

- asset belongs to current Finance tenant
- asset category belongs to current Finance tenant
- asset is capitalized and in a transferable status
- disposed, written-off, or held-for-sale assets cannot be transferred
- transfer date is not before capitalization date
- transfer date is not before placed-in-service date where present
- target custodian belongs to current tenant
- target segment lookup value belongs to current tenant and is active for the transfer date
- segment movement has a target segment
- duplicate idempotency key returns the existing transfer
- GL reclassification transfer is rejected clearly

Completed transfers are idempotent: re-completing an already completed transfer returns the existing result and does not add another asset transaction.

## Workflow And Permissions

Transfer request/approval uses the existing workflow infrastructure:

- `IWorkflowService.StartApprovalWorkflowAsync("AssetTransfer", transfer.Id)`
- `IWorkflowService.CanUserApproveAsync("AssetTransfer", transfer.Id, userId)`
- `IWorkflowService.ProcessApprovalStepAsync("AssetTransfer", transfer.Id, userId, "Approve" | "Reject", comments)`

No parallel approval system was introduced. Controller-level permission hardening remains inherited from the existing Finance permission model and prior batches.

## Accounting Impact

This batch has no GL impact for supported transfer types. It intentionally does not create transfer journals for custody/location-only movement or prospective segment movement.

Asset cost, accumulated depreciation, NBV, impairment history, revaluation history, acquisition currency snapshots, and posted journals remain unchanged. Future straight-line depreciation posting picks up the asset's current segment string.

GL-impacting reclassification of asset cost, accumulated depreciation, impairment, or revaluation balances is not implemented and is rejected. See `FIN-LIM-0038`.

## Audit Events

Added or verified Finance audit events:

- `Finance.FixedAsset.TransferRequested`
- `Finance.FixedAsset.TransferApproved`
- `Finance.FixedAsset.TransferRejected`
- `Finance.FixedAsset.Transferred`
- `Finance.FixedAsset.TransferPosted`
- `Finance.FixedAsset.TransferPostingFailed`
- `Finance.FixedAsset.TransferBlockedClosedPeriod`
- `Finance.FixedAsset.TransferBlockedInvalidTenantDimensionAccount`
- `Finance.FixedAsset.TransferConfigurationUsed`
- `Finance.FixedAsset.TransferPostedEditRejected`

The current implementation emits requested, approved, rejected, transferred, workflow-start failure, and blocked invalid-tenant/dimension events where applicable. Transfer posting events are reserved for a later GL reclassification transfer batch.

## Diagnostics

Run these before fixed asset transfer sign-off. Names follow current EF table mappings.

### Transfers Missing Tenant Or Asset References

```sql
SELECT t.Id, t.TenantId, t.FixedAssetId, t.TransferDate, t.Status
FROM AssetTransfers t
LEFT JOIN FixedAssets fa ON fa.Id = t.FixedAssetId AND fa.TenantId = t.TenantId
WHERE t.TenantId IS NULL
   OR t.FixedAssetId IS NULL
   OR fa.Id IS NULL;
```

### Cross-Tenant Custodian Or Segment References

```sql
SELECT t.Id, t.TenantId, t.FromCustodianId, t.ToCustodianId,
       t.FromSegmentLookupValueId, t.ToSegmentLookupValueId
FROM AssetTransfers t
LEFT JOIN Employees fromEmp ON fromEmp.Id = t.FromCustodianId AND fromEmp.TenantId = t.TenantId
LEFT JOIN Employees toEmp ON toEmp.Id = t.ToCustodianId AND toEmp.TenantId = t.TenantId
LEFT JOIN SegmentLookupValues fromSeg ON fromSeg.Id = t.FromSegmentLookupValueId AND fromSeg.TenantId = t.TenantId
LEFT JOIN SegmentLookupValues toSeg ON toSeg.Id = t.ToSegmentLookupValueId AND toSeg.TenantId = t.TenantId
WHERE (t.FromCustodianId IS NOT NULL AND fromEmp.Id IS NULL)
   OR (t.ToCustodianId IS NOT NULL AND toEmp.Id IS NULL)
   OR (t.FromSegmentLookupValueId IS NOT NULL AND fromSeg.Id IS NULL)
   OR (t.ToSegmentLookupValueId IS NOT NULL AND toSeg.Id IS NULL);
```

### Disposed Assets With Later Transfers

```sql
SELECT t.Id, t.TenantId, t.FixedAssetId, t.TransferDate, fa.DisposalDate
FROM AssetTransfers t
JOIN FixedAssets fa ON fa.Id = t.FixedAssetId AND fa.TenantId = t.TenantId
WHERE fa.DisposalDate IS NOT NULL
  AND t.TransferDate > fa.DisposalDate;
```

### Transfers Before Capitalization Or Placed In Service

```sql
SELECT t.Id, t.TenantId, t.FixedAssetId, t.TransferDate, fa.CapitalizationDate, fa.PlacedInServiceDate
FROM AssetTransfers t
JOIN FixedAssets fa ON fa.Id = t.FixedAssetId AND fa.TenantId = t.TenantId
WHERE (fa.CapitalizationDate IS NOT NULL AND t.TransferDate < fa.CapitalizationDate)
   OR (fa.PlacedInServiceDate IS NOT NULL AND t.TransferDate < fa.PlacedInServiceDate);
```

### GL-Impacting Transfers Without Journal References

```sql
SELECT Id, TenantId, FixedAssetId, TransferDate, TransferType, JournalEntryId, PostingEventId
FROM AssetTransfers
WHERE TransferType = 5
  AND Status = 4
  AND (JournalEntryId IS NULL OR PostingEventId IS NULL);
```

### Transfer Journals Without Transfer Records

```sql
SELECT je.Id, je.TenantId, je.JournalEntryNumber, je.SourceDocumentId, je.SourceDocumentType
FROM JournalEntries je
LEFT JOIN AssetTransfers t
  ON t.TenantId = je.TenantId
 AND t.Id = je.SourceDocumentId
WHERE je.SourceModule = 'FixedAssets'
  AND je.SourceDocumentType = 'FixedAssetTransfer'
  AND t.Id IS NULL;
```

### Duplicate Transfer Idempotency Keys

```sql
SELECT TenantId, IdempotencyKey, COUNT(*) AS DuplicateCount
FROM AssetTransfers
WHERE IdempotencyKey IS NOT NULL
GROUP BY TenantId, IdempotencyKey
HAVING COUNT(*) > 1;
```

### Current Asset Location Or Segment Not Matching Latest Completed Transfer

```sql
WITH LatestTransfer AS (
    SELECT t.*,
           ROW_NUMBER() OVER (PARTITION BY t.TenantId, t.FixedAssetId ORDER BY t.CompletedAt DESC, t.TransferDate DESC) AS rn
    FROM AssetTransfers t
    WHERE t.Status = 4
)
SELECT fa.Id, fa.TenantId, fa.Location, fa.CurrentSegmentString,
       lt.ToLocation, lt.ToSegmentString, lt.CompletedAt
FROM FixedAssets fa
JOIN LatestTransfer lt
  ON lt.TenantId = fa.TenantId
 AND lt.FixedAssetId = fa.Id
 AND lt.rn = 1
WHERE ISNULL(fa.Location, '') <> ISNULL(lt.ToLocation, '')
   OR ISNULL(fa.CurrentSegmentString, '') <> ISNULL(lt.ToSegmentString, '');
```

### Future Depreciation Schedules Not Aligned To Latest Completed Transfer Segment

```sql
SELECT s.Id, s.TenantId, s.FixedAssetId, s.FiscalPeriodId, fa.CurrentSegmentString
FROM AssetDepreciationSchedules s
JOIN FixedAssets fa ON fa.Id = s.FixedAssetId AND fa.TenantId = s.TenantId
WHERE s.IsPosted = 0
  AND fa.CurrentSegmentString IS NOT NULL;
```

This diagnostic identifies schedules that may need regeneration before posting if a future schedule predates a completed segment transfer.

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

## Limitations Register

- `FIN-LIM-0025`: resolved for custody/location and prospective segment movement foundation.
- `FIN-LIM-0038`: opened for GL-impacting fixed asset reclassification transfers.
- `FIN-LIM-0026`: fixed asset disposals remain open.
- `FIN-LIM-0027`: fixed asset reporting/reconciliation remains open.
- `FIN-LIM-0035`, `FIN-LIM-0036`, `FIN-LIM-0037`: valuation limitations remain open.

No remaining transfer limitation blocks the disposal batch because unsupported GL reclassification transfer requests fail clearly.

## Rollback Considerations

Rollback before use is straightforward through the migration down path. After transfer records exist, rollback would remove transfer segment/custodian snapshots and current asset custody/segment fields, so transfer history should be exported and reconciled before rollback in production-like environments.
