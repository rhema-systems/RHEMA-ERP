# Backend Reporting Export, Print Audit, and Financial Statement Presentation Foundation

## Scope

This batch adds backend CSV export/print foundations for core Finance reports and hardens disposal gain presentation in financial statement output.

No frontend UI, Ghana statutory filing pack, WHT certificate lifecycle, data migration/sign-off, or reversal redesign is included.

## Export Architecture

`FinanceReportExportService` is the central backend export service for this batch.

Implemented endpoints:

- `POST /api/finance/report-exports/export`
- `POST /api/finance/report-exports/print`

Implemented files:

- `src/ErpSystem.Core/DTOs/Finance/FinanceReportExportDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/IFinanceReportExportService.cs`
- `src/ErpSystem.Api/Services/Finance/Reporting/FinanceReportExportService.cs`
- `src/ErpSystem.Api/Controllers/Finance/FinanceReportExportsController.cs`

Supported backend format:

- CSV

Unsupported formats fail clearly with `NotSupportedException`. PDF/Excel packs remain outside this backend foundation unless an existing legacy endpoint already supports them safely.

## Report Source Of Truth

Exports use the same backend services as report view endpoints.

| Report | Export source |
|---|---|
| Trial balance | Posted GL through `GeneralLedgerService.GenerateTrialBalanceAsync` |
| Balance sheet | Posted GL through `GenerateBalanceSheetAsync` |
| Income statement | Posted GL through `GenerateIncomeStatementAsync` |
| Detailed ledger | Posted GL through `GenerateDetailedLedgerAsync` |
| Cash/bank ledger | Posted GL through `GenerateCashBankLedgerAsync`; stored bank snapshots are variance-only |
| AP aging | `SubledgerSettlementBalance` / `SubledgerSettlementApplication` through AP reporting service |
| AR aging | `SubledgerSettlementBalance` / `SubledgerSettlementApplication` through AR reporting service |
| AP control reconciliation | Settlement read model reconciled to posted AP control GL |
| AR control reconciliation | Settlement read model reconciled to posted AR control GL |
| Fixed asset register | Fixed asset subledger snapshots tied to posted GL references |
| Fixed asset roll-forward | Fixed asset movement records tied to posted GL movement |
| Fixed asset GL reconciliation | Posted GL reconciled to fixed asset subledger snapshots |

Export code must not recalculate accounting totals independently from view report services.

## AP/AR Aging Export Guard

AP and AR aging exports require `UsesSettlementReadModel = true`.

If a report service returns a legacy operational-field aging result, the export path fails instead of silently exporting mutable `PaidAmount` / `CreditedAmount` based aging.

Legacy AP/AR service export methods now also generate CSV from detailed settlement-read-model reports and fail if the read model is unavailable.

## Financial Statement Presentation

`GeneralLedgerService.GenerateIncomeStatementAsync` now applies a disposal gain presentation mapping for tenant fixed asset categories.

When a configured disposal gain account is still classified as an expense-class or revenue-class workaround:

- credit movement on that account is excluded from operating expenses;
- the disposal gain is presented in Other Income for financial statement output;
- `IncomeStatementDto.PresentationWarnings` flags the account presentation workaround for accountant review;
- CSV income statement exports include the warning metadata.

This resolves the main risk that disposal gains could appear as ordinary operating expense in core financial statement exports.

## Audit Events

Success events:

- `Finance.Report.Exported`
- `Finance.Report.Printed`
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

Failure events:

- `Finance.Report.ExportFailed`
- `Finance.Report.PrintFailed`

Audit payloads include report type, format, row count, source-of-truth mode, settlement-read-model flag, totals, warnings, date filters, account filter count, segment filter count, and generated file name where available.

## Permissions And Tenant Isolation

`FinanceReportExportsController` is authenticated and mapped to `FinancePermissions.ExportFinanceReports` through `FinancePermissionPolicyMap`.

Tenant, account, segment, supplier, customer, asset, category, and bank/cash filters are enforced by the underlying report services. Export paths call those services directly, so exports inherit the same tenant validation and permission behavior as the view/report layer.

## Legacy Fixed Asset Export Guard

The older `FixedAssetReportsService.ExportToExcelAsync` and `ExportToPdfAsync` methods remain limited to `AssetRegister`. Unsupported report types now fail clearly instead of returning an empty workbook or empty PDF bytes.

The central Finance export service should be used for fixed asset roll-forward and fixed asset GL reconciliation CSV exports.

## Diagnostics

Run before sign-off:

```sql
-- AP/AR aging exports should be backed by settlement read-model rows.
SELECT b.SourceModule, COUNT(*) AS BalanceRows
FROM SubledgerSettlementBalances b
WHERE b.TenantId = @TenantId
  AND b.IsDeleted = 0
GROUP BY b.SourceModule;

-- Export audit coverage.
SELECT al.TenantId, al.Action, COUNT(*) AS EventCount
FROM AuditLogs al
WHERE al.TenantId = @TenantId
  AND al.Action IN (
      'Finance.Report.Exported',
      'Finance.Report.Printed',
      'Finance.Report.ExportFailed',
      'Finance.Report.PrintFailed')
GROUP BY al.TenantId, al.Action;

-- Disposal gain accounts still using presentation workaround.
SELECT fac.Id AS CategoryId,
       fac.Name AS CategoryName,
       a.Id AS AccountId,
       a.AccountNumber,
       a.AccountName,
       a.AccountType
FROM FixedAssetCategories fac
JOIN Accounts a
  ON a.Id = fac.GainOnDisposalAccountId
 AND a.TenantId = fac.TenantId
WHERE fac.TenantId = @TenantId
  AND fac.IsDeleted = 0
  AND a.IsDeleted = 0
  AND a.AccountType IN (3, 4); -- Revenue or Expense enum values in the current model

-- Fixed asset GL reconciliation warnings should remain visible in exports.
SELECT fac.Id AS CategoryId, fac.Name AS CategoryName, fac.GainOnDisposalAccountId
FROM FixedAssetCategories fac
WHERE fac.TenantId = @TenantId
  AND fac.GainOnDisposalAccountId IS NOT NULL
  AND fac.IsDeleted = 0;
```

## Tests

Focused tests:

- `BackendReportingExportFoundationTests.TrialBalanceExport_UsesPostedGlAndMatchesViewTotals`
- `BackendReportingExportFoundationTests.IncomeStatementExport_FlagsDisposalGainPresentationWarning`
- `BackendReportingExportFoundationTests.DetailedLedgerExport_UsesPostedGlAndPreservesFilters`
- `BackendReportingExportFoundationTests.CashBankLedgerExport_UsesPostedGlAndExposesSnapshotVariance`
- `BackendReportingExportFoundationTests.ApAgingExport_RequiresSettlementReadModel`
- `BackendReportingExportFoundationTests.ArAgingExport_RejectsLegacyOperationalFieldFallback`
- `BackendReportingExportFoundationTests.ArAgingExport_UsesSettlementReadModel`
- `BackendReportingExportFoundationTests.FixedAssetGlReconciliationExport_ExposesVarianceAndPresentationWarning`
- `BackendReportingExportFoundationTests.UnsupportedExportFormat_FailsClearlyAndCreatesAuditEvent`
- `FinanceControllerSecurityTests.CriticalFinanceActions_ShouldMapToExpectedPermissions`

## Limitations

- Ghana statutory backend tax reporting and CSV exports are resolved by `docs/ghana-statutory-tax-reporting-export-foundation.md`.
- WHT/VAT withholding certificate/reference reporting is resolved by `docs/ghana-statutory-tax-reporting-export-foundation.md`; portal-specific filing packs and full certificate issuance/receipt workflow remain tracked under `FIN-LIM-0047`.
- Unapplied AP payments, unapplied AR receipts, and functional-currency advances are resolved under `FIN-LIM-0045` by the dedicated settlement read-model balance class. Foreign-currency advances remain rejected until advance-specific FX settlement is implemented.
- Rich PDF/Excel report pack formatting is not implemented in this backend CSV foundation. It should be handled in a later report-pack/export batch if product requires formatted packs beyond CSV.

## Rollback Considerations

No schema migration is required. Rollback is code-only:

- remove `FinanceReportExportsController`;
- remove `FinanceReportExportService` registration;
- revert export DTO/interface additions;
- revert disposal gain income statement presentation mapping if required.

Posted GL, fixed asset records, AP/AR settlement read models, and audit records are not mutated by export generation.

## PR Definition Of Done

- Backend export endpoints use view/report services.
- AP/AR aging exports require settlement read-model mode.
- CSV exports carry source-of-truth metadata and totals.
- Export/print success and failure audit events are emitted.
- Disposal gain presentation is mapped out of ordinary operating expense in income statement output.
- Tests cover source-of-truth, read-model guard, audit, fixed asset variance export, and unsupported format behavior.
- Limitations register is updated.
