using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260910120000_AddEstateLandSubDemarcationDisposition")]
public sealed class AddEstateLandSubDemarcationDisposition : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentDemarcationId') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ParentDemarcationId] uniqueidentifier NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'CostAllocationMethod') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [CostAllocationMethod] nvarchar(40) NOT NULL
                    CONSTRAINT [DF_EstateLandDemarcations_CostAllocationMethod] DEFAULT N'NotSet';

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'AllocatedCost') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [AllocatedCost] decimal(18,2) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'CostPerAcre') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [CostPerAcre] decimal(18,2) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'TargetSalePrice') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [TargetSalePrice] decimal(18,2) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentLandAssetReference') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ParentLandAssetReference] nvarchar(120) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentFixedAssetReference') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ParentFixedAssetReference] nvarchar(120) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ChildFixedAssetReference') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ChildFixedAssetReference] nvarchar(120) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'FixedAssetPostingStatus') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [FixedAssetPostingStatus] nvarchar(40) NOT NULL
                    CONSTRAINT [DF_EstateLandDemarcations_FixedAssetPostingStatus] DEFAULT N'NotReady';

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'FixedAssetPostedAt') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [FixedAssetPostedAt] datetime2 NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'IsReadyForProjectManagement') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [IsReadyForProjectManagement] bit NOT NULL
                    CONSTRAINT [DF_EstateLandDemarcations_IsReadyForProjectManagement] DEFAULT CAST(0 AS bit);

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'IsPublishedToExternalPortal') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [IsPublishedToExternalPortal] bit NOT NULL
                    CONSTRAINT [DF_EstateLandDemarcations_IsPublishedToExternalPortal] DEFAULT CAST(0 AS bit);

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingType') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingType] nvarchar(40) NOT NULL
                    CONSTRAINT [DF_EstateLandDemarcations_ExternalListingType] DEFAULT N'None';

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingStatus') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingStatus] nvarchar(40) NOT NULL
                    CONSTRAINT [DF_EstateLandDemarcations_ExternalListingStatus] DEFAULT N'Draft';

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingPrice') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingPrice] decimal(18,2) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalSalePrice') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalSalePrice] decimal(18,2) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalMonthlyRent') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalMonthlyRent] decimal(18,2) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalLeaseTermMonths') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalLeaseTermMonths] int NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingCurrency') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingCurrency] nvarchar(10) NOT NULL
                    CONSTRAINT [DF_EstateLandDemarcations_ExternalListingCurrency] DEFAULT N'GHS';

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingNotes') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalListingNotes] nvarchar(2000) NULL;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalPublishedAt') IS NULL
                ALTER TABLE [dbo].[EstateLandDemarcations] ADD [ExternalPublishedAt] datetime2 NULL;

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_EstateLandDemarcations_TenantId_EstateManagedAssetId_ParentDemarcationId'
                  AND [object_id] = OBJECT_ID(N'[dbo].[EstateLandDemarcations]'))
                CREATE INDEX [IX_EstateLandDemarcations_TenantId_EstateManagedAssetId_ParentDemarcationId]
                    ON [dbo].[EstateLandDemarcations] ([TenantId], [EstateManagedAssetId], [ParentDemarcationId]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_EstateLandDemarcations_TenantId_IsReadyForProjectManagement_IsPublishedToExternalPortal'
                  AND [object_id] = OBJECT_ID(N'[dbo].[EstateLandDemarcations]'))
                CREATE INDEX [IX_EstateLandDemarcations_TenantId_IsReadyForProjectManagement_IsPublishedToExternalPortal]
                    ON [dbo].[EstateLandDemarcations] ([TenantId], [IsReadyForProjectManagement], [IsPublishedToExternalPortal]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_EstateLandDemarcations_EstateLandDemarcations_ParentDemarcationId')
                ALTER TABLE [dbo].[EstateLandDemarcations]
                    ADD CONSTRAINT [FK_EstateLandDemarcations_EstateLandDemarcations_ParentDemarcationId]
                    FOREIGN KEY ([ParentDemarcationId])
                    REFERENCES [dbo].[EstateLandDemarcations] ([Id])
                    ON DELETE NO ACTION;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE [name] = N'FK_EstateLandDemarcations_EstateLandDemarcations_ParentDemarcationId')
                ALTER TABLE [dbo].[EstateLandDemarcations]
                    DROP CONSTRAINT [FK_EstateLandDemarcations_EstateLandDemarcations_ParentDemarcationId];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_EstateLandDemarcations_TenantId_IsReadyForProjectManagement_IsPublishedToExternalPortal'
                  AND [object_id] = OBJECT_ID(N'[dbo].[EstateLandDemarcations]'))
                DROP INDEX [IX_EstateLandDemarcations_TenantId_IsReadyForProjectManagement_IsPublishedToExternalPortal]
                    ON [dbo].[EstateLandDemarcations];

            IF EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE [name] = N'IX_EstateLandDemarcations_TenantId_EstateManagedAssetId_ParentDemarcationId'
                  AND [object_id] = OBJECT_ID(N'[dbo].[EstateLandDemarcations]'))
                DROP INDEX [IX_EstateLandDemarcations_TenantId_EstateManagedAssetId_ParentDemarcationId]
                    ON [dbo].[EstateLandDemarcations];

            DECLARE @sql nvarchar(max) = N'';
            SELECT @sql += N'ALTER TABLE [dbo].[EstateLandDemarcations] DROP CONSTRAINT [' + dc.[name] + N'];'
            FROM sys.default_constraints dc
            JOIN sys.columns c ON c.[object_id] = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[EstateLandDemarcations]')
              AND c.[name] IN (
                N'CostAllocationMethod',
                N'FixedAssetPostingStatus',
                N'IsReadyForProjectManagement',
                N'IsPublishedToExternalPortal',
                N'ExternalListingType',
                N'ExternalListingStatus',
                N'ExternalListingCurrency');
            IF @sql <> N'' EXEC sp_executesql @sql;

            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalPublishedAt') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalPublishedAt];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingNotes') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalListingNotes];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingCurrency') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalListingCurrency];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalLeaseTermMonths') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalLeaseTermMonths];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalMonthlyRent') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalMonthlyRent];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalSalePrice') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalSalePrice];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingPrice') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalListingPrice];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingStatus') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalListingStatus];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ExternalListingType') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ExternalListingType];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'IsPublishedToExternalPortal') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [IsPublishedToExternalPortal];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'IsReadyForProjectManagement') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [IsReadyForProjectManagement];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'FixedAssetPostedAt') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [FixedAssetPostedAt];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'FixedAssetPostingStatus') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [FixedAssetPostingStatus];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ChildFixedAssetReference') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ChildFixedAssetReference];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentFixedAssetReference') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ParentFixedAssetReference];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentLandAssetReference') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ParentLandAssetReference];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'TargetSalePrice') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [TargetSalePrice];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'CostPerAcre') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [CostPerAcre];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'AllocatedCost') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [AllocatedCost];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'CostAllocationMethod') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [CostAllocationMethod];
            IF COL_LENGTH('dbo.EstateLandDemarcations', 'ParentDemarcationId') IS NOT NULL ALTER TABLE [dbo].[EstateLandDemarcations] DROP COLUMN [ParentDemarcationId];
            """);
    }
}
