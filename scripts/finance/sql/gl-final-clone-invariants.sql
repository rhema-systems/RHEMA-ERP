SET NOCOUNT ON;
SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20260908120000_AddProducerIntentGroupsC8')
    THROW 51100, 'GLF001: final C8 migration is not applied.', 1;
IF OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NULL
   OR OBJECT_ID(N'dbo.AccountingEventProducerReceipts', N'U') IS NULL
   OR OBJECT_ID(N'dbo.ProducerIntentGroups', N'U') IS NULL
    THROW 51101, 'GLF002: C6-C8 schema is incomplete.', 1;

IF EXISTS (
    SELECT 1
    FROM dbo.AccountAccountingBooks m
    JOIN dbo.Accounts a ON a.Id=m.AccountId
    JOIN dbo.AccountingBooks b ON b.Id=m.AccountingBookId
    LEFT JOIN dbo.AccountClassifications c ON c.Id=m.AccountClassificationId
    WHERE m.IsDeleted=0 AND m.IsEnabled=1 AND
      (a.Id IS NULL OR a.IsDeleted=1 OR b.Id IS NULL OR b.IsDeleted=1 OR b.IsActive=0 OR b.AllowsPosting=0 OR
       m.TenantId<>a.TenantId OR m.TenantId<>b.TenantId OR c.Id IS NULL OR c.IsDeleted=1 OR c.Status<>2 OR
       c.IsPostingClassification=0 OR c.TenantId<>m.TenantId OR c.AccountingBookId<>m.AccountingBookId OR
       c.CoreAccountType<>a.AccountType))
    THROW 51102, 'GLF003: enabled account/book mapping has invalid lineage.', 1;
IF EXISTS (
    SELECT TenantId,AccountingBookId,SystemRole FROM dbo.AccountClassifications
    WHERE IsDeleted=0 AND SystemRole IS NOT NULL AND SystemRole NOT IN (1,2)
    GROUP BY TenantId,AccountingBookId,SystemRole HAVING COUNT_BIG(*)>1)
    THROW 51103, 'GLF004: singleton classification role occurs more than once.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.AccountSegmentValues v
    JOIN dbo.Accounts a ON a.Id=v.AccountId
    JOIN dbo.AccountSegmentStructures s ON s.Id=v.SegmentStructureId
    LEFT JOIN dbo.SegmentLookupValues l ON l.Id=v.SegmentLookupValueId
    WHERE v.IsDeleted=0 AND (a.IsDeleted=1 OR s.IsDeleted=1 OR v.TenantId<>a.TenantId OR
      v.TenantId<>s.TenantId OR v.SegmentPosition<>s.SegmentPosition OR
      (v.SegmentLookupValueId IS NOT NULL AND (l.Id IS NULL OR l.IsDeleted=1 OR
       l.TenantId<>v.TenantId OR l.SegmentStructureId<>v.SegmentStructureId))))
    THROW 51104, 'GLF005: account segment value has invalid lineage.', 1;

-- Surrogate IDs and audit timestamps are excluded. These rows are deliberately ordered so the
-- two seed passes can be compared byte-for-byte without erasing pre-existing business evidence.
SELECT N'MIGRATION|' + MigrationId + N'|' + ProductVersion
FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
SELECT N'BOOK|' + CONVERT(nvarchar(36),TenantId) + N'|' + Code + N'|' + Name + N'|' +
       CONVERT(nvarchar(1),IsActive) + N'|' + CONVERT(nvarchar(1),IsDefault) + N'|' + CONVERT(nvarchar(1),AllowsPosting)
FROM dbo.AccountingBooks WHERE IsDeleted=0 ORDER BY TenantId,Code;
SELECT N'CLASS|' + CONVERT(nvarchar(36),c.TenantId) + N'|' + b.Code + N'|' + c.Code + N'|' + c.Name + N'|' +
       CONVERT(nvarchar(10),c.CoreAccountType) + N'|' + CONVERT(nvarchar(10),c.DefaultRevaluationTreatment) + N'|' +
       COALESCE(CONVERT(nvarchar(10),c.SystemRole),N'') + N'|' + CONVERT(nvarchar(1),c.IsPostingClassification) + N'|' + CONVERT(nvarchar(10),c.Status)
FROM dbo.AccountClassifications c JOIN dbo.AccountingBooks b ON b.Id=c.AccountingBookId
WHERE c.IsDeleted=0 ORDER BY c.TenantId,b.Code,c.Code;
SELECT N'MAP|' + CONVERT(nvarchar(36),m.TenantId) + N'|' + a.AccountCode + N'|' + b.Code + N'|' +
       COALESCE(c.Code,N'') + N'|' + CONVERT(nvarchar(1),m.IsEnabled)
FROM dbo.AccountAccountingBooks m
JOIN dbo.Accounts a ON a.Id=m.AccountId
JOIN dbo.AccountingBooks b ON b.Id=m.AccountingBookId
LEFT JOIN dbo.AccountClassifications c ON c.Id=m.AccountClassificationId
WHERE m.IsDeleted=0 ORDER BY m.TenantId,a.AccountCode,b.Code;
SELECT N'SEGMENT|' + CONVERT(nvarchar(36),TenantId) + N'|' + SegmentCode + N'|' +
       CONVERT(nvarchar(10),SegmentPosition) + N'|' + CONVERT(nvarchar(10),SegmentLength) + N'|' +
       DataType + N'|' + CONVERT(nvarchar(10),LifecycleStatus)
FROM dbo.AccountSegmentStructures WHERE IsDeleted=0 ORDER BY TenantId,SegmentPosition,SegmentCode;
SELECT N'DIMENSION|' + CONVERT(nvarchar(36),TenantId) + N'|' + Code + N'|' + Name + N'|' +
       CONVERT(nvarchar(1),IsActive) + N'|' + CONVERT(nvarchar(10),DisplayOrder)
FROM dbo.FinanceDimensionDefinitions WHERE IsDeleted=0 ORDER BY TenantId,Code;
SELECT N'CONTROL_COUNTS|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.JournalEntries WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountTransactions WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.FinancePostingEvents WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountingEvents WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.AccountingEventProducerReceipts WHERE IsDeleted=0)) + N'|' +
       CONVERT(nvarchar(30),(SELECT COUNT_BIG(*) FROM dbo.ProducerIntentGroups WHERE IsDeleted=0));
