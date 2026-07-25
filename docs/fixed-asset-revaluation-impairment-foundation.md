# Fixed Asset Revaluation and Impairment Foundation

Date: 2026-07-08

Scope boundary: this batch implements Fixed Assets Batch 21A only: tenant-scoped revaluation and impairment calculation records, posting through `IFinancePostingEngine`, category account mappings, valuation audit events, book-value/NBV updates, depreciation interaction for prospective straight-line depreciation, diagnostics, and focused tests. It does not implement transfers, disposals, frontend UI, broad fixed asset reporting, data migration/sign-off, print/export, capitalization reversal, depreciation reversal, impairment reversal, or revaluation/impairment workflow routing.

## Architecture Decision

Posted GL remains the accounting source of truth. `AssetValuation`, `FixedAssetBookValue`, and `AssetTransaction` records are controlled fixed-asset subledger/read-side state that must reconcile to posted GL journals and `FinancePostingEvent` records.

Revaluation and impairment postings are generated only through `IFinancePostingEngine` with source document type `FixedAssetValuation`. The legacy `IJournalEntryService` valuation posting path is not used for this batch.

Historical capitalization and depreciation records are not rewritten to simulate valuation changes.

## Files And Modules

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
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetRevaluationImpairmentFoundationTests.cs`

## Model

`AssetValuation` now captures the accounting snapshot needed for revaluation and impairment:

- tenant ID
- fixed asset ID
- accounting book ID and book classification
- fiscal period ID
- valuation date and accounting date
- carrying amount before
- accumulated depreciation before
- NBV before
- fair value or recoverable amount
- carrying amount after
- adjustment amount
- revaluation surplus, deficit, surplus applied, and revaluation loss recognized
- impairment loss
- journal entry ID and posting event ID
- status: `Calculated`, `Posted`, or `Failed`
- idempotency key
- posted/failed timestamps and reason
- valuation evidence/reference fields

Impairment reversal is intentionally rejected in this batch and tracked as `FIN-LIM-0035`.

## Account Mappings

Fixed asset categories now support tenant-owned mappings for:

- revaluation surplus/reserve
- revaluation loss
- impairment loss
- accumulated impairment or impairment allowance
- impairment reversal income, reserved for a later supported reversal batch

Posting validates that accounts are same-tenant, active, direct-posting, and compatible with the posting purpose. No revaluation or impairment account is hardcoded.

## Posting Design

Revaluation increase:

- Dr fixed asset cost/carrying amount account
- Cr revaluation surplus

Revaluation decrease:

- Cr fixed asset cost/carrying amount account
- Dr existing revaluation surplus first, where available for the asset/book
- Dr revaluation loss for any excess decrease

Impairment loss:

- Dr impairment loss
- Cr accumulated impairment or impairment allowance

All postings use:

- source module: `FixedAssets`
- source document type: `FixedAssetValuation`
- posting action: `FixedAssetValuation`
- functional currency from tenant finance settings
- tenant-scoped fiscal period validation through the posting engine

## Validation Rules

Revaluation and impairment validate:

- asset belongs to the current Finance tenant
- asset category belongs to the same tenant
- asset is capitalized
- asset is not disposed
- valuation date is not before capitalization date
- fair value or recoverable amount is not negative
- reason is provided
- fiscal period is open and unlocked before posting
- required category accounts exist and belong to the same tenant
- duplicate valuation posting is idempotent for an already posted valuation
- posted valuation records cannot be reposted into duplicate GL entries

## Depreciation Interaction

When valuation changes carrying amount, book-value NBV is updated prospectively after the posting-engine journal succeeds.

Straight-line depreciation now distinguishes:

- unadjusted assets: original cost less residual divided by original useful life, capped at remaining NBV above residual
- revalued or impaired assets: adjusted NBV less residual divided by remaining useful life

Already-posted depreciation schedules are not rewritten. Later depreciation uses the adjusted carrying amount prospectively.

## Currency Behavior

Revaluation and impairment post in functional currency. Foreign-currency acquisition fields and exchange-rate snapshots remain immutable. Later exchange-rate edits do not mutate asset acquisition cost, valuation journals, or valuation snapshots.

Foreign-currency asset translation and FX revaluation are not part of this batch.

## Workflow And Permissions

This batch uses existing controller authorization, Finance permissions, and Finance audit infrastructure. It does not introduce a parallel approval system.

Configurable workflow routing for fixed asset valuation/impairment approval remains tracked as `FIN-LIM-0036`.

## Audit Events

Batch 21A adds or verifies Finance audit events for:

- `Finance.FixedAsset.ValuationBatchCreated`
- `Finance.FixedAsset.RevaluationCalculated`
- `Finance.FixedAsset.RevaluationPosted`
- `Finance.FixedAsset.RevaluationPostingFailed`
- `Finance.FixedAsset.ImpairmentCalculated`
- `Finance.FixedAsset.ImpairmentPosted`
- `Finance.FixedAsset.ImpairmentPostingFailed`
- `Finance.FixedAsset.ImpairmentReversed`
- `Finance.FixedAsset.ValuationConfigurationUsed`
- `Finance.FixedAsset.ValuationAccountMappingChanged`
- `Finance.FixedAsset.ValuationBlockedClosedPeriod`
- `Finance.FixedAsset.ValuationCrossTenantRejected`
- `Finance.FixedAsset.ValuationPostedEditRejected`

The current implementation emits calculated, configuration-used, posted, failed, and closed-period-blocked events where applicable. Mapping changes are available through category update audit payloads.

## Diagnostics

Run these before fixed asset valuation sign-off. Names follow current EF table mappings.

### Posted Valuations Missing Journal Or Posting Event

```sql
SELECT Id, TenantId, FixedAssetId, ValuationType, ValuationDate, JournalEntryId, PostingEventId
FROM AssetValuations
WHERE IsPostedToGL = 1
  AND (JournalEntryId IS NULL OR PostingEventId IS NULL);
```

### Posted Valuation Journals Without Asset Valuation References

```sql
SELECT je.Id, je.TenantId, je.JournalEntryNumber, je.SourceDocumentId, je.SourceDocumentType
FROM JournalEntries je
LEFT JOIN AssetValuations av
  ON av.TenantId = je.TenantId
 AND av.Id = je.SourceDocumentId
WHERE je.SourceModule = 'FixedAssets'
  AND je.SourceDocumentType = 'FixedAssetValuation'
  AND av.Id IS NULL;
```

### Missing Or Cross-Tenant Account Mappings

```sql
SELECT c.Id, c.TenantId, c.Code, c.RevaluationSurplusAccountId, c.RevaluationLossAccountId,
       c.ImpairmentLossAccountId, c.AccumulatedImpairmentAccountId
FROM FixedAssetCategories c
LEFT JOIN Accounts surplus ON surplus.Id = c.RevaluationSurplusAccountId AND surplus.TenantId = c.TenantId
LEFT JOIN Accounts revalLoss ON revalLoss.Id = c.RevaluationLossAccountId AND revalLoss.TenantId = c.TenantId
LEFT JOIN Accounts impairmentLoss ON impairmentLoss.Id = c.ImpairmentLossAccountId AND impairmentLoss.TenantId = c.TenantId
LEFT JOIN Accounts impairmentAllowance ON impairmentAllowance.Id = c.AccumulatedImpairmentAccountId AND impairmentAllowance.TenantId = c.TenantId
WHERE c.IsDeleted = 0
  AND (
    c.RevaluationSurplusAccountId IS NULL OR surplus.Id IS NULL OR
    c.RevaluationLossAccountId IS NULL OR revalLoss.Id IS NULL OR
    c.ImpairmentLossAccountId IS NULL OR impairmentLoss.Id IS NULL OR
    c.AccumulatedImpairmentAccountId IS NULL OR impairmentAllowance.Id IS NULL
  );
```

### Valuations Posted To Closed Or Locked Periods

```sql
SELECT av.Id, av.TenantId, av.FixedAssetId, av.FiscalPeriodId, fp.PeriodStatus, fp.IsClosed, fp.IsLocked
FROM AssetValuations av
JOIN FiscalPeriods fp ON fp.Id = av.FiscalPeriodId AND fp.TenantId = av.TenantId
WHERE av.IsPostedToGL = 1
  AND (fp.IsClosed = 1 OR fp.IsLocked = 1 OR fp.PeriodStatus <> 'Open');
```

### Duplicate Valuations For Same Asset Book And Date

```sql
SELECT TenantId, FixedAssetId, BookClassification, ValuationDate, COUNT(*) AS ValuationCount
FROM AssetValuations
WHERE IsDeleted = 0
GROUP BY TenantId, FixedAssetId, BookClassification, ValuationDate
HAVING COUNT(*) > 1;
```

### Negative Book Values

```sql
SELECT fa.Id, fa.TenantId, fa.AssetCode, fa.NetBookValue
FROM FixedAssets fa
WHERE fa.NetBookValue < 0
UNION ALL
SELECT bv.FixedAssetId, bv.TenantId, CAST(bv.FixedAssetId AS nvarchar(50)), bv.NetBookValue
FROM FixedAssetBookValues bv
WHERE bv.NetBookValue < 0;
```

### Asset Book Value Not Reconciled To Posted Valuation Journals

```sql
SELECT bv.TenantId, bv.FixedAssetId, bv.BookClassification, bv.NetBookValue,
       MAX(av.CarryingAmountAfter) AS LastPostedValuationCarryingAmount
FROM FixedAssetBookValues bv
JOIN AssetValuations av
  ON av.TenantId = bv.TenantId
 AND av.FixedAssetId = bv.FixedAssetId
 AND av.BookClassification = bv.BookClassification
 AND av.IsPostedToGL = 1
GROUP BY bv.TenantId, bv.FixedAssetId, bv.BookClassification, bv.NetBookValue
HAVING ABS(bv.NetBookValue - MAX(av.CarryingAmountAfter)) > 0.01;
```

### Impairments Missing Evidence Or Reason

```sql
SELECT Id, TenantId, FixedAssetId, ValuationDate, Reason, ValuationReportReference, Notes
FROM AssetValuations
WHERE ValuationType = 2
  AND (
    Reason IS NULL OR LTRIM(RTRIM(Reason)) = ''
    OR (ValuationReportReference IS NULL AND Notes IS NULL)
  );
```

### Revaluation Surplus Tracking Not Reconciled

```sql
SELECT av.TenantId, av.FixedAssetId, av.BookClassification,
       SUM(av.RevaluationSurplus - av.RevaluationSurplusApplied) AS NetTrackedSurplus
FROM AssetValuations av
WHERE av.IsPostedToGL = 1
GROUP BY av.TenantId, av.FixedAssetId, av.BookClassification
HAVING SUM(av.RevaluationSurplus - av.RevaluationSurplusApplied) < -0.01;
```

### Future Depreciation Schedule Not Aligned To Adjusted Carrying Amount

```sql
SELECT s.Id, s.TenantId, s.FixedAssetId, s.FiscalPeriodId, s.NetBookValueBefore, bv.NetBookValue
FROM AssetDepreciationSchedules s
JOIN FixedAssetBookValues bv
  ON bv.TenantId = s.TenantId
 AND bv.FixedAssetId = s.FixedAssetId
 AND bv.BookClassification = s.BookClassification
WHERE s.IsPosted = 0
  AND ABS(s.NetBookValueBefore - bv.NetBookValue) > 0.01;
```

## Tests

Focused suite: `FixedAssetRevaluationImpairmentFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetRevaluationImpairment`.

The suite covers capitalization/disposal guards, posting-engine Dr/Cr lines, missing/cross-tenant mappings, closed periods, idempotent reposting, NBV/acquisition-cost behavior, depreciation interaction, immutable FX acquisition snapshots, audit events, and unsupported impairment reversal.

## Known Limitations

- `FIN-LIM-0035`: impairment reversal remains unsupported and is rejected.
- `FIN-LIM-0036`: valuation workflow routing is not integrated with the configurable workflow engine.
- `FIN-LIM-0037`: posted valuation correction/reversal/supersession is not implemented.
- `FIN-LIM-0025`: asset transfers remain open.
- `FIN-LIM-0026`: disposals remain open.
- `FIN-LIM-0027`: fixed asset reporting/reconciliation remains open.
