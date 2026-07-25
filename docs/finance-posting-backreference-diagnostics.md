# Finance Posting Back-Reference Diagnostics

## Purpose

The posted General Ledger and `FinancePostingEvents` are the accounting source of truth. Source-document fields such as `VendorInvoices.JournalEntryId`, `VendorPayments.JournalEntryId`, `Invoices.JournalEntryId`, `CustomerPayment.JournalEntryId`, `CreditNotes.JournalEntryId`, and operational status fields are repairable back-references for UX and operations.

Run these diagnostics before go-live and after any failed posting retry, migration, or data cleanup. Every check is tenant-scoped; replace `@TenantId` with the tenant under review.

## AP Invoice Back-Reference Checks

### Posted event exists but invoice link is missing or incorrect

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    i.Id AS VendorInvoiceId,
    i.InvoiceNumber,
    i.JournalEntryId AS InvoiceJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS PostingEventJournalEntryId
FROM VendorInvoices i
JOIN FinancePostingEvents e
    ON e.TenantId = i.TenantId
   AND e.SourceDocumentType = 'VendorInvoice'
   AND e.SourceDocumentId = i.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND (i.JournalEntryId IS NULL OR i.JournalEntryId <> e.JournalEntryId);
```

### Invoice is marked posted/paid but has no valid posting event

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    i.Id AS VendorInvoiceId,
    i.InvoiceNumber,
    i.Status,
    i.ApprovalStatus,
    i.JournalEntryId
FROM VendorInvoices i
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND i.JournalEntryId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = i.TenantId
        AND e.SourceDocumentType = 'VendorInvoice'
        AND e.SourceDocumentId = i.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = i.JournalEntryId
        AND e.IsDeleted = 0
  );
```

## AP Payment Back-Reference Checks

### Posted event exists but payment link is missing or incorrect

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    p.Id AS VendorPaymentId,
    p.PaymentNumber,
    p.JournalEntryId AS PaymentJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS PostingEventJournalEntryId
FROM VendorPayments p
JOIN FinancePostingEvents e
    ON e.TenantId = p.TenantId
   AND e.SourceDocumentType = 'VendorPayment'
   AND e.SourceDocumentId = p.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND (p.JournalEntryId IS NULL OR p.JournalEntryId <> e.JournalEntryId);
```

### Payment is marked posted/processed but has no valid posting event

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    p.Id AS VendorPaymentId,
    p.PaymentNumber,
    p.Status,
    p.JournalEntryId
FROM VendorPayments p
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.JournalEntryId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = p.TenantId
        AND e.SourceDocumentType = 'VendorPayment'
        AND e.SourceDocumentId = p.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = p.JournalEntryId
        AND e.IsDeleted = 0
  );
```

## AR Invoice Back-Reference Checks

### Posted event exists but invoice link is missing or incorrect

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    i.Id AS InvoiceId,
    i.InvoiceNumber,
    i.JournalEntryId AS InvoiceJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS PostingEventJournalEntryId
FROM Invoices i
JOIN FinancePostingEvents e
    ON e.TenantId = i.TenantId
   AND e.SourceDocumentType = 'CustomerInvoice'
   AND e.SourceDocumentId = i.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND (i.JournalEntryId IS NULL OR i.JournalEntryId <> e.JournalEntryId);
```

### Invoice is sent/posted but has no valid posting event

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    i.Id AS InvoiceId,
    i.InvoiceNumber,
    i.Status,
    i.JournalEntryId
FROM Invoices i
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND i.Status IN (2, 3, 4, 5) -- Sent, PartiallyPaid, Paid, Overdue
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = i.TenantId
        AND e.SourceDocumentType = 'CustomerInvoice'
        AND e.SourceDocumentId = i.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = i.JournalEntryId
        AND e.IsDeleted = 0
  );
```

## AR Receipt Back-Reference Checks

### Posted event exists but receipt link is missing or incorrect

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    p.Id AS CustomerPaymentId,
    p.PaymentNumber,
    p.JournalEntryId AS PaymentJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS PostingEventJournalEntryId
FROM CustomerPayment p
JOIN FinancePostingEvents e
    ON e.TenantId = p.TenantId
   AND e.SourceDocumentType = 'CustomerPayment'
   AND e.SourceDocumentId = p.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND (p.JournalEntryId IS NULL OR p.JournalEntryId <> e.JournalEntryId);
```

### Receipt is marked posted but has no valid posting event

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    p.Id AS CustomerPaymentId,
    p.PaymentNumber,
    p.Status,
    p.JournalEntryId
FROM CustomerPayment p
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.Status = 'Posted'
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = p.TenantId
        AND e.SourceDocumentType = 'CustomerPayment'
        AND e.SourceDocumentId = p.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = p.JournalEntryId
        AND e.IsDeleted = 0
  );
```

## AR Credit Note Back-Reference Checks

### Sales credit note posted event exists but credit note link is missing or incorrect

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    c.Id AS CreditNoteId,
    c.DocumentNumber,
    c.JournalEntryId AS CreditNoteJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS PostingEventJournalEntryId
FROM CreditNotes c
JOIN FinancePostingEvents e
    ON e.TenantId = c.TenantId
   AND e.SourceDocumentType = 'SalesCreditNote'
   AND e.SourceDocumentId = c.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND (c.JournalEntryId IS NULL OR c.JournalEntryId <> e.JournalEntryId);
```

### Sales credit note is approved/applied but has no valid posting event

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    c.Id AS CreditNoteId,
    c.DocumentNumber,
    c.CreditNoteStatus,
    c.JournalEntryId
FROM CreditNotes c
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND c.CreditNoteStatus IN (3, 4) -- Approved, Applied
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = c.TenantId
        AND e.SourceDocumentType = 'SalesCreditNote'
        AND e.SourceDocumentId = c.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = c.JournalEntryId
        AND e.IsDeleted = 0
  );
```

### Compatibility customer credit note event exists but payment link is missing or incorrect

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    p.Id AS CustomerCreditNoteId,
    p.PaymentNumber,
    p.JournalEntryId AS CreditNoteJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS PostingEventJournalEntryId
FROM CustomerPayment p
JOIN FinancePostingEvents e
    ON e.TenantId = p.TenantId
   AND e.SourceDocumentType = 'CustomerCreditNote'
   AND e.SourceDocumentId = p.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.IsCreditNote = 1
  AND (p.JournalEntryId IS NULL OR p.JournalEntryId <> e.JournalEntryId);
```

### Compatibility customer credit note is marked posted but has no valid posting event

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    p.Id AS CustomerCreditNoteId,
    p.PaymentNumber,
    p.Status,
    p.JournalEntryId
FROM CustomerPayment p
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.IsCreditNote = 1
  AND p.Status = 'Posted'
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = p.TenantId
        AND e.SourceDocumentType = 'CustomerCreditNote'
        AND e.SourceDocumentId = p.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = p.JournalEntryId
        AND e.IsDeleted = 0
  );
```

## Cash/Bank Transaction Back-Reference Checks

### Cash/bank posted event exists but transaction link is missing or incorrect

For transfers, the posted event is recorded against the outgoing/source transfer leg. The destination leg should carry the same `JournalEntryId` as a repairable operational back-reference.

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    t.Id AS CashTransactionId,
    t.TransactionNumber,
    t.TransactionType,
    t.JournalEntryId AS CashTransactionJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS PostingEventJournalEntryId
FROM CashTransaction t
JOIN FinancePostingEvents e
    ON e.TenantId = t.TenantId
   AND e.SourceDocumentType IN ('CashBankReceipt', 'CashBankPayment', 'CashBankTransfer')
   AND e.SourceDocumentId = t.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE t.TenantId = @TenantId
  AND t.IsDeleted = 0
  AND (t.JournalEntryId IS NULL OR t.JournalEntryId <> e.JournalEntryId);
```

### Cash/bank transaction is marked posted but has no valid posting event

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    t.Id AS CashTransactionId,
    t.TransactionNumber,
    t.TransactionType,
    t.IsPosted,
    t.JournalEntryId
FROM CashTransaction t
WHERE t.TenantId = @TenantId
  AND t.IsDeleted = 0
  AND (t.IsPosted = 1 OR t.JournalEntryId IS NOT NULL)
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = t.TenantId
        AND e.SourceDocumentType IN ('CashBankReceipt', 'CashBankPayment', 'CashBankTransfer')
        AND e.SourceDocumentId = t.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = t.JournalEntryId
        AND e.IsDeleted = 0
  );
```

### Cash/bank transfer destination leg is missing the source posting link

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    src.Id AS SourceCashTransactionId,
    src.TransactionNumber AS SourceTransactionNumber,
    dest.Id AS DestinationCashTransactionId,
    dest.TransactionNumber AS DestinationTransactionNumber,
    src.JournalEntryId AS SourceJournalEntryId,
    dest.JournalEntryId AS DestinationJournalEntryId,
    e.Id AS PostingEventId
FROM CashTransaction src
JOIN CashTransaction dest
    ON dest.TenantId = src.TenantId
   AND dest.IsDeleted = 0
   AND dest.TransactionType = src.TransactionType
   AND dest.BankAccountId = src.ToBankAccountId
   AND dest.ToBankAccountId = src.BankAccountId
   AND dest.TransactionNumber = REPLACE(src.TransactionNumber, '-OUT', '-IN')
JOIN FinancePostingEvents e
    ON e.TenantId = src.TenantId
   AND e.SourceDocumentType = 'CashBankTransfer'
   AND e.SourceDocumentId = src.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
WHERE src.TenantId = @TenantId
  AND src.IsDeleted = 0
  AND src.TransactionType = 3
  AND src.TransactionNumber LIKE '%-OUT'
  AND (dest.JournalEntryId IS NULL OR dest.JournalEntryId <> e.JournalEntryId);
```

## Cross-Tenant Journal Reference Checks

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT 'VendorInvoice' AS SourceType, i.Id AS SourceDocumentId, i.InvoiceNumber AS Reference, i.JournalEntryId, j.TenantId AS JournalTenantId
FROM VendorInvoices i
LEFT JOIN JournalEntries j ON j.Id = i.JournalEntryId AND j.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND i.JournalEntryId IS NOT NULL
  AND (j.Id IS NULL OR j.TenantId <> i.TenantId)
UNION ALL
SELECT 'VendorPayment' AS SourceType, p.Id AS SourceDocumentId, p.PaymentNumber AS Reference, p.JournalEntryId, j.TenantId AS JournalTenantId
FROM VendorPayments p
LEFT JOIN JournalEntries j ON j.Id = p.JournalEntryId AND j.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.JournalEntryId IS NOT NULL
  AND (j.Id IS NULL OR j.TenantId <> p.TenantId)
UNION ALL
SELECT 'CustomerInvoice' AS SourceType, i.Id AS SourceDocumentId, i.InvoiceNumber AS Reference, i.JournalEntryId, j.TenantId AS JournalTenantId
FROM Invoices i
LEFT JOIN JournalEntries j ON j.Id = i.JournalEntryId AND j.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND i.JournalEntryId IS NOT NULL
  AND (j.Id IS NULL OR j.TenantId <> i.TenantId)
UNION ALL
SELECT 'CustomerPayment' AS SourceType, p.Id AS SourceDocumentId, p.PaymentNumber AS Reference, p.JournalEntryId, j.TenantId AS JournalTenantId
FROM CustomerPayment p
LEFT JOIN JournalEntries j ON j.Id = p.JournalEntryId AND j.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.JournalEntryId IS NOT NULL
  AND p.IsCreditNote = 0
  AND (j.Id IS NULL OR j.TenantId <> p.TenantId)
UNION ALL
SELECT 'CustomerCreditNote' AS SourceType, p.Id AS SourceDocumentId, p.PaymentNumber AS Reference, p.JournalEntryId, j.TenantId AS JournalTenantId
FROM CustomerPayment p
LEFT JOIN JournalEntries j ON j.Id = p.JournalEntryId AND j.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.IsCreditNote = 1
  AND p.JournalEntryId IS NOT NULL
  AND (j.Id IS NULL OR j.TenantId <> p.TenantId)
UNION ALL
SELECT 'SalesCreditNote' AS SourceType, c.Id AS SourceDocumentId, c.DocumentNumber AS Reference, c.JournalEntryId, j.TenantId AS JournalTenantId
FROM CreditNotes c
LEFT JOIN JournalEntries j ON j.Id = c.JournalEntryId AND j.IsDeleted = 0
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND c.JournalEntryId IS NOT NULL
  AND (j.Id IS NULL OR j.TenantId <> c.TenantId)
UNION ALL
SELECT 'CashBankTransaction' AS SourceType, t.Id AS SourceDocumentId, t.TransactionNumber AS Reference, t.JournalEntryId, j.TenantId AS JournalTenantId
FROM CashTransaction t
LEFT JOIN JournalEntries j ON j.Id = t.JournalEntryId AND j.IsDeleted = 0
WHERE t.TenantId = @TenantId
  AND t.IsDeleted = 0
  AND t.JournalEntryId IS NOT NULL
  AND (j.Id IS NULL OR j.TenantId <> t.TenantId);
```

## Duplicate Or Conflicting Posting Events

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

SELECT
    e.SourceDocumentType,
    e.SourceDocumentId,
    e.PostingAction,
    COUNT(*) AS PostingEventCount,
    COUNT(DISTINCT e.JournalEntryId) AS DistinctJournalCount
FROM FinancePostingEvents e
WHERE e.TenantId = @TenantId
  AND e.SourceDocumentType IN ('VendorInvoice', 'VendorPayment', 'CustomerInvoice', 'CustomerPayment', 'CustomerCreditNote', 'SalesCreditNote', 'CashBankReceipt', 'CashBankPayment', 'CashBankTransfer')
  AND e.PostingAction = 'Post'
  AND e.PostingStatus = 'Posted'
  AND e.IsDeleted = 0
GROUP BY e.SourceDocumentType, e.SourceDocumentId, e.PostingAction
HAVING COUNT(*) > 1 OR COUNT(DISTINCT e.JournalEntryId) > 1;
```

## Safe Repair Approach

1. Run diagnostics per tenant and export the result set for accountant review.
2. For each source document, confirm there is exactly one posted `FinancePostingEvent` with the expected `TenantId`, `SourceDocumentType`, `SourceDocumentId`, `PostingAction = 'Post'`, and a same-tenant posted `JournalEntry`.
3. Relink only missing or incorrect source-document back-references to that posting event journal.
4. Do not create, delete, or modify journal entries during relink repair.
5. Do not repair documents with duplicate/conflicting posting events until an accountant determines which posting is valid and which entry requires reversal.
6. For AR invoice repairs, match only `TenantId`, `SourceDocumentType = 'CustomerInvoice'`, `SourceDocumentId = Invoices.Id`, `PostingAction = 'Post'`, the posting event, and a same-tenant posted journal entry.
7. For AR receipt repairs, match only `TenantId`, `SourceDocumentType = 'CustomerPayment'`, `SourceDocumentId = CustomerPayment.Id`, `PostingAction = 'Post'`, the posting event, and a same-tenant posted journal entry.
8. For AR credit note repairs, match only `TenantId`, `SourceDocumentType = 'SalesCreditNote'` or `SourceDocumentType = 'CustomerCreditNote'`, the source document ID, `PostingAction = 'Post'`, the posting event, and a same-tenant posted journal entry.
9. For cash/bank transaction repairs, match only `TenantId`, `SourceDocumentType = 'CashBankReceipt'`, `SourceDocumentType = 'CashBankPayment'`, or `SourceDocumentType = 'CashBankTransfer'`, the source transaction ID, `PostingAction = 'Post'`, the posting event, and a same-tenant posted journal entry. For transfers, repair the outgoing/source transaction from the posting event first, then relink the destination leg to the same journal only when the reciprocal bank account pair is proven.
10. Record the repair as a Finance audit/migration-cleanup event with tenant ID, source document, old journal reference, new journal reference, operator, timestamp, and accountant sign-off reference.

Example relink pattern after accountant approval:

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

UPDATE i
SET i.JournalEntryId = e.JournalEntryId,
    i.UpdatedAt = SYSUTCDATETIME(),
    i.UpdatedBy = 'finance-backreference-repair'
FROM VendorInvoices i
JOIN FinancePostingEvents e
    ON e.TenantId = i.TenantId
   AND e.SourceDocumentType = 'VendorInvoice'
   AND e.SourceDocumentId = i.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
JOIN JournalEntries j
    ON j.TenantId = e.TenantId
   AND j.Id = e.JournalEntryId
   AND j.PostingStatus = 'Posted'
   AND j.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND (i.JournalEntryId IS NULL OR i.JournalEntryId <> e.JournalEntryId)
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents dup
      WHERE dup.TenantId = e.TenantId
        AND dup.SourceDocumentType = e.SourceDocumentType
        AND dup.SourceDocumentId = e.SourceDocumentId
        AND dup.PostingAction = e.PostingAction
        AND dup.PostingStatus = 'Posted'
        AND dup.IsDeleted = 0
        AND dup.Id <> e.Id
  );
```

Use the same pattern for `VendorPayments` with `SourceDocumentType = 'VendorPayment'`.

Use the same pattern for AR invoices with `Invoices` and `SourceDocumentType = 'CustomerInvoice'`:

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

UPDATE i
SET i.JournalEntryId = e.JournalEntryId,
    i.UpdatedAt = SYSUTCDATETIME(),
    i.UpdatedBy = 'finance-backreference-repair'
FROM Invoices i
JOIN FinancePostingEvents e
    ON e.TenantId = i.TenantId
   AND e.SourceDocumentType = 'CustomerInvoice'
   AND e.SourceDocumentId = i.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
JOIN JournalEntries j
    ON j.TenantId = e.TenantId
   AND j.Id = e.JournalEntryId
   AND j.PostingStatus = 'Posted'
   AND j.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND (i.JournalEntryId IS NULL OR i.JournalEntryId <> e.JournalEntryId)
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents dup
      WHERE dup.TenantId = e.TenantId
        AND dup.SourceDocumentType = e.SourceDocumentType
        AND dup.SourceDocumentId = e.SourceDocumentId
        AND dup.PostingAction = e.PostingAction
        AND dup.PostingStatus = 'Posted'
        AND dup.IsDeleted = 0
        AND dup.Id <> e.Id
  );
```

Every AR invoice repair must be reviewed by accounting before execution and signed off after execution. Attach the diagnostic result set, repair SQL output, same-tenant journal evidence, and finance audit/migration-cleanup event reference to the go-live sign-off pack.

Use the same pattern for AR receipts with `CustomerPayment` and `SourceDocumentType = 'CustomerPayment'`:

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

UPDATE p
SET p.JournalEntryId = e.JournalEntryId,
    p.UpdatedAt = SYSUTCDATETIME(),
    p.UpdatedBy = 'finance-backreference-repair'
FROM CustomerPayment p
JOIN FinancePostingEvents e
    ON e.TenantId = p.TenantId
   AND e.SourceDocumentType = 'CustomerPayment'
   AND e.SourceDocumentId = p.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
JOIN JournalEntries j
    ON j.TenantId = e.TenantId
   AND j.Id = e.JournalEntryId
   AND j.PostingStatus = 'Posted'
   AND j.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND (p.JournalEntryId IS NULL OR p.JournalEntryId <> e.JournalEntryId)
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents dup
      WHERE dup.TenantId = e.TenantId
        AND dup.SourceDocumentType = e.SourceDocumentType
        AND dup.SourceDocumentId = e.SourceDocumentId
        AND dup.PostingAction = e.PostingAction
        AND dup.PostingStatus = 'Posted'
        AND dup.IsDeleted = 0
        AND dup.Id <> e.Id
  );
```

Every AR receipt repair must be reviewed by accounting before execution and signed off after execution. Attach the diagnostic result set, repair SQL output, same-tenant journal evidence, affected allocation/invoice evidence, and finance audit/migration-cleanup event reference to the go-live sign-off pack.

Use the same pattern for Sales credit notes with `CreditNotes` and `SourceDocumentType = 'SalesCreditNote'`:

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

UPDATE c
SET c.JournalEntryId = e.JournalEntryId,
    c.UpdatedAt = SYSUTCDATETIME(),
    c.UpdatedBy = 'finance-backreference-repair'
FROM CreditNotes c
JOIN FinancePostingEvents e
    ON e.TenantId = c.TenantId
   AND e.SourceDocumentType = 'SalesCreditNote'
   AND e.SourceDocumentId = c.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
JOIN JournalEntries j
    ON j.TenantId = e.TenantId
   AND j.Id = e.JournalEntryId
   AND j.PostingStatus = 'Posted'
   AND j.IsDeleted = 0
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND (c.JournalEntryId IS NULL OR c.JournalEntryId <> e.JournalEntryId)
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents dup
      WHERE dup.TenantId = e.TenantId
        AND dup.SourceDocumentType = e.SourceDocumentType
        AND dup.SourceDocumentId = e.SourceDocumentId
        AND dup.PostingAction = e.PostingAction
        AND dup.PostingStatus = 'Posted'
        AND dup.IsDeleted = 0
        AND dup.Id <> e.Id
  );
```

Use the same pattern for compatibility customer credit notes with `CustomerPayment`, `IsCreditNote = 1`, and `SourceDocumentType = 'CustomerCreditNote'`:

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

UPDATE p
SET p.JournalEntryId = e.JournalEntryId,
    p.UpdatedAt = SYSUTCDATETIME(),
    p.UpdatedBy = 'finance-backreference-repair'
FROM CustomerPayment p
JOIN FinancePostingEvents e
    ON e.TenantId = p.TenantId
   AND e.SourceDocumentType = 'CustomerCreditNote'
   AND e.SourceDocumentId = p.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
JOIN JournalEntries j
    ON j.TenantId = e.TenantId
   AND j.Id = e.JournalEntryId
   AND j.PostingStatus = 'Posted'
   AND j.IsDeleted = 0
WHERE p.TenantId = @TenantId
  AND p.IsDeleted = 0
  AND p.IsCreditNote = 1
  AND (p.JournalEntryId IS NULL OR p.JournalEntryId <> e.JournalEntryId)
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents dup
      WHERE dup.TenantId = e.TenantId
        AND dup.SourceDocumentType = e.SourceDocumentType
        AND dup.SourceDocumentId = e.SourceDocumentId
        AND dup.PostingAction = e.PostingAction
        AND dup.PostingStatus = 'Posted'
        AND dup.IsDeleted = 0
        AND dup.Id <> e.Id
  );
```

Every AR credit note repair must be reviewed by accounting before execution and signed off after execution. Attach the diagnostic result set, repair SQL output, same-tenant original invoice evidence where applicable, same-tenant journal evidence, and finance audit/migration-cleanup event reference to the go-live sign-off pack.

Use the same pattern for cash/bank receipts, payments, and outgoing transfer source legs with `CashTransaction` and the applicable source document type:

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

UPDATE t
SET t.JournalEntryId = e.JournalEntryId,
    t.IsPosted = 1,
    t.PostedDate = COALESCE(t.PostedDate, e.PostedAt, SYSUTCDATETIME()),
    t.UpdatedAt = SYSUTCDATETIME(),
    t.UpdatedBy = 'finance-backreference-repair'
FROM CashTransaction t
JOIN FinancePostingEvents e
    ON e.TenantId = t.TenantId
   AND e.SourceDocumentType IN ('CashBankReceipt', 'CashBankPayment', 'CashBankTransfer')
   AND e.SourceDocumentId = t.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
JOIN JournalEntries j
    ON j.TenantId = e.TenantId
   AND j.Id = e.JournalEntryId
   AND j.PostingStatus = 'Posted'
   AND j.IsDeleted = 0
WHERE t.TenantId = @TenantId
  AND t.IsDeleted = 0
  AND (t.JournalEntryId IS NULL OR t.JournalEntryId <> e.JournalEntryId)
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents dup
      WHERE dup.TenantId = e.TenantId
        AND dup.SourceDocumentType = e.SourceDocumentType
        AND dup.SourceDocumentId = e.SourceDocumentId
        AND dup.PostingAction = e.PostingAction
        AND dup.PostingStatus = 'Posted'
        AND dup.IsDeleted = 0
        AND dup.Id <> e.Id
  );
```

For bank transfers, relink the destination leg only after confirming the outgoing leg and reciprocal bank account pair:

```sql
DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

UPDATE dest
SET dest.JournalEntryId = e.JournalEntryId,
    dest.IsPosted = 1,
    dest.PostedDate = COALESCE(dest.PostedDate, e.PostedAt, SYSUTCDATETIME()),
    dest.UpdatedAt = SYSUTCDATETIME(),
    dest.UpdatedBy = 'finance-backreference-repair'
FROM CashTransaction src
JOIN CashTransaction dest
    ON dest.TenantId = src.TenantId
   AND dest.IsDeleted = 0
   AND dest.TransactionType = src.TransactionType
   AND dest.BankAccountId = src.ToBankAccountId
   AND dest.ToBankAccountId = src.BankAccountId
   AND dest.TransactionNumber = REPLACE(src.TransactionNumber, '-OUT', '-IN')
JOIN FinancePostingEvents e
    ON e.TenantId = src.TenantId
   AND e.SourceDocumentType = 'CashBankTransfer'
   AND e.SourceDocumentId = src.Id
   AND e.PostingAction = 'Post'
   AND e.PostingStatus = 'Posted'
   AND e.IsDeleted = 0
JOIN JournalEntries j
    ON j.TenantId = e.TenantId
   AND j.Id = e.JournalEntryId
   AND j.PostingStatus = 'Posted'
   AND j.IsDeleted = 0
WHERE src.TenantId = @TenantId
  AND src.IsDeleted = 0
  AND src.TransactionType = 3
  AND src.TransactionNumber LIKE '%-OUT'
  AND src.JournalEntryId = e.JournalEntryId
  AND (dest.JournalEntryId IS NULL OR dest.JournalEntryId <> e.JournalEntryId);
```

Every cash/bank transaction repair must be reviewed by accounting before execution and signed off after execution. Attach the diagnostic result set, repair SQL output, same-tenant bank account evidence, same-tenant journal evidence, and finance audit/migration-cleanup event reference to the go-live sign-off pack.
