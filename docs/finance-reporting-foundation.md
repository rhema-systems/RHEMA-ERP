# Core Financial Reporting Foundation

## Source of Truth

Financial statements and ledger reports must derive accounting balances from posted `AccountTransaction` rows linked to posted `JournalEntry` rows for the same `TenantId`. `Account.Balance`, `BankAccount.CurrentBalance`, and other stored operational balances are read-side snapshots only and are not authoritative for reporting.

Implemented in:

- `src/ErpSystem.Api/Services/Finance/GL/GeneralLedgerService.cs`
  - `GenerateTrialBalanceAsync`
  - `GenerateBalanceSheetAsync`
  - `GenerateIncomeStatementAsync`
  - `GenerateDetailedLedgerAsync`
  - `GenerateCashBankLedgerAsync`
  - `BuildPostedLedgerQuery`
  - `GetReportingAccountsAsync`
  - `ResolveSegmentFiltersAsync`
- `src/ErpSystem.Api/Controllers/Finance/CashReportsController.cs`
  - `GetPosition`
  - `GetLedger`
- `src/ErpSystem.Api/Services/Finance/AP/ApReportsService.cs`
  - `GetAgingReportAsync`
  - `GetDetailedAgingReportAsync`
  - `RebuildSettlementReadModelAsync`
  - `GetControlReconciliationAsync`
  - `GetApSummaryAsync`
  - `ApplyPostedApInvoiceFilter`
- `src/ErpSystem.Api/Services/Finance/AR/ArReportsService.cs`
  - `GetAgingReportAsync`
  - `GetDetailedAgingReportAsync`
  - `RebuildSettlementReadModelAsync`
  - `GetControlReconciliationAsync`
  - `GetCollectionsDashboardAsync`
  - `GetArSummaryAsync`
  - `ApplyPostedArInvoiceFilter`
- `src/ErpSystem.Api/Services/Finance/SubledgerSettlementReadModelService.cs`
  - `RebuildAsync`
  - `GetBalancesAsync`
  - `GetControlReconciliationAsync`

## Supported Reports And Filters

- Trial balance: tenant, as-of date, period start, account IDs, reporting segment filters, book classification, zero-balance inclusion.
- Balance sheet: tenant, as-of date, account IDs, reporting segment filters, book classification, account-detail inclusion.
- Income statement: tenant, date range, account IDs, reporting segment filters, book classification, account-detail inclusion.
- Detailed ledger: tenant, date range, account IDs, reporting segment filters, book classification, reversal inclusion, opening-balance inclusion.
- Cash/bank ledger: tenant, date range, bank account IDs, GL account IDs, reporting segment filters, book classification, opening-balance inclusion.
- AP aging/payables summary: tenant, as-of date, optional supplier, posted AP invoices only, outstanding derived from the settlement read model.
- AR aging/receivables summary: tenant, as-of date, optional customer, posted AR invoices only, outstanding derived from the settlement read model.

Segment filters are tenant-validated against `AccountSegmentStructure` records where `IsReportingDimension = true`. Lookup-backed segment values are validated against tenant-owned active `SegmentLookupValue` records.

## Reporting Rules

- Posted ledger rows are included only when `AccountTransaction.TenantId`, `JournalEntry.TenantId`, and the current finance tenant match.
- Draft/unposted journal entries are excluded.
- Balance sheet amounts are normalized by account type: assets use debit-minus-credit; liabilities and equity use credit-minus-debit.
- Income statement amounts are normalized by account type: revenue uses credit-minus-debit; expenses use debit-minus-credit.
- Trial balance preserves debit/credit direction and separates opening balance, period debits, period credits, and closing debit/credit balances.
- Cash/bank ledger derives opening, movement, and closing balances from posted GL lines mapped through tenant-owned bank account GL accounts.
- Stored bank snapshots are shown only as a variance comparison in the cash/bank ledger.
- AP/AR aging derives outstanding from `SubledgerSettlementBalances` and `SubledgerSettlementApplications`; operational paid/credited fields are displayed only as drift diagnostics.

## Diagnostics

Run these tenant-scoped checks before accountant sign-off. Replace `@TenantId`, dates, and table/column casing as needed for the target database provider.

```sql
-- Trial balance imbalance from posted GL
SELECT
    at."TenantId",
    SUM(at."DebitAmount") AS TotalDebits,
    SUM(at."CreditAmount") AS TotalCredits,
    SUM(at."DebitAmount" - at."CreditAmount") AS Difference
FROM "AccountTransactions" at
JOIN "JournalEntries" je ON je."Id" = at."JournalEntryId"
WHERE at."TenantId" = @TenantId
  AND je."TenantId" = @TenantId
  AND at."PostingStatus" IN ('Posted', 'Reversed')
  AND je."PostingStatus" IN ('Posted', 'Reversed')
  AND at."IsDeleted" = 0
  AND je."IsDeleted" = 0
GROUP BY at."TenantId"
HAVING ABS(SUM(at."DebitAmount" - at."CreditAmount")) > 0.01;

-- Posted ledger lines missing valid same-tenant GL accounts
SELECT at.*
FROM "AccountTransactions" at
JOIN "JournalEntries" je ON je."Id" = at."JournalEntryId"
LEFT JOIN "Accounts" a ON a."Id" = at."AccountId" AND a."TenantId" = at."TenantId"
WHERE at."TenantId" = @TenantId
  AND je."TenantId" = @TenantId
  AND at."PostingStatus" IN ('Posted', 'Reversed')
  AND je."PostingStatus" IN ('Posted', 'Reversed')
  AND (a."Id" IS NULL OR a."IsDeleted" = 1);

-- Orphaned account transactions without same-tenant posted journals
SELECT at.*
FROM "AccountTransactions" at
LEFT JOIN "JournalEntries" je
  ON je."Id" = at."JournalEntryId"
 AND je."TenantId" = at."TenantId"
WHERE at."TenantId" = @TenantId
  AND at."PostingStatus" IN ('Posted', 'Reversed')
  AND (je."Id" IS NULL OR je."PostingStatus" NOT IN ('Posted', 'Reversed'));

-- Posted AP/AR documents missing posting events
SELECT 'VendorInvoice' AS SourceDocumentType, vi."Id", vi."InvoiceNumber"
FROM "VendorInvoices" vi
LEFT JOIN "FinancePostingEvents" fpe
  ON fpe."TenantId" = vi."TenantId"
 AND fpe."SourceDocumentType" = 'VendorInvoice'
 AND fpe."SourceDocumentId" = vi."Id"
 AND fpe."PostingAction" = 'Post'
 AND fpe."PostingStatus" = 'Posted'
WHERE vi."TenantId" = @TenantId
  AND vi."Status" IN (3, 4, 5, 6)
  AND fpe."Id" IS NULL
UNION ALL
SELECT 'CustomerInvoice', i."Id", i."InvoiceNumber"
FROM "Invoices" i
LEFT JOIN "FinancePostingEvents" fpe
  ON fpe."TenantId" = i."TenantId"
 AND fpe."SourceDocumentType" = 'CustomerInvoice'
 AND fpe."SourceDocumentId" = i."Id"
 AND fpe."PostingAction" = 'Post'
 AND fpe."PostingStatus" = 'Posted'
WHERE i."TenantId" = @TenantId
  AND i."Status" IN (2, 3, 4, 5)
  AND fpe."Id" IS NULL;

-- Stored bank snapshots not matching posted GL-derived closing balances
SELECT
    ba."Id" AS BankAccountId,
    ba."AccountNumber",
    ba."CurrentBalance" AS StoredSnapshot,
    COALESCE(SUM(at."DebitAmount" - at."CreditAmount"), 0) AS PostedGlBalance,
    ba."CurrentBalance" - COALESCE(SUM(at."DebitAmount" - at."CreditAmount"), 0) AS Variance
FROM "BankAccounts" ba
LEFT JOIN "AccountTransactions" at
  ON at."TenantId" = ba."TenantId"
 AND at."AccountId" = ba."GLAccountId"
 AND at."PostingStatus" IN ('Posted', 'Reversed')
LEFT JOIN "JournalEntries" je
  ON je."Id" = at."JournalEntryId"
 AND je."TenantId" = ba."TenantId"
 AND je."PostingStatus" IN ('Posted', 'Reversed')
WHERE ba."TenantId" = @TenantId
  AND ba."GLAccountId" IS NOT NULL
GROUP BY ba."Id", ba."AccountNumber", ba."CurrentBalance"
HAVING ABS(ba."CurrentBalance" - COALESCE(SUM(at."DebitAmount" - at."CreditAmount"), 0)) > 0.01;

-- Segment references on reportable account segments that do not belong to the reporting tenant
SELECT asv.*
FROM "AccountSegmentValues" asv
LEFT JOIN "AccountSegmentStructures" ass
  ON ass."Id" = asv."SegmentStructureId"
 AND ass."TenantId" = asv."TenantId"
WHERE asv."TenantId" = @TenantId
  AND (ass."Id" IS NULL OR ass."IsReportingDimension" = 0);
```

## Export And Audit Behavior

Report view endpoints continue to use the existing Finance permission model. `CashReportsController.GetLedger` is mapped to `RunFinanceReports` through `FinancePermissionPolicyMap`.

Backend CSV export/print endpoints are implemented by `FinanceReportExportsController` and `FinanceReportExportService`.

Implemented report exports:

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

Exports use the same backend report services as the view endpoints and enforce the same tenant/account/segment/source-of-truth rules below the controller layer. AP/AR aging exports require `UsesSettlementReadModel = true` and fail rather than exporting legacy operational-field aging. Export and print actions record `Finance.Report.Exported`, `Finance.Report.Printed`, report-specific export events, and failure events through `IFinanceAuditService`.

See `docs/backend-reporting-export-presentation-foundation.md`.

## Known Limitations

All open limitations are tracked in `docs/finance-go-live-limitations-register.md`.

- `FIN-LIM-0001`: Resolved by `docs/ap-ar-settlement-read-model-foundation.md`; AP/AR aging and control reconciliation now use the rebuildable settlement read model.
- `FIN-LIM-0002`: Resolved for backend CSV export/print endpoints and export/print audit by `docs/backend-reporting-export-presentation-foundation.md`.
- `FIN-LIM-0044`: Resolved for core income statement/export presentation by mapping fixed asset disposal gains out of operating expense and surfacing warnings where the COA still uses a presentation workaround.
- `FIN-LIM-0045`: Unapplied AP payments, unapplied AR receipts, and advances remain a separate reporting/statement scope where production requires them.
- `FIN-LIM-0015`: Cash/bank position and ledger are functional-currency GL reports. Foreign-currency cash/bank presentation and IAS 21 revaluation/translation belong to the FX batch.
- `FIN-LIM-0003`: Resolved by `docs/ghana-statutory-tax-reporting-export-foundation.md`; Ghana statutory tax reports and CSV exports now use posted tax snapshots and posted GL tax account movement.

## Test Coverage

- `tests/ErpSystem.Api.Tests/Services/Finance/CoreFinancialReportingFoundationTests.cs`
  - Trial balance derives from posted GL and excludes draft journals.
  - Balance sheet derives from posted GL and normalizes credit-balance sections.
  - Income statement derives from posted GL and excludes draft journals.
  - Cross-tenant account filters are rejected.
  - Cross-tenant segment filters are rejected.
  - Cash/bank ledger excludes unposted cash transactions and shows snapshot variance.
- AP aging excludes unposted vendor invoices.
- AR aging excludes unposted customer invoices.
- AP/AR settlement read-model tests are covered by `SubledgerSettlementReadModelFoundationTests` under `Batch=FinanceGoLive-SubledgerSettlementReadModel`.
- Backend export, print audit, settlement read-model export guards, and disposal gain presentation tests are covered by `BackendReportingExportFoundationTests` under `Batch=FinanceGoLive-BackendReportingExport`.

## PR Definition Of Done

- Backend build passes for `ErpSystem.Core`, `ErpSystem.Api`, and `ErpSystem.Api.Tests`.
- `Batch=FinanceGoLive-Reporting` tests pass.
- `Batch~FinanceGoLive` regression slice passes.
- No database migrations are required for backend export.
- No frontend changes are included.
- Tenant isolation is enforced below the controller layer for report account and segment filters.
- Accounting impact is documented: posted GL is the source of truth; stored balances are read-side only.
- Remaining export, FX, tax, and subledger read-model limitations are documented for later batches.
