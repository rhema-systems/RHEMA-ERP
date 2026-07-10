# Backend Reporting Export, Print Audit, and Financial Statement Presentation PR Summary

## Summary

Implemented backend CSV export/print foundations for core Finance reports and hardened disposal gain presentation in core financial statement output.

The batch keeps posted GL as the source of truth, enforces settlement-read-model AP/AR aging exports, emits Finance audit events for export/print success and failure, and prevents disposal gains from appearing as ordinary operating expense in income statement exports.

## Files Changed

- `src/ErpSystem.Core/DTOs/Finance/FinanceReportExportDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/IncomeStatementDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFinanceReportExportService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IApReportsService.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IArReportsService.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceReportExportsController.cs`
- `src/ErpSystem.Api/Services/Finance/Reporting/FinanceReportExportService.cs`
- `src/ErpSystem.Api/Services/Finance/GL/GeneralLedgerService.cs`
- `src/ErpSystem.Api/Services/Finance/AP/ApReportsService.cs`
- `src/ErpSystem.Api/Services/Finance/AR/ArReportsService.cs`
- `src/ErpSystem.Api/Services/Finance/FixedAssets/FixedAssetReportsService.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/BackendReportingExportFoundationTests.cs`
- `tests/ErpSystem.Api.Tests/Controllers/Finance/FinanceControllerSecurityTests.cs`
- `docs/finance-reporting-foundation.md`
- `docs/backend-reporting-export-presentation-foundation.md`
- `docs/finance-go-live-limitations-register.md`

## Export Format Support

Supported in this backend foundation:

- CSV

Supported reports:

- Trial balance
- Balance sheet
- Income statement
- Detailed ledger
- Cash/bank ledger
- AP aging
- AR aging
- AP control reconciliation
- AR control reconciliation
- Fixed asset register
- Fixed asset roll-forward
- Fixed asset GL reconciliation

Unsupported formats fail clearly. Rich formatted PDF/Excel report packs are tracked as `FIN-LIM-0046` if required by product scope.

## Source-Of-Truth Verification

- Trial balance, balance sheet, income statement, detailed ledger, and cash/bank ledger exports call `GeneralLedgerService` report methods.
- Cash/bank ledger exports show stored bank snapshots only as variance metadata.
- AP/AR aging exports require `UsesSettlementReadModel = true`.
- AP/AR control reconciliation exports use settlement read-model control reconciliation DTOs.
- Fixed asset exports use fixed asset report DTOs that reconcile subledger snapshots to posted GL.

## Financial Statement Presentation

`GeneralLedgerService.GenerateIncomeStatementAsync` now maps tenant fixed asset disposal gain accounts into Other Income presentation when the COA still uses an expense-class or revenue-class workaround.

The income statement DTO and export include `PresentationWarnings` so accountant review can see the workaround instead of silently treating disposal gains as ordinary expense or revenue.

## Audit Events

Added or verified:

- `Finance.Report.Exported`
- `Finance.Report.Printed`
- `Finance.Report.ExportFailed`
- `Finance.Report.PrintFailed`
- `Finance.Report.TrialBalanceExported`
- `Finance.Report.BalanceSheetExported`
- `Finance.Report.IncomeStatementExported`
- `Finance.Report.DetailedLedgerExported`
- `Finance.Report.CashBankLedgerExported`
- `Finance.Report.APAgingExported`
- `Finance.Report.ARAgingExported`
- `Finance.Report.APControlReconciliationExported`
- `Finance.Report.ARControlReconciliationExported`
- `Finance.Report.FixedAssetReportExported`
- `Finance.Report.FixedAssetGLReconciliationExported`

## Tests Added

- `BackendReportingExportFoundationTests.TrialBalanceExport_UsesPostedGlAndMatchesViewTotals`
- `BackendReportingExportFoundationTests.IncomeStatementExport_FlagsDisposalGainPresentationWarning`
- `BackendReportingExportFoundationTests.DetailedLedgerExport_UsesPostedGlAndPreservesFilters`
- `BackendReportingExportFoundationTests.CashBankLedgerExport_UsesPostedGlAndExposesSnapshotVariance`
- `BackendReportingExportFoundationTests.ApAgingExport_RequiresSettlementReadModel`
- `BackendReportingExportFoundationTests.ArAgingExport_RejectsLegacyOperationalFieldFallback`
- `BackendReportingExportFoundationTests.ArAgingExport_UsesSettlementReadModel`
- `BackendReportingExportFoundationTests.FixedAssetGlReconciliationExport_ExposesVarianceAndPresentationWarning`
- `BackendReportingExportFoundationTests.UnsupportedExportFormat_FailsClearlyAndCreatesAuditEvent`
- `FinanceControllerSecurityTests.CriticalFinanceActions_ShouldMapToExpectedPermissions` updated for `FinanceReportExportsController.Export` and `Print`.

## Build And Test Results

- `dotnet build ErpSystem.sln --no-restore /nr:false /m:1 /p:UseSharedCompilation=false -v:minimal`: passed.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~BackendReportingExportFoundationTests" /nr:false /m:1 /p:UseSharedCompilation=false -v:minimal`: passed `9/9`.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build --filter "FullyQualifiedName~FinanceControllerSecurityTests.CriticalFinanceActions_ShouldMapToExpectedPermissions" -v:minimal`: passed `21/21`.
- `dotnet test tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj --no-build --filter "Batch~FinanceGoLive" -v:minimal`: passed `358/358`.

## Migrations

No migration required. This batch adds backend services, DTOs, audit constants, report presentation logic, and tests only.

## Limitations Register

- `FIN-LIM-0002`: Resolved for backend CSV export/print endpoints and audit.
- `FIN-LIM-0044`: Resolved for core income statement/export disposal gain presentation.
- `FIN-LIM-0046`: Added as accepted non-blocking for richer formatted PDF/Excel report packs if product requires them.
- `FIN-LIM-0003`: Resolved by `docs/ghana-statutory-tax-reporting-export-foundation.md` for backend Ghana statutory tax reports and CSV exports.
- `FIN-LIM-0004`: Resolved by `docs/ghana-statutory-tax-reporting-export-foundation.md` for backend WHT/VAT withholding certificate/reference reporting and CSV exports.
- `FIN-LIM-0047`: Remains open for portal-specific filing pack formats and full withholding certificate issuance/receipt workflow if production requires them.
- `FIN-LIM-0045`: Remains open for unapplied payments/receipts and advances.

## Accounting Impact

No posted accounting records are created or mutated. Exports read from existing report services and audit export/print activity. Disposal gain presentation is reporting-only and does not change posted journals.

## Rollback

Rollback is code-only. Remove the export service/controller registration and revert DTO/audit/presentation changes. No data migration rollback is required.

## Safe To Proceed

Safe to proceed to Ghana statutory tax reporting/export, workflow hardening, or migration/sign-off foundations from a backend export/source-of-truth perspective.
