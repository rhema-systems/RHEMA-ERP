using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0401 narrowly registers Building Inspectorate development files and central-DMS evidence.</summary>
public partial class AddCivilEngineeringDevelopmentApprovalFiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilDevelopmentApprovalFiles (
              Id uniqueidentifier NOT NULL PRIMARY KEY, FileNumber varchar(40) NOT NULL, ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
              ApplicantBusinessPartnerId uniqueidentifier NULL, ApplicantName nvarchar(250) NOT NULL, ProjectId uniqueidentifier NOT NULL, EstateManagedAssetId uniqueidentifier NOT NULL,
              ApplicationReference varchar(120) NOT NULL, CurrentSection varchar(80) NOT NULL, DueDate datetime2 NOT NULL, Status int NOT NULL,
              SiteInspectionDueDate datetime2 NULL, SiteInspectedAt datetime2 NULL,
              ConfigurationProfileId uniqueidentifier NOT NULL, PermittingConfigurationDecisionId uniqueidentifier NOT NULL, DocumentConfigurationDecisionId uniqueidentifier NOT NULL,
              WorkflowDefinitionId uniqueidentifier NOT NULL, MetadataTemplateId uniqueidentifier NOT NULL, MetadataTemplateCodeSnapshot varchar(80) NOT NULL, PolicyHash varchar(64) NOT NULL,
              CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_Applicant FOREIGN KEY (ApplicantBusinessPartnerId) REFERENCES BusinessPartners(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_Property FOREIGN KEY (EstateManagedAssetId) REFERENCES EstateManagedAssets(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_PermittingDecision FOREIGN KEY (PermittingConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_DocumentDecision FOREIGN KEY (DocumentConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_Workflow FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_Template FOREIGN KEY (MetadataTemplateId) REFERENCES CentralDocumentMetadataTemplates(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFiles_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalFiles_Status CHECK (Status IN (0,1,2)),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalFiles_Section CHECK (CurrentSection='BuildingInspectorate'),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalFiles_Applicant CHECK (LEN(LTRIM(RTRIM(ApplicantName)))>0),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalFiles_Dates CHECK (CONVERT(date, DueDate)>=CONVERT(date, CreatedAt) AND (SiteInspectionDueDate IS NULL OR SiteInspectionDueDate<=DueDate) AND (SiteInspectedAt IS NULL OR SiteInspectedAt>=CreatedAt))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalFiles_TenantId_ClientRequestId ON ProjectCivilDevelopmentApprovalFiles(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalFiles_TenantId_FileNumber ON ProjectCivilDevelopmentApprovalFiles(TenantId, FileNumber) WHERE IsDeleted=0;
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalFiles_TenantId_ProjectId_EstateManagedAssetId_Status ON ProjectCivilDevelopmentApprovalFiles(TenantId, ProjectId, EstateManagedAssetId, Status);

            CREATE TABLE ProjectCivilDevelopmentApprovalEvidence (
              Id uniqueidentifier NOT NULL PRIMARY KEY, DevelopmentApprovalFileId uniqueidentifier NOT NULL, Kind int NOT NULL, CentralDocumentRecordId uniqueidentifier NOT NULL, CentralDocumentVersionId uniqueidentifier NOT NULL,
              CorrelationId nvarchar(100) NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEvidence_File FOREIGN KEY (DevelopmentApprovalFileId) REFERENCES ProjectCivilDevelopmentApprovalFiles(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEvidence_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEvidence_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEvidence_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalEvidence_Kind CHECK (Kind IN (0,1))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalEvidence_TenantId_FileId_VersionId ON ProjectCivilDevelopmentApprovalEvidence(TenantId, DevelopmentApprovalFileId, CentralDocumentVersionId);
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalEvidence_TenantId_FileId_Kind ON ProjectCivilDevelopmentApprovalEvidence(TenantId, DevelopmentApprovalFileId, Kind);

            CREATE TABLE ProjectCivilDevelopmentApprovalFileRevisions (
              Id uniqueidentifier NOT NULL PRIMARY KEY, DevelopmentApprovalFileId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
              ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileRevisions_File FOREIGN KEY (DevelopmentApprovalFileId) REFERENCES ProjectCivilDevelopmentApprovalFiles(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalFileRevisions_TenantId_FileId_CreatedAt ON ProjectCivilDevelopmentApprovalFileRevisions(TenantId, DevelopmentApprovalFileId, CreatedAt);
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalFileRevisions_TenantId_CorrelationId ON ProjectCivilDevelopmentApprovalFileRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalFiles_Lineage ON ProjectCivilDevelopmentApprovalFiles AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN EstateManagedAssets property ON property.Id=value.EstateManagedAssetId AND property.TenantId=value.TenantId AND property.IsDeleted=0 AND (property.ProjectId IS NULL OR property.ProjectId=value.ProjectId)
                LEFT JOIN BusinessPartners applicant ON applicant.Id=value.ApplicantBusinessPartnerId AND applicant.TenantId=value.TenantId AND applicant.IsDeleted=0 AND applicant.RegistrationStatus='Approved'
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions permitting ON permitting.Id=value.PermittingConfigurationDecisionId AND permitting.ProfileId=value.ConfigurationProfileId AND permitting.TenantId=value.TenantId AND permitting.IsDeleted=0 AND permitting.ConfigurationKey='CIV-CFG-009'
                LEFT JOIN CivilEngineeringConfigurationDecisions documentPolicy ON documentPolicy.Id=value.DocumentConfigurationDecisionId AND documentPolicy.ProfileId=value.ConfigurationProfileId AND documentPolicy.TenantId=value.TenantId AND documentPolicy.IsDeleted=0 AND documentPolicy.ConfigurationKey='CIV-CFG-004'
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0 AND workflow.IsActive=1 AND workflow.LifecycleStatus=1
                LEFT JOIN WorkflowEntityTypes workflowType ON workflowType.Id=workflow.EntityTypeId AND workflowType.TenantId=value.TenantId AND workflowType.IsDeleted=0 AND workflowType.IsActive=1 AND workflowType.Code='PROJECT_PERMITTING_REVIEW'
                LEFT JOIN CentralDocumentMetadataTemplates template ON template.Id=value.MetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0 AND template.IsActive=1 AND template.PublishedAt IS NOT NULL
                WHERE project.Id IS NULL OR property.Id IS NULL OR (value.ApplicantBusinessPartnerId IS NOT NULL AND applicant.Id IS NULL)
                   OR profile.Id IS NULL OR permitting.Id IS NULL OR documentPolicy.Id IS NULL OR workflow.Id IS NULL OR workflowType.Id IS NULL OR template.Id IS NULL
                   OR (value.ApplicantBusinessPartnerId IS NOT NULL AND value.ApplicantName<>applicant.PartnerName)
                   OR value.MetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR JSON_VALUE(permitting.ValueJson,'$.workflowDefinitionId')<>CONVERT(varchar(36),value.WorkflowDefinitionId)
                   OR JSON_VALUE(documentPolicy.ValueJson,'$.metadataTemplateId')<>CONVERT(varchar(36),value.MetadataTemplateId)
                   OR JSON_VALUE(documentPolicy.ValueJson,'$.requireVersioning')<>'true'
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (
                        profile.LifecycleStatus<>1 OR profile.EffectiveFrom>SYSUTCDATETIME() OR (profile.EffectiveTo IS NOT NULL AND profile.EffectiveTo<SYSUTCDATETIME())
                        OR permitting.Status<>2 OR permitting.ApprovalStatus<>1 OR permitting.EvidenceStatus<>2 OR documentPolicy.Status<>2 OR documentPolicy.ApprovalStatus<>1 OR documentPolicy.EvidenceStatus<>2
                        OR NOT EXISTS (SELECT 1 FROM WorkflowSteps step WHERE step.WorkflowDefinitionId=value.WorkflowDefinitionId AND step.TenantId=value.TenantId AND step.IsDeleted=0)
                   ))
              ) THROW 52240, 'Civil development approval file tenant, project/property, applicant, configuration, workflow, or DMS lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalFiles_Lifecycle ON ProjectCivilDevelopmentApprovalFiles AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52241, 'Civil development approval files cannot be deleted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE value.TenantId<>prior.TenantId OR value.FileNumber<>prior.FileNumber OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash
                   OR ISNULL(value.ApplicantBusinessPartnerId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ApplicantBusinessPartnerId,'00000000-0000-0000-0000-000000000000') OR value.ApplicantName<>prior.ApplicantName
                   OR value.ProjectId<>prior.ProjectId OR value.EstateManagedAssetId<>prior.EstateManagedAssetId OR value.ApplicationReference<>prior.ApplicationReference OR value.CurrentSection<>prior.CurrentSection OR value.DueDate<>prior.DueDate OR ISNULL(value.SiteInspectionDueDate,'19000101')<>ISNULL(prior.SiteInspectionDueDate,'19000101')
                   OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.PermittingConfigurationDecisionId<>prior.PermittingConfigurationDecisionId OR value.DocumentConfigurationDecisionId<>prior.DocumentConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.MetadataTemplateId<>prior.MetadataTemplateId OR value.MetadataTemplateCodeSnapshot<>prior.MetadataTemplateCodeSnapshot OR value.PolicyHash<>prior.PolicyHash OR value.IsDeleted<>prior.IsDeleted
              ) THROW 52242, 'Civil development approval-file identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.Status=0 AND value.Status IN (0,2)) OR (prior.Status=1 AND value.Status IN (1,2)) OR (prior.Status=2 AND value.Status=2))) THROW 52243, 'Invalid Civil development approval-file lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE (prior.Status=2 AND (value.SiteInspectedAt<>prior.SiteInspectedAt OR value.Status<>prior.Status)) OR (value.Status=2 AND value.SiteInspectedAt IS NULL)) THROW 52244, 'Completed Civil site inspection evidence is immutable and required.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEvidence_Lineage ON ProjectCivilDevelopmentApprovalEvidence AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                JOIN ProjectCivilDevelopmentApprovalFiles approvalFile ON approvalFile.Id=value.DevelopmentApprovalFileId AND approvalFile.TenantId=value.TenantId AND approvalFile.IsDeleted=0
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published' AND record.MetadataTemplateCode=approvalFile.MetadataTemplateCodeSnapshot
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL AND record.CurrentVersion=version.VersionNumber
                LEFT JOIN CivilEngineeringConfigurationDecisions documentPolicy ON documentPolicy.Id=approvalFile.DocumentConfigurationDecisionId AND documentPolicy.TenantId=value.TenantId AND documentPolicy.ProfileId=approvalFile.ConfigurationProfileId AND documentPolicy.ConfigurationKey='CIV-CFG-004' AND documentPolicy.IsDeleted=0
                WHERE record.Id IS NULL OR version.Id IS NULL OR documentPolicy.Id IS NULL
                   OR JSON_VALUE(documentPolicy.ValueJson,'$.requireVersioning')<>'true'
                   OR version.FileSize IS NULL OR version.FileSize<=0 OR version.FileSize>TRY_CONVERT(bigint,JSON_VALUE(documentPolicy.ValueJson,'$.maximumFileSizeMb'))*1048576
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(documentPolicy.ValueJson,'$.allowedFileExtensions') extension WHERE LOWER(extension.value)=LOWER(RIGHT(ISNULL(version.FileName,''),CHARINDEX('.',REVERSE(ISNULL(version.FileName,''))))))
              ) THROW 52245, 'Civil development-file evidence must be a current Published tenant DMS version under the frozen metadata template.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEvidence_AppendOnly ON ProjectCivilDevelopmentApprovalEvidence AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52246, 'Civil development-file evidence is append-only.', 1; END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalFileRevisions_Lineage ON ProjectCivilDevelopmentApprovalFileRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilDevelopmentApprovalFiles approvalFile ON approvalFile.Id=value.DevelopmentApprovalFileId AND approvalFile.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE approvalFile.Id IS NULL OR actor.Id IS NULL) THROW 52247, 'Civil development-file revision lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalFileRevisions_AppendOnly ON ProjectCivilDevelopmentApprovalFileRevisions AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52248, 'Civil development-file revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalFileRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalFileRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEvidence_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEvidence_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalFiles_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalFiles_Lineage;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalFileRevisions;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalEvidence;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalFiles;
            """);
    }
}
