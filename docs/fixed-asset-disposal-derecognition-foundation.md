# Fixed Asset Disposal and Derecognition Foundation

Date: 2026-07-08

Scope boundary: this batch implements Fixed Assets Batch 21C only: whole-asset disposal/write-off requests, workflow approval, posting-engine derecognition journals, accumulated depreciation and impairment clearing, sale proceeds clearing, gain/loss calculation, audit events, diagnostics, and focused tests. It does not implement fixed asset reporting, frontend UI, data migration/sign-off, print/export, GL-impacting transfer reclassification, impairment reversal, valuation correction/supersession, depreciation reversal, capitalization reversal, or additional depreciation methods.

## Architecture Decision

Posted GL remains the accounting source of truth. `AssetDisposal` is the disposal source document and stores the tenant, asset, book, fiscal period, proceeds, carrying amount, accumulated depreciation, accumulated impairment, revaluation surplus snapshot, gain/loss, journal, posting event, workflow instance, idempotency key, and failure metadata.

Disposal posting always uses `IFinancePostingEngine` with `SourceModule = FixedAssets`, `SourceDocumentType = FixedAssetDisposal`, and `PostingAction = Disposal`. The old direct `IJournalEntryService` disposal posting path was removed from `AssetDisposalService`.

## Files And Modules

- `src/ErpSystem.Core/Entities/Finance/FixedAssets/AssetDisposal.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAssetCategory.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/AssetDisposalService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetCategoryService.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260708120000_AddFixedAssetDisposalFoundation.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetDisposalFoundationTests.cs`
- `docs/finance-go-live-limitations-register.md`

## Supported Disposal Types

Whole-asset sale disposal:

- Requires approved `AssetDisposal`.
- Posts sale proceeds to a configured same-tenant disposal proceeds clearing account.
- Derecognizes the asset carrying account.
- Clears accumulated depreciation and accumulated impairment where applicable.
- Recognizes gain or loss using configured same-tenant gain/loss accounts.

Whole-asset write-off/no-proceeds disposal:

- Supported through `Scrap`, `Donation`, or `DamageTheft`.
- Requires zero proceeds and zero disposal cost in this foundation batch.
- Posts Dr accumulated depreciation, Dr accumulated impairment where applicable, Dr disposal/write-off loss, Cr asset carrying account.
- Marks the asset `WrittenOff`.

Unsupported and rejected or deferred:

- Partial/component disposal was added later under `FIN-LIM-0042`; see `docs/fixed-asset-partial-component-disposal-foundation.md`.
- Foreign-currency disposal proceeds.
- Sale proceeds through AR invoice, cash/bank receipt, or tax document creation.
- Final/partial-period depreciation at disposal is now implemented under `FIN-LIM-0039`; see `docs/fixed-asset-disposal-date-depreciation-foundation.md`.
- Revaluation surplus transfer within equity on disposal.

## Validation Rules

Disposal request and posting validate:

- asset belongs to the current Finance tenant
- category belongs to the current Finance tenant
- asset is capitalized
- disposed, written-off, or held-for-sale assets cannot be disposed again
- disposal date is not before capitalization date
- disposal date is not before placed-in-service date
- disposal reason is required
- proceeds and disposal costs are not negative
- write-off/no-proceeds disposal has zero proceeds and zero disposal cost
- foreign-currency proceeds are rejected
- duplicate idempotency key returns the existing request
- active non-rejected/non-cancelled disposal already exists for the asset is rejected
- depreciation must be posted through the prior fiscal period when the asset was placed in service before the disposal period
- all configured accounts are active, same tenant, direct-posting, and type-compatible
- posting into closed/locked periods is rejected through `IFinancePostingEngine`

## Accounting Design

Disposal snapshot:

- `CostAtDisposal` stores asset carrying account amount: capitalized cost plus posted revaluation asset adjustments.
- `AccumulatedDepreciationAtDisposal` comes from the fixed asset book value.
- `AccumulatedImpairmentAtDisposal` comes from posted impairment valuation records.
- `NetBookValueAtDisposal` comes from the fixed asset book value.
- `GainOrLoss = NetProceeds - NetBookValueAtDisposal`.

Sale disposal posting:

- Dr accumulated depreciation
- Dr accumulated impairment, if applicable
- Dr disposal proceeds clearing
- Dr disposal loss, if loss
- Cr fixed asset carrying account
- Cr disposal gain, if gain

Write-off/no-proceeds posting:

- Dr accumulated depreciation
- Dr accumulated impairment, if applicable
- Dr disposal/write-off loss
- Cr fixed asset carrying account

Disposal gain is not classified as revenue. The current chart-of-account enum has no `OtherIncome`, so the gain account is validated as a direct-posting expense-class account and credited as a contra/other gain presentation account until a richer financial statement classification exists.

## Revaluation Surplus Handling

The original Batch 21C did not recycle revaluation surplus through profit or loss. The `FIN-LIM-0041` follow-up now transfers the full remaining posted asset-specific surplus directly from the category revaluation-surplus account to the tenant retained-earnings account within the same disposal journal. The approved accounts and amount are frozen on the disposal record, rechecked before posting, and remain entirely outside disposal gain/loss. See `docs/fixed-asset-disposal-revaluation-surplus-policy-foundation.md`.

## Depreciation Interaction

The disposal service requires depreciation posted through the prior fiscal period and now calculates final depreciation through the disposal date under `FIN-LIM-0039`. Time-based methods use actual inclusive days; units-of-production requires verified usage evidence. The charge and derecognition post atomically.

After a successful disposal, the default book value clears accumulated depreciation and NBV to zero without mutating acquisition cost or prior depreciation schedules.

## Tax, Proceeds, And FX

The original Batch 21C posted sale proceeds to a configured disposal proceeds clearing account and deliberately did not create AR invoices, cash/bank receipts, VAT documents, or statutory sale tax outputs. That historical gap was tracked as `FIN-LIM-0040`.

**Current status (2026-08-13):** `FIN-LIM-0040` is resolved by the subsequent Fixed Asset Sale Settlement Foundation. Approved positive-proceeds sales now create/post a canonical AR invoice and, for immediate collection, an allocated receipt; see `docs/fixed-asset-sale-settlement-foundation.md`.

Disposal proceeds may be denominated in the tenant functional currency or an approved foreign currency. The foreign-currency workflow freezes the tenant-owned Daily rate and native/functional values described in `docs/fixed-asset-foreign-currency-disposal-proceeds-foundation.md`; this resolves `FIN-LIM-0043` without implying AR/cash/tax settlement integration.

## Workflow And Permissions

Disposal request/approval uses the existing workflow infrastructure:

- `IWorkflowService.StartApprovalWorkflowAsync("AssetDisposal", disposal.Id)`
- `IWorkflowService.CanUserApproveAsync("AssetDisposal", disposal.Id, userId)`
- `IWorkflowService.ProcessApprovalStepAsync("AssetDisposal", disposal.Id, userId, "Approve" | "Reject", comments)`

No parallel approval system was introduced.

## Audit Events

Added or verified Finance audit events:

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
- `Finance.FixedAsset.DisposalPostedEditRejected`

## Diagnostics

Run these before fixed asset disposal sign-off. Names follow current EF table mappings.

### Disposed Assets Without Completed Disposal Records

```sql
SELECT fa.Id, fa.TenantId, fa.AssetCode, fa.Status, fa.DisposalDate
FROM FixedAssets fa
LEFT JOIN AssetDisposals d
  ON d.TenantId = fa.TenantId
 AND d.FixedAssetId = fa.Id
 AND d.Status = 4
WHERE fa.Status IN (4, 6)
  AND d.Id IS NULL;
```

### Disposal Records Missing Tenant Or Asset References

```sql
SELECT d.Id, d.TenantId, d.FixedAssetId, d.DisposalDate, d.Status
FROM AssetDisposals d
LEFT JOIN FixedAssets fa ON fa.Id = d.FixedAssetId AND fa.TenantId = d.TenantId
WHERE d.TenantId IS NULL
   OR d.FixedAssetId IS NULL
   OR fa.Id IS NULL;
```

### Completed Disposals Missing Journal Or Posting Event

```sql
SELECT d.Id, d.TenantId, d.FixedAssetId, d.JournalEntryId, d.PostingEventId
FROM AssetDisposals d
WHERE d.Status = 4
  AND (d.JournalEntryId IS NULL OR d.PostingEventId IS NULL);
```

### Disposal Journals Without Disposal Records

```sql
SELECT je.Id, je.TenantId, je.SourceDocumentId, je.SourceDocumentType
FROM JournalEntries je
LEFT JOIN AssetDisposals d
  ON d.TenantId = je.TenantId
 AND d.Id = je.SourceDocumentId
WHERE je.SourceModule = 'FixedAssets'
  AND je.SourceDocumentType = 'FixedAssetDisposal'
  AND d.Id IS NULL;
```

### Duplicate Completed Disposals For One Asset Book

```sql
SELECT TenantId, FixedAssetId, BookClassification, COUNT(*) AS DisposalCount
FROM AssetDisposals
WHERE Status = 4
GROUP BY TenantId, FixedAssetId, BookClassification
HAVING COUNT(*) > 1;
```

### Disposals Posted Before Capitalization Or Placed In Service

```sql
SELECT d.Id, d.TenantId, d.FixedAssetId, d.DisposalDate, fa.CapitalizationDate, fa.PlacedInServiceDate
FROM AssetDisposals d
JOIN FixedAssets fa ON fa.Id = d.FixedAssetId AND fa.TenantId = d.TenantId
WHERE (fa.CapitalizationDate IS NOT NULL AND d.DisposalDate < fa.CapitalizationDate)
   OR (fa.PlacedInServiceDate IS NOT NULL AND d.DisposalDate < fa.PlacedInServiceDate);
```

### Disposals Posted To Closed Or Locked Periods

```sql
SELECT d.Id, d.TenantId, d.FiscalPeriodId, p.PeriodCode, p.IsOpen, p.IsClosed, p.IsLocked
FROM AssetDisposals d
JOIN FiscalPeriods p ON p.Id = d.FiscalPeriodId AND p.TenantId = d.TenantId
WHERE d.Status = 4
  AND (p.IsOpen = 0 OR p.IsClosed = 1 OR p.IsLocked = 1);
```

### Disposal Accounts Missing Or Cross-Tenant

```sql
SELECT c.Id, c.TenantId, c.Code,
       c.AssetAccountId,
       c.AccumulatedDepreciationAccountId,
       c.GainOnDisposalAccountId,
       c.LossOnDisposalAccountId,
       c.DisposalProceedsClearingAccountId
FROM FixedAssetCategories c
LEFT JOIN Accounts assetAccount ON assetAccount.Id = c.AssetAccountId AND assetAccount.TenantId = c.TenantId
LEFT JOIN Accounts accumDep ON accumDep.Id = c.AccumulatedDepreciationAccountId AND accumDep.TenantId = c.TenantId
LEFT JOIN Accounts gainAccount ON gainAccount.Id = c.GainOnDisposalAccountId AND gainAccount.TenantId = c.TenantId
LEFT JOIN Accounts lossAccount ON lossAccount.Id = c.LossOnDisposalAccountId AND lossAccount.TenantId = c.TenantId
LEFT JOIN Accounts proceedsAccount ON proceedsAccount.Id = c.DisposalProceedsClearingAccountId AND proceedsAccount.TenantId = c.TenantId
WHERE assetAccount.Id IS NULL
   OR accumDep.Id IS NULL
   OR (c.GainOnDisposalAccountId IS NOT NULL AND gainAccount.Id IS NULL)
   OR (c.LossOnDisposalAccountId IS NOT NULL AND lossAccount.Id IS NULL)
   OR (c.DisposalProceedsClearingAccountId IS NOT NULL AND proceedsAccount.Id IS NULL);
```

### Disposed Assets Still Being Depreciated, Transferred, Or Valued

```sql
SELECT fa.Id, fa.TenantId, fa.AssetCode, s.Id AS DepreciationScheduleId
FROM FixedAssets fa
JOIN AssetDepreciationSchedules s ON s.FixedAssetId = fa.Id AND s.TenantId = fa.TenantId
WHERE fa.Status IN (4, 6)
  AND fa.DisposalDate IS NOT NULL
  AND s.PostingDate > fa.DisposalDate;

SELECT fa.Id, fa.TenantId, fa.AssetCode, t.Id AS TransferId
FROM FixedAssets fa
JOIN AssetTransfers t ON t.FixedAssetId = fa.Id AND t.TenantId = fa.TenantId
WHERE fa.Status IN (4, 6)
  AND fa.DisposalDate IS NOT NULL
  AND t.TransferDate > fa.DisposalDate;

SELECT fa.Id, fa.TenantId, fa.AssetCode, v.Id AS ValuationId
FROM FixedAssets fa
JOIN AssetValuations v ON v.FixedAssetId = fa.Id AND v.TenantId = fa.TenantId
WHERE fa.Status IN (4, 6)
  AND fa.DisposalDate IS NOT NULL
  AND v.ValuationDate > fa.DisposalDate;
```

### Disposed Assets With Non-Zero NBV

```sql
SELECT fa.Id, fa.TenantId, fa.AssetCode, fa.NetBookValue, bv.NetBookValue AS BookNetBookValue
FROM FixedAssets fa
LEFT JOIN FixedAssetBookValues bv ON bv.FixedAssetId = fa.Id AND bv.TenantId = fa.TenantId
WHERE fa.Status IN (4, 6)
  AND (fa.NetBookValue <> 0 OR ISNULL(bv.NetBookValue, 0) <> 0);
```

### Disposal Gain/Loss Reconciliation

```sql
SELECT d.Id, d.TenantId, d.FixedAssetId, d.NetProceeds, d.NetBookValueAtDisposal, d.GainOrLoss
FROM AssetDisposals d
WHERE d.Status = 4
  AND ROUND(d.NetProceeds - d.NetBookValueAtDisposal, 2) <> ROUND(d.GainOrLoss, 2);
```

### Revaluation Surplus Incorrectly Posted To P&L

```sql
SELECT atx.Id, atx.TenantId, atx.JournalEntryId, atx.AccountId, atx.TransactionTag
FROM AccountTransactions atx
JOIN JournalEntries je ON je.Id = atx.JournalEntryId AND je.TenantId = atx.TenantId
JOIN AssetDisposals d ON d.Id = je.SourceDocumentId AND d.TenantId = je.TenantId
JOIN FixedAssets fa ON fa.Id = d.FixedAssetId AND fa.TenantId = d.TenantId
JOIN FixedAssetCategories c ON c.Id = fa.FixedAssetCategoryId AND c.TenantId = fa.TenantId
WHERE je.SourceModule = 'FixedAssets'
  AND je.SourceDocumentType = 'FixedAssetDisposal'
  AND c.RevaluationSurplusAccountId IS NOT NULL
  AND atx.AccountId = c.RevaluationSurplusAccountId;
```

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
| Prior depreciation schedules and valuation records are not rewritten | `PriorDepreciationSchedulesAndValuationsAreNotRewritten` |
| Missing disposal mappings block posting | `MissingDisposalGainLossOrProceedsMappingBlocksPosting` |
| Cross-tenant disposal account rejected | `CrossTenantDisposalAccountRejected` |
| Duplicate disposal is idempotent or safely rejected | `DuplicateDisposalIsIdempotentOrSafelyRejected` |
| Missing required depreciation blocks disposal | `DisposalWithMissingRequiredDepreciationIsRejected` |
| Revaluation surplus transfers within equity and never affects P&L | `RevaluationSurplusTransfersDirectlyToRetainedEarningsWithoutAffectingProfitAndLoss` |
| Missing retained-earnings policy blocks applicable disposal | `RevaluedAssetDisposalRequiresConfiguredRetainedEarningsAccount` |
| Changed policy account invalidates stale approval | `ChangedSurplusPolicyAccountCancelsStaleDisposalApproval` |
| Changed asset-specific reserve invalidates stale approval | `ChangedRevaluationSurplusBalanceCancelsStaleDisposalApproval` |
| Foreign-currency proceeds rejected clearly | `ForeignCurrencyProceedsRejectedClearly` |
| Disposal audit events are emitted | `DisposalAuditEventsAreEmitted` |

## Limitations Register

- `FIN-LIM-0026`: resolved for whole-asset sale/write-off disposal foundation.
- `FIN-LIM-0039`: resolved by controlled disposal-date depreciation; see `docs/fixed-asset-disposal-date-depreciation-foundation.md`.
- `FIN-LIM-0040`: resolved by the Fixed Asset Sale Settlement Foundation; see `docs/fixed-asset-sale-settlement-foundation.md`.
- `FIN-LIM-0041`: resolved for full remaining asset-specific surplus transfer directly to retained earnings on whole-asset disposal.
- `FIN-LIM-0042`: resolved by controlled proportional partial/component derecognition; see `docs/fixed-asset-partial-component-disposal-foundation.md`.
- `FIN-LIM-0043`: resolved by the foreign-currency disposal proceeds foundation.
- `FIN-LIM-0027`: fixed asset reporting/reconciliation remains open and is the natural next batch.

No remaining disposal limitation blocks fixed asset reporting/reconciliation because unsupported paths either fail clearly or post to controlled clearing accounts.

## Rollback Considerations

Rollback before production-like disposal records exist is straightforward through the migration down path. After disposal records and journals exist, rollback would remove disposal snapshot/back-reference fields and the proceeds-clearing category mapping, while posted GL journals would remain. Production rollback would require accountant-approved export and reconciliation of disposal records, asset status/NBV, and related journal/posting-event references.
