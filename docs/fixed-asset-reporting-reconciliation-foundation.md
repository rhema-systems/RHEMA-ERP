# Fixed Asset Reporting and GL Reconciliation Foundation

Date: 2026-07-08

Scope boundary: this batch implements Fixed Assets Batch 21D only: backend fixed asset register reporting, additions/capitalization reporting, depreciation and accumulated depreciation reporting, valuation movement reporting, transfer history reporting, disposal reporting, fixed asset roll-forward, fixed-asset-to-GL reconciliation, report diagnostics, report audit events, focused tests, and limitations-register updates. It does not implement frontend UI, print/export packs, data migration/sign-off, fixed asset reversal/correction batches, procurement-origin capitalization, partial disposal, foreign-currency disposal proceeds, disposal AR/cash/tax integration, additional depreciation methods, or workflow-routing hardening.

## Source Of Truth

Posted GL remains the accounting source of truth. Fixed asset register/book values are controlled subledger/read-side records that must tie back to posted journals and posting events.

The reporting service uses:

- posted `AccountTransaction` rows
- posted `JournalEntry` rows
- `FinancePostingEvent` references where present
- fixed asset source-document and journal references
- fixed asset subledger snapshots for asset-level detail

Reports do not treat mutable book values as the only accounting source. Register and reconciliation outputs expose posted-GL movement, subledger snapshots, and variance fields so accountants can identify drift.

## Files And Modules

- `src/ErpSystem.Core/DTOs/Finance/FixedAssetReportDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFixedAssetReportsService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Controllers/Finance/FixedAssetsController.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetReportsService.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `tests/ErpSystem.Api.Tests/Services/Finance/FixedAssetReportingReconciliationFoundationTests.cs`
- `docs/fixed-asset-reporting-reconciliation-foundation.md`
- `docs/fixed-asset-reporting-reconciliation-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`

## Supported Reports

Backend service/controller support was added or hardened for:

- fixed asset register
- fixed asset additions/capitalizations
- depreciation schedules and posted depreciation
- accumulated depreciation
- revaluation and impairment movements
- custody/location/segment transfer history
- disposal and derecognition
- fixed asset roll-forward
- fixed-asset-to-GL reconciliation

Existing export endpoints were not expanded in this batch. Backend report contracts and tests are the scope for Batch 21D.

## Filters And Tenant Isolation

Reports are tenant-scoped through `ICurrentUserService.GetRequiredFinanceTenantId()`.

Supported filters include:

- date range and as-of date
- asset
- category
- account
- fiscal period
- asset status
- location
- segment string

Asset, category, and account filters are tenant-validated below the controller layer. Cross-tenant filters fail before report data is queried. Report queries also join fixed asset records, journal entries, account transactions, and posting events by the current tenant.

## Roll-Forward Logic

The roll-forward report explains opening to closing balances by category/book and supports asset-level drilldown through report item references.

Movement columns include:

- opening cost
- additions and capitalizations
- revaluation increases and decreases
- impairment additions
- depreciation charge
- accumulated depreciation movement
- disposals/write-offs
- accumulated depreciation cleared on disposal
- accumulated impairment cleared on disposal
- closing cost/carrying amount
- closing accumulated depreciation
- closing accumulated impairment
- closing NBV

The report uses posted GL tags and fixed asset source references for financial movement, then reconciles back to subledger snapshots.

## GL Reconciliation Logic

The GL reconciliation report compares posted GL balances against fixed asset subledger snapshots for:

- asset cost/carrying accounts
- accumulated depreciation accounts
- depreciation expense accounts
- accumulated impairment or impairment allowance accounts
- impairment loss accounts
- revaluation surplus accounts
- revaluation loss accounts
- disposal proceeds clearing accounts
- disposal gain/loss accounts

Each reconciliation row includes:

- GL balance
- fixed asset subledger balance
- variance
- source document count
- missing posting-event count
- GL movement without fixed asset source count
- subledger records without posted GL count

Diagnostics are emitted from the same reconciliation output rather than relying only on separate SQL.

## Movement Source Mapping

The reporting service maps current fixed asset accounting movement from:

- AP capitalization
- direct capitalization
- depreciation runs and schedule lines
- revaluation and impairment valuations
- custody/location/segment transfers
- disposal and write-off records
- disposal proceeds clearing entries

Opening/import basis and procurement-origin capitalization remain outside this batch unless already represented as posted fixed asset capitalization events.

## Disposal Gain/Loss Presentation

Batch 21C allowed disposal gain to be credited to a configured direct-posting account even though the current chart-of-account enum does not yet have a richer `OtherIncome` classification.

Batch 21D handles that explicitly:

- fixed asset disposal reports classify disposal gains as non-operating disposal gains, not ordinary operating expense
- disposal gains are not treated as revenue unless intentionally configured by account mapping
- reports return a presentation warning when the gain account is expense-class or revenue-class
- fixed-asset-to-GL reconciliation includes disposal gain/loss rows separately from depreciation expense and asset cost rows

The broader financial statement account-classification gap is tracked as `FIN-LIM-0044`.

## Transfer And Segment Reporting

Transfer reports show:

- current and historical location/custody data
- from/to segment strings
- workflow/status fields
- journal/posting references where present
- whether the transfer had GL impact

Custody/location and prospective segment transfers do not mutate historical GL or prior depreciation schedules. GL-impacting reclassification transfers remain unsupported under `FIN-LIM-0038` and should not appear as normal fixed asset GL movement.

## Audit Events

Added or verified Finance audit events:

- `Finance.FixedAsset.Report.RegisterGenerated`
- `Finance.FixedAsset.Report.RollForwardGenerated`
- `Finance.FixedAsset.Report.GLReconciliationGenerated`
- `Finance.FixedAsset.Report.VarianceDiagnosticGenerated`

Register, roll-forward, and GL reconciliation generation write tenant-scoped Finance audit records when `IFinanceAuditService` is available. Variance diagnostics are audited when reconciliation output contains non-zero variance or missing-reference findings.

## Diagnostics

Run these checks before fixed asset reporting/sign-off. Names follow current EF table mappings and should be adjusted only for provider-specific identifier quoting.

### Fixed Asset Subledger Cost Not Matching Posted GL

```sql
SELECT fa.TenantId, fa.Id AS FixedAssetId, fa.AssetCode,
       COALESCE(SUM(at.DebitAmount - at.CreditAmount), 0) AS PostedGlCost,
       COALESCE(bv.Cost, fa.Cost, 0) AS SubledgerCost
FROM FixedAssets fa
LEFT JOIN FixedAssetBookValues bv
  ON bv.TenantId = fa.TenantId AND bv.FixedAssetId = fa.Id
LEFT JOIN AccountTransactions at
  ON at.TenantId = fa.TenantId
 AND at.FixedAssetId = fa.Id
 AND at.TransactionTag IN ('FA-Capitalization', 'FA-RevaluationIncrease', 'FA-RevaluationDecrease', 'FA-DisposalCost')
 AND at.PostingStatus = 'Posted'
LEFT JOIN JournalEntries je
  ON je.Id = at.JournalEntryId
 AND je.TenantId = at.TenantId
 AND je.PostingStatus = 'Posted'
GROUP BY fa.TenantId, fa.Id, fa.AssetCode, bv.Cost, fa.Cost
HAVING ABS(COALESCE(SUM(at.DebitAmount - at.CreditAmount), 0) - COALESCE(bv.Cost, fa.Cost, 0)) > 0.01;
```

### Accumulated Depreciation Subledger Not Matching Posted GL

```sql
SELECT fa.TenantId, fa.Id AS FixedAssetId, fa.AssetCode,
       COALESCE(SUM(at.CreditAmount - at.DebitAmount), 0) AS PostedAccumulatedDepreciation,
       COALESCE(bv.AccumulatedDepreciation, 0) AS SubledgerAccumulatedDepreciation
FROM FixedAssets fa
LEFT JOIN FixedAssetBookValues bv
  ON bv.TenantId = fa.TenantId AND bv.FixedAssetId = fa.Id
LEFT JOIN AccountTransactions at
  ON at.TenantId = fa.TenantId
 AND at.FixedAssetId = fa.Id
 AND at.TransactionTag IN ('FA-AccumulatedDepreciation', 'FA-DisposalAccumulatedDepreciation')
 AND at.PostingStatus = 'Posted'
GROUP BY fa.TenantId, fa.Id, fa.AssetCode, bv.AccumulatedDepreciation
HAVING ABS(COALESCE(SUM(at.CreditAmount - at.DebitAmount), 0) - COALESCE(bv.AccumulatedDepreciation, 0)) > 0.01;
```

### Posted Fixed Asset Source Records Missing Posting References

```sql
SELECT 'FixedAsset' AS SourceType, fa.TenantId, fa.Id, fa.JournalEntryId, fa.PostingEventId
FROM FixedAssets fa
WHERE fa.Status IN (2, 3)
  AND (fa.JournalEntryId IS NULL OR fa.PostingEventId IS NULL)
UNION ALL
SELECT 'AssetDepreciationSchedule', s.TenantId, s.Id, s.JournalEntryId, s.PostingEventId
FROM AssetDepreciationSchedules s
WHERE s.IsPosted = 1
  AND (s.JournalEntryId IS NULL OR s.PostingEventId IS NULL)
UNION ALL
SELECT 'AssetValuation', v.TenantId, v.Id, v.JournalEntryId, v.PostingEventId
FROM AssetValuations v
WHERE v.Status = 3
  AND (v.JournalEntryId IS NULL OR v.PostingEventId IS NULL)
UNION ALL
SELECT 'AssetDisposal', d.TenantId, d.Id, d.JournalEntryId, d.PostingEventId
FROM AssetDisposals d
WHERE d.Status = 4
  AND (d.JournalEntryId IS NULL OR d.PostingEventId IS NULL);
```

### GL Fixed Asset Lines Without Asset Source References

```sql
SELECT at.TenantId, at.Id, at.JournalEntryId, at.AccountId, at.DebitAmount, at.CreditAmount, at.TransactionTag
FROM AccountTransactions at
JOIN JournalEntries je ON je.Id = at.JournalEntryId AND je.TenantId = at.TenantId
WHERE at.PostingStatus = 'Posted'
  AND je.PostingStatus = 'Posted'
  AND at.TransactionTag LIKE 'FA-%'
  AND at.FixedAssetId IS NULL;
```

### Disposed Assets With Non-Zero NBV

```sql
SELECT fa.TenantId, fa.Id, fa.AssetCode, fa.Status, bv.NetBookValue
FROM FixedAssets fa
JOIN FixedAssetBookValues bv ON bv.TenantId = fa.TenantId AND bv.FixedAssetId = fa.Id
WHERE fa.Status IN (4, 6)
  AND ABS(COALESCE(bv.NetBookValue, 0)) > 0.01;
```

### Active Depreciable Assets Missing Depreciation For A Period

```sql
SELECT fa.TenantId, fa.Id, fa.AssetCode, fp.Id AS FiscalPeriodId
FROM FixedAssets fa
JOIN FiscalPeriods fp ON fp.TenantId = fa.TenantId
LEFT JOIN AssetDepreciationSchedules s
  ON s.TenantId = fa.TenantId
 AND s.FixedAssetId = fa.Id
 AND s.FiscalPeriodId = fp.Id
 AND s.IsPosted = 1
WHERE fa.Status = 3
  AND fa.IsDepreciable = 1
  AND fp.StartDate >= fa.PlacedInServiceDate
  AND s.Id IS NULL;
```

### Disposal Gain/Loss Not Reconciling To Disposal Snapshots

```sql
SELECT d.TenantId, d.Id, d.FixedAssetId, d.NetProceeds, d.NetBookValueAtDisposal, d.GainOrLoss
FROM AssetDisposals d
WHERE d.Status = 4
  AND ABS((COALESCE(d.NetProceeds, 0) - COALESCE(d.NetBookValueAtDisposal, 0)) - COALESCE(d.GainOrLoss, 0)) > 0.01;
```

### Cross-Tenant Fixed Asset References

```sql
SELECT fa.TenantId, fa.Id, fa.FixedAssetCategoryId
FROM FixedAssets fa
LEFT JOIN FixedAssetCategories c ON c.Id = fa.FixedAssetCategoryId AND c.TenantId = fa.TenantId
WHERE c.Id IS NULL;
```

## Tests

Focused test class: `FixedAssetReportingReconciliationFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetReporting`.

Covered scenarios:

- fixed asset register is tenant-scoped and audited
- cross-tenant asset/category/account filters are rejected
- additions report includes AP and direct capitalization
- additions report excludes non-capitalized AP expense lines
- depreciation report ties posted schedules to GL
- accumulated depreciation report ties to GL
- revaluation/impairment movement report ties records to GL
- transfer report shows custody/location/segment history without GL mutation
- disposal report shows proceeds, NBV, gain/loss, and GL links
- disposed asset has zero NBV in report after disposal
- roll-forward ties opening plus movements to closing
- category/book roll-forward ties to asset-level totals
- GL reconciliation shows zero variance for clean data
- GL reconciliation detects missing posting-event references
- GL reconciliation detects GL movement without fixed asset source reference
- GL reconciliation detects subledger record without posted GL
- disposal gain/loss presentation is handled or flagged
- reports do not rely solely on mutable book-value fields
- report generation audit events are emitted where supported

## Migration Notes

No schema migration is required for Batch 21D. Existing Batch 19 through Batch 21C journal, posting-event, and fixed asset source-reference fields provide the reporting basis.

## Rollback Considerations

Rollback is code-only for this batch. Removing the report endpoints/service methods does not mutate posted GL or fixed asset source records. If rollback happens after users rely on generated report results, retain report output used for accountant review because the underlying posted GL remains authoritative.

## Accounting Impact

- No new posting paths are introduced.
- No fixed asset values are mutated by reports.
- Reports surface posted GL versus subledger variance instead of hiding it.
- Disposal gains are separated as non-operating disposal gains in fixed asset reports, even when the current COA enum forces a workaround account class.

## Known Limitations

All open limitations remain centralized in `docs/finance-go-live-limitations-register.md`.

- `FIN-LIM-0027`: resolved for backend fixed asset reporting and GL reconciliation foundation.
- `FIN-LIM-0038`: GL-impacting fixed asset transfer reclassification remains unsupported.
- `FIN-LIM-0039` through `FIN-LIM-0043`: disposal-related limitations remain open.
- `FIN-LIM-0044`: broader financial statement classification for disposal gains remains open.

## PR Definition Of Done

- Backend report service contracts implemented.
- Report endpoints added without frontend/export expansion.
- Report output uses posted GL and exposes subledger variances.
- Tenant isolation enforced below the controller layer.
- Focused fixed asset reporting tests pass.
- Finance go-live regression slice passes.
- No database migration required.
- Documentation and limitations register updated.
