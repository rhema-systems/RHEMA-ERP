# Ghana Statutory Tax Reporting, Filing Export, and Withholding Certificate Foundation

## Scope

This batch adds backend statutory tax report services, controller endpoints, CSV exports, and audit events for Ghana tax reporting.

Included:

- VAT/NHIL/GETFund output tax report
- VAT/NHIL/GETFund recoverable input tax report
- net VAT/NHIL/GETFund summary
- exempt, zero-rated, and out-of-scope supplies report
- VAT withholding report
- WHT payable report
- WHT receivable/credit report
- tax account reconciliation
- tax configuration/rate history report
- current-active COVID-19 levy diagnostic

Not included:

- frontend UI
- portal-specific Ghana filing schemas/templates
- rich PDF/Excel statutory packs
- full withholding certificate issuance/receipt workflow
- AP/AR reversal redesign
- tax recalculation or rewriting of posted source documents

## Statutory Assumptions

- COVID-19 Health Recovery Levy must not be active for current postings or current-period reports.
- Ghana standard VAT configuration remains tenant data: VAT 15%, NHIL 2.5%, and GETFund 2.5% on the same taxable base for standard-rated supplies.
- VAT withholding for appointed agents remains tenant-configured and reported from posted withholding records; current guidance indicates 7% of taxable output value for standard-rated supplies.
- WHT rates are tenant-configured and effective-dated. Report logic does not hardcode WHT rates.
- Historical reports use posted `TaxCalculation` snapshots and source posting data. They do not recalculate taxes from the current `Tax.Rate`.

## Implemented Files

- `src/ErpSystem.Core/DTOs/Finance/TaxReportDtos.cs`
- `src/ErpSystem.Core/Interfaces/Finance/ITaxReportingService.cs`
- `src/ErpSystem.Api/Services/Finance/Taxation/TaxReportingService.cs`
- `src/ErpSystem.Api/Controllers/Finance/TaxReportsController.cs`
- `src/ErpSystem.Core/DTOs/Finance/FinanceReportExportDtos.cs`
- `src/ErpSystem.Api/Services/Finance/Reporting/FinanceReportExportService.cs`
- `src/ErpSystem.Api/Authorization/FinancePermissionPolicyMap.cs`
- `src/ErpSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `src/ErpSystem.Shared/FinanceAuditEvents.cs`
- `tests/ErpSystem.Api.Tests/Services/Finance/GhanaTaxReportingExportFoundationTests.cs`

No migration was required because the Ghana tax hardening pass already added explicit AP/AR line tax treatment, tax group links, WHT/VAT withholding amounts, withholding account references, and certificate/reference fields.

## Source Of Truth

Tax reports use posted accounting facts:

- `TaxCalculations` for AP/AR VAT/NHIL/GETFund snapshots
- posted `VendorInvoice` and `Invoices` source documents
- posted `VendorPayment` WHT records
- posted `CustomerPayment` WHT and VAT withholding records
- same-tenant `Taxes`, `TaxGroups`, and configured tax account mappings
- posted `JournalEntries`
- posted `AccountTransactions`
- posted `FinancePostingEvents`

Reports do not derive statutory tax solely from mutable invoice totals, paid fields, credited fields, or current tax rates.

## API Endpoints

`TaxReportsController` exposes tenant-scoped backend report endpoints:

- `POST /api/finance/tax-reports/output-tax`
- `POST /api/finance/tax-reports/input-tax`
- `POST /api/finance/tax-reports/net-summary`
- `POST /api/finance/tax-reports/non-taxable-supplies`
- `POST /api/finance/tax-reports/vat-withholding`
- `POST /api/finance/tax-reports/wht-payable`
- `POST /api/finance/tax-reports/wht-receivable`
- `POST /api/finance/tax-reports/tax-account-reconciliation`
- `POST /api/finance/tax-reports/configuration-history`
- `POST /api/finance/tax-reports/covid-levy-diagnostic`

The controller maps to `FinancePermissions.RunFinanceReports`.

## Export Support

`FinanceReportExportService` now supports CSV export for:

- `TaxOutput`
- `TaxInput`
- `TaxNetSummary`
- `TaxExemptZeroOutOfScope`
- `VatWithholding`
- `WhtPayable`
- `WhtReceivable`
- `TaxAccountReconciliation`
- `TaxConfigurationHistory`
- `TaxCovidDiagnostic`

Exports use the same `ITaxReportingService` methods as report views. Unsupported formats fail clearly.

## Report Behavior

Output tax:

- reads posted AR `TaxCalculation` snapshots for `CustomerInvoice`
- reports VAT/NHIL/GETFund taxable base, taxable amount, applied snapshot rate, tax amount, tax account, journal, and posting event
- reconciles to posted output tax GL movement tagged through AR tax posting lines

Input tax:

- reads posted AP `TaxCalculation` snapshots for `VendorInvoice`
- includes recoverable input tax only
- excludes non-recoverable tax from recoverable input tax totals
- reconciles to posted input tax GL movement tagged through AP tax posting lines

Net summary:

- combines output and recoverable input snapshots
- preserves source-document detail and posted snapshot rates

Non-taxable supplies:

- reports posted AP/AR source lines whose explicit `TaxTreatment` is `Exempt`, `ZeroRated`, or `OutOfScope`
- flags any such line that carries a nonzero tax amount

VAT withholding:

- reports posted AR receipt VAT withholding from `CustomerPayment.VatWithholdingAmount`
- shows counterparty, payment number/date, configured VAT withholding tax/account, certificate/reference number/date, journal, and posting event
- keeps VAT withholding separate from gross output VAT liability

WHT:

- AP WHT payable reports posted `VendorPayment.WithholdingTaxAmount`
- AR WHT receivable/credit reports posted `CustomerPayment.WithholdingTaxAmount`
- certificate/reference fields are exposed where present

Tax reconciliation:

- compares tax snapshots and withholding records to posted GL tax account movement
- surfaces variance rows and missing posting-reference diagnostics

Configuration/rate history:

- reports tenant tax code, name, category, applicability, active status, rate, effective date, account mappings, active COVID flag, and overlapping rate-history flag

## Audit Events

Implemented or verified:

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

The previously implemented tax configuration and posting audit events remain the source for tax rule creation/update/deactivation, account mapping changes, tax calculation, tax posting, posting failure, and configuration-used-in-posting.

Certificate/reference creation/update/link/mismatch constants are defined for the future managed certificate workflow; this batch reports existing backend reference fields but does not introduce a separate certificate workflow.

## Diagnostics

Table names below match current EF mappings.

```sql
-- Current active COVID-19 levy configuration.
select TenantId, Id, Code, Name, Rate, EffectiveFrom
from Taxes
where IsDeleted = 0
  and IsActive = 1
  and EffectiveFrom <= cast(getutcdate() as date)
  and (Code like '%COVID%' or Name like '%COVID%');

-- Tax snapshots missing posted GL lines.
select c.TenantId, c.DocumentType, c.DocumentId, c.TaxId, c.TaxAmount
from TaxCalculations c
left join FinancePostingEvents e
  on e.TenantId = c.TenantId
 and e.SourceDocumentType = c.DocumentType
 and e.SourceDocumentId = c.DocumentId
 and e.PostingStatus = 'Posted'
 and e.IsDeleted = 0
left join JournalEntries j
  on j.TenantId = c.TenantId
 and j.Id = e.JournalEntryId
 and j.PostingStatus = 'Posted'
 and j.IsDeleted = 0
where c.IsDeleted = 0
  and j.Id is null;

-- Posted GL tax lines missing tax snapshots.
select t.TenantId, t.JournalEntryId, t.SourceDocumentType, t.SourceDocumentId, t.TransactionTag, t.DebitAmount, t.CreditAmount
from AccountTransactions t
left join TaxCalculations c
  on c.TenantId = t.TenantId
 and c.DocumentType = t.SourceDocumentType
 and c.DocumentId = t.SourceDocumentId
 and c.IsDeleted = 0
where t.IsDeleted = 0
  and t.PostingStatus = 'Posted'
  and (t.TransactionTag like 'AP-Tax-%' or t.TransactionTag like 'AR-Tax-%')
  and c.Id is null;

-- AP/AR documents with tax amounts but missing tax snapshots.
select TenantId, 'VendorInvoice' as DocumentType, Id as DocumentId, InvoiceNumber as DocumentNumber, TaxAmount
from VendorInvoice
where IsDeleted = 0 and TaxAmount <> 0
  and not exists (
    select 1 from TaxCalculations c
    where c.TenantId = VendorInvoice.TenantId
      and c.DocumentType = 'VendorInvoice'
      and c.DocumentId = VendorInvoice.Id
      and c.IsDeleted = 0)
union all
select TenantId, 'CustomerInvoice', Id, InvoiceNumber, TaxAmount
from Invoices
where IsDeleted = 0 and TaxAmount <> 0
  and not exists (
    select 1 from TaxCalculations c
    where c.TenantId = Invoices.TenantId
      and c.DocumentType = 'CustomerInvoice'
      and c.DocumentId = Invoices.Id
      and c.IsDeleted = 0);

-- Tax accounts from another tenant.
select t.TenantId, t.Id as TaxId, t.Code, a.TenantId as AccountTenantId, 'Payable' as MappingType
from Taxes t
join Accounts a on a.Id = t.TaxPayableAccountId
where t.IsDeleted = 0 and a.TenantId <> t.TenantId
union all
select t.TenantId, t.Id, t.Code, a.TenantId, 'Receivable'
from Taxes t
join Accounts a on a.Id = t.TaxReceivableAccountId
where t.IsDeleted = 0 and a.TenantId <> t.TenantId;

-- Tax postings in closed or locked periods.
select t.TenantId, t.JournalEntryId, t.SourceDocumentType, t.SourceDocumentId, p.PeriodCode, p.PeriodStatus, p.IsLocked
from AccountTransactions t
join FiscalPeriods p on p.Id = t.FiscalPeriodId and p.TenantId = t.TenantId
where t.IsDeleted = 0
  and t.PostingStatus = 'Posted'
  and (t.TransactionTag like 'AP-Tax-%' or t.TransactionTag like 'AR-Tax-%' or t.TransactionTag in ('AP-WHT', 'AR-WHT', 'AR-VAT-WHT'))
  and (p.PeriodStatus <> 'Open' or p.IsLocked = 1);

-- VAT/NHIL/GETFund snapshot mismatch against source document tax total.
select c.TenantId, c.DocumentType, c.DocumentId, sum(c.TaxAmount) as SnapshotTaxTotal, vi.TaxAmount as DocumentTaxTotal
from TaxCalculations c
join VendorInvoice vi on vi.Id = c.DocumentId and vi.TenantId = c.TenantId
where c.IsDeleted = 0 and c.DocumentType = 'VendorInvoice'
group by c.TenantId, c.DocumentType, c.DocumentId, vi.TaxAmount
having sum(c.TaxAmount) <> vi.TaxAmount
union all
select c.TenantId, c.DocumentType, c.DocumentId, sum(c.TaxAmount), i.TaxAmount
from TaxCalculations c
join Invoices i on i.Id = c.DocumentId and i.TenantId = c.TenantId
where c.IsDeleted = 0 and c.DocumentType = 'CustomerInvoice'
group by c.TenantId, c.DocumentType, c.DocumentId, i.TaxAmount
having sum(c.TaxAmount) <> i.TaxAmount;

-- VAT withholding/WHT postings missing certificate/reference where required.
select TenantId, 'VendorPayment' as SourceDocumentType, Id as SourceDocumentId, PaymentNumber, WithholdingTaxAmount, WithholdingCertificateNumber
from VendorPayment
where IsDeleted = 0 and WithholdingTaxAmount > 0 and (WithholdingCertificateNumber is null or WithholdingCertificateNumber = '')
union all
select TenantId, 'CustomerPayment', Id, PaymentNumber, WithholdingTaxAmount + VatWithholdingAmount, WithholdingCertificateNumber
from CustomerPayment
where IsDeleted = 0
  and (WithholdingTaxAmount > 0 or VatWithholdingAmount > 0)
  and (WithholdingCertificateNumber is null or WithholdingCertificateNumber = '');

-- Certificate amount not matching posted withholding amount.
select p.TenantId, 'VendorPayment' as SourceDocumentType, p.Id, p.PaymentNumber, p.WithholdingTaxAmount,
       sum(case when t.TransactionTag = 'AP-WHT' then t.CreditAmount - t.DebitAmount else 0 end) as PostedWithholding
from VendorPayment p
join AccountTransactions t on t.TenantId = p.TenantId and t.SourceDocumentType = 'VendorPayment' and t.SourceDocumentId = p.Id
where p.IsDeleted = 0 and p.WithholdingTaxAmount > 0 and t.PostingStatus = 'Posted'
group by p.TenantId, p.Id, p.PaymentNumber, p.WithholdingTaxAmount
having p.WithholdingTaxAmount <> sum(case when t.TransactionTag = 'AP-WHT' then t.CreditAmount - t.DebitAmount else 0 end)
union all
select p.TenantId, 'CustomerPayment', p.Id, p.PaymentNumber, p.WithholdingTaxAmount + p.VatWithholdingAmount,
       sum(case when t.TransactionTag in ('AR-WHT', 'AR-VAT-WHT') then t.DebitAmount - t.CreditAmount else 0 end)
from CustomerPayment p
join AccountTransactions t on t.TenantId = p.TenantId and t.SourceDocumentType = 'CustomerPayment' and t.SourceDocumentId = p.Id
where p.IsDeleted = 0 and (p.WithholdingTaxAmount > 0 or p.VatWithholdingAmount > 0) and t.PostingStatus = 'Posted'
group by p.TenantId, p.Id, p.PaymentNumber, p.WithholdingTaxAmount, p.VatWithholdingAmount
having p.WithholdingTaxAmount + p.VatWithholdingAmount <> sum(case when t.TransactionTag in ('AR-WHT', 'AR-VAT-WHT') then t.DebitAmount - t.CreditAmount else 0 end);

-- Zero-rated/exempt/out-of-scope lines with nonzero tax postings.
select TenantId, 'VendorInvoiceLineItem' as SourceLineType, Id, VendorInvoiceId as SourceDocumentId, TaxTreatment, TaxAmount
from VendorInvoiceLineItem
where IsDeleted = 0 and TaxTreatment in (2, 3, 4) and TaxAmount <> 0
union all
select TenantId, 'InvoiceLineItem', Id, InvoiceId, TaxTreatment, TaxAmount
from InvoiceLineItem
where IsDeleted = 0 and TaxTreatment in (2, 3, 4) and TaxAmount <> 0;
```

## Tests

`GhanaTaxReportingExportFoundationTests` covers:

- `OutputInputAndNetReports_ShouldUsePostedTaxSnapshots_NotCurrentRates`
- `NonTaxableSuppliesReport_ShouldExposeExplicitLineTreatments`
- `WithholdingReports_ShouldUsePostedPaymentDataAndCertificateReferences`
- `TaxAccountReconciliation_ShouldTieSnapshotsAndWithholdingToPostedGl`
- `TaxReports_ShouldRejectCrossTenantTaxAccountFilter`
- `TaxOutputExport_ShouldUseTaxReportServiceAndCreateAuditEvent`
- `CovidLevyDiagnostic_ShouldDetectCurrentActiveCovidConfiguration`

Regression checks run:

- Ghana tax reporting/export focused tests: `7/7`
- Ghana statutory tax engine tests: `17/17`
- backend reporting export tests: `9/9`
- finance controller security tests: `29/29`
- Finance service regression slice: `346/346`
- solution build: passed

## Limitations Register

- `FIN-LIM-0003`: resolved for backend Ghana statutory tax report and CSV export foundation.
- `FIN-LIM-0004`: resolved for backend WHT/VAT withholding certificate/reference reporting and CSV export foundation.
- `FIN-LIM-0047`: added for portal-specific statutory filing pack formats and full withholding certificate issuance/receipt workflow if required before final sign-off.
- `FIN-LIM-0045`: remains open for unapplied payments/receipts and advances.
- `FIN-LIM-0046`: remains accepted non-blocking for richer formatted finance report packs.

## PR Definition Of Done

- Reports derive statutory values from posted snapshots, posted source documents, posted withholding records, posted GL, and same-tenant posting events.
- Exports use the same backend report service as report views.
- Tenant-owned filters are validated before report generation.
- Cross-tenant tax accounts, tax groups, taxes, suppliers, and customers are rejected.
- Current active COVID-19 levy configuration is diagnosed.
- WHT/VAT withholding reports expose certificate/reference fields where present.
- Tax reports and exports emit Finance audit events.
- No schema migration is required for this batch.
