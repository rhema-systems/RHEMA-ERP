/*
Final migration sign-off diagnostics.

These SQL Server diagnostics are read-only and tenant-scoped.
Replace @TenantId and @AsOfDate before running.
*/

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';
DECLARE @AsOfDate date = '2026-01-31';

-- 1. Posted GL trial balance imbalance.
SELECT
    SUM(DebitAmount) AS TotalDebit,
    SUM(CreditAmount) AS TotalCredit,
    SUM(DebitAmount - CreditAmount) AS Difference
FROM AccountTransactions
WHERE TenantId = @TenantId
  AND PostingStatus = 'Posted'
  AND IsDeleted = 0
  AND TransactionDate <= @AsOfDate;

-- 2. Opening-balance batches not posted.
SELECT Id, BatchNumber, Status, OpeningDate, TotalDebit, TotalCredit, Difference
FROM OpeningBalanceBatches
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND OpeningDate <= @AsOfDate
  AND Status <> 'Posted';

-- 3. Posted opening-balance batches missing references.
SELECT Id, BatchNumber, JournalEntryId, PostingEventId
FROM OpeningBalanceBatches
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND Status = 'Posted'
  AND (JournalEntryId IS NULL OR PostingEventId IS NULL);

-- 4. Duplicate opening-balance posting events.
SELECT SourceDocumentId, COUNT(*) AS PostedEventCount
FROM FinancePostingEvents
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND SourceDocumentType = 'OpeningBalanceBatch'
  AND PostingStatus = 'Posted'
GROUP BY SourceDocumentId
HAVING COUNT(*) > 1;

-- 5. Bank snapshot variance against posted GL.
SELECT
    b.Id AS BankAccountId,
    b.AccountNumber,
    b.AccountName,
    b.CurrentBalance AS StoredCurrentBalance,
    COALESCE(SUM(t.DebitAmount - t.CreditAmount), 0) AS PostedGlBalance,
    b.CurrentBalance - COALESCE(SUM(t.DebitAmount - t.CreditAmount), 0) AS Variance
FROM BankAccounts b
LEFT JOIN AccountTransactions t
    ON t.TenantId = b.TenantId
   AND t.AccountId = b.GLAccountId
   AND t.PostingStatus = 'Posted'
   AND t.IsDeleted = 0
   AND t.TransactionDate <= @AsOfDate
WHERE b.TenantId = @TenantId
  AND b.IsDeleted = 0
GROUP BY b.Id, b.AccountNumber, b.AccountName, b.CurrentBalance
HAVING b.CurrentBalance - COALESCE(SUM(t.DebitAmount - t.CreditAmount), 0) <> 0;

-- 6. Subledger settlement rows with diagnostics or missing posting references.
SELECT SourceModule, SourceDocumentType, SourceDocumentId, SourceDocumentNumber, HasDiagnostics, DiagnosticFlags, OperationalVariance, SourceJournalEntryId, SourcePostingEventId
FROM SubledgerSettlementBalances
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (HasDiagnostics = 1 OR OperationalVariance <> 0 OR SourceJournalEntryId IS NULL OR SourcePostingEventId IS NULL);

-- 7. Posted AP invoices missing settlement read-model rows.
SELECT vi.Id, vi.InvoiceNumber, vi.JournalEntryId
FROM VendorInvoices vi
LEFT JOIN SubledgerSettlementBalances b
    ON b.TenantId = vi.TenantId
   AND b.SourceModule = 'AP'
   AND b.SourceDocumentId = vi.Id
   AND b.IsDeleted = 0
WHERE vi.TenantId = @TenantId
  AND vi.IsDeleted = 0
  AND vi.JournalEntryId IS NOT NULL
  AND vi.InvoiceDate <= @AsOfDate
  AND b.Id IS NULL;

-- 8. Posted AR invoices missing settlement read-model rows.
SELECT i.Id, i.InvoiceNumber, i.JournalEntryId
FROM Invoices i
LEFT JOIN SubledgerSettlementBalances b
    ON b.TenantId = i.TenantId
   AND b.SourceModule = 'AR'
   AND b.SourceDocumentId = i.Id
   AND b.IsDeleted = 0
WHERE i.TenantId = @TenantId
  AND i.IsDeleted = 0
  AND i.JournalEntryId IS NOT NULL
  AND i.InvoiceDate <= @AsOfDate
  AND b.Id IS NULL;

-- 9. Fixed asset blocking diagnostics.
SELECT Id, AssetCode, Status, NetBookValue, JournalEntryId, PostingEventId
FROM FixedAssets
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (
      (CapitalizationDate IS NOT NULL AND (JournalEntryId IS NULL OR PostingEventId IS NULL))
      OR (Status IN (4, 6) AND NetBookValue <> 0)
  );

-- 10. Tax snapshots missing tax configuration.
SELECT c.Id, c.DocumentType, c.DocumentId, c.TaxId, c.TaxAmount
FROM TaxCalculations c
LEFT JOIN Taxes t
    ON t.TenantId = c.TenantId
   AND t.Id = c.TaxId
   AND t.IsDeleted = 0
WHERE c.TenantId = @TenantId
  AND c.IsDeleted = 0
  AND t.Id IS NULL;

-- 11. Current active COVID-19 levy configuration.
SELECT Id, Code, Name, Rate, EffectiveFrom, IsActive
FROM Taxes
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND IsActive = 1
  AND EffectiveFrom <= @AsOfDate
  AND (Code LIKE '%COVID%' OR Name LIKE '%COVID%');

-- 12. Pending workflow instances.
SELECT Id, WorkflowDefinitionId, EntityId, EntityTypeId, Status, CreatedDate, StartedDate
FROM WorkflowInstances
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND Status IN (0, 1);
