using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// CIV-REQ-FU-001 extends the existing Civil design-case owner with immutable, tenant-safe
/// Works/Engineering Case initiation provenance. It deliberately does not introduce a parallel
/// request store: project design continues to own the downstream lifecycle.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821230000_AddCivilEngineeringWorksCaseInitiation")]
public partial class AddCivilEngineeringWorksCaseInitiation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE dbo.ProjectCivilDesignCases ADD
              InitiationSource int NULL,
              InitiationSourceId uniqueidentifier NULL,
              InitiationSourceDocumentVersionId uniqueidentifier NULL,
              InitiationSourceReference varchar(240) NULL,
              EstateManagedAssetId uniqueidentifier NULL,
              EngineeringCategoryId uniqueidentifier NULL,
              WorkClassification int NULL,
              ScopeSummary nvarchar(4000) NULL,
              ConstraintSummary nvarchar(4000) NULL,
              RiskSummary nvarchar(4000) NULL,
              Recommendation nvarchar(4000) NULL;
            """);

        // SQL Server compiles a whole command before it recognizes a preceding
        // ALTER TABLE. Keep dependent constraints and indexes in later EF
        // commands so a normal database update resolves the new columns.
        migrationBuilder.Sql("""
            ALTER TABLE dbo.ProjectCivilDesignCases ADD
              CONSTRAINT FK_ProjectCivilDesignCases_EstateManagedAsset
                FOREIGN KEY (EstateManagedAssetId) REFERENCES dbo.EstateManagedAssets(Id),
              CONSTRAINT FK_ProjectCivilDesignCases_EngineeringCategory
                FOREIGN KEY (EngineeringCategoryId) REFERENCES dbo.ProjectCatalogEntries(Id),
              CONSTRAINT CK_ProjectCivilDesignCases_InitiationShape CHECK
              (
                (
                  InitiationSource IS NULL AND InitiationSourceId IS NULL
                  AND InitiationSourceDocumentVersionId IS NULL AND InitiationSourceReference IS NULL
                  AND EstateManagedAssetId IS NULL AND EngineeringCategoryId IS NULL
                  AND WorkClassification IS NULL AND ScopeSummary IS NULL
                  AND ConstraintSummary IS NULL AND RiskSummary IS NULL AND Recommendation IS NULL
                )
                OR
                (
                  InitiationSource BETWEEN 0 AND 6
                  AND InitiationSourceId IS NOT NULL
                  AND InitiationSourceReference IS NOT NULL AND LEN(LTRIM(RTRIM(InitiationSourceReference))) >= 3
                  AND EstateManagedAssetId IS NOT NULL AND EngineeringCategoryId IS NOT NULL
                  AND WorkClassification BETWEEN 0 AND 5
                  AND ScopeSummary IS NOT NULL AND LEN(LTRIM(RTRIM(ScopeSummary))) >= 3
                  AND ConstraintSummary IS NOT NULL AND LEN(LTRIM(RTRIM(ConstraintSummary))) >= 3
                  AND RiskSummary IS NOT NULL AND LEN(LTRIM(RTRIM(RiskSummary))) >= 3
                  AND Recommendation IS NOT NULL AND LEN(LTRIM(RTRIM(Recommendation))) >= 3
                  AND ((InitiationSource = 4 AND InitiationSourceDocumentVersionId IS NOT NULL)
                    OR (InitiationSource <> 4 AND InitiationSourceDocumentVersionId IS NULL))
                )
              );
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IX_ProjectCivilDesignCases_EstateManagedAssetId
              ON dbo.ProjectCivilDesignCases(EstateManagedAssetId);
            """);

        migrationBuilder.Sql("""
            CREATE INDEX IX_ProjectCivilDesignCases_EngineeringCategoryId
              ON dbo.ProjectCivilDesignCases(EngineeringCategoryId);
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IX_ProjectCivilDesignCases_TenantId_InitiationSource_InitiationSourceId
              ON dbo.ProjectCivilDesignCases(TenantId, InitiationSource, InitiationSourceId)
              WHERE IsDeleted = 0 AND InitiationSource IS NOT NULL AND InitiationSourceId IS NOT NULL
                AND Status <> 'Approved' AND Status <> 'Rejected' AND Status <> 'Cancelled';
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilDesignCases_InitiationLineage
            ON dbo.ProjectCivilDesignCases
            AFTER INSERT, UPDATE
            AS
            BEGIN
              SET NOCOUNT ON;

              IF EXISTS (
                SELECT 1
                FROM inserted i
                LEFT JOIN deleted d ON d.Id = i.Id
                WHERE d.Id IS NULL
                  AND (i.InitiationSource IS NULL OR i.InitiationSourceId IS NULL
                    OR i.EstateManagedAssetId IS NULL OR i.EngineeringCategoryId IS NULL
                    OR i.WorkClassification IS NULL OR i.ScopeSummary IS NULL
                    OR i.ConstraintSummary IS NULL OR i.RiskSummary IS NULL OR i.Recommendation IS NULL)
              )
                THROW 51921, 'New Civil Engineering Cases require controlled source, property/site, category, classification and assessment lineage.', 1;

              IF EXISTS (
                SELECT 1
                FROM inserted i
                JOIN deleted d ON d.Id = i.Id
                WHERE d.InitiationSource IS NOT NULL
                  AND (
                    ISNULL(i.InitiationSource, -1) <> ISNULL(d.InitiationSource, -1)
                    OR ISNULL(CONVERT(varchar(36), i.InitiationSourceId), '') <> ISNULL(CONVERT(varchar(36), d.InitiationSourceId), '')
                    OR ISNULL(CONVERT(varchar(36), i.InitiationSourceDocumentVersionId), '') <> ISNULL(CONVERT(varchar(36), d.InitiationSourceDocumentVersionId), '')
                    OR ISNULL(i.InitiationSourceReference, '') <> ISNULL(d.InitiationSourceReference, '')
                    OR ISNULL(CONVERT(varchar(36), i.EstateManagedAssetId), '') <> ISNULL(CONVERT(varchar(36), d.EstateManagedAssetId), '')
                    OR ISNULL(CONVERT(varchar(36), i.EngineeringCategoryId), '') <> ISNULL(CONVERT(varchar(36), d.EngineeringCategoryId), '')
                    OR ISNULL(i.WorkClassification, -1) <> ISNULL(d.WorkClassification, -1)
                    OR ISNULL(i.ScopeSummary, '') <> ISNULL(d.ScopeSummary, '')
                    OR ISNULL(i.ConstraintSummary, '') <> ISNULL(d.ConstraintSummary, '')
                    OR ISNULL(i.RiskSummary, '') <> ISNULL(d.RiskSummary, '')
                    OR ISNULL(i.Recommendation, '') <> ISNULL(d.Recommendation, '')
                  )
              )
                THROW 51922, 'Civil Engineering Case source, property/site, category, classification and assessment lineage is immutable.', 1;

              IF EXISTS (
                SELECT 1
                FROM inserted i
                LEFT JOIN dbo.EstateManagedAssets estate
                  ON estate.Id = i.EstateManagedAssetId AND estate.TenantId = i.TenantId AND estate.IsDeleted = 0
                LEFT JOIN dbo.ProjectCatalogEntries category
                  ON category.Id = i.EngineeringCategoryId AND category.TenantId = i.TenantId
                    AND category.IsDeleted = 0 AND category.IsActive = 1
                    AND category.CatalogType = 'civil-engineering-categories'
                WHERE i.InitiationSource IS NOT NULL
                  AND (estate.Id IS NULL OR category.Id IS NULL)
              )
                THROW 51923, 'Civil Engineering Case property/site or category lineage is invalid for this tenant.', 1;

              IF EXISTS (
                SELECT 1 FROM inserted i
                WHERE i.InitiationSource IS NOT NULL
                  AND (
                    (i.InitiationSource = 0 AND NOT EXISTS (
                      SELECT 1 FROM dbo.CapitalProjects source
                      WHERE source.Id = i.InitiationSourceId AND source.TenantId = i.TenantId
                        AND source.IsDeleted = 0 AND source.Status = 7))
                    OR
                    (i.InitiationSource = 1 AND NOT EXISTS (
                      SELECT 1 FROM dbo.CivilEngineeringMaintenanceIntakes source
                      WHERE source.Id = i.InitiationSourceId AND source.TenantId = i.TenantId
                        AND source.IsDeleted = 0 AND source.Source = 1 AND source.Status <> 'Assessed'
                        AND (source.ProjectId IS NULL OR source.ProjectId = i.ProjectId)
                        AND (source.EstateManagedAssetId IS NULL OR source.EstateManagedAssetId = i.EstateManagedAssetId)))
                    OR
                    (i.InitiationSource = 2 AND NOT EXISTS (
                      SELECT 1 FROM dbo.EstateManagedAssets source
                      WHERE source.Id = i.InitiationSourceId AND source.Id = i.EstateManagedAssetId
                        AND source.TenantId = i.TenantId AND source.IsDeleted = 0
                        AND source.IsReadyForProjectManagement = 1
                        AND (source.ProjectId IS NULL OR source.ProjectId = i.ProjectId)))
                    OR
                    (i.InitiationSource = 3 AND NOT EXISTS (
                      SELECT 1 FROM dbo.ProjectCivilDevelopmentApprovalFiles source
                      WHERE source.Id = i.InitiationSourceId AND source.TenantId = i.TenantId
                        AND source.IsDeleted = 0 AND source.ProjectId = i.ProjectId
                        AND source.EstateManagedAssetId = i.EstateManagedAssetId))
                    OR
                    (i.InitiationSource = 4 AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.CentralDocumentRecords record
                      JOIN dbo.CentralDocumentVersions version
                        ON version.Id = i.InitiationSourceDocumentVersionId
                        AND version.DocumentRecordId = record.Id AND version.TenantId = i.TenantId
                        AND version.IsDeleted = 0 AND version.Status = 'Published'
                        AND version.PublishedAt IS NOT NULL AND record.CurrentVersion = version.VersionNumber
                      WHERE record.Id = i.InitiationSourceId AND record.TenantId = i.TenantId
                        AND record.IsDeleted = 0 AND record.LifecycleStatus = 'Active'
                        AND record.VersionStatus = 'Published'
                        AND record.MetadataTemplateCode = 'TDC-CIV-ENGINEERING-FILE'))
                    OR
                    (i.InitiationSource = 5 AND NOT EXISTS (
                      SELECT 1 FROM dbo.ProjectDefectLiabilityCases source
                      WHERE source.Id = i.InitiationSourceId AND source.TenantId = i.TenantId
                        AND source.IsDeleted = 0 AND source.ProjectId = i.ProjectId
                        AND source.Status NOT IN ('Closed', 'Resolved')))
                    OR
                    (i.InitiationSource = 6 AND NOT EXISTS (
                      SELECT 1 FROM dbo.ProjectIssues source
                      WHERE source.Id = i.InitiationSourceId AND source.TenantId = i.TenantId
                        AND source.IsDeleted = 0 AND source.ProjectId = i.ProjectId
                        AND source.Status NOT IN ('Closed', 'Resolved')))
                  )
              )
                THROW 51924, 'Civil Engineering Case source lineage is not active, current, approved, or tenant-safe.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilDesignCases_InitiationLineage;
            DROP INDEX IF EXISTS IX_ProjectCivilDesignCases_TenantId_InitiationSource_InitiationSourceId ON dbo.ProjectCivilDesignCases;
            DROP INDEX IF EXISTS IX_ProjectCivilDesignCases_EngineeringCategoryId ON dbo.ProjectCivilDesignCases;
            DROP INDEX IF EXISTS IX_ProjectCivilDesignCases_EstateManagedAssetId ON dbo.ProjectCivilDesignCases;
            ALTER TABLE dbo.ProjectCivilDesignCases DROP CONSTRAINT CK_ProjectCivilDesignCases_InitiationShape;
            ALTER TABLE dbo.ProjectCivilDesignCases DROP CONSTRAINT FK_ProjectCivilDesignCases_EngineeringCategory;
            ALTER TABLE dbo.ProjectCivilDesignCases DROP CONSTRAINT FK_ProjectCivilDesignCases_EstateManagedAsset;
            ALTER TABLE dbo.ProjectCivilDesignCases DROP COLUMN
              InitiationSource, InitiationSourceId, InitiationSourceDocumentVersionId, InitiationSourceReference,
              EstateManagedAssetId, EngineeringCategoryId, WorkClassification,
              ScopeSummary, ConstraintSummary, RiskSummary, Recommendation;
            """);
    }
}
