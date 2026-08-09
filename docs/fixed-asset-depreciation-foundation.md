# Fixed Asset Depreciation Foundation

Date: 2026-07-07

Scope boundary: Batch 20 established straight-line depreciation setup, schedule/run creation, posting through `IFinancePostingEngine`, book-value updates, audit events, diagnostics, and focused tests. Subsequent Finance slices added workflow approval, period-close completeness, correction/reversal, frontend operations and the standards-aligned methods described below. Revaluation, impairment, transfers and disposals remain separate asset-accounting workflows.

## Architecture Decision

Posted GL remains the accounting source of truth. Fixed asset book values and depreciation schedules are controlled subledger/read-side state that must reconcile to posted depreciation journals and `FinancePostingEvent` records.

Depreciation journals are posted only through `IFinancePostingEngine`; this batch removes the legacy depreciation posting dependency on `IJournalEntryService`.

## Files And Modules

- `src/ErpSystem.Core/Entities/Finance/FixedAssets/FixedAssetDepreciationRun.cs`
- `src/ErpSystem.Core/Entities/Finance/FixedAssets/AssetDepreciationSchedule.cs`
- `src/ErpSystem.Core/DTOs/Finance/FixedAssetDtos.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetDepreciationService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetService.cs`
- `src/ErpSystem.Data/ApplicationDbContext.cs`
- `src/ErpSystem.Data/Migrations/20260707103000_AddFixedAssetDepreciationFoundation.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetDepreciationFoundationTests.cs`

## Supported Methods

The original Batch 20 foundation supported straight-line depreciation only. The 2026-08-09 `FIN-LIM-0031` extension adds:

- declining balance using an approved annual rate
- double-declining balance using an approved rate or a derived `200% / useful life in years` rate
- units of production using approved lifetime capacity and period-specific usage evidence

Sum-of-years-digits and `None` remain unavailable as normal depreciation choices. Revenue-based depreciation is deliberately not offered. See `docs/fixed-asset-depreciation-method-policy.md` for the standards research and TDC policy decision.

Method, rate/capacity and usage values are copied into immutable schedule snapshots so future configuration does not rewrite posted accounting evidence.

## Depreciation Run Model

`FixedAssetDepreciationRun` is the run/header record for a tenant, fiscal period, book classification, and optional fixed asset scope.

It captures:

- tenant ID
- fiscal period
- fixed asset scope, when single-asset run
- book classification
- posting date
- status
- total depreciation amount
- journal entry ID
- posting event ID
- idempotency key
- calculated/posted/failed timestamps
- failure reason

`AssetDepreciationSchedule` now captures the line-level depreciation snapshot:

- depreciation run ID
- accounting book
- period
- depreciation amount
- accumulated depreciation before/after
- NBV before/after
- depreciable amount
- residual value snapshot
- useful life snapshot
- method snapshot
- diminishing-balance rate snapshot, when applicable
- lifetime production capacity, period usage and cumulative usage before/after, when applicable
- production evidence reference and notes, when applicable
- placed-in-service date snapshot
- journal entry ID
- posting event ID
- posting date

## Validation Rules

Depreciation validates:

- fiscal period is open and unlocked
- posting date falls in the selected fiscal period
- asset belongs to current tenant
- category belongs to current tenant
- asset is capitalized before depreciation
- asset is active before depreciation
- placed-in-service date exists and is not after the posting period
- depreciation does not post before capitalization date
- depreciation does not post before placed-in-service date
- residual value does not exceed acquisition cost
- useful life is greater than zero
- depreciation cannot reduce NBV below residual value
- duplicate tenant/asset/period/book depreciation is idempotent
- unsupported methods fail clearly
- diminishing-balance rates are greater than zero and no more than 100%
- units-of-production capacity and period usage are positive
- cumulative production usage cannot exceed approved lifetime capacity
- units-of-production posting requires asset-specific usage evidence
- posting revalidates saved method assumptions against the tracked asset book before creating the journal
- depreciation account mappings are same-tenant, active, direct-posting, and account-type compatible

## Posting Design

Depreciation posts one run journal through `IFinancePostingEngine`.

Accounting treatment:

- Dr depreciation expense
- Cr accumulated depreciation

The posting request uses:

- source module: `FixedAssets`
- source document type: `FixedAssetDepreciationRun`
- posting action: `Depreciation`
- deterministic idempotency key by tenant, period, book classification, fixed asset scope, and posting/calculation mode

Posting lines include asset ID, run ID, schedule ID, and book classification in line notes for diagnostics and GL/subledger reconciliation.

## Book Values And NBV

When depreciation posts:

- schedule lines are marked posted
- run stores journal and posting-event references
- book value accumulated depreciation and NBV are updated
- default/IFRS book updates the asset-level NBV
- acquisition cost is not mutated
- asset transactions are appended for the depreciation event

Projected schedule generation with `PostToGl = false` does not update book values or NBV.

## Currency Behavior

Depreciation uses functional-currency capitalized cost from the asset book value. Foreign-currency asset depreciation does not retranslate the asset cost using current exchange rates. Later exchange-rate edits do not mutate posted depreciation schedules or GL lines.

## Workflow And Permissions

This batch uses existing controller authorization and Finance audit infrastructure. It does not introduce a parallel approval system.

Depreciation run workflow approval routing is not implemented in this batch and is tracked as `FIN-LIM-0032`.

## Period Close

Posting-date enforcement is handled by `IFinancePostingEngine` and fiscal-period validation. Full period-close integration for missing depreciation completeness is tracked as `FIN-LIM-0034`.

## Audit Events

Batch 20 adds or verifies Finance audit event types for:

- `Finance.FixedAsset.DepreciationPolicyConfigured`
- `Finance.FixedAsset.DepreciationScheduleGenerated`
- `Finance.FixedAsset.DepreciationRunCreated`
- `Finance.FixedAsset.DepreciationCalculated`
- `Finance.FixedAsset.DepreciationPosted`
- `Finance.FixedAsset.DepreciationPostingFailed`
- `Finance.FixedAsset.DepreciationRunReversed`
- `Finance.FixedAsset.DepreciationConfigurationChanged`
- `Finance.FixedAsset.DepreciationBlockedClosedPeriod`
- `Finance.FixedAsset.DepreciationAccountMappingInvalid`

The current implementation emits run-created, calculated, posted, failed, closed-period blocked, and invalid-account events where applicable.

## Diagnostics

Run these before fixed asset depreciation sign-off. Names follow the current EF table mappings.

### Capitalized Active Assets Without Depreciation Policy

```sql
SELECT fa.Id, fa.TenantId, fa.AssetCode, fa.UsefulLifeMonths, fa.DepreciationMethod, fa.ResidualValue
FROM FixedAssets fa
WHERE fa.CapitalizedAt IS NOT NULL
  AND fa.Status = 2
  AND (fa.UsefulLifeMonths <= 0 OR fa.DepreciationMethod IS NULL);
```

### Residual Value Greater Than Cost

```sql
SELECT fa.Id, fa.TenantId, fa.AssetCode, fa.AcquisitionCost, fa.ResidualValue
FROM FixedAssets fa
WHERE fa.ResidualValue > fa.AcquisitionCost;
```

### Invalid Useful Life

```sql
SELECT fa.Id, fa.TenantId, fa.AssetCode, fa.UsefulLifeMonths
FROM FixedAssets fa
WHERE fa.CapitalizedAt IS NOT NULL
  AND fa.UsefulLifeMonths <= 0;
```

### Depreciation Schedules Exceeding Depreciable Amount

```sql
SELECT s.TenantId, s.FixedAssetId, SUM(s.DepreciationAmount) AS TotalDepreciation, MAX(s.DepreciableAmount) AS DepreciableAmount
FROM AssetDepreciationSchedules s
WHERE s.IsPosted = 1
GROUP BY s.TenantId, s.FixedAssetId, s.BookClassification
HAVING SUM(s.DepreciationAmount) - MAX(s.DepreciableAmount) > 0.01;
```

### Posted Depreciation Lines Missing Journal Or Posting Event

```sql
SELECT s.Id, s.TenantId, s.FixedAssetId, s.FiscalPeriodId, s.JournalEntryId, s.PostingEventId
FROM AssetDepreciationSchedules s
WHERE s.IsPosted = 1
  AND (s.JournalEntryId IS NULL OR s.PostingEventId IS NULL);
```

### Depreciation Journals Without Run-Line References

```sql
SELECT je.Id, je.TenantId, je.EntryNumber, je.SourceDocumentId
FROM JournalEntries je
WHERE je.SourceModule = 'FixedAssets'
  AND je.SourceDocumentType = 'FixedAssetDepreciationRun'
  AND je.PostingStatus = 'Posted'
  AND NOT EXISTS (
      SELECT 1
      FROM AssetDepreciationSchedules s
      WHERE s.TenantId = je.TenantId
        AND s.JournalEntryId = je.Id
  );
```

### Duplicate Depreciation For Same Asset And Period

```sql
SELECT TenantId, FixedAssetId, FiscalPeriodId, BookClassification, COUNT(*) AS DepreciationLines
FROM AssetDepreciationSchedules
WHERE IsDeleted = 0
GROUP BY TenantId, FixedAssetId, FiscalPeriodId, BookClassification
HAVING COUNT(*) > 1;
```

### Depreciation Posted Before Capitalization Or Placed In Service

```sql
SELECT s.Id, s.TenantId, fa.AssetCode, s.PostingDate, fa.CapitalizationDate, s.PlacedInServiceDateSnapshot
FROM AssetDepreciationSchedules s
JOIN FixedAssets fa ON fa.Id = s.FixedAssetId AND fa.TenantId = s.TenantId
WHERE s.IsPosted = 1
  AND (
      (fa.CapitalizationDate IS NOT NULL AND s.PostingDate < fa.CapitalizationDate)
      OR (s.PlacedInServiceDateSnapshot IS NOT NULL AND s.PostingDate < s.PlacedInServiceDateSnapshot)
  );
```

### Depreciation Posted To Closed Or Locked Periods

```sql
SELECT s.Id, s.TenantId, s.FiscalPeriodId, fp.PeriodStatus, fp.IsLocked
FROM AssetDepreciationSchedules s
JOIN FiscalPeriods fp ON fp.Id = s.FiscalPeriodId AND fp.TenantId = s.TenantId
WHERE s.IsPosted = 1
  AND (fp.IsOpen = 0 OR fp.IsLocked = 1);
```

### Depreciation Accounts Missing Or Cross-Tenant

```sql
SELECT c.Id, c.TenantId, c.Code, c.AccumulatedDepreciationAccountId, c.DepreciationExpenseAccountId
FROM FixedAssetCategories c
LEFT JOIN Accounts acc ON acc.Id = c.AccumulatedDepreciationAccountId
LEFT JOIN Accounts exp ON exp.Id = c.DepreciationExpenseAccountId
WHERE acc.Id IS NULL
   OR exp.Id IS NULL
   OR acc.TenantId <> c.TenantId
   OR exp.TenantId <> c.TenantId
   OR acc.Status <> 1
   OR exp.Status <> 1
   OR acc.AllowDirectPosting = 0
   OR exp.AllowDirectPosting = 0;
```

### Accumulated Depreciation Subledger Not Matching Posted GL

```sql
SELECT s.TenantId, s.FixedAssetId, SUM(s.DepreciationAmount) AS ScheduleAccumulated, gl.GlAccumulated
FROM AssetDepreciationSchedules s
OUTER APPLY (
    SELECT SUM(atx.CreditAmount - atx.DebitAmount) AS GlAccumulated
    FROM AccountTransactions atx
    JOIN JournalEntries je ON je.Id = atx.JournalEntryId AND je.TenantId = atx.TenantId
    JOIN FixedAssets fa ON fa.Id = s.FixedAssetId AND fa.TenantId = s.TenantId
    JOIN FixedAssetCategories c ON c.Id = fa.FixedAssetCategoryId AND c.TenantId = fa.TenantId
    WHERE atx.TenantId = s.TenantId
      AND atx.AccountId = c.AccumulatedDepreciationAccountId
      AND je.SourceDocumentType = 'FixedAssetDepreciationRun'
      AND je.PostingStatus = 'Posted'
) gl
WHERE s.IsPosted = 1
GROUP BY s.TenantId, s.FixedAssetId, gl.GlAccumulated
HAVING ABS(SUM(s.DepreciationAmount) - ISNULL(gl.GlAccumulated, 0)) > 0.01;
```

### NBV Below Residual Value

```sql
SELECT FixedAssetId, TenantId, BookClassification, NetBookValue, ResidualValue
FROM FixedAssetBookValues
WHERE NetBookValue < ResidualValue;
```

### Book Values Changed Without Posted Depreciation

```sql
SELECT bv.Id, bv.TenantId, bv.FixedAssetId, bv.AccumulatedDepreciation
FROM FixedAssetBookValues bv
WHERE bv.AccumulatedDepreciation > 0
  AND NOT EXISTS (
      SELECT 1
      FROM AssetDepreciationSchedules s
      WHERE s.TenantId = bv.TenantId
        AND s.FixedAssetId = bv.FixedAssetId
        AND s.BookClassification = bv.BookClassification
        AND s.IsPosted = 1
  );
```

### Disposed Assets Still Being Depreciated

```sql
SELECT s.Id, s.TenantId, fa.AssetCode, fa.Status
FROM AssetDepreciationSchedules s
JOIN FixedAssets fa ON fa.Id = s.FixedAssetId AND fa.TenantId = s.TenantId
WHERE s.IsPosted = 1
  AND fa.Status IN (4, 6);
```

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

## Migration And Rollback

Migration: `20260707103000_AddFixedAssetDepreciationFoundation`.

Schema impact:

- Adds `FixedAssetDepreciationRuns`.
- Adds depreciation run, posting-event, posting-date, before/after accumulated depreciation, before/after NBV, depreciable amount, residual/useful-life/method, and placed-in-service snapshots to `AssetDepreciationSchedules`.
- Adds tenant-aware indexes and posting/run relationships.

Rollback is safe only before depreciation postings are used. After production depreciation, rollback would remove run headers, journal/posting-event links, and depreciation calculation snapshots required for audit and GL reconciliation.

## Limitations Register

- `FIN-LIM-0023`: resolved for straight-line depreciation foundation.
- `FIN-LIM-0031`: resolved by the standards-aligned diminishing-balance, double-declining and units-of-production extension.
- `FIN-LIM-0032`: resolved by Finance workflow approval hardening.
- `FIN-LIM-0033`: resolved by controlled depreciation run reversal/correction.
- `FIN-LIM-0034`: resolved by the non-waivable fixed-asset depreciation period-close control.

None of the open Batch 20 limitations blocks starting revaluation/impairment, transfer, or disposal foundation work. They remain final go-live controls unless explicitly accepted by accounting/product leadership.

## Definition Of Done

- [x] Depreciation posts through `IFinancePostingEngine`.
- [x] Straight-line method is implemented and tested.
- [x] Unsupported methods fail clearly.
- [x] Run/header and line snapshots are persisted.
- [x] Accumulated depreciation and NBV update after posting.
- [x] Acquisition cost is not mutated by depreciation.
- [x] Tenant account and cross-tenant guards are enforced.
- [x] Closed periods reject depreciation posting.
- [x] Audit events are emitted for run lifecycle and failures.
- [x] Diagnostics and limitations register entries are documented.
