using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFixedAssetLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'Location') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] ADD [Location] nvarchar(500) NULL;
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND NOT EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [name] = N'IX_FixedAssets_TenantId_AssetCode'
                          AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]')
                    )
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM [dbo].[FixedAssets]
                        GROUP BY [TenantId], [AssetCode]
                        HAVING COUNT(*) > 1
                    )
                    BEGIN
                        THROW 51000, 'Cannot create IX_FixedAssets_TenantId_AssetCode because duplicate fixed asset codes exist in one or more tenants.', 1;
                    END

                    CREATE UNIQUE INDEX [IX_FixedAssets_TenantId_AssetCode]
                        ON [dbo].[FixedAssets] ([TenantId], [AssetCode]);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE [name] = N'IX_FixedAssets_TenantId_AssetCode'
                          AND [object_id] = OBJECT_ID(N'[dbo].[FixedAssets]')
                    )
                BEGIN
                    DROP INDEX [IX_FixedAssets_TenantId_AssetCode] ON [dbo].[FixedAssets];
                END

                IF OBJECT_ID(N'[dbo].[FixedAssets]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[FixedAssets]', N'Location') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[FixedAssets] DROP COLUMN [Location];
                END
                """);
        }
    }
}
