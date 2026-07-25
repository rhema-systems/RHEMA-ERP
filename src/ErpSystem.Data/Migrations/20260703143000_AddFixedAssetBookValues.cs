using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260703143000_AddFixedAssetBookValues")]
    public partial class AddFixedAssetBookValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AssetTransactions]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetTransactions]', N'AccountingBookId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetTransactions] ADD [AccountingBookId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[AssetTransactions]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetTransactions]', N'BookClassification') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetTransactions]
                        ADD [BookClassification] nvarchar(20) NOT NULL
                            CONSTRAINT [DF_AssetTransactions_BookClassification] DEFAULT N'IFRS';
                END

                IF OBJECT_ID(N'[dbo].[AssetTransactions]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetTransactions]', N'AccountingBookId') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE [name] = N'FK_AssetTransactions_AccountingBooks_AccountingBookId'
                    )
                BEGIN
                    ALTER TABLE [dbo].[AssetTransactions]
                        ADD CONSTRAINT [FK_AssetTransactions_AccountingBooks_AccountingBookId]
                        FOREIGN KEY ([AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[AssetTransactions]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetTransactions]', N'AccountingBookId') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_AssetTransactions_AccountingBookId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransactions]')
                    )
                BEGIN
                    CREATE INDEX [IX_AssetTransactions_AccountingBookId]
                        ON [dbo].[AssetTransactions] ([AccountingBookId]);
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetDepreciationSchedules]', N'AccountingBookId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] ADD [AccountingBookId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetDepreciationSchedules]', N'BookClassification') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetDepreciationSchedules]
                        ADD [BookClassification] nvarchar(20) NOT NULL
                            CONSTRAINT [DF_AssetDepreciationSchedules_BookClassification] DEFAULT N'IFRS';
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetDepreciationSchedules]', N'AccountingBookId') IS NOT NULL
                    AND OBJECT_ID(N'[dbo].[AccountingBooks]', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE [name] = N'FK_AssetDepreciationSchedules_AccountingBooks_AccountingBookId'
                    )
                BEGIN
                    ALTER TABLE [dbo].[AssetDepreciationSchedules]
                        ADD CONSTRAINT [FK_AssetDepreciationSchedules_AccountingBooks_AccountingBookId]
                        FOREIGN KEY ([AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([Id]) ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetDepreciationSchedules]', N'AccountingBookId') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_AssetDepreciationSchedules_AccountingBookId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]')
                    )
                BEGIN
                    CREATE INDEX [IX_AssetDepreciationSchedules_AccountingBookId]
                        ON [dbo].[AssetDepreciationSchedules] ([AccountingBookId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[FixedAssetBookValues] (
                        [Id] uniqueidentifier NOT NULL,
                        [TenantId] uniqueidentifier NOT NULL,
                        [FixedAssetId] uniqueidentifier NOT NULL,
                        [AccountingBookId] uniqueidentifier NOT NULL,
                        [BookClassification] nvarchar(20) NOT NULL,
                        [AcquisitionCost] decimal(18,2) NOT NULL,
                        [AccumulatedDepreciation] decimal(18,2) NOT NULL,
                        [NetBookValue] decimal(18,2) NOT NULL,
                        [ResidualValue] decimal(18,2) NOT NULL,
                        [UsefulLifeMonths] int NOT NULL,
                        [RemainingUsefulLifeMonths] int NULL,
                        [DepreciationMethod] int NOT NULL,
                        [DepreciationConvention] int NOT NULL,
                        [PlacedInServiceDate] datetime2 NULL,
                        [OpeningAsOfDate] datetime2 NULL,
                        [OpeningYtdDepreciation] decimal(18,2) NOT NULL,
                        [LastDepreciationDate] datetime2 NULL,
                        [OpeningJournalEntryId] uniqueidentifier NULL,
                        [OpeningPostedToGl] bit NOT NULL,
                        [OpeningPostedDate] datetime2 NULL,
                        [OpeningSource] nvarchar(50) NOT NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [CreatedBy] nvarchar(max) NULL,
                        [UpdatedBy] nvarchar(max) NULL,
                        [CreatedById] uniqueidentifier NULL,
                        [LastModifiedById] uniqueidentifier NULL,
                        [IsDeleted] bit NOT NULL,
                        [DeletedAt] datetime2 NULL,
                        [DeletedBy] nvarchar(max) NULL,
                        CONSTRAINT [PK_FixedAssetBookValues] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_FixedAssetBookValues_AccountingBooks_AccountingBookId] FOREIGN KEY ([AccountingBookId]) REFERENCES [dbo].[AccountingBooks] ([Id]) ON DELETE NO ACTION,
                        CONSTRAINT [FK_FixedAssetBookValues_FixedAssets_FixedAssetId] FOREIGN KEY ([FixedAssetId]) REFERENCES [dbo].[FixedAssets] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_FixedAssetBookValues_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE NO ACTION
                    );
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_FixedAssetBookValues_AccountingBookId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]')
                    )
                BEGIN
                    CREATE INDEX [IX_FixedAssetBookValues_AccountingBookId]
                        ON [dbo].[FixedAssetBookValues] ([AccountingBookId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_FixedAssetBookValues_FixedAssetId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]')
                    )
                BEGIN
                    CREATE INDEX [IX_FixedAssetBookValues_FixedAssetId]
                        ON [dbo].[FixedAssetBookValues] ([FixedAssetId]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_FixedAssetBookValues_TenantId_BookClassification'
                          AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]')
                    )
                BEGIN
                    CREATE INDEX [IX_FixedAssetBookValues_TenantId_BookClassification]
                        ON [dbo].[FixedAssetBookValues] ([TenantId], [BookClassification]);
                END

                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_FixedAssetBookValues_TenantId_FixedAssetId_AccountingBookId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssetBookValues]')
                    )
                BEGIN
                    CREATE UNIQUE INDEX [IX_FixedAssetBookValues_TenantId_FixedAssetId_AccountingBookId]
                        ON [dbo].[FixedAssetBookValues] ([TenantId], [FixedAssetId], [AccountingBookId]);
                END
            """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetDepreciationSchedules]', N'BookClassification') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification'
                          AND [object_id] = OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]')
                    )
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM [dbo].[AssetDepreciationSchedules]
                        GROUP BY [TenantId], [FixedAssetId], [FiscalPeriodId], [BookClassification]
                        HAVING COUNT(*) > 1
                    )
                    BEGIN
                        THROW 51000, 'Cannot create per-book fixed asset depreciation uniqueness because duplicate schedules exist for the same tenant, asset, fiscal period, and book.', 1;
                    END

                    CREATE UNIQUE INDEX [IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification]
                        ON [dbo].[AssetDepreciationSchedules] ([TenantId], [FixedAssetId], [FiscalPeriodId], [BookClassification]);
                END
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FixedAssetBookValues]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [dbo].[FixedAssetBookValues];
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification'
                          AND [object_id] = OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]')
                    )
                BEGIN
                    DROP INDEX [IX_AssetDepreciationSchedules_TenantId_FixedAssetId_FiscalPeriodId_BookClassification]
                        ON [dbo].[AssetDepreciationSchedules];
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_AssetDepreciationSchedules_AccountingBookId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]')
                    )
                BEGIN
                    DROP INDEX [IX_AssetDepreciationSchedules_AccountingBookId]
                        ON [dbo].[AssetDepreciationSchedules];
                END

                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE [name] = N'FK_AssetDepreciationSchedules_AccountingBooks_AccountingBookId'
                )
                BEGIN
                    ALTER TABLE [dbo].[AssetDepreciationSchedules]
                        DROP CONSTRAINT [FK_AssetDepreciationSchedules_AccountingBooks_AccountingBookId];
                END

                IF OBJECT_ID(N'[dbo].[DF_AssetDepreciationSchedules_BookClassification]', N'D') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetDepreciationSchedules]
                        DROP CONSTRAINT [DF_AssetDepreciationSchedules_BookClassification];
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetDepreciationSchedules]', N'BookClassification') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] DROP COLUMN [BookClassification];
                END

                IF OBJECT_ID(N'[dbo].[AssetDepreciationSchedules]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetDepreciationSchedules]', N'AccountingBookId') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetDepreciationSchedules] DROP COLUMN [AccountingBookId];
                END

                IF OBJECT_ID(N'[dbo].[AssetTransactions]', N'U') IS NOT NULL
                    AND EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [name] = N'IX_AssetTransactions_AccountingBookId'
                          AND [object_id] = OBJECT_ID(N'[dbo].[AssetTransactions]')
                    )
                BEGIN
                    DROP INDEX [IX_AssetTransactions_AccountingBookId] ON [dbo].[AssetTransactions];
                END

                IF EXISTS (
                    SELECT 1 FROM sys.foreign_keys
                    WHERE [name] = N'FK_AssetTransactions_AccountingBooks_AccountingBookId'
                )
                BEGIN
                    ALTER TABLE [dbo].[AssetTransactions]
                        DROP CONSTRAINT [FK_AssetTransactions_AccountingBooks_AccountingBookId];
                END

                IF OBJECT_ID(N'[dbo].[DF_AssetTransactions_BookClassification]', N'D') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetTransactions]
                        DROP CONSTRAINT [DF_AssetTransactions_BookClassification];
                END

                IF OBJECT_ID(N'[dbo].[AssetTransactions]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetTransactions]', N'BookClassification') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetTransactions] DROP COLUMN [BookClassification];
                END

                IF OBJECT_ID(N'[dbo].[AssetTransactions]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[AssetTransactions]', N'AccountingBookId') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[AssetTransactions] DROP COLUMN [AccountingBookId];
                END
                """);
        }
    }
}
