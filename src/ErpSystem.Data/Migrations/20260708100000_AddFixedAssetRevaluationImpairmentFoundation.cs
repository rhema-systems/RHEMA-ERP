using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260708100000_AddFixedAssetRevaluationImpairmentFoundation")]
    public partial class AddFixedAssetRevaluationImpairmentFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FixedAssetCategories]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[FixedAssetCategories]', N'RevaluationLossAccountId') IS NULL
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD [RevaluationLossAccountId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[FixedAssetCategories]', N'ImpairmentLossAccountId') IS NULL
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD [ImpairmentLossAccountId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[FixedAssetCategories]', N'AccumulatedImpairmentAccountId') IS NULL
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD [AccumulatedImpairmentAccountId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[FixedAssetCategories]', N'ImpairmentReversalAccountId') IS NULL
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD [ImpairmentReversalAccountId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[AssetValuations]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'AccountingBookId') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [AccountingBookId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'BookClassification') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [BookClassification] nvarchar(20) NOT NULL CONSTRAINT [DF_AssetValuations_BookClassification] DEFAULT N'IFRS';
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'FiscalPeriodId') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [FiscalPeriodId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'AccountingDate') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[AssetValuations] ADD [AccountingDate] datetime2 NULL;
                        EXEC(N'UPDATE [dbo].[AssetValuations] SET [AccountingDate] = [ValuationDate] WHERE [AccountingDate] IS NULL');
                        ALTER TABLE [dbo].[AssetValuations] ALTER COLUMN [AccountingDate] datetime2 NOT NULL;
                    END
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'AccumulatedDepreciationBefore') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [AccumulatedDepreciationBefore] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetValuations_AccumulatedDepreciationBefore] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'NetBookValueBefore') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [NetBookValueBefore] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetValuations_NetBookValueBefore] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'AdjustmentAmount') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [AdjustmentAmount] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetValuations_AdjustmentAmount] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'RevaluationSurplusApplied') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [RevaluationSurplusApplied] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetValuations_RevaluationSurplusApplied] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'RevaluationLossRecognized') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [RevaluationLossRecognized] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetValuations_RevaluationLossRecognized] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'PostedAt') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [PostedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'PostingEventId') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [PostingEventId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'Status') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [Status] nvarchar(30) NOT NULL CONSTRAINT [DF_AssetValuations_Status] DEFAULT N'Calculated';
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'IdempotencyKey') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [IdempotencyKey] nvarchar(150) NULL;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'FailedAt') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [FailedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetValuations]', N'FailureReason') IS NULL
                        ALTER TABLE [dbo].[AssetValuations] ADD [FailureReason] nvarchar(1000) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AssetValuations]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_FixedAssetId_BookClassification_ValuationDate' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        CREATE INDEX [IX_AssetValuations_TenantId_FixedAssetId_BookClassification_ValuationDate] ON [dbo].[AssetValuations] ([TenantId], [FixedAssetId], [BookClassification], [ValuationDate]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        CREATE UNIQUE INDEX [IX_AssetValuations_TenantId_IdempotencyKey] ON [dbo].[AssetValuations] ([TenantId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        CREATE INDEX [IX_AssetValuations_TenantId_JournalEntryId] ON [dbo].[AssetValuations] ([TenantId], [JournalEntryId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        CREATE INDEX [IX_AssetValuations_TenantId_PostingEventId] ON [dbo].[AssetValuations] ([TenantId], [PostingEventId]);
                END

                IF OBJECT_ID(N'[dbo].[AssetValuations]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_AccountingBooks_AccountingBookId')
                    ALTER TABLE [dbo].[AssetValuations] ADD CONSTRAINT [FK_AssetValuations_AccountingBooks_AccountingBookId] FOREIGN KEY ([AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetValuations]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FiscalPeriods]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[AssetValuations] ADD CONSTRAINT [FK_AssetValuations_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetValuations]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[AssetValuations] ADD CONSTRAINT [FK_AssetValuations_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetValuations]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[AssetValuations] ADD CONSTRAINT [FK_AssetValuations_FinancePostingEvents_PostingEventId] FOREIGN KEY ([PostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;

                IF OBJECT_ID(N'[dbo].[FixedAssetCategories]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_RevaluationLossAccountId')
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD CONSTRAINT [FK_FixedAssetCategories_Accounts_RevaluationLossAccountId] FOREIGN KEY ([RevaluationLossAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;
                    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_ImpairmentLossAccountId')
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD CONSTRAINT [FK_FixedAssetCategories_Accounts_ImpairmentLossAccountId] FOREIGN KEY ([ImpairmentLossAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;
                    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_AccumulatedImpairmentAccountId')
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD CONSTRAINT [FK_FixedAssetCategories_Accounts_AccumulatedImpairmentAccountId] FOREIGN KEY ([AccumulatedImpairmentAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;
                    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_ImpairmentReversalAccountId')
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD CONSTRAINT [FK_FixedAssetCategories_Accounts_ImpairmentReversalAccountId] FOREIGN KEY ([ImpairmentReversalAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_ImpairmentReversalAccountId')
                    ALTER TABLE [dbo].[FixedAssetCategories] DROP CONSTRAINT [FK_FixedAssetCategories_Accounts_ImpairmentReversalAccountId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_AccumulatedImpairmentAccountId')
                    ALTER TABLE [dbo].[FixedAssetCategories] DROP CONSTRAINT [FK_FixedAssetCategories_Accounts_AccumulatedImpairmentAccountId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_ImpairmentLossAccountId')
                    ALTER TABLE [dbo].[FixedAssetCategories] DROP CONSTRAINT [FK_FixedAssetCategories_Accounts_ImpairmentLossAccountId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_RevaluationLossAccountId')
                    ALTER TABLE [dbo].[FixedAssetCategories] DROP CONSTRAINT [FK_FixedAssetCategories_Accounts_RevaluationLossAccountId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[AssetValuations] DROP CONSTRAINT [FK_AssetValuations_FinancePostingEvents_PostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[AssetValuations] DROP CONSTRAINT [FK_AssetValuations_JournalEntries_JournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[AssetValuations] DROP CONSTRAINT [FK_AssetValuations_FiscalPeriods_FiscalPeriodId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetValuations_AccountingBooks_AccountingBookId')
                    ALTER TABLE [dbo].[AssetValuations] DROP CONSTRAINT [FK_AssetValuations_AccountingBooks_AccountingBookId];

                IF OBJECT_ID(N'[dbo].[AssetValuations]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        DROP INDEX [IX_AssetValuations_TenantId_PostingEventId] ON [dbo].[AssetValuations];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        DROP INDEX [IX_AssetValuations_TenantId_JournalEntryId] ON [dbo].[AssetValuations];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        DROP INDEX [IX_AssetValuations_TenantId_IdempotencyKey] ON [dbo].[AssetValuations];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetValuations_TenantId_FixedAssetId_BookClassification_ValuationDate' AND [object_id] = OBJECT_ID(N'[dbo].[AssetValuations]'))
                        DROP INDEX [IX_AssetValuations_TenantId_FixedAssetId_BookClassification_ValuationDate] ON [dbo].[AssetValuations];
                END

                DECLARE @dropDefaults TABLE ([Name] sysname);
                INSERT INTO @dropDefaults ([Name]) VALUES
                    (N'DF_AssetValuations_BookClassification'),
                    (N'DF_AssetValuations_AccumulatedDepreciationBefore'),
                    (N'DF_AssetValuations_NetBookValueBefore'),
                    (N'DF_AssetValuations_AdjustmentAmount'),
                    (N'DF_AssetValuations_RevaluationSurplusApplied'),
                    (N'DF_AssetValuations_RevaluationLossRecognized'),
                    (N'DF_AssetValuations_Status');

                DECLARE @constraintName sysname;
                DECLARE default_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT [Name] FROM @dropDefaults;
                OPEN default_cursor;
                FETCH NEXT FROM default_cursor INTO @constraintName;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF OBJECT_ID(N'[dbo].[' + @constraintName + N']', N'D') IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[AssetValuations] DROP CONSTRAINT [' + @constraintName + N']');
                    FETCH NEXT FROM default_cursor INTO @constraintName;
                END
                CLOSE default_cursor;
                DEALLOCATE default_cursor;

                DECLARE @dropColumns TABLE ([TableName] sysname, [ColumnName] sysname);
                INSERT INTO @dropColumns ([TableName], [ColumnName]) VALUES
                    (N'AssetValuations', N'AccountingBookId'),
                    (N'AssetValuations', N'BookClassification'),
                    (N'AssetValuations', N'FiscalPeriodId'),
                    (N'AssetValuations', N'AccountingDate'),
                    (N'AssetValuations', N'AccumulatedDepreciationBefore'),
                    (N'AssetValuations', N'NetBookValueBefore'),
                    (N'AssetValuations', N'AdjustmentAmount'),
                    (N'AssetValuations', N'RevaluationSurplusApplied'),
                    (N'AssetValuations', N'RevaluationLossRecognized'),
                    (N'AssetValuations', N'PostedAt'),
                    (N'AssetValuations', N'PostingEventId'),
                    (N'AssetValuations', N'Status'),
                    (N'AssetValuations', N'IdempotencyKey'),
                    (N'AssetValuations', N'FailedAt'),
                    (N'AssetValuations', N'FailureReason'),
                    (N'FixedAssetCategories', N'RevaluationLossAccountId'),
                    (N'FixedAssetCategories', N'ImpairmentLossAccountId'),
                    (N'FixedAssetCategories', N'AccumulatedImpairmentAccountId'),
                    (N'FixedAssetCategories', N'ImpairmentReversalAccountId');

                DECLARE @tableName sysname;
                DECLARE @columnName sysname;
                DECLARE drop_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT [TableName], [ColumnName] FROM @dropColumns;
                OPEN drop_cursor;
                FETCH NEXT FROM drop_cursor INTO @tableName, @columnName;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF OBJECT_ID(N'[dbo].[' + @tableName + N']', N'U') IS NOT NULL
                        AND COL_LENGTH(N'[dbo].[' + @tableName + N']', @columnName) IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[' + @tableName + N'] DROP COLUMN [' + @columnName + N']');
                    FETCH NEXT FROM drop_cursor INTO @tableName, @columnName;
                END
                CLOSE drop_cursor;
                DEALLOCATE drop_cursor;
                """);
        }
    }
}
