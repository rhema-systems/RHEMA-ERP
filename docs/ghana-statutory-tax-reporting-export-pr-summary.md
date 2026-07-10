# Ghana Statutory Tax Reporting, Filing Export, and Withholding Certificate Foundation - PR Summary

## Scope

Implemented backend Ghana statutory tax reporting and CSV export foundation only.

No frontend UI, AP/AR reversal redesign, cash/bank reversal redesign, broader workflow hardening, data migration/sign-off, or portal-specific filing pack was implemented.

## Files Changed

- `src/ErpSystem.Core/DTOs/Finance/TaxReportDtos.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinanceReportExportDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ITaxReportingService.cs`
- `src/ErpSystem.Api/Services/Finance/Taxation/TaxReportingService.cs`
- `src/ErpSystem.Api/Services/Finance/Reporting/FinanceReportExportService.cs`
- `src/ErpSystem.Api/Controllers/Finance/TaxReportsController.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/GhanaTaxReportingExportFoundationTests.cs`
- `tests/ErpSystem.Api.Tests/ErpSystem.Api.Tests.csproj`
- `docs/ghana-statutory-tax-reporting-export-foundation.md`
- `docs/ghana-statutory-tax-reporting-export-pr-summary.md`
- `docs/finance-go-live-limitations-register.md`
- `docs/ghana-statutory-tax-engine-foundation.md`
- `docs/backend-reporting-export-presentation-foundation.md`
- `docs/finance-reporting-foundation.md`

## Implemented Reports

- VAT/NHIL/GETFund output tax report
- VAT/NHIL/GETFund recoverable input tax report
- net VAT/NHIL/GETFund summary
- exempt/zero-rated/out-of-scope supplies report
- VAT withholding report
- WHT payable report
- WHT receivable/credit report
- tax account reconciliation report
- tax configuration/rate history report
- current active COVID-19 levy diagnostic

## Source Of Truth Verification

Reports read:

- `TaxCalculations`
- posted `VendorInvoice` and `Invoices`
- posted `VendorPayment` WHT fields
- posted `CustomerPayment` WHT/VAT withholding fields
- same-tenant tax accounts and tax configuration
- posted `JournalEntries`
- posted `AccountTransactions`
- posted `FinancePostingEvents`

Reports do not recalculate historical tax using current rates and do not derive statutory outputs from mutable invoice totals alone.

## Export Behavior

`FinanceReportExportService` now supports CSV export for all new tax report types.

Unsupported export formats still fail clearly.

Export success and failure use the existing Finance audit infrastructure.

## Audit Events

Added and used:

- `Finance.Tax.VATReportGenerated`
- `Finance.Tax.VATReportExported`
- `Finance.Tax.InputTaxReportGenerated`
- `Finance.Tax.InputTaxReportExported`
- `Finance.Tax.OutputTaxReportGenerated`
- `Finance.Tax.OutputTaxReportExported`
- `Finance.Tax.VATWithholdingReportGenerated`
- `Finance.Tax.VATWithholdingReportExported`
- `Finance.Tax.WHTReportGenerated`
- `Finance.Tax.WHTReportExported`
- `Finance.Tax.AccountReconciliationGenerated`
- `Finance.Tax.AccountReconciliationExported`
- `Finance.Tax.ReportExportFailed`

Certificate/reference workflow constants were added for future workflow implementation, but this batch only reports existing backend reference fields.

## Tenant Isolation

The tax reporting service validates same-tenant:

- tax filter
- tax group filter
- tax account filter
- supplier filter
- customer filter
- source documents
- journals
- posting events
- GL lines

Cross-tenant tax account filters are tested and rejected.

## Accounting Impact

No posting behavior changed.

Tax reporting now reconciles statutory tax snapshots and withholding records to posted GL tax account movement. VAT withholding remains separate from gross output VAT liability and is reported as a clearing/receivable item where modeled.

## Migrations

No migration required. The previous Ghana tax hardening migration already added line-level tax treatment, AP tax group support, withholding references, and certificate/reference fields.

## Rollback Considerations

Rollback is code-only:

- remove `TaxReportingService`, `TaxReportsController`, DTO/interface additions, export switch additions, DI registration, audit constants, docs, and tests
- no data rollback is required
- no posted accounting data is mutated by this batch

## Tests

Added:

- `GhanaTaxReportingExportFoundationTests.OutputInputAndNetReports_ShouldUsePostedTaxSnapshots_NotCurrentRates`
- `GhanaTaxReportingExportFoundationTests.NonTaxableSuppliesReport_ShouldExposeExplicitLineTreatments`
- `GhanaTaxReportingExportFoundationTests.WithholdingReports_ShouldUsePostedPaymentDataAndCertificateReferences`
- `GhanaTaxReportingExportFoundationTests.TaxAccountReconciliation_ShouldTieSnapshotsAndWithholdingToPostedGl`
- `GhanaTaxReportingExportFoundationTests.TaxReports_ShouldRejectCrossTenantTaxAccountFilter`
- `GhanaTaxReportingExportFoundationTests.TaxOutputExport_ShouldUseTaxReportServiceAndCreateAuditEvent`
- `GhanaTaxReportingExportFoundationTests.CovidLevyDiagnostic_ShouldDetectCurrentActiveCovidConfiguration`

Results:

- focused Ghana tax reporting/export tests: `7/7`
- Ghana statutory tax engine tests: `17/17`
- backend reporting export tests: `9/9`
- finance controller security tests: `29/29`
- Finance service regression slice: `346/346`
- solution build: passed

## Limitations Register

- `FIN-LIM-0003`: resolved for backend statutory tax reports and CSV exports.
- `FIN-LIM-0004`: resolved for backend WHT/VAT withholding certificate/reference reporting and CSV exports.
- `FIN-LIM-0047`: added for portal-specific filing pack formats and full withholding certificate issuance/receipt workflow if required before final sign-off.
- `FIN-LIM-0045`: remains open for unapplied payments/receipts and advances.
- `FIN-LIM-0046`: remains accepted non-blocking for richer formatted report packs.

## Safe To Proceed

Safe to proceed to workflow hardening or migration/sign-off preparation from a backend tax reporting foundation perspective.

Final go-live sign-off still needs an explicit decision on `FIN-LIM-0047` if official filing pack output or managed withholding certificate lifecycle is production scope.
