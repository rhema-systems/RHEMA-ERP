# AP/AR Settlement Read Model and Aging/Control Reconciliation Foundation

## Source Of Truth

Posted GL remains the accounting source of truth. The AP/AR settlement read model is a rebuildable reporting and reconciliation projection derived from posted source documents, posted settlement allocations, `FinancePostingEvent`, posted `JournalEntry`, and posted `AccountTransaction`.

Operational fields such as `VendorInvoice.PaidAmount`, `Invoice.PaidAmount`, and `Invoice.CreditedAmount` remain read-side snapshots. Aging and control reconciliation must not rely on those fields as the sole accounting basis.

## Implemented Components

- `SubledgerSettlementBalance`
  - One tenant-scoped row per posted AP/AR source document.
  - Stores source module, counterparty, document type/id/number, posting-event and journal references, document currency, functional currency, original amount, settled amount, credited amount, withheld amount, outstanding amount, status, rebuild batch, and diagnostics.
- `SubledgerSettlementApplication`
  - One tenant-scoped row per posted settlement application.
  - Stores payment/receipt/credit-note source, allocation id, settlement posting-event/journal references, settled/credited/withheld amounts, FX realized settlement reference, and rebuild batch.
- `SubledgerSettlementReadModelService`
  - Rebuilds AP, AR, or both modules per tenant and as-of date.
  - Clears and rebuilds read-side rows idempotently.
  - Excludes unposted source documents and unposted settlement events.
  - Emits diagnostics for cross-tenant allocations, missing source journals, operational snapshot drift, and control-account variance.
- AP and AR aging report services
  - Use the settlement read model when the service is registered.
  - Preserve legacy fallback behavior only when the read-model service is not injected.
  - Add `UsesSettlementReadModel` and diagnostic payloads to response DTOs.
- AP/AR control reconciliation
  - Compares read-model outstanding to posted GL control-account movement.
  - AP control balance is `credit - debit`.
  - AR control balance is `debit - credit`.

## Settlement Behavior

### AP

AP read-model source rows come from posted `VendorInvoice` events with `SourceDocumentType = VendorInvoice`.

AP applications come from posted `VendorPayment` events and same-tenant `VendorPaymentAllocation` rows:

- `SettledAmount = AllocatedAmount + DiscountAmount`
- `WithheldAmount = WithholdingTaxAmount`
- `OutstandingAmount = Invoice.TotalAmount - SettledAmount - WithheldAmount - CreditedAmount`

AP WHT is treated as a settlement component when posted payment allocation data supports it.

### AR

AR read-model source rows come from posted `Invoice` events with `SourceDocumentType = CustomerInvoice`.

AR applications come from posted `CustomerPayment`, `SalesCreditNote`, and compatibility `CustomerCreditNote` events:

- Customer receipt withholding is allocated proportionally across payment allocations.
- `SettledAmount` excludes allocated WHT/VAT withholding.
- `WithheldAmount` records AR WHT/VAT withholding settlement components.
- `CreditedAmount` includes posted Sales credit notes and compatibility customer credit-note allocations.
- `OutstandingAmount = Invoice.TotalAmount - SettledAmount - WithheldAmount - CreditedAmount`

Compatibility customer credit notes remain visible under `FIN-LIM-0013`.

## Foreign Currency

The read model preserves source document currency, functional currency, original functional amount, and FX realized settlement references where posted FX settlement rows exist.

The read model does not calculate FX gain/loss. It consumes posted FX accounting records from the FX foundation and exposes the linkage for AP/AR aging and reconciliation.

## Rebuild Process

`ISubledgerSettlementReadModelService.RebuildAsync` accepts:

- `SourceModule = AP`, `AR`, or `Both`
- `AsOfDate`
- `RecordAudit`

The rebuild:

- is tenant-scoped through the current Finance tenant;
- deletes existing read-model rows for the requested module and tenant;
- recreates source balance rows from posted source events;
- recreates settlement application rows from posted allocations and posted settlement events;
- never mutates posted invoices, payments, receipts, credit notes, journals, posting events, or account transactions;
- can be re-run safely for migration/sign-off and diagnostics.

## Audit Events

Implemented event constants and call sites:

- `Finance.ApSettlementReadModelRebuilt`
- `Finance.ArSettlementReadModelRebuilt`
- `Finance.ApAgingGeneratedFromSettlementReadModel`
- `Finance.ArAgingGeneratedFromSettlementReadModel`
- `Finance.ApControlReconciliationGenerated`
- `Finance.ArControlReconciliationGenerated`
- `Finance.SettlementRebuildVarianceDetected`
- `Finance.SettlementRebuildFailed`

## Diagnostics

Run these tenant-scoped SQL checks before accountant sign-off. The SQL uses SQL Server names from the EF model and the migration for this batch.

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

-- Posted AP invoices missing read-model rows.
SELECT vi.Id, vi.InvoiceNumber
FROM VendorInvoices vi
JOIN FinancePostingEvents fpe
  ON fpe.TenantId = vi.TenantId
 AND fpe.SourceDocumentType = 'VendorInvoice'
 AND fpe.SourceDocumentId = vi.Id
 AND fpe.PostingAction = 'Post'
 AND fpe.PostingStatus = 'Posted'
WHERE vi.TenantId = @TenantId
  AND vi.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1
      FROM SubledgerSettlementBalances b
      WHERE b.TenantId = vi.TenantId
        AND b.SourceModule = 'AP'
        AND b.SourceDocumentType = 'VendorInvoice'
        AND b.SourceDocumentId = vi.Id
        AND b.IsDeleted = 0
  );

-- Posted AR invoices missing read-model rows.
SELECT i.Id, i.InvoiceNumber
FROM Invoices i
JOIN FinancePostingEvents fpe
  ON fpe.TenantId = i.TenantId
 AND fpe.SourceDocumentType = 'CustomerInvoice'
 AND fpe.SourceDocumentId = i.Id
 AND fpe.PostingAction = 'Post'
 AND fpe.PostingStatus = 'Posted'
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1
      FROM SubledgerSettlementBalances b
      WHERE b.TenantId = i.TenantId
        AND b.SourceModule = 'AR'
        AND b.SourceDocumentType = 'CustomerInvoice'
        AND b.SourceDocumentId = i.Id
        AND b.IsDeleted = 0
  );

-- Read-model rows with missing or cross-tenant posting events.
SELECT b.*
FROM SubledgerSettlementBalances b
LEFT JOIN FinancePostingEvents fpe
  ON fpe.Id = b.SourcePostingEventId
 AND fpe.TenantId = b.TenantId
 AND fpe.PostingStatus = 'Posted'
WHERE b.TenantId = @TenantId
  AND b.IsDeleted = 0
  AND fpe.Id IS NULL;

-- Read-model rows with missing or cross-tenant posted journals.
SELECT b.*
FROM SubledgerSettlementBalances b
LEFT JOIN JournalEntries je
  ON je.Id = b.SourceJournalEntryId
 AND je.TenantId = b.TenantId
 AND je.PostingStatus = 'Posted'
WHERE b.TenantId = @TenantId
  AND b.IsDeleted = 0
  AND (b.SourceJournalEntryId IS NULL OR je.Id IS NULL);

-- Posted AP payments not reflected in settlement applications.
SELECT p.Id, p.PaymentNumber
FROM VendorPayment p
JOIN FinancePostingEvents fpe
  ON fpe.TenantId = p.TenantId
 AND fpe.SourceDocumentType = 'VendorPayment'
 AND fpe.SourceDocumentId = p.Id
 AND fpe.PostingAction = 'Post'
 AND fpe.PostingStatus = 'Posted'
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1
      FROM SubledgerSettlementApplications a
      WHERE a.TenantId = p.TenantId
        AND a.SourceModule = 'AP'
        AND a.SettlementSourceType = 'VendorPayment'
        AND a.SettlementSourceId = p.Id
        AND a.IsDeleted = 0
  );

-- Posted AR receipts not reflected in settlement applications.
SELECT p.Id, p.PaymentNumber
FROM CustomerPayment p
JOIN FinancePostingEvents fpe
  ON fpe.TenantId = p.TenantId
 AND fpe.SourceDocumentType = 'CustomerPayment'
 AND fpe.SourceDocumentId = p.Id
 AND fpe.PostingAction = 'Post'
 AND fpe.PostingStatus = 'Posted'
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.IsCreditNote = 0
  AND NOT EXISTS (
      SELECT 1
      FROM SubledgerSettlementApplications a
      WHERE a.TenantId = p.TenantId
        AND a.SourceModule = 'AR'
        AND a.SettlementSourceType = 'CustomerPayment'
        AND a.SettlementSourceId = p.Id
        AND a.IsDeleted = 0
  );

-- Posted AR credit notes not reflected in settlement applications.
SELECT cn.Id, cn.DocumentNumber
FROM CreditNotes cn
JOIN FinancePostingEvents fpe
  ON fpe.TenantId = cn.TenantId
 AND fpe.SourceDocumentType = 'SalesCreditNote'
 AND fpe.SourceDocumentId = cn.Id
 AND fpe.PostingAction = 'Post'
 AND fpe.PostingStatus = 'Posted'
WHERE cn.TenantId = @TenantId
  AND cn.IsDeleted = 0
  AND NOT EXISTS (
      SELECT 1
      FROM SubledgerSettlementApplications a
      WHERE a.TenantId = cn.TenantId
        AND a.SourceModule = 'AR'
        AND a.SettlementSourceType = 'SalesCreditNote'
        AND a.SettlementSourceId = cn.Id
        AND a.IsDeleted = 0
  );

-- AP operational paid snapshot drift.
SELECT
    b.SourceDocumentId,
    b.SourceDocumentNumber,
    b.OutstandingAmount AS ReadModelOutstanding,
    vi.TotalAmount - vi.PaidAmount AS OperationalOutstanding,
    b.OperationalVariance
FROM SubledgerSettlementBalances b
JOIN VendorInvoices vi
  ON vi.TenantId = b.TenantId
 AND vi.Id = b.SourceDocumentId
WHERE b.TenantId = @TenantId
  AND b.SourceModule = 'AP'
  AND b.IsDeleted = 0
  AND ABS(b.OperationalVariance) > 0.01;

-- AR operational paid/credited snapshot drift.
SELECT
    b.SourceDocumentId,
    b.SourceDocumentNumber,
    b.OutstandingAmount AS ReadModelOutstanding,
    i.TotalAmount - i.PaidAmount - i.CreditedAmount AS OperationalOutstanding,
    b.OperationalVariance
FROM SubledgerSettlementBalances b
JOIN Invoices i
  ON i.TenantId = b.TenantId
 AND i.Id = b.SourceDocumentId
WHERE b.TenantId = @TenantId
  AND b.SourceModule = 'AR'
  AND b.IsDeleted = 0
  AND ABS(b.OperationalVariance) > 0.01;

-- AP control reconciliation variance.
SELECT
    fs.ControlAccountApId,
    COALESCE(SUM(at.CreditAmount - at.DebitAmount), 0) AS PostedGlApBalance,
    COALESCE((SELECT SUM(b.OutstandingAmount)
              FROM SubledgerSettlementBalances b
              WHERE b.TenantId = fs.TenantId
                AND b.SourceModule = 'AP'
                AND b.IsDeleted = 0), 0) AS ReadModelApOutstanding
FROM FinanceSettings fs
LEFT JOIN AccountTransactions at
  ON at.TenantId = fs.TenantId
 AND at.AccountId = fs.ControlAccountApId
 AND at.PostingStatus = 'Posted'
WHERE fs.TenantId = @TenantId
GROUP BY fs.TenantId, fs.ControlAccountApId
HAVING ABS(COALESCE(SUM(at.CreditAmount - at.DebitAmount), 0) -
       COALESCE((SELECT SUM(b.OutstandingAmount)
                 FROM SubledgerSettlementBalances b
                 WHERE b.TenantId = fs.TenantId
                   AND b.SourceModule = 'AP'
                   AND b.IsDeleted = 0), 0)) > 0.01;

-- AR control reconciliation variance.
SELECT
    fs.ControlAccountArId,
    COALESCE(SUM(at.DebitAmount - at.CreditAmount), 0) AS PostedGlArBalance,
    COALESCE((SELECT SUM(b.OutstandingAmount)
              FROM SubledgerSettlementBalances b
              WHERE b.TenantId = fs.TenantId
                AND b.SourceModule = 'AR'
                AND b.IsDeleted = 0), 0) AS ReadModelArOutstanding
FROM FinanceSettings fs
LEFT JOIN AccountTransactions at
  ON at.TenantId = fs.TenantId
 AND at.AccountId = fs.ControlAccountArId
 AND at.PostingStatus = 'Posted'
WHERE fs.TenantId = @TenantId
GROUP BY fs.TenantId, fs.ControlAccountArId
HAVING ABS(COALESCE(SUM(at.DebitAmount - at.CreditAmount), 0) -
       COALESCE((SELECT SUM(b.OutstandingAmount)
                 FROM SubledgerSettlementBalances b
                 WHERE b.TenantId = fs.TenantId
                   AND b.SourceModule = 'AR'
                   AND b.IsDeleted = 0), 0)) > 0.01;

-- Duplicate read-model rows.
SELECT TenantId, SourceModule, SourceDocumentType, SourceDocumentId, COUNT(*) AS RowCount
FROM SubledgerSettlementBalances
WHERE TenantId = @TenantId
  AND IsDeleted = 0
GROUP BY TenantId, SourceModule, SourceDocumentType, SourceDocumentId
HAVING COUNT(*) > 1;

-- Duplicate settlement applications for one allocation.
SELECT TenantId, SourceModule, SettlementSourceType, SettlementSourceId, SettlementAllocationId, COUNT(*) AS RowCount
FROM SubledgerSettlementApplications
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND SettlementAllocationId IS NOT NULL
GROUP BY TenantId, SourceModule, SettlementSourceType, SettlementSourceId, SettlementAllocationId
HAVING COUNT(*) > 1;

-- Over-settled documents.
SELECT *
FROM SubledgerSettlementBalances
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND OutstandingAmount < -0.01;

-- Unapplied AP payments that have posted payment events but no allocation applications.
SELECT p.Id, p.PaymentNumber, p.TotalAmount, p.AllocatedAmount
FROM VendorPayment p
JOIN FinancePostingEvents fpe
  ON fpe.TenantId = p.TenantId
 AND fpe.SourceDocumentType = 'VendorPayment'
 AND fpe.SourceDocumentId = p.Id
 AND fpe.PostingAction = 'Post'
 AND fpe.PostingStatus = 'Posted'
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.TotalAmount > p.AllocatedAmount
  AND NOT EXISTS (
      SELECT 1
      FROM SubledgerSettlementApplications a
      WHERE a.TenantId = p.TenantId
        AND a.SettlementSourceType = 'VendorPayment'
        AND a.SettlementSourceId = p.Id
        AND a.IsDeleted = 0
  );

-- Unapplied AR receipts that have posted receipt events but no allocation applications.
SELECT p.Id, p.PaymentNumber, p.TotalAmount, p.AllocatedAmount
FROM CustomerPayment p
JOIN FinancePostingEvents fpe
  ON fpe.TenantId = p.TenantId
 AND fpe.SourceDocumentType = 'CustomerPayment'
 AND fpe.SourceDocumentId = p.Id
 AND fpe.PostingAction = 'Post'
 AND fpe.PostingStatus = 'Posted'
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.IsCreditNote = 0
  AND p.TotalAmount > p.AllocatedAmount
  AND NOT EXISTS (
      SELECT 1
      FROM SubledgerSettlementApplications a
      WHERE a.TenantId = p.TenantId
        AND a.SettlementSourceType = 'CustomerPayment'
        AND a.SettlementSourceId = p.Id
        AND a.IsDeleted = 0
  );

-- Foreign-currency documents missing snapshots.
SELECT *
FROM SubledgerSettlementBalances
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND DocumentCurrencyCode <> FunctionalCurrencyCode
  AND (OriginalFunctionalAmount = 0 OR FunctionalCurrencyCode IS NULL);
```

## Known Limitations

- `FIN-LIM-0001` is resolved for rebuildable AP/AR settlement read models, aging, and control reconciliation.
- `FIN-LIM-0045` tracks unapplied AP payments, unapplied AR receipts, and advances as a separate go-live reporting scope.
- `FIN-LIM-0013` remains open for the compatibility `CustomerPayment.IsCreditNote` workflow path.
- `FIN-LIM-0009`, `FIN-LIM-0010`, and `FIN-LIM-0012` remain open for AP payment, AR receipt, and AR credit-note reversal/correction accounting.

## Test Coverage

`tests/ErpSystem.Api.Tests/Services/Finance/SubledgerSettlementReadModelFoundationTests.cs`

- `ApPostedInvoiceAppearsInSettlementReadModel`
- `ArPostedInvoiceAppearsInSettlementReadModel`
- `UnpostedApAndArInvoicesAreExcluded`
- `PartialApPaymentReducesOutstandingAndHandlesWithholding`
- `PartialArReceiptReducesOutstandingAndReportsWithholding`
- `FullApPaymentClosesOutstanding`
- `FullArReceiptClosesOutstanding`
- `ArCreditNoteReducesOutstanding`
- `ForeignCurrencyApSettlementUsesPostedSnapshotsAndFxLink`
- `ForeignCurrencyArSettlementUsesPostedSnapshotsAndFxLink`
- `CrossTenantAllocationsAreDiagnosedAndExcluded`
- `ApAgingUsesSettlementReadModelInsteadOfMutablePaidField`
- `ArAgingUsesSettlementReadModelInsteadOfMutablePaidFields`
- `ApControlReconciliationShowsZeroVarianceForCleanData`
- `ArControlReconciliationShowsZeroVarianceForCleanData`
- `MissingSourceJournalIsDiagnosed`
- `RebuildIsIdempotent`
- `RebuildAndControlReportsEmitAuditEvents`

## PR Definition Of Done

- Backend build passes for the API test project.
- Focused `Batch=FinanceGoLive-SubledgerSettlementReadModel` tests pass.
- Finance go-live regression slice passes.
- Migration for read-model tables is included.
- Reports and rebuilds are tenant-scoped.
- AP/AR aging derives outstanding balances from the read model, not mutable paid/credited fields.
- AP/AR control reconciliation ties read-model outstanding to posted GL control accounts.
- Remaining limitations are recorded in `docs/finance-go-live-limitations-register.md`.
