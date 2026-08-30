using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds a governed Civil inspection envelope over the authoritative Projects quality
/// checkpoint and non-conformance records. Planning/GIS and central DMS remain owners.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822003000_AddCivilEngineeringInspectionControls")]
public partial class AddCivilEngineeringInspectionControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE dbo.ProjectCivilInspectionControls (
              Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, QualityCheckpointId uniqueidentifier NOT NULL,
              PlanningGisValidationId uniqueidentifier NOT NULL, InspectorUserId uniqueidentifier NOT NULL, ReinspectionInspectorUserId uniqueidentifier NULL,
              NonConformanceId uniqueidentifier NULL, Purpose nvarchar(500) NOT NULL, ScheduledAt datetime2 NOT NULL,
              SpatialReferenceSnapshot nvarchar(512) NOT NULL, BoundaryCoordinatesSnapshot nvarchar(4000) NULL,
              Stage varchar(40) NOT NULL, Status varchar(40) NOT NULL, Findings nvarchar(4000) NULL, CorrectiveAction nvarchar(2000) NULL,
              InspectedAt datetime2 NULL, CorrectiveActionRecordedAt datetime2 NULL, ReinspectedAt datetime2 NULL, ClosedAt datetime2 NULL, ClosedById uniqueidentifier NULL,
              PlanDocumentRecordId uniqueidentifier NOT NULL, PlanDocumentVersionId uniqueidentifier NOT NULL,
              InspectionDocumentRecordId uniqueidentifier NULL, InspectionDocumentVersionId uniqueidentifier NULL,
              CorrectiveActionDocumentRecordId uniqueidentifier NULL, CorrectiveActionDocumentVersionId uniqueidentifier NULL,
              ReinspectionDocumentRecordId uniqueidentifier NULL, ReinspectionDocumentVersionId uniqueidentifier NULL,
              ClosureDocumentRecordId uniqueidentifier NULL, ClosureDocumentVersionId uniqueidentifier NULL,
              ConfigurationProfileId uniqueidentifier NOT NULL, SupervisionConfigurationDecisionId uniqueidentifier NOT NULL,
              QualityConfigurationDecisionId uniqueidentifier NOT NULL, EvidenceMetadataTemplateId uniqueidentifier NOT NULL,
              EvidenceMetadataTemplateCodeSnapshot varchar(80) NOT NULL, PolicyHash varchar(64) NOT NULL,
              ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL,
              LastMutationRequestHash varchar(64) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
              CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilInspectionControls_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_Checkpoint FOREIGN KEY (QualityCheckpointId) REFERENCES dbo.ProjectQualityCheckpoints(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_PlanningGis FOREIGN KEY (PlanningGisValidationId) REFERENCES dbo.ProjectCivilPlanningGisValidations(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_NonConformance FOREIGN KEY (NonConformanceId) REFERENCES dbo.ProjectNonConformances(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_Inspector FOREIGN KEY (InspectorUserId) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_ReinspectionInspector FOREIGN KEY (ReinspectionInspectorUserId) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_ClosedBy FOREIGN KEY (ClosedById) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_PlanRecord FOREIGN KEY (PlanDocumentRecordId) REFERENCES dbo.CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_PlanVersion FOREIGN KEY (PlanDocumentVersionId) REFERENCES dbo.CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_InspectionRecord FOREIGN KEY (InspectionDocumentRecordId) REFERENCES dbo.CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_InspectionVersion FOREIGN KEY (InspectionDocumentVersionId) REFERENCES dbo.CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_CorrectiveRecord FOREIGN KEY (CorrectiveActionDocumentRecordId) REFERENCES dbo.CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_CorrectiveVersion FOREIGN KEY (CorrectiveActionDocumentVersionId) REFERENCES dbo.CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_ReinspectionRecord FOREIGN KEY (ReinspectionDocumentRecordId) REFERENCES dbo.CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_ReinspectionVersion FOREIGN KEY (ReinspectionDocumentVersionId) REFERENCES dbo.CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_ClosureRecord FOREIGN KEY (ClosureDocumentRecordId) REFERENCES dbo.CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_ClosureVersion FOREIGN KEY (ClosureDocumentVersionId) REFERENCES dbo.CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES dbo.CivilEngineeringConfigurationProfiles(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_SupervisionDecision FOREIGN KEY (SupervisionConfigurationDecisionId) REFERENCES dbo.CivilEngineeringConfigurationDecisions(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_QualityDecision FOREIGN KEY (QualityConfigurationDecisionId) REFERENCES dbo.CivilEngineeringConfigurationDecisions(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_Template FOREIGN KEY (EvidenceMetadataTemplateId) REFERENCES dbo.CentralDocumentMetadataTemplates(Id),
              CONSTRAINT FK_ProjectCivilInspectionControls_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
              CONSTRAINT CK_ProjectCivilInspectionControls_Stage CHECK (Stage IN ('Scheduled','CorrectiveActionRequired','ReinspectionScheduled','Passed','Closed')),
              CONSTRAINT CK_ProjectCivilInspectionControls_Status CHECK (Status IN ('Scheduled','Active','Blocked','Passed','Closed')),
              CONSTRAINT CK_ProjectCivilInspectionControls_InspectionEvidence CHECK ((InspectionDocumentRecordId IS NULL AND InspectionDocumentVersionId IS NULL) OR (InspectionDocumentRecordId IS NOT NULL AND InspectionDocumentVersionId IS NOT NULL)),
              CONSTRAINT CK_ProjectCivilInspectionControls_CorrectiveEvidence CHECK ((CorrectiveActionDocumentRecordId IS NULL AND CorrectiveActionDocumentVersionId IS NULL) OR (CorrectiveActionDocumentRecordId IS NOT NULL AND CorrectiveActionDocumentVersionId IS NOT NULL)),
              CONSTRAINT CK_ProjectCivilInspectionControls_ReinspectionEvidence CHECK ((ReinspectionDocumentRecordId IS NULL AND ReinspectionDocumentVersionId IS NULL) OR (ReinspectionDocumentRecordId IS NOT NULL AND ReinspectionDocumentVersionId IS NOT NULL)),
              CONSTRAINT CK_ProjectCivilInspectionControls_ClosureEvidence CHECK ((ClosureDocumentRecordId IS NULL AND ClosureDocumentVersionId IS NULL) OR (ClosureDocumentRecordId IS NOT NULL AND ClosureDocumentVersionId IS NOT NULL))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilInspectionControls_TenantId_ClientRequestId ON dbo.ProjectCivilInspectionControls(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilInspectionControls_TenantId_QualityCheckpointId ON dbo.ProjectCivilInspectionControls(TenantId, QualityCheckpointId);
            CREATE UNIQUE INDEX IX_ProjectCivilInspectionControls_TenantId_NonConformanceId ON dbo.ProjectCivilInspectionControls(TenantId, NonConformanceId) WHERE NonConformanceId IS NOT NULL AND IsDeleted=0;
            CREATE INDEX IX_ProjectCivilInspectionControls_TenantId_ProjectId_Stage_ScheduledAt ON dbo.ProjectCivilInspectionControls(TenantId, ProjectId, Stage, ScheduledAt);

            CREATE TABLE dbo.ProjectCivilInspectionRevisions (
              Id uniqueidentifier NOT NULL PRIMARY KEY, InspectionControlId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
              FromStage nvarchar(40) NOT NULL, ToStage nvarchar(40) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
              ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL,
              Reason nvarchar(2000) NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL, RequestHash varchar(64) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
              CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilInspectionRevisions_Control FOREIGN KEY (InspectionControlId) REFERENCES dbo.ProjectCivilInspectionControls(Id),
              CONSTRAINT FK_ProjectCivilInspectionRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilInspectionRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilInspectionRevisions_TenantId_InspectionControlId_CreatedAt ON dbo.ProjectCivilInspectionRevisions(TenantId, InspectionControlId, CreatedAt);
            CREATE INDEX IX_ProjectCivilInspectionRevisions_TenantId_CorrelationId ON dbo.ProjectCivilInspectionRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionControls_Lineage ON dbo.ProjectCivilInspectionControls AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN dbo.ProjectQualityCheckpoints qualityCheckpoint ON qualityCheckpoint.Id=value.QualityCheckpointId AND qualityCheckpoint.ProjectId=value.ProjectId AND qualityCheckpoint.TenantId=value.TenantId AND qualityCheckpoint.IsDeleted=0
                LEFT JOIN dbo.ProjectCivilPlanningGisValidations planning ON planning.Id=value.PlanningGisValidationId AND planning.TenantId=value.TenantId AND planning.IsDeleted=0 AND planning.Status=2
                LEFT JOIN dbo.ProjectCivilDesignCases design ON design.Id=planning.DesignCaseId AND design.ProjectId=value.ProjectId AND design.TenantId=value.TenantId AND design.IsDeleted=0
                LEFT JOIN dbo.Users inspector ON inspector.Id=value.InspectorUserId AND inspector.TenantId=value.TenantId AND inspector.IsActive=1
                LEFT JOIN dbo.CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationDecisions supervision ON supervision.Id=value.SupervisionConfigurationDecisionId AND supervision.ProfileId=value.ConfigurationProfileId AND supervision.TenantId=value.TenantId AND supervision.ConfigurationKey='CIV-CFG-005' AND supervision.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationDecisions quality ON quality.Id=value.QualityConfigurationDecisionId AND quality.ProfileId=value.ConfigurationProfileId AND quality.TenantId=value.TenantId AND quality.ConfigurationKey='CIV-CFG-011' AND quality.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsActive=1 AND template.PublishedAt IS NOT NULL AND template.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentVersions planVersion ON planVersion.Id=value.PlanDocumentVersionId AND planVersion.DocumentRecordId=value.PlanDocumentRecordId AND planVersion.TenantId=value.TenantId AND planVersion.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentRecords planDocument ON planDocument.Id=planVersion.DocumentRecordId AND planDocument.TenantId=value.TenantId AND planDocument.IsDeleted=0
                WHERE project.Id IS NULL OR qualityCheckpoint.Id IS NULL OR planning.Id IS NULL OR design.Id IS NULL OR inspector.Id IS NULL OR profile.Id IS NULL OR supervision.Id IS NULL OR quality.Id IS NULL OR template.Id IS NULL OR planVersion.Id IS NULL OR planDocument.Id IS NULL
                   OR value.SpatialReferenceSnapshot<>planning.SpatialReferenceSnapshot
                   OR ISNULL(value.BoundaryCoordinatesSnapshot,'')<>ISNULL(planning.BoundaryCoordinatesSnapshot,'')
                   OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR planVersion.Status<>'Published' OR planDocument.LifecycleStatus<>'Active' OR planDocument.VersionStatus<>'Published' OR planDocument.CurrentVersion<>planVersion.VersionNumber OR planVersion.PublishedAt IS NULL OR planDocument.MetadataTemplateCode<>template.TemplateCode
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR supervision.Status<>2 OR supervision.ApprovalStatus<>1 OR supervision.EvidenceStatus<>2 OR quality.Status<>2 OR quality.ApprovalStatus<>1 OR quality.EvidenceStatus<>2))
              ) THROW 52078, 'Civil inspection project, Planning/GIS, DMS or frozen policy lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionControls_Lifecycle ON dbo.ProjectCivilInspectionControls AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id))
                THROW 52079, 'Civil inspection controls cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.QualityCheckpointId<>prior.QualityCheckpointId OR value.PlanningGisValidationId<>prior.PlanningGisValidationId OR value.InspectorUserId<>prior.InspectorUserId OR value.Purpose<>prior.Purpose OR value.ScheduledAt<>prior.ScheduledAt OR value.SpatialReferenceSnapshot<>prior.SpatialReferenceSnapshot OR ISNULL(value.BoundaryCoordinatesSnapshot,'')<>ISNULL(prior.BoundaryCoordinatesSnapshot,'') OR value.PlanDocumentRecordId<>prior.PlanDocumentRecordId OR value.PlanDocumentVersionId<>prior.PlanDocumentVersionId OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.SupervisionConfigurationDecisionId<>prior.SupervisionConfigurationDecisionId OR value.QualityConfigurationDecisionId<>prior.QualityConfigurationDecisionId OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted)
                THROW 52080, 'Civil inspection identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE
                  (prior.Stage='Scheduled' AND NOT ((value.Stage='Passed' AND value.Status='Passed') OR (value.Stage='CorrectiveActionRequired' AND value.Status='Blocked'))) OR
                  (prior.Stage='CorrectiveActionRequired' AND NOT (value.Stage='ReinspectionScheduled' AND value.Status='Active')) OR
                  (prior.Stage='ReinspectionScheduled' AND NOT ((value.Stage='Passed' AND value.Status='Passed') OR (value.Stage='CorrectiveActionRequired' AND value.Status='Blocked'))) OR
                  (prior.Stage='Passed' AND NOT (value.Stage='Closed' AND value.Status='Closed')) OR
                  (prior.Stage='Closed' AND value.Stage<>prior.Stage))
                THROW 52081, 'Invalid Civil inspection lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE
                 ((prior.Stage='Scheduled' AND value.Stage IN ('Passed','CorrectiveActionRequired')) AND (value.LastModifiedById<>value.InspectorUserId OR value.InspectedAt IS NULL OR value.InspectionDocumentRecordId IS NULL OR value.InspectionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.Findings,''))))<3)) OR
                 ((prior.Stage='CorrectiveActionRequired' AND value.Stage='ReinspectionScheduled') AND (value.LastModifiedById<>value.InspectorUserId OR value.ReinspectionInspectorUserId IS NULL OR value.ReinspectionInspectorUserId=value.InspectorUserId OR value.CorrectiveActionRecordedAt IS NULL OR value.CorrectiveActionDocumentRecordId IS NULL OR value.CorrectiveActionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.CorrectiveAction,''))))<3)) OR
                 ((prior.Stage='ReinspectionScheduled' AND value.Stage IN ('Passed','CorrectiveActionRequired')) AND (value.LastModifiedById<>value.ReinspectionInspectorUserId OR value.ReinspectedAt IS NULL OR value.ReinspectionDocumentRecordId IS NULL OR value.ReinspectionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.Findings,''))))<3)) OR
                 ((prior.Stage='Passed' AND value.Stage='Closed') AND (value.ClosedById IS NULL OR value.ClosedById<>value.LastModifiedById OR value.ClosedById IN (value.CreatedById,value.InspectorUserId,ISNULL(value.ReinspectionInspectorUserId,'00000000-0000-0000-0000-000000000000')) OR value.ClosedAt IS NULL OR value.ClosureDocumentRecordId IS NULL OR value.ClosureDocumentVersionId IS NULL)))
                THROW 52082, 'Civil inspection actor, independent reinspection, evidence, or closure lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionRevisions_Lineage ON dbo.ProjectCivilInspectionRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.ProjectCivilInspectionControls control ON control.Id=value.InspectionControlId AND control.TenantId=value.TenantId AND control.IsDeleted=0
                LEFT JOIN dbo.Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1
                WHERE control.Id IS NULL OR actor.Id IS NULL OR LEN(value.RequestHash)<>64 OR value.Action NOT IN ('CreateCivilInspection','ApproveCivilInspection','RejectCivilInspection','UpdateCivilDefectCorrectiveAction','ApproveCivilDefectClosure','RejectCivilDefectClosure','ApproveCivilWorkClosure')
              ) THROW 52083, 'Civil inspection revision lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionRevisions_AppendOnly ON dbo.ProjectCivilInspectionRevisions AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              THROW 52084, 'Civil inspection revisions are append-only.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilInspectionRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilInspectionRevisions_Lineage;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilInspectionControls_Lifecycle;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilInspectionControls_Lineage;
            DROP TABLE IF EXISTS dbo.ProjectCivilInspectionRevisions;
            DROP TABLE IF EXISTS dbo.ProjectCivilInspectionControls;
            """);
    }
}
