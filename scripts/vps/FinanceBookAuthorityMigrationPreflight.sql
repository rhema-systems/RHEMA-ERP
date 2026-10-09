-- Read-only Up preconditions for the Finance book-authority migration chain,
-- including the 20261009163000 evidence-trigger origin alignment.
-- Only table variables are written. The checks reject missing prerequisites,
-- partial pending schema and the explicit source-authority schema collision.
SET NOCOUNT ON;

DECLARE @Checks TABLE (CheckName nvarchar(300) NOT NULL, AffectedRows bigint NOT NULL);

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT CAST(N'FinanceBookAuthority.MigrationHistoryMissing' AS nvarchar(300)) AS CheckName,
           CAST(1 AS bigint) AS AffectedRows;
    RETURN;
END;

DECLARE @Applied TABLE (MigrationId nvarchar(150) NOT NULL PRIMARY KEY);
INSERT @Applied (MigrationId)
EXEC sys.sp_executesql N'
    SELECT MigrationId
    FROM dbo.__EFMigrationsHistory
    WHERE MigrationId IN
    (
        N''20260930000100_YearEndBookCloseCycles'',
        N''20260930000200_ApWithholdingNetBasisEvidence'',
        N''20260930000300_LeaseInstalmentApOpenItems'',
        N''20260930000400_FinanceSourceBookAuthority'',
        N''20260930000500_AddFinanceSourceBookAuthorityCallerBindings'',
        N''20260930000600_AddCapitalizationLineage'',
        N''20261009163000_AlignSourceBookAuthorityJournalOrigin''
    );';

IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000100_YearEndBookCloseCycles')
BEGIN
    DECLARE @YearEndTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT @YearEndTables VALUES
        (N'Tenants'),(N'FiscalYears'),(N'AccountingBooks'),(N'Accounts'),(N'JournalEntries');

    INSERT @Checks
    SELECT N'FinanceBookAuthority.YearEnd.RequiredTableMissing:' + TableName, 1
    FROM @YearEndTables
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U') IS NULL;

    IF OBJECT_ID(N'dbo.YearEndBookCloseCycles', N'U') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.YearEnd.UnexpectedPendingSchema:YearEndBookCloseCycles', 1);
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FiscalYears') AND name=N'AK_FiscalYears_TenantId_Id')
        INSERT @Checks VALUES(N'FinanceBookAuthority.YearEnd.UnexpectedPendingSchema:AK_FiscalYears_TenantId_Id', 1);
END;

IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000200_ApWithholdingNetBasisEvidence')
BEGIN
    IF OBJECT_ID(N'dbo.VendorPaymentAllocation', N'U') IS NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.ApWithholding.RequiredTableMissing:VendorPaymentAllocation', 1);
    ELSE IF COL_LENGTH(N'dbo.VendorPaymentAllocation', N'WithholdingTaxBaseFunctionalAmount') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.ApWithholding.UnexpectedPendingSchema:WithholdingTaxBaseFunctionalAmount', 1);
END;

IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000300_LeaseInstalmentApOpenItems')
BEGIN
    DECLARE @LeaseTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT @LeaseTables VALUES
        (N'LeaseContracts'),(N'VendorInvoice'),(N'VendorInvoiceLineItem'),(N'VendorPayment'),
        (N'LeaseScheduleLines'),(N'FinancePostingEvents'),(N'WorkflowInstances'),
        (N'AccountingBooks'),(N'Accounts'),(N'JournalEntries');

    INSERT @Checks
    SELECT N'FinanceBookAuthority.Lease.RequiredTableMissing:' + TableName, 1
    FROM @LeaseTables
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U') IS NULL;

    DECLARE @LeaseColumns TABLE (TableName sysname NOT NULL, ColumnName sysname NOT NULL, PRIMARY KEY(TableName,ColumnName));
    INSERT @LeaseColumns VALUES
        (N'LeaseContracts',N'AccountingBookId'),
        (N'LeaseContracts',N'ActivationWorkflowInstanceId'),
        (N'LeaseContracts',N'RecognitionJournalEntryId'),
        (N'LeaseContracts',N'RecognitionPostingEventId'),
        (N'VendorInvoice',N'LeaseScheduleLineId'),
        (N'VendorInvoice',N'ReplacesLeaseVendorInvoiceId'),
        (N'VendorInvoice',N'LeaseAccountingBookId'),
        (N'VendorInvoiceLineItem',N'LeaseComponent'),
        (N'VendorPayment',N'AccountingBookId');

    INSERT @Checks
    SELECT N'FinanceBookAuthority.Lease.UnexpectedPendingSchema:' + TableName + N'.' + ColumnName, 1
    FROM @LeaseColumns
    WHERE COL_LENGTH(N'dbo.' + TableName, ColumnName) IS NOT NULL;

    DECLARE @LeaseConstraints TABLE (ConstraintName sysname NOT NULL PRIMARY KEY);
    INSERT @LeaseConstraints VALUES
        (N'AK_LeaseScheduleLines_TenantId_Id'),
        (N'AK_VendorInvoice_TenantId_Id'),
        (N'AK_FinancePostingEvents_TenantId_Id_AccountingBookId'),
        (N'AK_WorkflowInstances_TenantId_Id'),
        (N'CK_VendorInvoice_LeaseSourceCoherent'),
        (N'CK_VendorInvoiceLineItem_LeaseComponent');

    INSERT @Checks
    SELECT N'FinanceBookAuthority.Lease.UnexpectedPendingSchema:' + ConstraintName, 1
    FROM @LeaseConstraints expected
    WHERE EXISTS (SELECT 1 FROM sys.objects actual WHERE actual.name=expected.ConstraintName);
END;

IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000400_FinanceSourceBookAuthority')
BEGIN
    DECLARE @AuthorityTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT @AuthorityTables VALUES
        (N'Tenants'),(N'AccountingBooks'),(N'WorkflowInstances'),(N'WorkflowEntityTypes'),
        (N'WorkflowStepInstances'),(N'WorkflowApprovals'),(N'FinancePostingEvents'),(N'JournalEntries');

    INSERT @Checks
    SELECT N'FinanceBookAuthority.Authority.RequiredTableMissing:' + TableName, 1
    FROM @AuthorityTables
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U') IS NULL;

    -- Mirrors SOURCE_BOOK_AUTHORITY_SCHEMA_EXISTS in the migration Up path.
    IF OBJECT_ID(N'dbo.FinanceSourceBookAuthorities', N'U') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Authority.SchemaExists:FinanceSourceBookAuthorities', 1);
    IF OBJECT_ID(N'dbo.FinanceSourceBookAuthorityOrigins', N'U') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Authority.SchemaExists:FinanceSourceBookAuthorityOrigins', 1);
END;

IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000500_AddFinanceSourceBookAuthorityCallerBindings')
BEGIN
    DECLARE @CallerTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT @CallerTables VALUES (N'Invoices'),(N'CustomerPayment'),(N'CashTransaction'),(N'VendorInvoice');

    INSERT @Checks
    SELECT N'FinanceBookAuthority.Callers.RequiredTableMissing:' + TableName, 1
    FROM @CallerTables
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U') IS NULL;

    -- Migration 004 may be pending in the same run. Require its table only when
    -- history says 004 is already applied; otherwise that migration creates it.
    IF EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000400_FinanceSourceBookAuthority')
       AND OBJECT_ID(N'dbo.FinanceSourceBookAuthorities', N'U') IS NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Callers.RequiredTableMissing:FinanceSourceBookAuthorities', 1);

    INSERT @Checks
    SELECT N'FinanceBookAuthority.Callers.UnexpectedPendingSchema:' + TableName + N'.SourceBookAuthorityId', 1
    FROM @CallerTables
    WHERE COL_LENGTH(N'dbo.' + TableName, N'SourceBookAuthorityId') IS NOT NULL;

    IF COL_LENGTH(N'dbo.FinanceSourceBookAuthorities', N'SourceWorkflowEntityType') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Callers.UnexpectedPendingSchema:FinanceSourceBookAuthorities.SourceWorkflowEntityType', 1);
END;

IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000600_AddCapitalizationLineage')
BEGIN
    DECLARE @CapitalizationTables TABLE (TableName sysname NOT NULL PRIMARY KEY);
    INSERT @CapitalizationTables VALUES
        (N'CapitalProjects'),(N'ProjectCostLines'),(N'FixedAssets'),(N'WorkflowInstances'),
        (N'Accounts'),(N'FinancePostingEvents'),(N'JournalEntries');

    INSERT @Checks
    SELECT N'FinanceBookAuthority.Capitalization.RequiredTableMissing:' + TableName, 1
    FROM @CapitalizationTables
    WHERE OBJECT_ID(N'dbo.' + QUOTENAME(TableName), N'U') IS NULL;

    IF EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000400_FinanceSourceBookAuthority')
       AND OBJECT_ID(N'dbo.FinanceSourceBookAuthorities', N'U') IS NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Capitalization.RequiredTableMissing:FinanceSourceBookAuthorities', 1);

    IF OBJECT_ID(N'dbo.FixedAssetCapitalizationCycles', N'U') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Capitalization.UnexpectedPendingSchema:FixedAssetCapitalizationCycles', 1);
    IF COL_LENGTH(N'dbo.CapitalProjects', N'CapitalizationWorkflowInstanceId') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Capitalization.UnexpectedPendingSchema:CapitalProjects.CapitalizationWorkflowInstanceId', 1);
    IF COL_LENGTH(N'dbo.ProjectCostLines', N'SourceFinancePostingEventId') IS NOT NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.Capitalization.UnexpectedPendingSchema:ProjectCostLines.SourceFinancePostingEventId', 1);
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.FixedAssets') AND name=N'AK_FixedAssets_TenantId_Id')
        INSERT @Checks VALUES(N'FinanceBookAuthority.Capitalization.UnexpectedPendingSchema:AK_FixedAssets_TenantId_Id', 1);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CapitalProjects') AND name=N'IX_CapitalProjects_TenantId')
        INSERT @Checks VALUES(N'FinanceBookAuthority.Capitalization.RequiredIndexMissing:IX_CapitalProjects_TenantId', 1);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.ProjectCostLines') AND name=N'IX_ProjectCostLines_TenantId')
        INSERT @Checks VALUES(N'FinanceBookAuthority.Capitalization.RequiredIndexMissing:IX_ProjectCostLines_TenantId', 1);
END;

IF NOT EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20261009163000_AlignSourceBookAuthorityJournalOrigin')
   AND EXISTS (SELECT 1 FROM @Applied WHERE MigrationId=N'20260930000500_AddFinanceSourceBookAuthorityCallerBindings')
BEGIN
    -- This migration replaces an existing evidence-trigger definition. Its only
    -- new database dependency is the explicit journal producer-origin column;
    -- the source-module coordinate remains the legacy fallback.
    IF OBJECT_ID(N'dbo.FinanceSourceBookAuthorities', N'U') IS NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.JournalOrigin.RequiredTableMissing:FinanceSourceBookAuthorities', 1);
    IF OBJECT_ID(N'dbo.JournalEntries', N'U') IS NULL
        INSERT @Checks VALUES(N'FinanceBookAuthority.JournalOrigin.RequiredTableMissing:JournalEntries', 1);
    ELSE
    BEGIN
        IF COL_LENGTH(N'dbo.JournalEntries', N'OriginModuleCode') IS NULL
            INSERT @Checks VALUES(N'FinanceBookAuthority.JournalOrigin.RequiredColumnMissing:JournalEntries.OriginModuleCode', 1);
        IF COL_LENGTH(N'dbo.JournalEntries', N'SourceModule') IS NULL
            INSERT @Checks VALUES(N'FinanceBookAuthority.JournalOrigin.RequiredColumnMissing:JournalEntries.SourceModule', 1);
    END;
END;

SELECT CheckName,AffectedRows FROM @Checks WHERE AffectedRows>0 ORDER BY CheckName;
