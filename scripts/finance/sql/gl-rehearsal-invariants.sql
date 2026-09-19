SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DefaultTenant uniqueidentifier;
IF (SELECT COUNT_BIG(*) FROM dbo.Tenants WHERE Code=N'DEFAULT' AND IsDeleted=0 AND Status=1) <> 1
    THROW 51000, 'GLR001: expected exactly one active DEFAULT tenant.', 1;
SELECT @DefaultTenant=Id FROM dbo.Tenants WHERE Code=N'DEFAULT' AND IsDeleted=0 AND Status=1;

IF (SELECT COUNT_BIG(*) FROM dbo.AccountingBooks WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND Code IN (N'IFRS',N'LOCAL_STATUTORY',N'MANAGEMENT')) <> 3
    THROW 51001, 'GLR002: canonical accounting books are missing or duplicated.', 1;
IF (SELECT COUNT_BIG(*) FROM dbo.AccountingBooks WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND IsActive=1 AND IsDefault=1) <> 1
    THROW 51002, 'GLR003: expected exactly one active default accounting book.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.AccountingBooks WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND IsActive=1 AND IsDefault=1 AND Code=N'IFRS')
    THROW 51003, 'GLR004: IFRS must be the seeded default book.', 1;
IF EXISTS (SELECT TenantId,Code FROM dbo.AccountingBooks WHERE IsDeleted=0 GROUP BY TenantId,Code HAVING COUNT_BIG(*)>1)
    THROW 51004, 'GLR005: duplicate live accounting-book code.', 1;

IF EXISTS (
    SELECT b.Id
    FROM dbo.AccountingBooks b
    LEFT JOIN dbo.AccountClassifications c ON c.AccountingBookId=b.Id AND c.TenantId=b.TenantId AND c.IsDeleted=0
    WHERE b.TenantId=@DefaultTenant AND b.IsDeleted=0 AND b.Code IN (N'IFRS',N'LOCAL_STATUTORY',N'MANAGEMENT')
    GROUP BY b.Id HAVING COUNT(c.Id)<>29)
    THROW 51005, 'GLR006: each canonical book must contain the 29-code classification manifest.', 1;
IF EXISTS (SELECT TenantId,AccountingBookId,Code FROM dbo.AccountClassifications WHERE IsDeleted=0 GROUP BY TenantId,AccountingBookId,Code HAVING COUNT_BIG(*)>1)
    THROW 51006, 'GLR007: duplicate live classification stable code.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.AccountClassifications c
    JOIN dbo.AccountClassifications p ON p.Id=c.ParentClassificationId
    WHERE c.IsDeleted=0 AND (p.IsDeleted=1 OR p.TenantId<>c.TenantId OR p.AccountingBookId<>c.AccountingBookId OR p.CoreAccountType<>c.CoreAccountType))
    THROW 51007, 'GLR008: invalid classification parent lineage.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.AccountClassifications c
    WHERE c.IsDeleted=0 AND c.IsPostingClassification=1
      AND EXISTS (SELECT 1 FROM dbo.AccountClassifications child WHERE child.ParentClassificationId=c.Id AND child.IsDeleted=0 AND child.Status<>3))
    THROW 51008, 'GLR009: a posting classification has a live child.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.AccountClassifications root
    CROSS APPLY (SELECT COUNT_BIG(*) AS Ancestors FROM dbo.AccountClassifications p WHERE p.Id=root.ParentClassificationId AND p.IsDeleted=0) directParent
    WHERE root.IsDeleted=0 AND root.ParentClassificationId IS NOT NULL AND directParent.Ancestors<>1)
    THROW 51009, 'GLR010: classification hierarchy contains an orphan.', 1;

IF EXISTS (
    SELECT 1 FROM dbo.AccountAccountingBooks m
    JOIN dbo.Accounts a ON a.Id=m.AccountId
    JOIN dbo.AccountingBooks b ON b.Id=m.AccountingBookId
    LEFT JOIN dbo.AccountClassifications c ON c.Id=m.AccountClassificationId
    WHERE m.IsDeleted=0 AND m.IsEnabled=1 AND
      (a.IsDeleted=1 OR b.IsDeleted=1 OR b.IsActive=0 OR b.AllowsPosting=0 OR
       m.TenantId<>a.TenantId OR m.TenantId<>b.TenantId OR c.Id IS NULL OR c.IsDeleted=1 OR c.Status<>2 OR
       c.IsPostingClassification=0 OR c.TenantId<>m.TenantId OR c.AccountingBookId<>m.AccountingBookId OR c.CoreAccountType<>a.AccountType))
    THROW 51010, 'GLR011: enabled account/book mapping has invalid tenant, book, classification, or type evidence.', 1;
IF EXISTS (SELECT TenantId,AccountId,AccountingBookId FROM dbo.AccountAccountingBooks WHERE IsDeleted=0 GROUP BY TenantId,AccountId,AccountingBookId HAVING COUNT_BIG(*)>1)
    THROW 51011, 'GLR012: duplicate live account/book mapping.', 1;
IF EXISTS (
    SELECT TenantId,AccountingBookId,SystemRole FROM dbo.AccountClassifications
    WHERE IsDeleted=0 AND SystemRole IS NOT NULL AND SystemRole NOT IN (1,2)
    GROUP BY TenantId,AccountingBookId,SystemRole HAVING COUNT_BIG(*)>1)
    THROW 51012, 'GLR013: singleton classification SystemRole occurs more than once.', 1;

IF (SELECT COUNT_BIG(*) FROM dbo.AccountSegmentStructures WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND IsActive=1 AND LifecycleStatus IN (2,3)) <> 2
    THROW 51013, 'GLR014: expected exactly two active/frozen account identity segments.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.AccountSegmentStructures WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND IsActive=1 AND SegmentCode=N'COMPANY' AND SegmentPosition=1 AND LifecycleStatus IN (2,3))
    THROW 51014, 'GLR015: COMPANY identity segment is not active at position 1.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.AccountSegmentStructures WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND IsActive=1 AND SegmentCode=N'NATURAL_ACCOUNT' AND SegmentPosition=2 AND LifecycleStatus IN (2,3) AND IsNaturalAccount=1)
    THROW 51015, 'GLR016: NATURAL_ACCOUNT identity segment is not active at position 2.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.AccountSegmentValues v
    JOIN dbo.Accounts a ON a.Id=v.AccountId
    JOIN dbo.AccountSegmentStructures s ON s.Id=v.SegmentStructureId
    LEFT JOIN dbo.SegmentLookupValues l ON l.Id=v.SegmentLookupValueId
    WHERE v.IsDeleted=0 AND (v.TenantId<>a.TenantId OR v.TenantId<>s.TenantId OR v.SegmentPosition<>s.SegmentPosition OR
      (v.SegmentLookupValueId IS NOT NULL AND (l.Id IS NULL OR l.IsDeleted=1 OR l.TenantId<>v.TenantId OR l.SegmentStructureId<>v.SegmentStructureId))))
    THROW 51016, 'GLR017: account segment value has invalid tenant/position/lookup lineage.', 1;
IF EXISTS (
    SELECT a.Id FROM dbo.Accounts a
    LEFT JOIN dbo.AccountSegmentValues v ON v.AccountId=a.Id AND v.TenantId=a.TenantId AND v.IsDeleted=0
    WHERE a.TenantId=@DefaultTenant AND a.IsDeleted=0 AND a.IsSegmented=1
    GROUP BY a.Id HAVING COUNT(v.Id)<>2)
    THROW 51017, 'GLR018: a segmented account lacks the exact two-value identity.', 1;

IF (SELECT COUNT_BIG(*) FROM dbo.FinanceDimensionDefinitions WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND Code IN (N'DEPARTMENT',N'PROJECT',N'ESTATE',N'CONTRACT',N'FUNDING_SOURCE',N'ACTIVITY')) <> 6
    THROW 51018, 'GLR019: six canonical transaction dimensions are required.', 1;
IF EXISTS (SELECT 1 FROM dbo.AccountSegmentStructures WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND SegmentCode IN (N'DEPARTMENT',N'PROJECT',N'ESTATE',N'CONTRACT',N'FUNDING_SOURCE',N'ACTIVITY'))
    THROW 51019, 'GLR020: transaction dimension was duplicated as an account identity segment.', 1;

IF (SELECT COUNT_BIG(*) FROM dbo.FinancialStatementLayouts WHERE TenantId=@DefaultTenant AND IsDeleted=0 AND IsProtectedStandard=1 AND IsActive=1) <> 6
    THROW 51020, 'GLR021: expected six protected standard layouts.', 1;
IF EXISTS (
    SELECT 1 FROM dbo.FinancialStatementLayouts l
    JOIN dbo.FinancialStatementLayoutVersions v ON v.FinancialStatementLayoutId=l.Id AND v.IsDeleted=0
    WHERE l.TenantId=@DefaultTenant AND l.IsDeleted=0 AND l.IsProtectedStandard=1 AND v.Status<>1)
    THROW 51021, 'GLR022: protected standards must remain Draft until explicitly published.', 1;
IF EXISTS (SELECT 1 FROM dbo.FinancialStatementPublicationAccounts WHERE TenantId=@DefaultTenant AND IsDeleted=0)
    THROW 51022, 'GLR023: seed must not create publication membership evidence.', 1;

IF EXISTS (SELECT 1 FROM dbo.FxRevaluationBatches WHERE TenantId=@DefaultTenant AND IsDeleted=0)
    THROW 51023, 'GLR024: seed must not create revaluation batches.', 1;
IF EXISTS (SELECT 1 FROM dbo.JournalEntries WHERE TenantId=@DefaultTenant AND IsDeleted=0)
    THROW 51024, 'GLR025: seed must not create journal entries.', 1;
IF EXISTS (SELECT 1 FROM dbo.AccountTransactions WHERE TenantId=@DefaultTenant AND IsDeleted=0)
    THROW 51025, 'GLR026: seed must not create account transactions.', 1;
IF EXISTS (SELECT 1 FROM dbo.FinancePostingEvents WHERE TenantId=@DefaultTenant AND IsDeleted=0)
    THROW 51026, 'GLR027: seed must not create posting events.', 1;

-- Canonical, surrogate-ID-free snapshot used to prove second-pass idempotency.
SELECT N'BOOK|' + Code + N'|' + Name + N'|' + CONVERT(nvarchar(1),IsActive) + N'|' + CONVERT(nvarchar(1),IsDefault) + N'|' + CONVERT(nvarchar(1),AllowsPosting)
FROM dbo.AccountingBooks WHERE TenantId=@DefaultTenant AND IsDeleted=0 ORDER BY Code;
SELECT N'CLASS|' + b.Code + N'|' + c.Code + N'|' + c.Name + N'|' + CONVERT(nvarchar(10),c.CoreAccountType) + N'|' + CONVERT(nvarchar(10),c.DefaultRevaluationTreatment) + N'|' + COALESCE(CONVERT(nvarchar(10),c.SystemRole),N'') + N'|' + CONVERT(nvarchar(1),c.IsPostingClassification) + N'|' + CONVERT(nvarchar(10),c.Status)
FROM dbo.AccountClassifications c JOIN dbo.AccountingBooks b ON b.Id=c.AccountingBookId
WHERE c.TenantId=@DefaultTenant AND c.IsDeleted=0 ORDER BY b.Code,c.Code;
SELECT N'MAP|' + a.AccountCode + N'|' + b.Code + N'|' + COALESCE(c.Code,N'') + N'|' + CONVERT(nvarchar(1),m.IsEnabled)
FROM dbo.AccountAccountingBooks m JOIN dbo.Accounts a ON a.Id=m.AccountId JOIN dbo.AccountingBooks b ON b.Id=m.AccountingBookId LEFT JOIN dbo.AccountClassifications c ON c.Id=m.AccountClassificationId
WHERE m.TenantId=@DefaultTenant AND m.IsDeleted=0 ORDER BY a.AccountCode,b.Code;
SELECT N'SEGMENT|' + SegmentCode + N'|' + CONVERT(nvarchar(10),SegmentPosition) + N'|' + CONVERT(nvarchar(10),SegmentLength) + N'|' + DataType + N'|' + CONVERT(nvarchar(10),LifecycleStatus)
FROM dbo.AccountSegmentStructures WHERE TenantId=@DefaultTenant AND IsDeleted=0 ORDER BY SegmentPosition,SegmentCode;
SELECT N'DIMENSION|' + Code + N'|' + Name + N'|' + CONVERT(nvarchar(1),IsActive) + N'|' + CONVERT(nvarchar(10),DisplayOrder)
FROM dbo.FinanceDimensionDefinitions WHERE TenantId=@DefaultTenant AND IsDeleted=0 ORDER BY Code;
SELECT N'LAYOUT|' + b.Code + N'|' + l.Code + N'|' + CONVERT(nvarchar(10),l.StatementType) + N'|' + CONVERT(nvarchar(1),l.IsProtectedStandard)
FROM dbo.FinancialStatementLayouts l JOIN dbo.AccountingBooks b ON b.Id=l.AccountingBookId
WHERE l.TenantId=@DefaultTenant AND l.IsDeleted=0 ORDER BY b.Code,l.Code;
SELECT N'MIGRATION|' + MigrationId + N'|' + ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;
