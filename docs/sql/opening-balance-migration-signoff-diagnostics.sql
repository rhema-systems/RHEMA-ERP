/*
Opening-Balance and Migration Sign-Off Diagnostics

Provider: SQL Server
Purpose: accountant-reviewable diagnostics for controlled opening-balance posting,
posting back-reference repair, bank snapshot variance, and subledger opening scope.
*/

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

/* 1. Opening-balance batches not posted or failed */
SELECT
    b.Id AS OpeningBalanceBatchId,
    b.BatchNumber,
    b.OpeningDate,
    b.Status,
    b.TotalDebit,
    b.TotalCredit,
    b.Difference,
    b.FailureReason,
    b.JournalEntryId,
    b.PostingEventId
FROM dbo.OpeningBalanceBatches b
WHERE b.TenantId = @TenantId
  AND b.IsDeleted = 0
  AND b.Status IN (N'Draft', N'Validated', N'PendingApproval', N'Approved', N'Failed');

/* 2. Posted opening-balance batches missing journal/posting-event references */
SELECT
    b.Id AS OpeningBalanceBatchId,
    b.BatchNumber,
    b.Status,
    b.JournalEntryId,
    b.PostingEventId
FROM dbo.OpeningBalanceBatches b
WHERE b.TenantId = @TenantId
  AND b.IsDeleted = 0
  AND b.Status = N'Posted'
  AND (b.JournalEntryId IS NULL OR b.PostingEventId IS NULL);

/* 3. Duplicate posted opening-balance posting events */
SELECT
    e.SourceDocumentId AS OpeningBalanceBatchId,
    COUNT(*) AS PostedEventCount
FROM dbo.FinancePostingEvents e
WHERE e.TenantId = @TenantId
  AND e.IsDeleted = 0
  AND e.SourceDocumentType = N'OpeningBalanceBatch'
  AND e.PostingStatus = N'Posted'
GROUP BY e.SourceDocumentId
HAVING COUNT(*) > 1;

/* 4. Opening-balance lines referencing missing or cross-tenant accounts */
SELECT
    l.Id AS OpeningBalanceLineId,
    l.OpeningBalanceBatchId,
    l.LineNumber,
    l.AccountId,
    a.TenantId AS AccountTenantId
FROM dbo.OpeningBalanceLines l
LEFT JOIN dbo.Accounts a ON a.Id = l.AccountId AND a.IsDeleted = 0
WHERE l.TenantId = @TenantId
  AND l.IsDeleted = 0
  AND (a.Id IS NULL OR a.TenantId <> l.TenantId);

/* 5. Bank stored snapshot variance versus posted GL movement */
SELECT
    b.Id AS BankAccountId,
    b.AccountNumber,
    b.AccountName,
    b.GLAccountId,
    b.CurrentBalance AS StoredCurrentBalance,
    b.AvailableBalance AS StoredAvailableBalance,
    b.OpeningBalance AS StoredOpeningBalance,
    COALESCE(SUM(t.DebitAmount - t.CreditAmount), 0) AS PostedGlBalance,
    b.CurrentBalance - COALESCE(SUM(t.DebitAmount - t.CreditAmount), 0) AS CurrentBalanceVariance
FROM dbo.BankAccounts b
LEFT JOIN dbo.AccountTransactions t
    ON t.TenantId = b.TenantId
   AND t.AccountId = b.GLAccountId
   AND t.PostingStatus = N'Posted'
   AND t.IsDeleted = 0
WHERE b.TenantId = @TenantId
  AND b.IsDeleted = 0
GROUP BY
    b.Id,
    b.AccountNumber,
    b.AccountName,
    b.GLAccountId,
    b.CurrentBalance,
    b.AvailableBalance,
    b.OpeningBalance
HAVING b.CurrentBalance <> COALESCE(SUM(t.DebitAmount - t.CreditAmount), 0)
    OR b.AvailableBalance <> COALESCE(SUM(t.DebitAmount - t.CreditAmount), 0);

/* 6. Posted AP invoices with posting event but missing source journal back-reference */
SELECT
    i.Id AS VendorInvoiceId,
    i.InvoiceNumber,
    i.JournalEntryId AS SourceJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS EventJournalEntryId
FROM dbo.VendorInvoices i
JOIN dbo.FinancePostingEvents e
    ON e.TenantId = i.TenantId
   AND e.SourceDocumentType = N'VendorInvoice'
   AND e.SourceDocumentId = i.Id
   AND e.PostingStatus = N'Posted'
   AND e.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND i.JournalEntryId IS NULL;

/* 7. Posted AR invoices with posting event but missing source journal back-reference */
SELECT
    i.Id AS CustomerInvoiceId,
    i.InvoiceNumber,
    i.JournalEntryId AS SourceJournalEntryId,
    e.Id AS PostingEventId,
    e.JournalEntryId AS EventJournalEntryId
FROM dbo.Invoices i
JOIN dbo.FinancePostingEvents e
    ON e.TenantId = i.TenantId
   AND e.SourceDocumentType = N'CustomerInvoice'
   AND e.SourceDocumentId = i.Id
   AND e.PostingStatus = N'Posted'
   AND e.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND i.JournalEntryId IS NULL;

/* 8. Posted opening-balance GL lines without source batch reference */
SELECT
    t.Id AS AccountTransactionId,
    t.JournalEntryId,
    t.AccountId,
    t.DebitAmount,
    t.CreditAmount,
    t.SourceDocumentType,
    t.SourceDocumentId
FROM dbo.AccountTransactions t
LEFT JOIN dbo.OpeningBalanceBatches b
    ON b.TenantId = t.TenantId
   AND b.Id = t.SourceDocumentId
   AND b.IsDeleted = 0
WHERE t.TenantId = @TenantId
  AND t.IsDeleted = 0
  AND t.PostingStatus = N'Posted'
  AND t.SourceDocumentType = N'OpeningBalanceBatch'
  AND b.Id IS NULL;

/* 9. GL-only opening balances are not AP/AR/fixed-asset subledger openings */
SELECT
    N'FIN-LIM-0048' AS LimitationId,
    N'GL-only opening balances do not create AP invoices, AR invoices, or fixed asset register records. Load those through supported posted source-document/import flows when production cutover needs subledger aging/register sign-off.' AS DiagnosticMessage;
