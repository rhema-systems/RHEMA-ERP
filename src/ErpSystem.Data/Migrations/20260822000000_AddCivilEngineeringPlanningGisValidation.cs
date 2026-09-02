using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// CIV-REQ-FU-002 adds a narrow Planning/GIS validation child to the existing
/// Civil design case. Estate, permitting files, central DMS and shared audit
/// remain the authoritative owners; this migration creates no GIS or file store.
/// </summary>
public partial class AddCivilEngineeringPlanningGisValidation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE dbo.ProjectCivilDesignCases ADD
              RequirePlanningGisValidation bit NOT NULL
                CONSTRAINT DF_ProjectCivilDesignCases_RequirePlanningGisValidation DEFAULT (0);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE dbo.ProjectCivilPlanningGisValidations
            (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_ProjectCivilPlanningGisValidations PRIMARY KEY,
              DesignCaseId uniqueidentifier NOT NULL,
              EstateManagedAssetId uniqueidentifier NOT NULL,
              DevelopmentApprovalFileId uniqueidentifier NOT NULL,
              ClientRequestId uniqueidentifier NOT NULL,
              RequestHash varchar(64) NOT NULL,
              LastMutationClientRequestId uniqueidentifier NULL,
              LastMutationRequestHash varchar(64) NULL,
              PlanningConditionId uniqueidentifier NOT NULL,
              DevelopmentConstraintId uniqueidentifier NOT NULL,
              LandUseImpactId uniqueidentifier NOT NULL,
              LayoutConformity int NOT NULL,
              SpatialReferenceSnapshot varchar(512) NOT NULL,
              GisProviderSnapshot varchar(200) NULL,
              GisFeatureIdSnapshot varchar(200) NULL,
              GisSourceCrsSnapshot varchar(120) NULL,
              BoundaryCoordinatesSnapshot nvarchar(4000) NULL,
              ZoningClassificationSnapshot varchar(120) NULL,
              PlanningComplianceSnapshot varchar(120) NULL,
              CentralDocumentRecordId uniqueidentifier NOT NULL,
              CentralDocumentVersionId uniqueidentifier NOT NULL,
              Status int NOT NULL,
              PreparedByUserId uniqueidentifier NOT NULL,
              SubmittedAt datetime2 NULL,
              SubmittedByUserId uniqueidentifier NULL,
              ReviewedAt datetime2 NULL,
              ReviewedByUserId uniqueidentifier NULL,
              CorrelationId varchar(100) NOT NULL,
              RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL,
              UpdatedAt datetime2 NULL,
              CreatedBy nvarchar(max) NULL,
              UpdatedBy nvarchar(max) NULL,
              CreatedById uniqueidentifier NULL,
              LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL,
              DeletedAt datetime2 NULL,
              DeletedBy nvarchar(max) NULL,
              TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_DesignCase FOREIGN KEY (DesignCaseId) REFERENCES dbo.ProjectCivilDesignCases(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_Estate FOREIGN KEY (EstateManagedAssetId) REFERENCES dbo.EstateManagedAssets(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_ApprovalFile FOREIGN KEY (DevelopmentApprovalFileId) REFERENCES dbo.ProjectCivilDevelopmentApprovalFiles(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_PlanningCondition FOREIGN KEY (PlanningConditionId) REFERENCES dbo.ProjectCatalogEntries(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_DevelopmentConstraint FOREIGN KEY (DevelopmentConstraintId) REFERENCES dbo.ProjectCatalogEntries(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_LandUseImpact FOREIGN KEY (LandUseImpactId) REFERENCES dbo.ProjectCatalogEntries(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_DocumentRecord FOREIGN KEY (CentralDocumentRecordId) REFERENCES dbo.CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_DocumentVersion FOREIGN KEY (CentralDocumentVersionId) REFERENCES dbo.CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidations_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
              CONSTRAINT CK_ProjectCivilPlanningGisValidations_Status CHECK (Status IN (0,1,2,3)),
              CONSTRAINT CK_ProjectCivilPlanningGisValidations_Layout CHECK (LayoutConformity IN (0,1,2))
            );
            """);

        migrationBuilder.Sql("""
            CREATE TABLE dbo.ProjectCivilPlanningGisValidationRevisions
            (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_ProjectCivilPlanningGisValidationRevisions PRIMARY KEY,
              PlanningGisValidationId uniqueidentifier NOT NULL,
              Action nvarchar(100) NOT NULL,
              ActorUserId uniqueidentifier NOT NULL,
              ActorName nvarchar(300) NOT NULL,
              ActorRoles nvarchar(500) NULL,
              CorrelationId varchar(100) NOT NULL,
              BeforeJson nvarchar(max) NULL,
              AfterJson nvarchar(max) NOT NULL,
              CreatedAt datetime2 NOT NULL,
              UpdatedAt datetime2 NULL,
              CreatedBy nvarchar(max) NULL,
              UpdatedBy nvarchar(max) NULL,
              CreatedById uniqueidentifier NULL,
              LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL,
              DeletedAt datetime2 NULL,
              DeletedBy nvarchar(max) NULL,
              TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilPlanningGisValidationRevisions_Validation FOREIGN KEY (PlanningGisValidationId) REFERENCES dbo.ProjectCivilPlanningGisValidations(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidationRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilPlanningGisValidationRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
            );
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IX_ProjectCivilPlanningGisValidations_TenantId_ClientRequestId
              ON dbo.ProjectCivilPlanningGisValidations(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilPlanningGisValidations_TenantId_DesignCaseId
              ON dbo.ProjectCivilPlanningGisValidations(TenantId, DesignCaseId)
              WHERE IsDeleted = 0 AND Status IN (0,1);
            CREATE INDEX IX_ProjectCivilPlanningGisValidations_TenantId_DevelopmentApprovalFileId
              ON dbo.ProjectCivilPlanningGisValidations(TenantId, DevelopmentApprovalFileId);
            CREATE INDEX IX_ProjectCivilPlanningGisValidationRevisions_TenantId_PlanningGisValidationId_CreatedAt
              ON dbo.ProjectCivilPlanningGisValidationRevisions(TenantId, PlanningGisValidationId, CreatedAt);
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilPlanningGisValidations_Lineage
            ON dbo.ProjectCivilPlanningGisValidations
            AFTER INSERT, UPDATE
            AS
            BEGIN
              SET NOCOUNT ON;

              IF EXISTS (
                SELECT 1
                FROM inserted i
                LEFT JOIN dbo.ProjectCivilDesignCases c
                  ON c.Id = i.DesignCaseId AND c.TenantId = i.TenantId AND c.IsDeleted = 0
                LEFT JOIN dbo.EstateManagedAssets estate
                  ON estate.Id = i.EstateManagedAssetId AND estate.TenantId = i.TenantId AND estate.IsDeleted = 0
                LEFT JOIN dbo.ProjectCivilDevelopmentApprovalFiles approvalFile
                  ON approvalFile.Id = i.DevelopmentApprovalFileId AND approvalFile.TenantId = i.TenantId AND approvalFile.IsDeleted = 0
                    AND approvalFile.ProjectId = c.ProjectId AND approvalFile.EstateManagedAssetId = i.EstateManagedAssetId
                LEFT JOIN dbo.ProjectCatalogEntries conditionEntry
                  ON conditionEntry.Id = i.PlanningConditionId AND conditionEntry.TenantId = i.TenantId
                    AND conditionEntry.IsDeleted = 0 AND conditionEntry.IsActive = 1
                    AND conditionEntry.CatalogType = 'civil-planning-conditions'
                LEFT JOIN dbo.ProjectCatalogEntries constraintEntry
                  ON constraintEntry.Id = i.DevelopmentConstraintId AND constraintEntry.TenantId = i.TenantId
                    AND constraintEntry.IsDeleted = 0 AND constraintEntry.IsActive = 1
                    AND constraintEntry.CatalogType = 'civil-development-constraints'
                LEFT JOIN dbo.ProjectCatalogEntries landUseEntry
                  ON landUseEntry.Id = i.LandUseImpactId AND landUseEntry.TenantId = i.TenantId
                    AND landUseEntry.IsDeleted = 0 AND landUseEntry.IsActive = 1
                    AND landUseEntry.CatalogType = 'civil-land-use-impacts'
                LEFT JOIN dbo.CentralDocumentRecords record
                  ON record.Id = i.CentralDocumentRecordId AND record.TenantId = i.TenantId
                    AND record.IsDeleted = 0 AND record.LifecycleStatus = 'Active'
                    AND record.VersionStatus = 'Published' AND record.MetadataTemplateCode = 'TDC-CIV-ENGINEERING-FILE'
                LEFT JOIN dbo.CentralDocumentVersions version
                  ON version.Id = i.CentralDocumentVersionId AND version.DocumentRecordId = record.Id
                    AND version.TenantId = i.TenantId AND version.IsDeleted = 0
                    AND version.Status = 'Published' AND version.PublishedAt IS NOT NULL
                    AND record.CurrentVersion = version.VersionNumber
                WHERE c.Id IS NULL OR estate.Id IS NULL OR approvalFile.Id IS NULL
                   OR conditionEntry.Id IS NULL OR constraintEntry.Id IS NULL OR landUseEntry.Id IS NULL
                   OR record.Id IS NULL OR version.Id IS NULL
                   OR estate.BoundaryVerified = 0
                   OR NULLIF(LTRIM(RTRIM(i.SpatialReferenceSnapshot)), '') IS NULL
              )
                THROW 51930, 'Planning/GIS validation lineage is not current, complete, or tenant-safe.', 1;

              IF EXISTS (
                SELECT 1
                FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE ISNULL(CONVERT(varchar(36), i.DesignCaseId), '') <> ISNULL(CONVERT(varchar(36), d.DesignCaseId), '')
                   OR ISNULL(CONVERT(varchar(36), i.EstateManagedAssetId), '') <> ISNULL(CONVERT(varchar(36), d.EstateManagedAssetId), '')
                   OR ISNULL(CONVERT(varchar(36), i.DevelopmentApprovalFileId), '') <> ISNULL(CONVERT(varchar(36), d.DevelopmentApprovalFileId), '')
                   OR ISNULL(CONVERT(varchar(36), i.PlanningConditionId), '') <> ISNULL(CONVERT(varchar(36), d.PlanningConditionId), '')
                   OR ISNULL(CONVERT(varchar(36), i.DevelopmentConstraintId), '') <> ISNULL(CONVERT(varchar(36), d.DevelopmentConstraintId), '')
                   OR ISNULL(CONVERT(varchar(36), i.LandUseImpactId), '') <> ISNULL(CONVERT(varchar(36), d.LandUseImpactId), '')
                   OR ISNULL(i.LayoutConformity, -1) <> ISNULL(d.LayoutConformity, -1)
                   OR ISNULL(i.SpatialReferenceSnapshot, '') <> ISNULL(d.SpatialReferenceSnapshot, '')
                   OR ISNULL(i.GisProviderSnapshot, '') <> ISNULL(d.GisProviderSnapshot, '')
                   OR ISNULL(i.GisFeatureIdSnapshot, '') <> ISNULL(d.GisFeatureIdSnapshot, '')
                   OR ISNULL(i.GisSourceCrsSnapshot, '') <> ISNULL(d.GisSourceCrsSnapshot, '')
                   OR ISNULL(i.BoundaryCoordinatesSnapshot, '') <> ISNULL(d.BoundaryCoordinatesSnapshot, '')
                   OR ISNULL(i.ZoningClassificationSnapshot, '') <> ISNULL(d.ZoningClassificationSnapshot, '')
                   OR ISNULL(i.PlanningComplianceSnapshot, '') <> ISNULL(d.PlanningComplianceSnapshot, '')
                   OR ISNULL(CONVERT(varchar(36), i.CentralDocumentRecordId), '') <> ISNULL(CONVERT(varchar(36), d.CentralDocumentRecordId), '')
                   OR ISNULL(CONVERT(varchar(36), i.CentralDocumentVersionId), '') <> ISNULL(CONVERT(varchar(36), d.CentralDocumentVersionId), '')
                   OR ISNULL(CONVERT(varchar(36), i.PreparedByUserId), '') <> ISNULL(CONVERT(varchar(36), d.PreparedByUserId), '')
              )
                THROW 51931, 'Planning/GIS site, spatial, planning, evidence and preparer lineage is immutable.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilPlanningGisValidations_Lifecycle
            ON dbo.ProjectCivilPlanningGisValidations
            AFTER INSERT, UPDATE
            AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted i LEFT JOIN deleted d ON d.Id = i.Id
                WHERE d.Id IS NULL AND (i.Status <> 0 OR i.SubmittedAt IS NOT NULL
                  OR i.SubmittedByUserId IS NOT NULL OR i.ReviewedAt IS NOT NULL OR i.ReviewedByUserId IS NOT NULL)
              )
                THROW 51932, 'A Planning/GIS validation must start as a draft.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE NOT (
                  (d.Status = 0 AND i.Status = 1 AND i.SubmittedAt IS NOT NULL
                    AND i.SubmittedByUserId = i.PreparedByUserId AND i.ReviewedAt IS NULL AND i.ReviewedByUserId IS NULL)
                  OR
                  (d.Status = 1 AND i.Status IN (2,3) AND i.SubmittedAt IS NOT NULL
                    AND i.SubmittedByUserId = i.PreparedByUserId AND i.ReviewedAt IS NOT NULL
                    AND i.ReviewedByUserId IS NOT NULL AND i.ReviewedByUserId <> i.PreparedByUserId)
                )
              )
                THROW 51933, 'Invalid Planning/GIS validation lifecycle transition.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilPlanningGisValidationRevisions_Lineage
            ON dbo.ProjectCivilPlanningGisValidationRevisions
            AFTER INSERT, UPDATE
            AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted i
                LEFT JOIN dbo.ProjectCivilPlanningGisValidations validation
                  ON validation.Id = i.PlanningGisValidationId AND validation.TenantId = i.TenantId AND validation.IsDeleted = 0
                LEFT JOIN dbo.Users actor ON actor.Id = i.ActorUserId AND actor.TenantId = i.TenantId
                WHERE validation.Id IS NULL OR actor.Id IS NULL
              )
                THROW 51934, 'Planning/GIS validation revision lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilPlanningGisValidationRevisions_AppendOnly
            ON dbo.ProjectCivilPlanningGisValidationRevisions
            AFTER UPDATE, DELETE
            AS
            BEGIN
              SET NOCOUNT ON;
              THROW 51935, 'Planning/GIS validation revisions are append-only.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilDesignCases_PlanningGisGate
            ON dbo.ProjectCivilDesignCases
            AFTER INSERT, UPDATE
            AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted i JOIN deleted d ON d.Id = i.Id
                WHERE ISNULL(i.RequirePlanningGisValidation, 0) <> ISNULL(d.RequirePlanningGisValidation, 0)
              )
                THROW 51936, 'The Planning/GIS requirement frozen on a Civil Engineering Case is immutable.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted i
                WHERE i.RequirePlanningGisValidation = 1
                  AND i.Stage IN ('CivilEngineerDesign','SceDesignReview','Drafting','SceDrawingReview','HodFinalReview','Approved')
                  AND NOT EXISTS (
                    SELECT 1 FROM dbo.ProjectCivilPlanningGisValidations validation
                    WHERE validation.DesignCaseId = i.Id AND validation.TenantId = i.TenantId
                      AND validation.IsDeleted = 0 AND validation.Status = 2
                  )
              )
                THROW 51937, 'An independently approved Planning/GIS validation is required before technical Civil Engineering work can proceed.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilDesignCases_PlanningGisGate;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilPlanningGisValidationRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilPlanningGisValidationRevisions_Lineage;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilPlanningGisValidations_Lifecycle;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilPlanningGisValidations_Lineage;
            DROP TABLE IF EXISTS dbo.ProjectCivilPlanningGisValidationRevisions;
            DROP TABLE IF EXISTS dbo.ProjectCivilPlanningGisValidations;
            ALTER TABLE dbo.ProjectCivilDesignCases DROP CONSTRAINT DF_ProjectCivilDesignCases_RequirePlanningGisValidation;
            ALTER TABLE dbo.ProjectCivilDesignCases DROP COLUMN RequirePlanningGisValidation;
            """);
    }
}
