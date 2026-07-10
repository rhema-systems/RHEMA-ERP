using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260706183000_AddFixedAssetCapitalizationFoundation")]
    public partial class AddFixedAssetCapitalizationFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'FixedAssetId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoiceLineItem] ADD [FixedAssetId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'CapitalizationJournalEntryId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoiceLineItem] ADD [CapitalizationJournalEntryId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'CapitalizationPostingEventId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoiceLineItem] ADD [CapitalizationPostingEventId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoiceLineItem]', N'CapitalizedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoiceLineItem] ADD [CapitalizedAt] datetime2 NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'CapitalizationDate') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [CapitalizationDate] datetime2 NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'FunctionalCurrencyCode') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets]
                        ADD [FunctionalCurrencyCode] nvarchar(3) NOT NULL
                            CONSTRAINT [DF_FixedAssets_FunctionalCurrencyCode] DEFAULT N'GHS';
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'TransactionCurrencyCode') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [TransactionCurrencyCode] nvarchar(3) NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'ExchangeRate') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [ExchangeRate] decimal(18,6) NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'ExchangeRateId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [ExchangeRateId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'ExchangeRateDate') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [ExchangeRateDate] datetime2 NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'SourceDocumentType') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [SourceDocumentType] nvarchar(50) NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'SourceDocumentId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [SourceDocumentId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'SourceDocumentLineId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [SourceDocumentLineId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'JournalEntryId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [JournalEntryId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'PostingEventId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [PostingEventId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'CapitalizedAt') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [CapitalizedAt] datetime2 NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssetBookValues]', N'CapitalizationDate') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues] ADD [CapitalizationDate] datetime2 NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssetBookValues]', N'CapitalizationJournalEntryId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues] ADD [CapitalizationJournalEntryId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssetBookValues]', N'CapitalizationPostingEventId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues] ADD [CapitalizationPostingEventId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssetBookValues]', N'SourceDocumentType') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues] ADD [SourceDocumentType] nvarchar(50) NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssetBookValues]', N'SourceDocumentId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues] ADD [SourceDocumentId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssetBookValues]', N'SourceDocumentLineId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues] ADD [SourceDocumentLineId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorInvoiceLineItem_TenantId_FixedAssetId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]'))
                BEGIN
                    CREATE INDEX [IX_VendorInvoiceLineItem_TenantId_FixedAssetId]
                        ON [dbo].[VendorInvoiceLineItem] ([TenantId], [FixedAssetId]);
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorInvoiceLineItem_TenantId_CapitalizationPostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]'))
                BEGIN
                    CREATE INDEX [IX_VendorInvoiceLineItem_TenantId_CapitalizationPostingEventId]
                        ON [dbo].[VendorInvoiceLineItem] ([TenantId], [CapitalizationPostingEventId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_TenantId_SourceDocumentType_SourceDocumentId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                BEGIN
                    CREATE INDEX [IX_FixedAssets_TenantId_SourceDocumentType_SourceDocumentId]
                        ON [dbo].[FixedAssets] ([TenantId], [SourceDocumentType], [SourceDocumentId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                BEGIN
                    CREATE INDEX [IX_FixedAssets_TenantId_JournalEntryId]
                        ON [dbo].[FixedAssets] ([TenantId], [JournalEntryId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                BEGIN
                    CREATE INDEX [IX_FixedAssets_TenantId_PostingEventId]
                        ON [dbo].[FixedAssets] ([TenantId], [PostingEventId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssetBookValues_TenantId_CapitalizationPostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]'))
                BEGIN
                    CREATE INDEX [IX_FixedAssetBookValues_TenantId_CapitalizationPostingEventId]
                        ON [dbo].[FixedAssetBookValues] ([TenantId], [CapitalizationPostingEventId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssetBookValues_TenantId_CapitalizationJournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]'))
                BEGIN
                    CREATE INDEX [IX_FixedAssetBookValues_TenantId_CapitalizationJournalEntryId]
                        ON [dbo].[FixedAssetBookValues] ([TenantId], [CapitalizationJournalEntryId]);
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorInvoiceLineItem_FixedAssets_FixedAssetId')
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoiceLineItem]
                        ADD CONSTRAINT [FK_VendorInvoiceLineItem_FixedAssets_FixedAssetId]
                        FOREIGN KEY ([FixedAssetId]) REFERENCES [dbo].[FixedAssets] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_JournalEntries_JournalEntryId')
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets]
                        ADD CONSTRAINT [FK_FixedAssets_JournalEntries_JournalEntryId]
                        FOREIGN KEY ([JournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_FinancePostingEvents_PostingEventId')
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets]
                        ADD CONSTRAINT [FK_FixedAssets_FinancePostingEvents_PostingEventId]
                        FOREIGN KEY ([PostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[ExchangeRates]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_ExchangeRates_ExchangeRateId')
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets]
                        ADD CONSTRAINT [FK_FixedAssets_ExchangeRates_ExchangeRateId]
                        FOREIGN KEY ([ExchangeRateId]) REFERENCES [dbo].[ExchangeRates] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[JournalEntries]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetBookValues_JournalEntries_CapitalizationJournalEntryId')
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues]
                        ADD CONSTRAINT [FK_FixedAssetBookValues_JournalEntries_CapitalizationJournalEntryId]
                        FOREIGN KEY ([CapitalizationJournalEntryId]) REFERENCES [dbo].[JournalEntries] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[FinancePostingEvents]', N'U') IS NOT NULL
                    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetBookValues_FinancePostingEvents_CapitalizationPostingEventId')
                BEGIN
                    ALTER TABLE [dbo].[FixedAssetBookValues]
                        ADD CONSTRAINT [FK_FixedAssetBookValues_FinancePostingEvents_CapitalizationPostingEventId]
                        FOREIGN KEY ([CapitalizationPostingEventId]) REFERENCES [dbo].[FinancePostingEvents] ([Id]) ON DELETE NO ACTION;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetBookValues_FinancePostingEvents_CapitalizationPostingEventId')
                    ALTER TABLE [dbo].[FixedAssetBookValues] DROP CONSTRAINT [FK_FixedAssetBookValues_FinancePostingEvents_CapitalizationPostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssetBookValues_JournalEntries_CapitalizationJournalEntryId')
                    ALTER TABLE [dbo].[FixedAssetBookValues] DROP CONSTRAINT [FK_FixedAssetBookValues_JournalEntries_CapitalizationJournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_ExchangeRates_ExchangeRateId')
                    ALTER TABLE [dbo].[FixedAssets] DROP CONSTRAINT [FK_FixedAssets_ExchangeRates_ExchangeRateId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_FinancePostingEvents_PostingEventId')
                    ALTER TABLE [dbo].[FixedAssets] DROP CONSTRAINT [FK_FixedAssets_FinancePostingEvents_PostingEventId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_FixedAssets_JournalEntries_JournalEntryId')
                    ALTER TABLE [dbo].[FixedAssets] DROP CONSTRAINT [FK_FixedAssets_JournalEntries_JournalEntryId];
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = N'FK_VendorInvoiceLineItem_FixedAssets_FixedAssetId')
                    ALTER TABLE [dbo].[VendorInvoiceLineItem] DROP CONSTRAINT [FK_VendorInvoiceLineItem_FixedAssets_FixedAssetId];

                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorInvoiceLineItem_TenantId_CapitalizationPostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]'))
                    DROP INDEX [IX_VendorInvoiceLineItem_TenantId_CapitalizationPostingEventId] ON [dbo].[VendorInvoiceLineItem];
                IF OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_VendorInvoiceLineItem_TenantId_FixedAssetId' AND [object_id] = OBJECT_ID(N'[dbo].[VendorInvoiceLineItem]'))
                    DROP INDEX [IX_VendorInvoiceLineItem_TenantId_FixedAssetId] ON [dbo].[VendorInvoiceLineItem];
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_TenantId_PostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                    DROP INDEX [IX_FixedAssets_TenantId_PostingEventId] ON [dbo].[FixedAssets];
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_TenantId_JournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                    DROP INDEX [IX_FixedAssets_TenantId_JournalEntryId] ON [dbo].[FixedAssets];
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssets_TenantId_SourceDocumentType_SourceDocumentId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]'))
                    DROP INDEX [IX_FixedAssets_TenantId_SourceDocumentType_SourceDocumentId] ON [dbo].[FixedAssets];
                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssetBookValues_TenantId_CapitalizationPostingEventId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]'))
                    DROP INDEX [IX_FixedAssetBookValues_TenantId_CapitalizationPostingEventId] ON [dbo].[FixedAssetBookValues];
                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_FixedAssetBookValues_TenantId_CapitalizationJournalEntryId' AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]'))
                    DROP INDEX [IX_FixedAssetBookValues_TenantId_CapitalizationJournalEntryId] ON [dbo].[FixedAssetBookValues];

                IF OBJECT_ID(N'[dbo].[DF_FixedAssets_FunctionalCurrencyCode]', N'D') IS NOT NULL
                    ALTER TABLE [dbo].[FixedAssets] DROP CONSTRAINT [DF_FixedAssets_FunctionalCurrencyCode];

                DECLARE @dropColumns TABLE ([TableName] sysname, [ColumnName] sysname);
                INSERT INTO @dropColumns ([TableName], [ColumnName]) VALUES
                    (N'VendorInvoiceLineItem', N'FixedAssetId'),
                    (N'VendorInvoiceLineItem', N'CapitalizationJournalEntryId'),
                    (N'VendorInvoiceLineItem', N'CapitalizationPostingEventId'),
                    (N'VendorInvoiceLineItem', N'CapitalizedAt'),
                    (N'FixedAssets', N'CapitalizationDate'),
                    (N'FixedAssets', N'FunctionalCurrencyCode'),
                    (N'FixedAssets', N'TransactionCurrencyCode'),
                    (N'FixedAssets', N'ExchangeRate'),
                    (N'FixedAssets', N'ExchangeRateId'),
                    (N'FixedAssets', N'ExchangeRateDate'),
                    (N'FixedAssets', N'SourceDocumentType'),
                    (N'FixedAssets', N'SourceDocumentId'),
                    (N'FixedAssets', N'SourceDocumentLineId'),
                    (N'FixedAssets', N'JournalEntryId'),
                    (N'FixedAssets', N'PostingEventId'),
                    (N'FixedAssets', N'CapitalizedAt'),
                    (N'FixedAssetBookValues', N'CapitalizationDate'),
                    (N'FixedAssetBookValues', N'CapitalizationJournalEntryId'),
                    (N'FixedAssetBookValues', N'CapitalizationPostingEventId'),
                    (N'FixedAssetBookValues', N'SourceDocumentType'),
                    (N'FixedAssetBookValues', N'SourceDocumentId'),
                    (N'FixedAssetBookValues', N'SourceDocumentLineId');

                DECLARE @tableName sysname;
                DECLARE @columnName sysname;
                DECLARE drop_cursor CURSOR LOCAL FAST_FORWARD FOR
                    SELECT [TableName], [ColumnName] FROM @dropColumns;

                OPEN drop_cursor;
                FETCH NEXT FROM drop_cursor INTO @tableName, @columnName;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    IF OBJECT_ID(N'[dbo].[' + @tableName + N']', N'U') IS NOT NULL
                        AND COL_LENGTH(N'[dbo].[' + @tableName + N']', @columnName) IS NOT NULL
                    BEGIN
                        EXEC(N'ALTER TABLE [dbo].[' + @tableName + N'] DROP COLUMN [' + @columnName + N']');
                    END
                    FETCH NEXT FROM drop_cursor INTO @tableName, @columnName;
                END
                CLOSE drop_cursor;
                DEALLOCATE drop_cursor;
                """);
        }
    }
}
