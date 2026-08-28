# Fixed Asset Reporting and GL Reconciliation Foundation - PR Summary

Date: 2026-07-08

## Scope

Implemented Fixed Assets Batch 21D only: fixed asset register reporting, additions/capitalization reporting, depreciation and accumulated depreciation reporting, valuation movement reporting, transfer history reporting, disposal reporting, roll-forward reporting, fixed-asset-to-GL reconciliation, report-generation audit events, reporting diagnostics, limitations-register updates, and focused tests.

Frontend UI, print/export packs, data migration/sign-off, fixed asset reversal/correction batches, procurement-origin capitalization, partial disposal, foreign-currency disposal proceeds, disposal AR/cash/tax integration, additional depreciation methods, and workflow-routing hardening were not started.

## Definition Of Done

- [x] Backend build impact verified.
- [x] Frontend build/type-check impact: no frontend files were intentionally changed.
- [x] Tests added and focused Batch 21D suite passes.
- [x] No database migration required.
- [x] Rollback considerations documented.
- [x] Tenant-isolation verification documented.
- [x] Report source-of-truth documented.
- [x] GL reconciliation behavior documented.
- [x] Disposal gain/loss presentation handling documented.
- [x] Audit-event verification documented.
- [x] Limitations register updated.

## Files Changed

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

## Migrations

No migration was added. Batch 21D uses existing fixed asset journal/posting-event/source-reference fields added in Batch 19 through Batch 21C.

## Reports Added Or Hardened

- Fixed asset register.
- Fixed asset additions/capitalization report.
- Depreciation report.
- Accumulated depreciation report.
- Revaluation and impairment movement report.
- Transfer/custody/location/segment movement report.
- Disposal and derecognition report.
- Fixed asset roll-forward by category/book.
- Fixed-asset-to-GL reconciliation.

## Tests

Focused suite: `FixedAssetReportingReconciliationFoundationTests`, trait `Batch=FinanceGoLive-FixedAssetReporting`.

| Scenario | Test method |
|---|---|
| Fixed asset register is tenant-scoped and audited | `FixedAssetRegisterIsTenantScopedAndAudited` |
| Cross-tenant asset/category/account filters are rejected | `CrossTenantAssetCategoryAndAccountFiltersAreRejected` |
| Additions report includes AP/direct capitalization and excludes AP expense lines | `AdditionsReportIncludesApAndDirectCapitalizationAndExcludesExpenseLines` |
| Depreciation and accumulated depreciation reports tie schedules to GL | `DepreciationAndAccumulatedDepreciationReportsTiePostedSchedulesToGl` |
| Revaluation and impairment movement report ties records to GL | `RevaluationAndImpairmentMovementReportTiesRecordsToGl` |
| Transfer report shows custody/segment history without GL mutation | `TransferReportShowsCustodySegmentHistoryWithoutGlMutation` |
| Disposal report shows proceeds, NBV, gain/loss, GL links, and presentation warning | `DisposalReportShowsProceedsNbvGainAndPresentationWarning` |
| Disposed asset has zero NBV in register/report | `RegisterShowsDisposedAssetWithZeroCurrentNbv` |
| Roll-forward ties opening plus movements to closing and category totals | `RollForwardTiesOpeningPlusMovementsToClosingAndCategoryTotals` |
| GL reconciliation shows zero variance for clean data | `GlReconciliationShowsZeroVarianceForCleanData` |
| Reconciliation detects missing posting references and subledger without GL | `ReconciliationDetectsMissingPostingReferencesAndSubledgerWithoutGl` |
| Reconciliation detects GL movement without fixed asset source reference | `ReconciliationDetectsGlMovementWithoutFixedAssetSourceReference` |
| Reports do not rely solely on mutable book-value fields | `ReportsDoNotRelySolelyOnMutableBookValueFields` |

Results:

- Backend API build: passed.
- Focused fixed asset reporting suite: `13/13` passed.
- Finance go-live regression slice: `329/329` passed for `Batch~FinanceGoLive`.

## Tenant Isolation Verification

- Report service resolves the current Finance tenant below the controller layer.
- Asset, category, and account filters are tenant-validated before report queries execute.
- GL movement joins require same-tenant `AccountTransaction` and `JournalEntry` rows.
- Fixed asset source records are joined by same tenant.
- Cross-tenant filters are covered by focused tests.

## Report Source-Of-Truth Verification

Reports derive accounting balances from posted GL movement and reconcile that movement to fixed asset source snapshots. Mutable book values are not treated as sufficient proof: register and reconciliation DTOs expose posted GL values, subledger values, and variances.

## GL Reconciliation Behavior

The reconciliation report compares posted GL against fixed asset subledger snapshots for asset cost/carrying accounts, accumulated depreciation, depreciation expense, accumulated impairment, impairment loss, revaluation surplus/loss, disposal proceeds clearing, and disposal gain/loss. The later `FIN-LIM-0041` policy slice also deducts completed disposal transfers from the asset-specific revaluation-surplus subledger before comparing it with GL.

Diagnostic counters flag missing posting-event references, GL movement without fixed asset source references, and subledger records without posted GL.

## Disposal Gain/Loss Presentation Handling

Fixed asset reports classify disposal gain separately as a non-operating disposal gain. When the configured gain account is expense-class or revenue-class because the current COA enum lacks richer classification, the disposal report returns a presentation warning rather than allowing silent ordinary operating expense or revenue presentation.

Broader financial statement classification remains tracked as `FIN-LIM-0044`.

## Audit Event Verification

Implemented/emitted where applicable:

- `Finance.FixedAsset.Report.RegisterGenerated`
- `Finance.FixedAsset.Report.RollForwardGenerated`
- `Finance.FixedAsset.Report.GLReconciliationGenerated`
- `Finance.FixedAsset.Report.VarianceDiagnosticGenerated`

Report-generation audit is tenant-scoped through the existing Finance audit infrastructure when `IFinanceAuditService` is available.

## Accounting Impact

- No new GL postings were added.
- No fixed asset register values are mutated by reports.
- Posted GL remains authoritative.
- Subledger/book values are exposed as controlled snapshots and reconciled to posted GL.
- Disposal gain/loss is separated from ordinary expense/revenue presentation in fixed asset reports.

## Limitations Register

- `FIN-LIM-0027`: resolved for backend fixed asset reporting and GL reconciliation foundation.
- `FIN-LIM-0044`: opened for broader financial statement disposal gain classification.
- `FIN-LIM-0038` through `FIN-LIM-0043`: remain open where not directly resolved.

No remaining fixed asset reporting limitation blocks broader backend reporting/export work. `FIN-LIM-0044` blocks final financial-statement presentation/sign-off until resolved or explicitly accepted.

## Rollback Considerations

Rollback is code-only. Report endpoints, DTOs, and service methods can be removed without changing posted GL or fixed asset source records. If report outputs have been used for accountant review, retain those outputs for sign-off traceability because the underlying posted GL remains unchanged.

## Safe To Proceed

Safe to proceed to broader reporting/export, workflow hardening, or migration/sign-off work. The fixed asset lifecycle now has backend reporting and GL reconciliation coverage from acquisition through disposal, with remaining presentation and unsupported lifecycle gaps tracked by limitation ID.
