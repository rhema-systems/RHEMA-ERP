-- Read-only Up preconditions for 20260929102006_JournalBatchStableAccountingBook.
-- Mirrors both executable THROW guards before AccountingBookId is added.
SET NOCOUNT ON;

DECLARE @Checks TABLE (CheckName nvarchar(300) NOT NULL, AffectedRows bigint NOT NULL);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'JournalBatchBook.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @Applied bit = 0;
EXEC sys.sp_executesql
    N'SELECT @Applied = CASE WHEN EXISTS
      (SELECT 1 FROM dbo.__EFMigrationsHistory
       WHERE MigrationId = N''20260929102006_JournalBatchStableAccountingBook'')
      THEN 1 ELSE 0 END;',
    N'@Applied bit OUTPUT', @Applied = @Applied OUTPUT;

IF @Applied = 0
BEGIN
    DECLARE @RequiredTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT @RequiredTables VALUES
        (N'JournalBatches'),(N'JournalBatchItems'),(N'JournalEntries'),(N'AccountingBooks');

    INSERT @Checks
    SELECT N'JournalBatchBook.RequiredTableMissing:' + TableName, 1
    FROM @RequiredTables
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U') IS NULL;

    IF COL_LENGTH(N'dbo.JournalBatches', N'AccountingBookId') IS NOT NULL
        INSERT @Checks VALUES(N'JournalBatchBook.UnexpectedPendingSchema:JournalBatches.AccountingBookId', 1);

    IF OBJECT_ID(N'dbo.JournalBatches', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.AccountingBooks', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatches', N'TenantId') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatches', N'BookClassification') IS NOT NULL
       AND COL_LENGTH(N'dbo.AccountingBooks', N'TenantId') IS NOT NULL
       AND COL_LENGTH(N'dbo.AccountingBooks', N'Code') IS NOT NULL
        INSERT @Checks EXEC sys.sp_executesql N'
            SELECT N''JournalBatchBook.UnresolvedOrAmbiguousClassification'',COUNT_BIG(*)
            FROM dbo.JournalBatches batch
            WHERE (SELECT COUNT_BIG(*) FROM dbo.AccountingBooks book
                   WHERE book.TenantId=batch.TenantId
                     AND book.Code=batch.BookClassification)<>1;';

    IF OBJECT_ID(N'dbo.JournalBatches', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.JournalBatchItems', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.JournalEntries', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.AccountingBooks', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatches', N'TenantId') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatches', N'BookClassification') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatches', N'IsDeleted') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatchItems', N'JournalBatchId') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatchItems', N'JournalEntryId') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalBatchItems', N'IsDeleted') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalEntries', N'AccountingBookId') IS NOT NULL
       AND COL_LENGTH(N'dbo.JournalEntries', N'IsDeleted') IS NOT NULL
        INSERT @Checks EXEC sys.sp_executesql N'
            SELECT N''JournalBatchBook.AttachedJournalBookMismatch'',COUNT_BIG(*)
            FROM dbo.JournalBatchItems item
            INNER JOIN dbo.JournalBatches batch ON batch.Id=item.JournalBatchId
            INNER JOIN dbo.JournalEntries journalEntry ON journalEntry.Id=item.JournalEntryId
            CROSS APPLY
            (SELECT TOP (1) book.Id FROM dbo.AccountingBooks book
             WHERE book.TenantId=batch.TenantId AND book.Code=batch.BookClassification
             ORDER BY book.Id) resolved
            WHERE item.IsDeleted=0 AND batch.IsDeleted=0 AND journalEntry.IsDeleted=0
              AND 1=(SELECT COUNT_BIG(*) FROM dbo.AccountingBooks candidate
                     WHERE candidate.TenantId=batch.TenantId
                       AND candidate.Code=batch.BookClassification)
              AND journalEntry.AccountingBookId<>resolved.Id;';
END;

SELECT CheckName,AffectedRows FROM @Checks WHERE AffectedRows>0 ORDER BY CheckName;
