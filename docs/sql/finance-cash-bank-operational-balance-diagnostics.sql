/*
  Cash/Bank Operational Balance Diagnostics

  Purpose:
  - Treat BankAccounts.CurrentBalance and AvailableBalance as rebuildable read-side snapshots.
  - Compare stored snapshots to posted GL activity for the bank account's linked GL account.
  - Identify unposted cash/bank transactions that may explain legacy snapshot drift.
  - Detect cross-tenant posting/journal references and inconsistent transfer legs.

  Usage:
  - Set @TenantId before running.
  - Run before bank reconciliation migration and before go-live sign-off.
  - Accountant review is required before any repair/update is executed.
*/

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

/* 1. Stored bank snapshots that do not match posted GL for the linked bank GL account. */
WITH PostedGl AS
(
    SELECT
        ba.Id AS BankAccountId,
        SUM(COALESCE(at.DebitAmount, 0) - COALESCE(at.CreditAmount, 0)) AS PostedGlBalance
    FROM dbo.BankAccounts ba
    LEFT JOIN dbo.AccountTransactions at
        ON at.TenantId = ba.TenantId
       AND at.AccountId = ba.GLAccountId
       AND at.PostingStatus = 'Posted'
       AND COALESCE(at.IsDeleted, 0) = 0
    WHERE ba.TenantId = @TenantId
      AND COALESCE(ba.IsDeleted, 0) = 0
    GROUP BY ba.Id
)
SELECT
    ba.Id AS BankAccountId,
    ba.AccountNumber,
    ba.AccountName,
    ba.GLAccountId,
    ba.OpeningBalance,
    ba.CurrentBalance,
    ba.AvailableBalance,
    COALESCE(pg.PostedGlBalance, 0) AS PostedGlBalance,
    ba.CurrentBalance - COALESCE(pg.PostedGlBalance, 0) AS CurrentBalanceVariance,
    ba.AvailableBalance - COALESCE(pg.PostedGlBalance, 0) AS AvailableBalanceVariance
FROM dbo.BankAccounts ba
LEFT JOIN PostedGl pg ON pg.BankAccountId = ba.Id
WHERE ba.TenantId = @TenantId
  AND COALESCE(ba.IsDeleted, 0) = 0
  AND (
        ba.CurrentBalance <> COALESCE(pg.PostedGlBalance, 0)
     OR ba.AvailableBalance <> COALESCE(pg.PostedGlBalance, 0)
  )
ORDER BY ba.AccountNumber;

/* 2. Unposted cash/bank transactions on accounts with snapshot variances. */
WITH PostedGl AS
(
    SELECT
        ba.Id AS BankAccountId,
        SUM(COALESCE(at.DebitAmount, 0) - COALESCE(at.CreditAmount, 0)) AS PostedGlBalance
    FROM dbo.BankAccounts ba
    LEFT JOIN dbo.AccountTransactions at
        ON at.TenantId = ba.TenantId
       AND at.AccountId = ba.GLAccountId
       AND at.PostingStatus = 'Posted'
       AND COALESCE(at.IsDeleted, 0) = 0
    WHERE ba.TenantId = @TenantId
      AND COALESCE(ba.IsDeleted, 0) = 0
    GROUP BY ba.Id
),
MismatchedAccounts AS
(
    SELECT ba.Id
    FROM dbo.BankAccounts ba
    LEFT JOIN PostedGl pg ON pg.BankAccountId = ba.Id
    WHERE ba.TenantId = @TenantId
      AND COALESCE(ba.IsDeleted, 0) = 0
      AND (
            ba.CurrentBalance <> COALESCE(pg.PostedGlBalance, 0)
         OR ba.AvailableBalance <> COALESCE(pg.PostedGlBalance, 0)
      )
)
SELECT
    ct.Id AS CashTransactionId,
    ct.TransactionNumber,
    ct.TransactionDate,
    ct.TransactionType,
    ct.ApprovalStatus,
    ct.IsPosted,
    ct.JournalEntryId,
    ct.BankAccountId,
    ct.ToBankAccountId,
    ct.Amount,
    ct.Currency,
    ct.BaseAmount
FROM dbo.CashTransaction ct
JOIN MismatchedAccounts ma
    ON ma.Id = ct.BankAccountId
    OR ma.Id = ct.ToBankAccountId
WHERE ct.TenantId = @TenantId
  AND COALESCE(ct.IsDeleted, 0) = 0
  AND COALESCE(ct.IsPosted, 0) = 0
ORDER BY ct.TransactionDate, ct.TransactionNumber;

/* 3. Posted cash/bank transactions with posting events but missing or inconsistent snapshot effect. */
SELECT
    ct.Id AS CashTransactionId,
    ct.TransactionNumber,
    ct.TransactionType,
    ct.BankAccountId,
    ct.ToBankAccountId,
    ct.Amount,
    ct.JournalEntryId,
    fpe.Id AS FinancePostingEventId,
    fpe.JournalEntryId AS PostingEventJournalEntryId,
    fpe.PostingStatus
FROM dbo.CashTransaction ct
LEFT JOIN dbo.FinancePostingEvents fpe
    ON fpe.TenantId = ct.TenantId
   AND fpe.SourceDocumentId = ct.Id
   AND fpe.PostingAction = 'Post'
   AND fpe.PostingStatus = 'Posted'
   AND COALESCE(fpe.IsDeleted, 0) = 0
WHERE ct.TenantId = @TenantId
  AND COALESCE(ct.IsDeleted, 0) = 0
  AND COALESCE(ct.IsPosted, 0) = 1
  AND (
        fpe.Id IS NULL
     OR ct.JournalEntryId IS NULL
     OR ct.JournalEntryId <> fpe.JournalEntryId
  )
ORDER BY ct.TransactionDate, ct.TransactionNumber;

/* 4. Cross-tenant journal/posting event links that must be repaired before reconciliation. */
SELECT
    ct.Id AS CashTransactionId,
    ct.TenantId AS CashTransactionTenantId,
    ct.TransactionNumber,
    ct.JournalEntryId,
    je.TenantId AS JournalTenantId,
    fpe.Id AS FinancePostingEventId,
    fpe.TenantId AS PostingEventTenantId
FROM dbo.CashTransaction ct
LEFT JOIN dbo.JournalEntries je
    ON je.Id = ct.JournalEntryId
LEFT JOIN dbo.FinancePostingEvents fpe
    ON fpe.SourceDocumentId = ct.Id
   AND fpe.SourceDocumentType IN ('CashBankReceipt', 'CashBankPayment', 'CashBankTransfer')
   AND fpe.PostingAction = 'Post'
   AND COALESCE(fpe.IsDeleted, 0) = 0
WHERE ct.TenantId = @TenantId
  AND COALESCE(ct.IsDeleted, 0) = 0
  AND (
        (je.Id IS NOT NULL AND je.TenantId <> ct.TenantId)
     OR (fpe.Id IS NOT NULL AND fpe.TenantId <> ct.TenantId)
  );

/* 5. Transfer source/destination legs not consistently reflected. */
SELECT
    src.Id AS SourceCashTransactionId,
    src.TransactionNumber AS SourceTransactionNumber,
    dest.Id AS DestinationCashTransactionId,
    dest.TransactionNumber AS DestinationTransactionNumber,
    src.BankAccountId AS SourceBankAccountId,
    src.ToBankAccountId AS ExpectedDestinationBankAccountId,
    dest.BankAccountId AS DestinationBankAccountId,
    src.Amount AS SourceAmount,
    dest.Amount AS DestinationAmount,
    src.IsPosted AS SourceIsPosted,
    dest.IsPosted AS DestinationIsPosted,
    src.JournalEntryId AS SourceJournalEntryId,
    dest.JournalEntryId AS DestinationJournalEntryId
FROM dbo.CashTransaction src
LEFT JOIN dbo.CashTransaction dest
    ON dest.TenantId = src.TenantId
   AND dest.TransactionType = src.TransactionType
   AND dest.BankAccountId = src.ToBankAccountId
   AND dest.ToBankAccountId = src.BankAccountId
   AND COALESCE(dest.IsDeleted, 0) = 0
WHERE src.TenantId = @TenantId
  AND COALESCE(src.IsDeleted, 0) = 0
  AND src.TransactionType = 3
  AND src.TransactionNumber LIKE '%-OUT'
  AND (
        dest.Id IS NULL
     OR src.Amount <> dest.Amount
     OR src.IsPosted <> dest.IsPosted
     OR COALESCE(src.JournalEntryId, '00000000-0000-0000-0000-000000000000')
        <> COALESCE(dest.JournalEntryId, '00000000-0000-0000-0000-000000000000')
  )
ORDER BY src.TransactionDate, src.TransactionNumber;
