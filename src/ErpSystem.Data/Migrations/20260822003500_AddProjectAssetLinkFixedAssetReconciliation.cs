using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Extends the existing Project asset-link owner with Finance fixed-asset lineage.  It does not
/// create assets, post Finance entries, or replace Property/Maintenance history; it makes the
/// cross-owner reconciliation retry-safe and tenant-safe.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822003500_AddProjectAssetLinkFixedAssetReconciliation")]
public partial class AddProjectAssetLinkFixedAssetReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF COL_LENGTH('dbo.ProjectAssetLinks', 'FixedAssetId') IS NULL
                ALTER TABLE dbo.ProjectAssetLinks ADD FixedAssetId uniqueidentifier NULL;
            IF COL_LENGTH('dbo.ProjectAssetLinks', 'ReconciliationKey') IS NULL
                ALTER TABLE dbo.ProjectAssetLinks ADD ReconciliationKey varchar(64) NULL;
            """);

        // SQL Server compiles a batch before the preceding ALTER TABLE is executed.  Keep the
        // dependent FK/index statements in a second command so a clean database can apply this
        // forward migration without an invalid-column parse failure.
        migrationBuilder.Sql("""
            SET QUOTED_IDENTIFIER ON;
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProjectAssetLinks_FixedAssets_FixedAssetId')
                ALTER TABLE dbo.ProjectAssetLinks ADD CONSTRAINT FK_ProjectAssetLinks_FixedAssets_FixedAssetId
                    FOREIGN KEY (FixedAssetId) REFERENCES dbo.FixedAssets(Id);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProjectAssetLinks') AND name = 'IX_ProjectAssetLinks_FixedAssetId')
                CREATE INDEX IX_ProjectAssetLinks_FixedAssetId ON dbo.ProjectAssetLinks(FixedAssetId) WHERE FixedAssetId IS NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.ProjectAssetLinks') AND name = 'UX_ProjectAssetLinks_Tenant_ReconciliationKey')
                CREATE UNIQUE INDEX UX_ProjectAssetLinks_Tenant_ReconciliationKey
                    ON dbo.ProjectAssetLinks(TenantId, ReconciliationKey)
                    WHERE ReconciliationKey IS NOT NULL AND IsDeleted = 0;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectAssetLinks_ReconciliationLineage
            ON dbo.ProjectAssetLinks
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted link
                    LEFT JOIN dbo.Projects project
                        ON project.Id = link.ProjectId AND project.TenantId = link.TenantId AND project.IsDeleted = 0
                    LEFT JOIN dbo.MaintenanceAssets maintenanceAsset
                        ON maintenanceAsset.Id = link.MaintenanceAssetId AND maintenanceAsset.TenantId = link.TenantId AND maintenanceAsset.IsDeleted = 0
                    LEFT JOIN dbo.FixedAssets fixedAsset
                        ON fixedAsset.Id = link.FixedAssetId AND fixedAsset.TenantId = link.TenantId AND fixedAsset.IsDeleted = 0
                    LEFT JOIN dbo.CompanyAssets companyAsset
                        ON companyAsset.Id = link.CompanyAssetId AND companyAsset.TenantId = link.TenantId AND companyAsset.IsDeleted = 0
                    LEFT JOIN dbo.JobCard jobCard
                        ON jobCard.Id = link.JobCardId AND jobCard.TenantId = link.TenantId AND jobCard.IsDeleted = 0
                    WHERE project.Id IS NULL
                       OR (link.MaintenanceAssetId IS NULL AND link.FixedAssetId IS NULL AND link.CompanyAssetId IS NULL AND link.JobCardId IS NULL)
                       OR (link.MaintenanceAssetId IS NOT NULL AND maintenanceAsset.Id IS NULL)
                       OR (link.FixedAssetId IS NOT NULL AND fixedAsset.Id IS NULL)
                       OR (link.CompanyAssetId IS NOT NULL AND companyAsset.Id IS NULL)
                       OR (link.JobCardId IS NOT NULL AND jobCard.Id IS NULL)
                       OR (link.JobCardId IS NOT NULL AND link.MaintenanceAssetId IS NOT NULL AND jobCard.AssetId <> link.MaintenanceAssetId)
                       OR ((link.MaintenanceAssetId IS NOT NULL OR link.FixedAssetId IS NOT NULL OR link.CompanyAssetId IS NOT NULL OR link.JobCardId IS NOT NULL)
                           AND (link.ReconciliationKey IS NULL OR LEN(link.ReconciliationKey) <> 64))
                )
                    THROW 52155, 'Project asset reconciliation lineage is invalid or crosses tenant boundaries.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProjectAssetLinks_ReconciliationLineage;
            DROP INDEX IF EXISTS UX_ProjectAssetLinks_Tenant_ReconciliationKey ON dbo.ProjectAssetLinks;
            DROP INDEX IF EXISTS IX_ProjectAssetLinks_FixedAssetId ON dbo.ProjectAssetLinks;
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ProjectAssetLinks_FixedAssets_FixedAssetId')
                ALTER TABLE dbo.ProjectAssetLinks DROP CONSTRAINT FK_ProjectAssetLinks_FixedAssets_FixedAssetId;
            IF COL_LENGTH('dbo.ProjectAssetLinks', 'ReconciliationKey') IS NOT NULL
                ALTER TABLE dbo.ProjectAssetLinks DROP COLUMN ReconciliationKey;
            IF COL_LENGTH('dbo.ProjectAssetLinks', 'FixedAssetId') IS NOT NULL
                ALTER TABLE dbo.ProjectAssetLinks DROP COLUMN FixedAssetId;
            """);
    }
}
