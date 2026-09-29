SET XACT_ABORT ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF DB_NAME() <> N'RhemaERP'
    THROW 59100, 'This schema repair is restricted to the local RhemaERP test database.', 1;

IF EXISTS (
    SELECT 1 FROM JournalEntries j
    LEFT JOIN AccountingBooks b ON b.TenantId = j.TenantId
        AND b.Code = j.BookClassification AND b.IsDeleted = 0
    WHERE b.Id IS NULL)
    THROW 59101, 'A journal has no matching tenant accounting book.', 1;

IF EXISTS (
    SELECT 1 FROM AccountTransactions a
    LEFT JOIN JournalEntries j ON j.Id = a.JournalEntryId AND j.TenantId = a.TenantId
    WHERE j.Id IS NULL OR a.BookClassification <> j.BookClassification)
    THROW 59102, 'A transaction has no matching journal or book classification.', 1;

IF COL_LENGTH('dbo.JournalEntries', 'AccountingBookId') IS NULL
    ALTER TABLE dbo.JournalEntries ADD AccountingBookId uniqueidentifier NULL;
IF COL_LENGTH('dbo.AccountTransactions', 'AccountingBookId') IS NULL
    ALTER TABLE dbo.AccountTransactions ADD AccountingBookId uniqueidentifier NULL;

EXEC sys.sp_executesql N'
    UPDATE j SET AccountingBookId = b.Id
    FROM dbo.JournalEntries j
    JOIN dbo.AccountingBooks b ON b.TenantId = j.TenantId
        AND b.Code = j.BookClassification AND b.IsDeleted = 0
    WHERE j.AccountingBookId IS NULL;

    UPDATE a SET AccountingBookId = j.AccountingBookId
    FROM dbo.AccountTransactions a
    JOIN dbo.JournalEntries j ON j.Id = a.JournalEntryId AND j.TenantId = a.TenantId
    WHERE a.AccountingBookId IS NULL;

    IF EXISTS (SELECT 1 FROM dbo.JournalEntries WHERE AccountingBookId IS NULL)
        THROW 59103, ''A journal could not be assigned to an accounting book.'', 1;
    IF EXISTS (SELECT 1 FROM dbo.AccountTransactions WHERE AccountingBookId IS NULL)
        THROW 59104, ''A transaction could not be assigned to an accounting book.'', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.AccountTransactions a
        JOIN dbo.JournalEntries j ON j.Id = a.JournalEntryId AND j.TenantId = a.TenantId
        WHERE a.AccountingBookId <> j.AccountingBookId)
        THROW 59105, ''A transaction and its journal disagree on accounting book.'', 1;
';

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.JournalEntries')
    AND name = 'AccountingBookId' AND is_nullable = 1)
    ALTER TABLE dbo.JournalEntries ALTER COLUMN AccountingBookId uniqueidentifier NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountTransactions')
    AND name = 'AccountingBookId' AND is_nullable = 1)
    ALTER TABLE dbo.AccountTransactions ALTER COLUMN AccountingBookId uniqueidentifier NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.AccountingBooks')
    AND name = 'AK_AccountingBooks_TenantId_Id')
    CREATE UNIQUE INDEX AK_AccountingBooks_TenantId_Id
        ON dbo.AccountingBooks(TenantId, Id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.JournalEntries')
    AND name = 'AK_JournalEntries_TenantId_Id_AccountingBookId')
    CREATE UNIQUE INDEX AK_JournalEntries_TenantId_Id_AccountingBookId
        ON dbo.JournalEntries(TenantId, Id, AccountingBookId);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FacilitiesTest_JournalEntries_AccountingBooks')
    ALTER TABLE dbo.JournalEntries WITH CHECK ADD CONSTRAINT FK_FacilitiesTest_JournalEntries_AccountingBooks
        FOREIGN KEY (TenantId, AccountingBookId) REFERENCES dbo.AccountingBooks(TenantId, Id);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FacilitiesTest_AccountTransactions_JournalEntries_Book')
    ALTER TABLE dbo.AccountTransactions WITH CHECK ADD CONSTRAINT FK_FacilitiesTest_AccountTransactions_JournalEntries_Book
        FOREIGN KEY (TenantId, JournalEntryId, AccountingBookId)
        REFERENCES dbo.JournalEntries(TenantId, Id, AccountingBookId);

COMMIT TRANSACTION;

EXEC sys.sp_executesql N'
    SELECT N''JournalEntries'' AS TableName, COUNT(*) AS RowsWithBook
    FROM dbo.JournalEntries WHERE AccountingBookId IS NOT NULL
    UNION ALL
    SELECT N''AccountTransactions'', COUNT(*)
    FROM dbo.AccountTransactions WHERE AccountingBookId IS NOT NULL;
';
