:ON ERROR EXIT
-- Opt-in, disposable-development fixture. Run only with sqlcmd -b and explicit variables.
-- This does not post opening balances or approve accounting-book workflows.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'$(ExpectedDatabase)' OR N'$(ConfirmDevOnly)' <> N'OPEN_FY2026_DEV_ONLY'
    THROW 51000, 'Explicit development database and acknowledgement are required.', 1;
IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51001, 'System databases are not valid fixture targets.', 1;

DECLARE @tenantId uniqueidentifier =
    (SELECT Id FROM dbo.Tenants WHERE Code = N'DEFAULT' AND IsDeleted = 0);
IF @tenantId IS NULL
    THROW 51002, 'The DEFAULT development tenant is missing.', 1;
DECLARE @fiscalYearId uniqueidentifier =
    (SELECT Id FROM dbo.FiscalYears WHERE TenantId = @tenantId AND FiscalYearCode = N'FY2026' AND IsDeleted = 0);
IF @fiscalYearId IS NULL
    THROW 51003, 'FY2026 is missing.', 1;
IF (SELECT COUNT(*) FROM dbo.FiscalPeriods WHERE TenantId = @tenantId AND FiscalYearId = @fiscalYearId AND IsDeleted = 0) <> 12
    THROW 51004, 'FY2026 must have exactly twelve live periods.', 1;
IF EXISTS (SELECT 1 FROM dbo.FiscalYears WHERE Id = @fiscalYearId AND (IsClosed = 1 OR IsLocked = 1))
    THROW 51005, 'FY2026 is closed or locked; this fixture will not override it.', 1;
IF EXISTS (SELECT 1 FROM dbo.FiscalPeriods WHERE TenantId = @tenantId AND FiscalYearId = @fiscalYearId AND IsGlobalLockSuspended = 1)
    THROW 51006, 'A FY2026 global lock suspension exists.', 1;
IF EXISTS (SELECT 1 FROM dbo.PeriodModuleLock WHERE TenantId = @tenantId AND IsDeleted = 0
    AND FiscalPeriodId IN (SELECT Id FROM dbo.FiscalPeriods WHERE FiscalYearId = @fiscalYearId))
    THROW 51007, 'FY2026 has module-specific period locks.', 1;

-- Existing accounting evidence would make a direct development-calendar fixture unsafe.
IF EXISTS (SELECT 1 FROM dbo.JournalEntries)
    OR EXISTS (SELECT 1 FROM dbo.JournalBatches)
    OR EXISTS (SELECT 1 FROM dbo.AccountBalances)
    OR EXISTS (SELECT 1 FROM dbo.OpeningBalanceBatches)
    OR EXISTS (SELECT 1 FROM dbo.AccountingBookInitializations)
    OR EXISTS (SELECT 1 FROM dbo.AccountingBookPeriods)
    THROW 51008, 'Accounting evidence exists; use governed period transitions instead.', 1;

DECLARE @backupDirectory nvarchar(4000) = CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'));
IF NULLIF(@backupDirectory, N'') IS NULL
    THROW 51009, 'SQL Server default backup directory is unavailable.', 1;
DECLARE @backupFile nvarchar(4000) = @backupDirectory
    + CASE WHEN RIGHT(@backupDirectory, 1) IN (N'\', N'/') THEN N'' ELSE N'\' END
    + DB_NAME() + N'_FY2026_DEV_' + CONVERT(nvarchar(8), SYSUTCDATETIME(), 112)
    + REPLACE(CONVERT(nvarchar(8), SYSUTCDATETIME(), 108), N':', N'')
    + N'_' + REPLACE(CONVERT(nvarchar(36), NEWID()), N'-', N'') + N'.bak';
DECLARE @databaseName sysname = DB_NAME();
BACKUP DATABASE @databaseName TO DISK = @backupFile WITH COPY_ONLY, CHECKSUM;
RESTORE VERIFYONLY FROM DISK = @backupFile WITH CHECKSUM;
PRINT N'Verified backup: ' + @backupFile;

BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE dbo.FiscalPeriods
    SET Status = N'Open', PeriodStatus = N'Open', IsOpen = 1, IsClosed = 0, IsLocked = 0,
        UpdatedAt = SYSUTCDATETIME(), UpdatedBy = N'FY2026 development scenario'
    WHERE TenantId = @tenantId AND FiscalYearId = @fiscalYearId AND IsDeleted = 0;
    IF @@ROWCOUNT <> 12
        THROW 51010, 'FY2026 period count changed during update.', 1;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT FiscalYearCode, Status AS YearStatus, IsClosed AS YearClosed, IsLocked AS YearLocked
FROM dbo.FiscalYears WHERE Id = @fiscalYearId;
SELECT PeriodCode, PeriodStatus, IsOpen, IsClosed, IsLocked
FROM dbo.FiscalPeriods WHERE FiscalYearId = @fiscalYearId AND IsDeleted = 0 ORDER BY PeriodNumber;
