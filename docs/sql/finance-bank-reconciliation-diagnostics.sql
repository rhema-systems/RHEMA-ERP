/*
Finance Bank Reconciliation Diagnostics

Run per tenant before go-live reconciliation sign-off.
Replace @TenantId with the tenant being reviewed.

These checks are read-only diagnostics. Any repair should be reviewed and signed off by Finance.
*/

DECLARE @TenantId uniqueidentifier = '00000000-0000-0000-0000-000000000000';

/* 1. Reconciliation matches linked across tenants. */
SELECT
    rm.Id AS ReconciliationMatchId,
    rm.TenantId AS MatchTenantId,
    r.TenantId AS ReconciliationTenantId,
    ct.TenantId AS CashTransactionTenantId,
    bsl.TenantId AS StatementLineTenantId,
    bs.TenantId AS StatementTenantId
FROM ReconciliationMatch rm
LEFT JOIN BankReconciliation r ON r.Id = rm.ReconciliationId
LEFT JOIN CashTransaction ct ON ct.Id = rm.CashTransactionId
LEFT JOIN BankStatementLine bsl ON bsl.Id = rm.BankStatementLineId
LEFT JOIN BankStatement bs ON bs.Id = bsl.BankStatementId
WHERE rm.TenantId = @TenantId
  AND COALESCE(rm.IsDeleted, 0) = 0
  AND (
      r.TenantId <> rm.TenantId
      OR ct.TenantId <> rm.TenantId
      OR bsl.TenantId <> rm.TenantId
      OR bs.TenantId <> rm.TenantId
  );

/* 2. Finalized or approved reconciliations where stored difference is not zero. */
SELECT
    r.Id AS ReconciliationId,
    r.BankAccountId,
    r.ReconciliationDate,
    r.Status,
    r.StatementBalance,
    r.BookBalance,
    r.Difference
FROM BankReconciliation r
WHERE r.TenantId = @TenantId
  AND COALESCE(r.IsDeleted, 0) = 0
  AND r.Status IN (3, 4)
  AND ABS(COALESCE(r.Difference, 0)) > 0.005;

/* 3. Statement lines matched to unposted or non-posted cash/bank transactions. */
SELECT
    rm.Id AS ReconciliationMatchId,
    rm.ReconciliationId,
    rm.CashTransactionId,
    ct.TransactionNumber,
    ct.IsPosted,
    ct.ApprovalStatus,
    ct.JournalEntryId,
    je.PostingStatus AS JournalPostingStatus,
    rm.BankStatementLineId
FROM ReconciliationMatch rm
JOIN CashTransaction ct ON ct.Id = rm.CashTransactionId
LEFT JOIN JournalEntries je ON je.Id = ct.JournalEntryId
WHERE rm.TenantId = @TenantId
  AND COALESCE(rm.IsDeleted, 0) = 0
  AND (
      ct.IsPosted <> 1
      OR ct.ApprovalStatus <> 7
      OR ct.JournalEntryId IS NULL
      OR je.Id IS NULL
      OR je.TenantId <> @TenantId
      OR je.PostingStatus <> 'Posted'
  );

/* 4. Cash/bank transactions reconciled more than once. */
SELECT
    rm.CashTransactionId,
    COUNT(*) AS MatchCount,
    STRING_AGG(CONVERT(varchar(36), rm.ReconciliationId), ',') AS ReconciliationIds
FROM ReconciliationMatch rm
WHERE rm.TenantId = @TenantId
  AND COALESCE(rm.IsDeleted, 0) = 0
GROUP BY rm.CashTransactionId
HAVING COUNT(*) > 1;

/* 5. Reconciliation adjustments missing posting events.
   Reconciliation adjustments are represented as posted cash/bank transactions with ReconciliationId populated and no statement-line match.
*/
SELECT
    ct.Id AS CashTransactionId,
    ct.ReconciliationId,
    ct.TransactionNumber,
    ct.TransactionType,
    ct.Amount,
    ct.JournalEntryId
FROM CashTransaction ct
LEFT JOIN ReconciliationMatch rm
    ON rm.TenantId = ct.TenantId
   AND rm.CashTransactionId = ct.Id
   AND COALESCE(rm.IsDeleted, 0) = 0
LEFT JOIN FinancePostingEvents fpe
    ON fpe.TenantId = ct.TenantId
   AND fpe.SourceDocumentId = ct.Id
   AND fpe.SourceDocumentType =
        CASE ct.TransactionType
            WHEN 1 THEN 'CashBankReceipt'
            WHEN 2 THEN 'CashBankPayment'
            WHEN 3 THEN 'CashBankTransfer'
            ELSE 'CashBankTransaction'
        END
   AND fpe.PostingAction = 'Post'
   AND fpe.PostingStatus = 'Posted'
   AND COALESCE(fpe.IsDeleted, 0) = 0
WHERE ct.TenantId = @TenantId
  AND COALESCE(ct.IsDeleted, 0) = 0
  AND ct.ReconciliationId IS NOT NULL
  AND rm.Id IS NULL
  AND ct.IsPosted = 1
  AND fpe.Id IS NULL;

/* 6. Posting events without same-tenant journal references. */
SELECT
    fpe.Id AS PostingEventId,
    fpe.SourceDocumentType,
    fpe.SourceDocumentId,
    fpe.JournalEntryId,
    je.TenantId AS JournalTenantId,
    je.PostingStatus AS JournalPostingStatus
FROM FinancePostingEvents fpe
LEFT JOIN JournalEntries je ON je.Id = fpe.JournalEntryId
WHERE fpe.TenantId = @TenantId
  AND COALESCE(fpe.IsDeleted, 0) = 0
  AND fpe.SourceModule = 'CASHBANK'
  AND (
      fpe.JournalEntryId IS NULL
      OR je.Id IS NULL
      OR je.TenantId <> fpe.TenantId
      OR je.PostingStatus <> 'Posted'
  );

/* 7. Stored bank snapshots that do not match posted GL after reconciliation activity. */
WITH PostedBankMovement AS (
    SELECT
        ba.Id AS BankAccountId,
        SUM(COALESCE(at.DebitAmount, 0) - COALESCE(at.CreditAmount, 0)) AS PostedMovement
    FROM BankAccounts ba
    LEFT JOIN AccountTransactions at
        ON at.TenantId = ba.TenantId
       AND at.AccountId = ba.GLAccountId
       AND at.PostingStatus = 'Posted'
       AND COALESCE(at.IsDeleted, 0) = 0
    LEFT JOIN JournalEntries je
        ON je.Id = at.JournalEntryId
       AND je.TenantId = ba.TenantId
       AND je.PostingStatus = 'Posted'
       AND COALESCE(je.IsDeleted, 0) = 0
    WHERE ba.TenantId = @TenantId
      AND COALESCE(ba.IsDeleted, 0) = 0
    GROUP BY ba.Id
)
SELECT
    ba.Id AS BankAccountId,
    ba.AccountNumber,
    ba.AccountName,
    ba.OpeningBalance,
    ba.CurrentBalance,
    ba.AvailableBalance,
    ExpectedBalance = COALESCE(ba.OpeningBalance, 0) + COALESCE(pbm.PostedMovement, 0),
    CurrentVariance = ba.CurrentBalance - (COALESCE(ba.OpeningBalance, 0) + COALESCE(pbm.PostedMovement, 0)),
    AvailableVariance = ba.AvailableBalance - (COALESCE(ba.OpeningBalance, 0) + COALESCE(pbm.PostedMovement, 0))
FROM BankAccounts ba
LEFT JOIN PostedBankMovement pbm ON pbm.BankAccountId = ba.Id
WHERE ba.TenantId = @TenantId
  AND COALESCE(ba.IsDeleted, 0) = 0
  AND (
      ABS(ba.CurrentBalance - (COALESCE(ba.OpeningBalance, 0) + COALESCE(pbm.PostedMovement, 0))) > 0.005
      OR ABS(ba.AvailableBalance - (COALESCE(ba.OpeningBalance, 0) + COALESCE(pbm.PostedMovement, 0))) > 0.005
  );
