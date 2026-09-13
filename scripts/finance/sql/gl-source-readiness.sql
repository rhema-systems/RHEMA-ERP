SET NOCOUNT ON;
SET XACT_ABORT ON;

SELECT
    DB_NAME() AS DatabaseName,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS ProductVersion,
    CAST(SERVERPROPERTY('Edition') AS nvarchar(128)) AS Edition,
    CAST(SERVERPROPERTY('EngineEdition') AS int) AS EngineEdition,
    d.state_desc AS DatabaseState,
    d.user_access_desc AS UserAccess,
    d.is_read_only AS IsReadOnly,
    d.recovery_model_desc AS RecoveryModel,
    d.compatibility_level AS CompatibilityLevel,
    d.containment_desc AS Containment,
    CAST(IS_SRVROLEMEMBER('sysadmin') AS int) AS IsSysAdmin,
    CAST(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'VIEW DEFINITION') AS int) AS CanViewDefinition,
    CAST(HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'BACKUP DATABASE') AS int) AS CanBackupDatabase,
    CAST(HAS_PERMS_BY_NAME(NULL, 'SERVER', 'CREATE ANY DATABASE') AS int) AS CanCreateDatabase
FROM sys.databases d
WHERE d.name = DB_NAME();

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT N'MIGRATION_HISTORY_MISSING' AS Finding;
END
ELSE
BEGIN
    SELECT MigrationId, ProductVersion
    FROM dbo.__EFMigrationsHistory
    ORDER BY MigrationId;
END;

IF OBJECT_ID(N'dbo.AccountClassifications', N'U') IS NULL
    SELECT N'CLASSIFICATION_SCHEMA_ABSENT' AS FindingCode, N'BLOCKER' AS Severity, CAST(1 AS bigint) AS AffectedRows,
           N'Phase 1 classification schema is absent; later classification-dependent preflights cannot be satisfied by post-migration seeding.' AS OperatorMessage;
ELSE
    EXEC(N'
      SELECT N''DUPLICATE_SINGLETON_SYSTEM_ROLE'' AS FindingCode, N''BLOCKER'' AS Severity, COUNT_BIG(*) AS AffectedRows,
             N''Resolve duplicate non-Cash/non-Bank SystemRole values per tenant/book before Phase 2.'' AS OperatorMessage
      FROM (
        SELECT TenantId, AccountingBookId, SystemRole
        FROM dbo.AccountClassifications
        WHERE IsDeleted=0 AND SystemRole IS NOT NULL AND SystemRole NOT IN (1,2)
        GROUP BY TenantId, AccountingBookId, SystemRole HAVING COUNT_BIG(*)>1
      ) duplicates HAVING COUNT_BIG(*)>0;');

IF OBJECT_ID(N'dbo.FinancialStatementLayoutVersions', N'U') IS NOT NULL
    EXEC(N'
      SELECT N''HISTORICAL_PUBLISHED_LAYOUTS'' AS FindingCode, N''REVIEW'' AS Severity, COUNT_BIG(*) AS AffectedRows,
             N''Published/retired layouts need an explicit snapshot-compatibility decision; immutable membership cannot be reconstructed silently.'' AS OperatorMessage
      FROM dbo.FinancialStatementLayoutVersions
      WHERE IsDeleted=0 AND Status IN (2,3) HAVING COUNT_BIG(*)>0;');

IF OBJECT_ID(N'dbo.FxRevaluationBatches', N'U') IS NOT NULL
    EXEC(N'
      SELECT N''LIVE_FX_REVALUATION_BATCH'' AS FindingCode, N''BLOCKER'' AS Severity, COUNT_BIG(*) AS AffectedRows,
             N''Phase 4 explicitly refuses any nondeleted historical FX revaluation batch.'' AS OperatorMessage
      FROM dbo.FxRevaluationBatches WHERE IsDeleted=0 HAVING COUNT_BIG(*)>0;');

IF OBJECT_ID(N'dbo.AccountCurrencyLinks', N'U') IS NOT NULL
    EXEC(N'
      SELECT N''ACTIVE_LEGACY_CURRENCY_LINKS'' AS FindingCode, N''REVIEW'' AS Severity, COUNT_BIG(*) AS AffectedRows,
             N''Final clone must stop before backup unless separately reviewed evidence fully proves Phase 4 equivalence for every active legacy currency link.'' AS OperatorMessage
      FROM dbo.AccountCurrencyLinks WHERE IsDeleted=0 AND IsActive=1 HAVING COUNT_BIG(*)>0;');

IF OBJECT_ID(N'dbo.AccountSegmentValues', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.AccountSegmentStructures', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Accounts', N'U') IS NOT NULL
BEGIN
    EXEC(N'
      SELECT N''SEGMENT_DUPLICATE_ASSIGNMENT'' AS FindingCode, N''BLOCKER'' AS Severity, COUNT_BIG(*) AS AffectedRows,
             N''Resolve duplicate live account/segment assignments before Phase 5.'' AS OperatorMessage
      FROM (
        SELECT TenantId, AccountId, SegmentStructureId
        FROM dbo.AccountSegmentValues WHERE IsDeleted=0
        GROUP BY TenantId, AccountId, SegmentStructureId HAVING COUNT_BIG(*)>1
      ) duplicates HAVING COUNT_BIG(*)>0;

      SELECT N''SEGMENT_TENANT_LINEAGE'' AS FindingCode, N''BLOCKER'' AS Severity, COUNT_BIG(*) AS AffectedRows,
             N''Resolve missing, deleted or cross-tenant account/segment lineage before Phase 5.'' AS OperatorMessage
      FROM dbo.AccountSegmentValues value
      LEFT JOIN dbo.Accounts account ON account.Id=value.AccountId
      LEFT JOIN dbo.AccountSegmentStructures segment ON segment.Id=value.SegmentStructureId
      WHERE value.IsDeleted=0 AND
        (account.Id IS NULL OR segment.Id IS NULL OR account.IsDeleted=1 OR segment.IsDeleted=1 OR
         value.TenantId<>account.TenantId OR value.TenantId<>segment.TenantId)
      HAVING COUNT_BIG(*)>0;');
END;

SELECT
    df.type_desc,
    COUNT_BIG(*) AS FileCount,
    SUM(CAST(df.size AS bigint)) * 8 * 1024 AS AllocatedBytes
FROM sys.database_files df
GROUP BY df.type_desc
ORDER BY df.type_desc;

SELECT
    dek.encryption_state AS EncryptionState,
    dek.key_algorithm AS KeyAlgorithm,
    dek.key_length AS KeyLength
FROM sys.dm_database_encryption_keys dek
WHERE dek.database_id = DB_ID();
