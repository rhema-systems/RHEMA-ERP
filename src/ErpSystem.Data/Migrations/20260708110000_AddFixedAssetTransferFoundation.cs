using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260708110000_AddFixedAssetTransferFoundation")]
    public partial class AddFixedAssetTransferFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[FixedAssets]', N'CurrentCustodianId') IS NULL
                        ALTER TABLE [dbo].[FixedAssets] ADD [CurrentCustodianId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[FixedAssets]', N'CurrentSegmentString') IS NULL
                        ALTER TABLE [dbo].[FixedAssets] ADD [CurrentSegmentString] nvarchar(500) NULL;
                    IF COL_LENGTH(N'[dbo].[FixedAssets]', N'CurrentSegmentLookupValueId') IS NULL
                        ALTER TABLE [dbo].[FixedAssets] ADD [CurrentSegmentLookupValueId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'AccountingDate') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [AccountingDate] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'FiscalPeriodId') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [FiscalPeriodId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'FromSegmentString') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [FromSegmentString] nvarchar(500) NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'FromSegmentLookupValueId') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [FromSegmentLookupValueId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'ToSegmentString') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [ToSegmentString] nvarchar(500) NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'ToSegmentLookupValueId') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [ToSegmentLookupValueId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'CompletedAt') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [CompletedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'PostedAt') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [PostedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'FailedAt') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [FailedAt] datetime2 NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'FailureReason') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [FailureReason] nvarchar(1000) NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'IdempotencyKey') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [IdempotencyKey] nvarchar(150) NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'WorkflowInstanceId') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [WorkflowInstanceId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'JournalEntryId') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [JournalEntryId] uniqueidentifier NULL;
                    IF COL_LENGTH(N'[dbo].[AssetTransfers]', N'PostingEventId') IS NULL
                        ALTER TABLE [dbo].[AssetTransfers] ADD [PostingEventId] uniqueidentifier NULL;
                END
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_CurrentCustodianId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                        CREATE INDEX [IX_FixedAssets_CurrentCustodianId] ON [dbo].[FixedAssets] ([CurrentCustodianId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_CurrentSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                        CREATE INDEX [IX_FixedAssets_CurrentSegmentLookupValueId] ON [dbo].[FixedAssets] ([CurrentSegmentLookupValueId]);
                END

                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_FixedAssetId_TransferDate' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE INDEX [IX_AssetTransfers_TenantId_FixedAssetId_TransferDate] ON [dbo].[AssetTransfers] ([TenantId], [FixedAssetId], [TransferDate]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE UNIQUE INDEX [IX_AssetTransfers_TenantId_IdempotencyKey] ON [dbo].[AssetTransfers] ([TenantId], [IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE INDEX [IX_AssetTransfers_TenantId_JournalEntryId] ON [dbo].[AssetTransfers] ([TenantId], [JournalEntryId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE INDEX [IX_AssetTransfers_TenantId_PostingEventId] ON [dbo].[AssetTransfers] ([TenantId], [PostingEventId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_ToSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE INDEX [IX_AssetTransfers_TenantId_ToSegmentLookupValueId] ON [dbo].[AssetTransfers] ([TenantId], [ToSegmentLookupValueId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_FiscalPeriodId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE INDEX [IX_AssetTransfers_FiscalPeriodId] ON [dbo].[AssetTransfers] ([FiscalPeriodId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_FromSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE INDEX [IX_AssetTransfers_FromSegmentLookupValueId] ON [dbo].[AssetTransfers] ([FromSegmentLookupValueId]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_ToSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        CREATE INDEX [IX_AssetTransfers_ToSegmentLookupValueId] ON [dbo].[AssetTransfers] ([ToSegmentLookupValueId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[Employees]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_Employees_CurrentCustodianId')
                    ALTER TABLE [dbo].[FixedAssets] ADD CONSTRAINT [FK_FixedAssets_Employees_CurrentCustodianId] FOREIGN KEY ([CurrentCustodianId]) REFERENCES [dbo].[Employees] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[SegmentLookupValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_SegmentLookupValues_CurrentSegmentLookupValueId')
                    ALTER TABLE [dbo].[FixedAssets] ADD CONSTRAINT [FK_FixedAssets_SegmentLookupValues_CurrentSegmentLookupValueId] FOREIGN KEY ([CurrentSegmentLookupValueId]) REFERENCES [dbo].[SegmentLookupValues] ([Id]) ON DELETE NO ACTION;

                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FiscalPeriods]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[AssetTransfers] ADD CONSTRAINT [FK_AssetTransfers_FiscalPeriods_FiscalPeriodId] FOREIGN KEY ([FiscalPeriodId]) REFERENCES [dbo].[FiscalPeriods] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[SegmentLookupValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_SegmentLookupValues_FromSegmentLookupValueId')
                    ALTER TABLE [dbo].[AssetTransfers] ADD CONSTRAINT [FK_AssetTransfers_SegmentLookupValues_FromSegmentLookupValueId] FOREIGN KEY ([FromSegmentLookupValueId]) REFERENCES [dbo].[SegmentLookupValues] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[SegmentLookupValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_SegmentLookupValues_ToSegmentLookupValueId')
                    ALTER TABLE [dbo].[AssetTransfers] ADD CONSTRAINT [FK_AssetTransfers_SegmentLookupValues_ToSegmentLookupValueId] FOREIGN KEY ([ToSegmentLookupValueId]) REFERENCES [dbo].[SegmentLookupValues] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[AssetTransfers] ADD CONSTRAINT [FK_AssetTransfers_JournalEntries_JournalEntryId] FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;
                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[AssetTransfers] ADD CONSTRAINT [FK_AssetTransfers_FinancePostingEvents_PostingEventId] FOREIGN KEY ([PostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[AssetTransfers] DROP CONSTRAINT [FK_AssetTransfers_FinancePostingEvents_PostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[AssetTransfers] DROP CONSTRAINT [FK_AssetTransfers_JournalEntries_JournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_SegmentLookupValues_ToSegmentLookupValueId')
                    ALTER TABLE [dbo].[AssetTransfers] DROP CONSTRAINT [FK_AssetTransfers_SegmentLookupValues_ToSegmentLookupValueId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_SegmentLookupValues_FromSegmentLookupValueId')
                    ALTER TABLE [dbo].[AssetTransfers] DROP CONSTRAINT [FK_AssetTransfers_SegmentLookupValues_FromSegmentLookupValueId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_AssetTransfers_FiscalPeriods_FiscalPeriodId')
                    ALTER TABLE [dbo].[AssetTransfers] DROP CONSTRAINT [FK_AssetTransfers_FiscalPeriods_FiscalPeriodId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_SegmentLookupValues_CurrentSegmentLookupValueId')
                    ALTER TABLE [dbo].[FixedAssets] DROP CONSTRAINT [FK_FixedAssets_SegmentLookupValues_CurrentSegmentLookupValueId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_Employees_CurrentCustodianId')
                    ALTER TABLE [dbo].[FixedAssets] DROP CONSTRAINT [FK_FixedAssets_Employees_CurrentCustodianId];

                IF OBJECT_ID(N'[dbo].[AssetTransfers]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_ToSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_ToSegmentLookupValueId] ON [dbo].[AssetTransfers];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_FromSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_FromSegmentLookupValueId] ON [dbo].[AssetTransfers];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_FiscalPeriodId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_FiscalPeriodId] ON [dbo].[AssetTransfers];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_ToSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_TenantId_ToSegmentLookupValueId] ON [dbo].[AssetTransfers];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_TenantId_PostingEventId] ON [dbo].[AssetTransfers];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_TenantId_JournalEntryId] ON [dbo].[AssetTransfers];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_IdempotencyKey' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_TenantId_IdempotencyKey] ON [dbo].[AssetTransfers];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_AssetTransfers_TenantId_FixedAssetId_TransferDate' AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransfers]'))
                        DROP INDEX [IX_AssetTransfers_TenantId_FixedAssetId_TransferDate] ON [dbo].[AssetTransfers];
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_CurrentSegmentLookupValueId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                        DROP INDEX [IX_FixedAssets_CurrentSegmentLookupValueId] ON [dbo].[FixedAssets];
                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_CurrentCustodianId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                        DROP INDEX [IX_FixedAssets_CurrentCustodianId] ON [dbo].[FixedAssets];
                END

                DECLARE @dropColumns TABLE ([TableName] sysname, [ColumnName] sysname);
                INSERT INTO @dropColumns ([TableName], [ColumnName]) VALUES
                    (N'AssetTransfers', N'AccountingDate'),
                    (N'AssetTransfers', N'FiscalPeriodId'),
                    (N'AssetTransfers', N'FromSegmentString'),
                    (N'AssetTransfers', N'FromSegmentLookupValueId'),
                    (N'AssetTransfers', N'ToSegmentString'),
                    (N'AssetTransfers', N'ToSegmentLookupValueId'),
                    (N'AssetTransfers', N'CompletedAt'),
                    (N'AssetTransfers', N'PostedAt'),
                    (N'AssetTransfers', N'FailedAt'),
                    (N'AssetTransfers', N'FailureReason'),
                    (N'AssetTransfers', N'IdempotencyKey'),
                    (N'AssetTransfers', N'WorkflowInstanceId'),
                    (N'AssetTransfers', N'JournalEntryId'),
                    (N'AssetTransfers', N'PostingEventId'),
                    (N'FixedAssets', N'CurrentCustodianId'),
                    (N'FixedAssets', N'CurrentSegmentString'),
                    (N'FixedAssets', N'CurrentSegmentLookupValueId');

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
