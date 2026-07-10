using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260708120000_AddFixedAssetDisposalFoundation")]
    public partial class AddFixedAssetDisposalFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FixedAssetCategories]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[FixedAssetCategories]', N'DisposalProceedsClearingAccountId') IS NULL
                        ALTER TABLE [dbo].[FixedAssetCategories] ADD [DisposalProceedsClearingAccountId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'AccountingDate') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [AccountingDate] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'FiscalPeriodId') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [FiscalPeriodId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'AccountingBookId') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [AccountingBookId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'BookClassification') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [BookClassification] nvarchar(20) NOT NULL CONSTRAINT [DF_AssetDisposals_BookClassification] DEFAULT N'IFRS';
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'NetProceeds') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [NetProceeds] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetDisposals_NetProceeds] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'ProceedsCurrencyCode') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [ProceedsCurrencyCode] nvarchar(3) NOT NULL CONSTRAINT [DF_AssetDisposals_ProceedsCurrencyCode] DEFAULT N'GHS';
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'ProceedsAccountId') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [ProceedsAccountId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'CostAtDisposal') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [CostAtDisposal] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetDisposals_CostAtDisposal] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'AccumulatedDepreciationAtDisposal') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [AccumulatedDepreciationAtDisposal] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetDisposals_AccumulatedDepreciationAtDisposal] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'AccumulatedImpairmentAtDisposal') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [AccumulatedImpairmentAtDisposal] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetDisposals_AccumulatedImpairmentAtDisposal] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'RevaluationSurplusAtDisposal') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [RevaluationSurplusAtDisposal] decimal(18,2) NOT NULL CONSTRAINT [DF_AssetDisposals_RevaluationSurplusAtDisposal] DEFAULT 0;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'CompletedAt') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [CompletedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'PostedAt') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [PostedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'FailedAt') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [FailedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'FailureReason') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [FailureReason] nvarchar(1000) NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'PostingEventId') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [PostingEventId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'WorkflowInstanceId') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [WorkflowInstanceId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetDisposals]', N'IdempotencyKey') IS NULL
                        ALTER TABLE [dbo].[AssetDisposals] ADD [IdempotencyKey] nvarchar(150) NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_FixedAssetId_BookClassification' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        CREATE INDEX [IX_AssetDisposals_TenantId_FixedAssetId_BookClassification] ON [dbo].[AssetDisposals] ([TenantId], [FixedAssetId], [BookClassification]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        CREATE UNIQUE INDEX [IX_AssetDisposals_TenantId_IdempotencyKey] ON [dbo].[AssetDisposals] ([TenantId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        CREATE INDEX [IX_AssetDisposals_TenantId_JournalEntryId] ON [dbo].[AssetDisposals] ([TenantId], [JournalEntryId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        CREATE INDEX [IX_AssetDisposals_TenantId_PostingEventId] ON [dbo].[AssetDisposals] ([TenantId], [PostingEventId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_FiscalPeriodId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        CREATE INDEX [IX_AssetDisposals_TenantId_FiscalPeriodId] ON [dbo].[AssetDisposals] ([TenantId], [FiscalPeriodId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_AccountingBookId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        CREATE INDEX [IX_AssetDisposals_TenantId_AccountingBookId] ON [dbo].[AssetDisposals] ([TenantId], [AccountingBookId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_ProceedsAccountId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        CREATE INDEX [IX_AssetDisposals_TenantId_ProceedsAccountId] ON [dbo].[AssetDisposals] ([TenantId], [ProceedsAccountId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetCategories]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_DisposalProceedsClearingAccountId')
                    ALTER TABLE [dbo].[FixedAssetCategories] ADD CONSTRAINT [FK_FixedAssetCategories_Accounts_DisposalProceedsClearingAccountId] FOREIGN KEY ([DisposalProceedsClearingAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;

                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_AccountingBooks_AccountingBookId')
                    ALTER TABLE [dbo].[AssetDisposals] ADD CONSTRAINT [FK_AssetDisposals_AccountingBooks_AccountingBookId] FOREIGN KEY ([AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FiscalPeriods]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[AssetDisposals] ADD CONSTRAINT [FK_AssetDisposals_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Accounts]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_Accounts_ProceedsAccountId')
                    ALTER TABLE [dbo].[AssetDisposals] ADD CONSTRAINT [FK_AssetDisposals_Accounts_ProceedsAccountId] FOREIGN KEY ([ProceedsAccountId]) REFERENCES [dbo].[Accounts] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[AssetDisposals] ADD CONSTRAINT [FK_AssetDisposals_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[AssetDisposals] ADD CONSTRAINT [FK_AssetDisposals_FinancePostingEvents_PostingEventId] FOREIGN KEY ([PostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[AssetDisposals] DROP CONSTRAINT [FK_AssetDisposals_FinancePostingEvents_PostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[AssetDisposals] DROP CONSTRAINT [FK_AssetDisposals_JournalEntries_JournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_Accounts_ProceedsAccountId')
                    ALTER TABLE [dbo].[AssetDisposals] DROP CONSTRAINT [FK_AssetDisposals_Accounts_ProceedsAccountId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[AssetDisposals] DROP CONSTRAINT [FK_AssetDisposals_FiscalPeriods_FiscalPeriodId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetDisposals_AccountingBooks_AccountingBookId')
                    ALTER TABLE [dbo].[AssetDisposals] DROP CONSTRAINT [FK_AssetDisposals_AccountingBooks_AccountingBookId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetCategories_Accounts_DisposalProceedsClearingAccountId')
                    ALTER TABLE [dbo].[FixedAssetCategories] DROP CONSTRAINT [FK_FixedAssetCategories_Accounts_DisposalProceedsClearingAccountId];

                IF OBJECT_ID(N'[dbo].[AssetDisposals]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_ProceedsAccountId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        DROP INDEX [IX_AssetDisposals_TenantId_ProceedsAccountId] ON [dbo].[AssetDisposals];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_AccountingBookId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        DROP INDEX [IX_AssetDisposals_TenantId_AccountingBookId] ON [dbo].[AssetDisposals];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_FiscalPeriodId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        DROP INDEX [IX_AssetDisposals_TenantId_FiscalPeriodId] ON [dbo].[AssetDisposals];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        DROP INDEX [IX_AssetDisposals_TenantId_PostingEventId] ON [dbo].[AssetDisposals];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        DROP INDEX [IX_AssetDisposals_TenantId_JournalEntryId] ON [dbo].[AssetDisposals];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        DROP INDEX [IX_AssetDisposals_TenantId_IdempotencyKey] ON [dbo].[AssetDisposals];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetDisposals_TenantId_FixedAssetId_BookClassification' AND [object_id] = OBJECT_ID(N'[dbo].[AssetDisposals]'))
                        DROP INDEX [IX_AssetDisposals_TenantId_FixedAssetId_BookClassification] ON [dbo].[AssetDisposals];
                END

                DECLARE @dropDefaults TABLE ([Name] sysname);
                INSERT INTO @dropDefaults ([Name]) VALUES
                    (N'DF_AssetDisposals_BookClassification'),
                    (N'DF_AssetDisposals_NetProceeds'),
                    (N'DF_AssetDisposals_ProceedsCurrencyCode'),
                    (N'DF_AssetDisposals_CostAtDisposal'),
                    (N'DF_AssetDisposals_AccumulatedDepreciationAtDisposal'),
                    (N'DF_AssetDisposals_AccumulatedImpairmentAtDisposal'),
                    (N'DF_AssetDisposals_RevaluationSurplusAtDisposal');

                DECLARE @constraintName sysname;
                DECLARE default_cursor CURSOR LOCAL FAST_FORWARD FOR SELECT [Name] FROM @dropDefaults;
                OPEN default_cursor;
                FETCH NEXT FROM default_cursor INTO @constraintName;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF OBJECT_ID(N'[dbo].[' + @constraintName + N']', N'D') IS NOT NULL
                        EXEC(N'ALTER TABLE [dbo].[AssetDisposals] DROP CONSTRAINT [' + @constraintName + N']');
                    FETCH NEXT FROM default_cursor INTO @constraintName;
                END
                CLOSE default_cursor;
                DEALLOCATE default_cursor;

                DECLARE @dropColumns TABLE ([TableName] sysname, [ColumnName] sysname);
                INSERT INTO @dropColumns ([TableName], [ColumnName]) VALUES
                    (N'AssetDisposals', N'AccountingDate'),
                    (N'AssetDisposals', N'FiscalPeriodId'),
                    (N'AssetDisposals', N'AccountingBookId'),
                    (N'AssetDisposals', N'BookClassification'),
                    (N'AssetDisposals', N'NetProceeds'),
                    (N'AssetDisposals', N'ProceedsCurrencyCode'),
                    (N'AssetDisposals', N'ProceedsAccountId'),
                    (N'AssetDisposals', N'CostAtDisposal'),
                    (N'AssetDisposals', N'AccumulatedDepreciationAtDisposal'),
                    (N'AssetDisposals', N'AccumulatedImpairmentAtDisposal'),
                    (N'AssetDisposals', N'RevaluationSurplusAtDisposal'),
                    (N'AssetDisposals', N'CompletedAt'),
                    (N'AssetDisposals', N'PostedAt'),
                    (N'AssetDisposals', N'FailedAt'),
                    (N'AssetDisposals', N'FailureReason'),
                    (N'AssetDisposals', N'PostingEventId'),
                    (N'AssetDisposals', N'WorkflowInstanceId'),
                    (N'AssetDisposals', N'IdempotencyKey'),
                    (N'FixedAssetCategories', N'DisposalProceedsClearingAccountId');

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
