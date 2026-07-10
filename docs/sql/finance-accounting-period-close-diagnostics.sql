/*
Finance accounting period close diagnostics

Run before closing a fiscal period in production-like data.
Set @TenantId and @FiscalPeriodId first. Review every non-empty result set with Finance/accounting.

These checks assume the default EF table names match entity names. If a deployment maps table
names differently, adapt only the table names, not the tenant/date/source-document predicates.
*/

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';
DECLARE @FiscalPeriodId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000';

DECLARE @PeriodStart DATE;
DECLARE @PeriodEnd DATE;

SELECT
    @PeriodStart = CAST(StartDate AS DATE),
    @PeriodEnd = CAST(EndDate AS DATE)
FROM FiscalPeriods
WHERE TenantId = @TenantId
  AND Id = @FiscalPeriodId
  AND IsDeleted = 0;

-- 1. Period state and tenant ownership.
SELECT
    Id,
    TenantId,
    PeriodCode,
    PeriodName,
    PeriodStatus,
    IsOpen,
    IsClosed,
    IsLocked,
    StartDate,
    EndDate
FROM FiscalPeriods
WHERE Id = @FiscalPeriodId;

-- 2. Approved/submitted journals that are not posted.
SELECT
    Id,
    JournalEntryNumber,
    EntryDate,
    PostingStatus,
    ApprovalStatus,
    TotalDebitAmount,
    TotalCreditAmount
FROM JournalEntries
WHERE TenantId = @TenantId
  AND FiscalPeriodId = @FiscalPeriodId
  AND IsDeleted = 0
  AND PostingStatus <> 'Posted'
  AND (PostingStatus IN ('Submitted', 'Approved') OR ApprovalStatus IN ('Pending', 'PendingApproval', 'Approved'));

-- 3. Unbalanced posted journals.
SELECT
    Id,
    JournalEntryNumber,
    EntryDate,
    TotalDebitAmount,
    TotalCreditAmount,
    TotalDebitAmount - TotalCreditAmount AS Difference
FROM JournalEntries
WHERE TenantId = @TenantId
  AND FiscalPeriodId = @FiscalPeriodId
  AND IsDeleted = 0
  AND PostingStatus = 'Posted'
  AND TotalDebitAmount <> TotalCreditAmount;

-- 4. Posted journals missing posting events.
SELECT
    j.Id,
    j.JournalEntryNumber,
    j.EntryDate,
    j.SourceModule,
    j.SourceDocumentType,
    j.SourceDocumentId
FROM JournalEntries j
WHERE j.TenantId = @TenantId
  AND j.FiscalPeriodId = @FiscalPeriodId
  AND j.IsDeleted = 0
  AND j.PostingStatus = 'Posted'
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = @TenantId
        AND e.IsDeleted = 0
        AND e.PostingStatus = 'Posted'
        AND e.JournalEntryId = j.Id
  );

-- 5. Posted posting events with missing or cross-tenant journal references.
SELECT
    e.Id,
    e.SourceModule,
    e.SourceDocumentType,
    e.SourceDocumentId,
    e.PostingAction,
    e.PostingDate,
    e.JournalEntryId
FROM FinancePostingEvents e
LEFT JOIN JournalEntries j
    ON j.Id = e.JournalEntryId
   AND j.TenantId = e.TenantId
   AND j.IsDeleted = 0
   AND j.PostingStatus = 'Posted'
WHERE e.TenantId = @TenantId
  AND e.IsDeleted = 0
  AND e.PostingStatus = 'Posted'
  AND CAST(e.PostingDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd
  AND (e.JournalEntryId IS NULL OR j.Id IS NULL);

-- 6. Posted AP/AR/cash documents with journal references but no posting event.
SELECT 'VendorInvoice' AS SourceDocumentType, Id, InvoiceNumber AS DocumentNumber, InvoiceDate AS DocumentDate, JournalEntryId
FROM VendorInvoices d
WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND CAST(d.InvoiceDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd
  AND d.JournalEntryId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM FinancePostingEvents e WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND e.SourceDocumentType = 'VendorInvoice' AND e.SourceDocumentId = d.Id AND e.PostingAction = 'Post' AND e.PostingStatus = 'Posted')
UNION ALL
SELECT 'VendorPayment', Id, PaymentNumber, PaymentDate, JournalEntryId
FROM VendorPayments d
WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND CAST(d.PaymentDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd
  AND d.JournalEntryId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM FinancePostingEvents e WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND e.SourceDocumentType = 'VendorPayment' AND e.SourceDocumentId = d.Id AND e.PostingAction = 'Post' AND e.PostingStatus = 'Posted')
UNION ALL
SELECT 'CustomerInvoice', Id, InvoiceNumber, InvoiceDate, JournalEntryId
FROM Invoices d
WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND CAST(d.InvoiceDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd
  AND d.JournalEntryId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM FinancePostingEvents e WHERE e.TenantId = @TenantId AND e.IsDeleted = 0 AND e.SourceDocumentType = 'CustomerInvoice' AND e.SourceDocumentId = d.Id AND e.PostingAction = 'Post' AND e.PostingStatus = 'Posted')
UNION ALL
SELECT CASE WHEN IsCreditNote = 1 THEN 'CustomerCreditNote' ELSE 'CustomerPayment' END, Id, PaymentNumber, PaymentDate, JournalEntryId
FROM CustomerPayments d
WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND CAST(d.PaymentDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd
  AND d.JournalEntryId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = @TenantId
        AND e.IsDeleted = 0
        AND e.SourceDocumentType = CASE WHEN d.IsCreditNote = 1 THEN 'CustomerCreditNote' ELSE 'CustomerPayment' END
        AND e.SourceDocumentId = d.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
  )
UNION ALL
SELECT CASE TransactionType WHEN 1 THEN 'CashBankReceipt' WHEN 2 THEN 'CashBankPayment' ELSE 'CashBankTransfer' END, Id, TransactionNumber, TransactionDate, JournalEntryId
FROM CashTransactions d
WHERE d.TenantId = @TenantId AND d.IsDeleted = 0 AND CAST(d.TransactionDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd
  AND d.IsPosted = 1
  AND d.JournalEntryId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM FinancePostingEvents e
      WHERE e.TenantId = @TenantId
        AND e.IsDeleted = 0
        AND e.SourceDocumentType = CASE d.TransactionType WHEN 1 THEN 'CashBankReceipt' WHEN 2 THEN 'CashBankPayment' ELSE 'CashBankTransfer' END
        AND e.SourceDocumentId = d.Id
        AND e.PostingAction = 'Post'
        AND e.PostingStatus = 'Posted'
  );

-- 7. Approved source documents still awaiting posting.
SELECT 'VendorInvoice' AS SourceDocumentType, Id, InvoiceNumber AS DocumentNumber, InvoiceDate AS DocumentDate, Status, ApprovalStatus
FROM VendorInvoices
WHERE TenantId = @TenantId AND IsDeleted = 0 AND CAST(InvoiceDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd AND Status = 3 AND JournalEntryId IS NULL
UNION ALL
SELECT 'VendorPayment', Id, PaymentNumber, PaymentDate, Status, NULL
FROM VendorPayments
WHERE TenantId = @TenantId AND IsDeleted = 0 AND CAST(PaymentDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd AND Status = 3 AND JournalEntryId IS NULL
UNION ALL
SELECT 'CustomerInvoice', Id, InvoiceNumber, InvoiceDate, Status, NULL
FROM Invoices
WHERE TenantId = @TenantId AND IsDeleted = 0 AND CAST(InvoiceDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd AND Status = 2 AND JournalEntryId IS NULL
UNION ALL
SELECT 'CashTransaction', Id, TransactionNumber, TransactionDate, ApprovalStatus, NULL
FROM CashTransactions
WHERE TenantId = @TenantId AND IsDeleted = 0 AND CAST(TransactionDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd AND ApprovalStatus = 3 AND IsPosted = 0;

-- 8. Unfinalized reconciliations in the period.
SELECT
    Id,
    BankAccountId,
    ReconciliationDate,
    StatementBalance,
    BookBalance,
    Difference,
    Status
FROM BankReconciliations
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND CAST(ReconciliationDate AS DATE) BETWEEN @PeriodStart AND @PeriodEnd
  AND Status NOT IN (3, 4, 6);

-- 9. Cross-tenant references to this period.
SELECT 'JournalEntry' AS RecordType, Id, TenantId, JournalEntryNumber AS Reference
FROM JournalEntries
WHERE FiscalPeriodId = @FiscalPeriodId AND TenantId <> @TenantId AND IsDeleted = 0
UNION ALL
SELECT 'AccountTransaction', Id, TenantId, TransactionNumber
FROM AccountTransactions
WHERE FiscalPeriodId = @FiscalPeriodId AND TenantId <> @TenantId AND IsDeleted = 0;
