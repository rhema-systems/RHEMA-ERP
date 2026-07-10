/*
Finance Batch 3 cash/bank tenant-isolation migration preflight

Run this before applying migration 20260704100000_AddCashBankTenantIsolation
to production-like data.

Purpose:
- Detect duplicate bank account numbers that would violate
  IX_BankAccounts_TenantId_AccountNumber after default-tenant backfill.
- Detect duplicate cash transaction numbers that would violate
  IX_CashTransaction_TenantId_TransactionNumber after default-tenant backfill.
- Identify cash/bank rows that will be assigned to the seeded default tenant.
- Highlight tenant mapping assumptions that need accountant/data-owner sign-off.

Assumption:
- Existing cash/bank tables do not yet have TenantId before the migration.
- The migration backfills existing rows to the seeded default tenant:
  00000000-0000-0000-0000-000000000001.
*/

DECLARE @DefaultTenantId uniqueidentifier = '00000000-0000-0000-0000-000000000001';

PRINT 'Finance Batch 3 cash/bank tenant migration preflight';
PRINT 'Default tenant used for backfill:';
SELECT @DefaultTenantId AS DefaultTenantId;

PRINT '1. Duplicate bank account numbers after default-tenant backfill';
IF OBJECT_ID(N'dbo.BankAccounts', N'U') IS NOT NULL
BEGIN
    SELECT
        @DefaultTenantId AS AssumedTenantId,
        AccountNumber,
        COUNT(*) AS DuplicateCount,
        STRING_AGG(CONVERT(nvarchar(36), Id), ', ') AS BankAccountIds
    FROM dbo.BankAccounts
    WHERE IsDeleted = 0
    GROUP BY AccountNumber
    HAVING COUNT(*) > 1
    ORDER BY AccountNumber;
END
ELSE
BEGIN
    SELECT 'dbo.BankAccounts not found' AS DiagnosticMessage;
END

PRINT '2. Duplicate cash transaction numbers after default-tenant backfill';
IF OBJECT_ID(N'dbo.CashTransaction', N'U') IS NOT NULL
BEGIN
    SELECT
        @DefaultTenantId AS AssumedTenantId,
        TransactionNumber,
        COUNT(*) AS DuplicateCount,
        STRING_AGG(CONVERT(nvarchar(36), Id), ', ') AS CashTransactionIds
    FROM dbo.CashTransaction
    WHERE IsDeleted = 0
    GROUP BY TransactionNumber
    HAVING COUNT(*) > 1
    ORDER BY TransactionNumber;
END
ELSE
BEGIN
    SELECT 'dbo.CashTransaction not found' AS DiagnosticMessage;
END

PRINT '3. Cash/bank row counts that will be assigned to the default tenant';
SELECT 'BankAccounts' AS TableName, COUNT(*) AS RowsAssignedToDefaultTenant
FROM dbo.BankAccounts
WHERE OBJECT_ID(N'dbo.BankAccounts', N'U') IS NOT NULL
UNION ALL
SELECT 'CashTransaction', COUNT(*)
FROM dbo.CashTransaction
WHERE OBJECT_ID(N'dbo.CashTransaction', N'U') IS NOT NULL
UNION ALL
SELECT 'BankStatement', COUNT(*)
FROM dbo.BankStatement
WHERE OBJECT_ID(N'dbo.BankStatement', N'U') IS NOT NULL
UNION ALL
SELECT 'BankStatementLine', COUNT(*)
FROM dbo.BankStatementLine
WHERE OBJECT_ID(N'dbo.BankStatementLine', N'U') IS NOT NULL
UNION ALL
SELECT 'BankReconciliation', COUNT(*)
FROM dbo.BankReconciliation
WHERE OBJECT_ID(N'dbo.BankReconciliation', N'U') IS NOT NULL
UNION ALL
SELECT 'ReconciliationMatch', COUNT(*)
FROM dbo.ReconciliationMatch
WHERE OBJECT_ID(N'dbo.ReconciliationMatch', N'U') IS NOT NULL;

PRINT '4. Bank accounts requiring tenant ownership sign-off';
IF OBJECT_ID(N'dbo.BankAccounts', N'U') IS NOT NULL
BEGIN
    SELECT
        @DefaultTenantId AS AssumedTenantId,
        Id,
        AccountNumber,
        AccountName,
        BankName,
        Currency,
        CurrentBalance,
        OpeningDate,
        IsDeleted
    FROM dbo.BankAccounts
    ORDER BY AccountNumber, Id;
END

PRINT '5. Cash transactions requiring tenant ownership sign-off';
IF OBJECT_ID(N'dbo.CashTransaction', N'U') IS NOT NULL
BEGIN
    SELECT
        @DefaultTenantId AS AssumedTenantId,
        Id,
        TransactionNumber,
        TransactionDate,
        TransactionType,
        BankAccountId,
        ToBankAccountId,
        Amount,
        Currency,
        IsPosted,
        IsReconciled,
        IsDeleted
    FROM dbo.CashTransaction
    ORDER BY TransactionDate, TransactionNumber, Id;
END

PRINT '6. Cash transactions referencing missing bank accounts';
IF OBJECT_ID(N'dbo.CashTransaction', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.BankAccounts', N'U') IS NOT NULL
BEGIN
    SELECT
        ct.Id,
        ct.TransactionNumber,
        ct.BankAccountId,
        ct.ToBankAccountId
    FROM dbo.CashTransaction ct
    LEFT JOIN dbo.BankAccounts bankAccount
        ON bankAccount.Id = ct.BankAccountId
    LEFT JOIN dbo.BankAccounts toBankAccount
        ON toBankAccount.Id = ct.ToBankAccountId
    WHERE bankAccount.Id IS NULL
       OR (ct.ToBankAccountId IS NOT NULL AND toBankAccount.Id IS NULL);
END

PRINT '7. Reconciliation rows referencing missing bank accounts or cash transactions';
IF OBJECT_ID(N'dbo.BankReconciliation', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.BankAccounts', N'U') IS NOT NULL
BEGIN
    SELECT
        r.Id,
        r.BankAccountId,
        r.ReconciliationDate,
        r.Status
    FROM dbo.BankReconciliation r
    LEFT JOIN dbo.BankAccounts bankAccount
        ON bankAccount.Id = r.BankAccountId
    WHERE bankAccount.Id IS NULL;
END

IF OBJECT_ID(N'dbo.ReconciliationMatch', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.CashTransaction', N'U') IS NOT NULL
BEGIN
    SELECT
        m.Id,
        m.ReconciliationId,
        m.CashTransactionId,
        m.BankStatementLineId
    FROM dbo.ReconciliationMatch m
    LEFT JOIN dbo.CashTransaction ct
        ON ct.Id = m.CashTransactionId
    WHERE ct.Id IS NULL;
END

/*
Go/no-go guidance:
- Sections 1 and 2 must return zero rows before applying the migration.
- Sections 4 and 5 must be reviewed for tenant ownership. Any row that does not
  belong to the seeded default tenant needs a controlled data mapping plan before
  migration or a post-migration tenant reassignment script approved by Finance.
- Sections 6 and 7 must return zero rows or have documented remediation.
*/
