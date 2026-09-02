using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds a Civil workflow envelope over the existing Projects EOT record. Quantity Survey
/// retains variation/budget application; Procurement retains the contract; central DMS owns files.
/// </summary>
public partial class AddCivilEngineeringExtensionOfTimeControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET QUOTED_IDENTIFIER ON;
            CREATE TABLE dbo.ProjectCivilExtensionOfTimeControls (
              Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, ProjectExtensionOfTimeId uniqueidentifier NOT NULL,
              ContractId uniqueidentifier NOT NULL, QuantitySurveyVariationOrderId uniqueidentifier NULL, ScopeSummary nvarchar(500) NOT NULL,
              HasCostImpact bit NOT NULL, Status varchar(40) NOT NULL, ApprovalStatus varchar(40) NOT NULL,
              SubmittedById uniqueidentifier NOT NULL, SubmittedAt datetime2 NOT NULL, ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL,
              RejectionReason nvarchar(2000) NULL, WorkflowInstanceId uniqueidentifier NULL,
              EvidenceDocumentRecordId uniqueidentifier NOT NULL, EvidenceDocumentVersionId uniqueidentifier NOT NULL,
              ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL, WorkflowDefinitionId uniqueidentifier NOT NULL,
              EvidenceMetadataTemplateId uniqueidentifier NOT NULL, EvidenceMetadataTemplateCodeSnapshot varchar(80) NOT NULL, PolicyHash varchar(64) NOT NULL,
              ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL,
              LastMutationRequestHash varchar(64) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
              CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Project FOREIGN KEY (ProjectId) REFERENCES dbo.Projects(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Eot FOREIGN KEY (ProjectExtensionOfTimeId) REFERENCES dbo.ProjectExtensionOfTimeRequests(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Contract FOREIGN KEY (ContractId) REFERENCES dbo.Contracts(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_QsVariation FOREIGN KEY (QuantitySurveyVariationOrderId) REFERENCES dbo.ProjectVariationOrders(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_EvidenceRecord FOREIGN KEY (EvidenceDocumentRecordId) REFERENCES dbo.CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_EvidenceVersion FOREIGN KEY (EvidenceDocumentVersionId) REFERENCES dbo.CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES dbo.CivilEngineeringConfigurationProfiles(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES dbo.CivilEngineeringConfigurationDecisions(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Workflow FOREIGN KEY (WorkflowDefinitionId) REFERENCES dbo.WorkflowDefinitions(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_WorkflowInstance FOREIGN KEY (WorkflowInstanceId) REFERENCES dbo.WorkflowInstances(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Template FOREIGN KEY (EvidenceMetadataTemplateId) REFERENCES dbo.CentralDocumentMetadataTemplates(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_SubmittedBy FOREIGN KEY (SubmittedById) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_ApprovedBy FOREIGN KEY (ApprovedById) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeControls_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id),
              CONSTRAINT CK_ProjectCivilExtensionOfTimeControls_State CHECK (
                (Status='PendingApproval' AND ApprovalStatus='Pending' AND ApprovedById IS NULL AND ApprovedAt IS NULL AND RejectionReason IS NULL) OR
                (Status='Approved' AND ApprovalStatus='Approved' AND ApprovedById IS NOT NULL AND ApprovedAt IS NOT NULL AND RejectionReason IS NULL) OR
                (Status='Rejected' AND ApprovalStatus='Rejected' AND ApprovedById IS NULL AND ApprovedAt IS NULL AND RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(RejectionReason)))>=3)),
              CONSTRAINT CK_ProjectCivilExtensionOfTimeControls_Variation CHECK (HasCostImpact=0 OR QuantitySurveyVariationOrderId IS NOT NULL)
            );
            CREATE UNIQUE INDEX IX_ProjectCivilExtensionOfTimeControls_TenantId_ClientRequestId ON dbo.ProjectCivilExtensionOfTimeControls(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilExtensionOfTimeControls_TenantId_ProjectExtensionOfTimeId ON dbo.ProjectCivilExtensionOfTimeControls(TenantId, ProjectExtensionOfTimeId) WHERE IsDeleted=0;
            CREATE INDEX IX_ProjectCivilExtensionOfTimeControls_TenantId_ProjectId_Status_SubmittedAt ON dbo.ProjectCivilExtensionOfTimeControls(TenantId, ProjectId, Status, SubmittedAt);

            CREATE TABLE dbo.ProjectCivilExtensionOfTimeRevisions (
              Id uniqueidentifier NOT NULL PRIMARY KEY, ExtensionOfTimeControlId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
              FromStatus nvarchar(40) NOT NULL, ToStatus nvarchar(40) NOT NULL, ActorUserId uniqueidentifier NOT NULL, ActorName nvarchar(300) NOT NULL,
              ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL, BeforeJson nvarchar(max) NULL,
              AfterJson nvarchar(max) NOT NULL, RequestHash varchar(64) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
              CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilExtensionOfTimeRevisions_Control FOREIGN KEY (ExtensionOfTimeControlId) REFERENCES dbo.ProjectCivilExtensionOfTimeControls(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES dbo.Users(Id),
              CONSTRAINT FK_ProjectCivilExtensionOfTimeRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES dbo.Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilExtensionOfTimeRevisions_TenantId_Control_CreatedAt ON dbo.ProjectCivilExtensionOfTimeRevisions(TenantId, ExtensionOfTimeControlId, CreatedAt);
            CREATE INDEX IX_ProjectCivilExtensionOfTimeRevisions_TenantId_CorrelationId ON dbo.ProjectCivilExtensionOfTimeRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilExtensionOfTimeControls_Lineage ON dbo.ProjectCivilExtensionOfTimeControls AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN dbo.ProjectExtensionOfTimeRequests eot ON eot.Id=value.ProjectExtensionOfTimeId AND eot.ProjectId=value.ProjectId AND eot.ContractId=value.ContractId AND eot.TenantId=value.TenantId AND eot.IsDeleted=0
                LEFT JOIN dbo.Contracts contract ON contract.Id=value.ContractId AND contract.TenantId=value.TenantId AND contract.IsDeleted=0 AND contract.Status='Active' AND contract.ContractType='Works'
                LEFT JOIN dbo.ProjectVariationOrders variation ON variation.Id=value.QuantitySurveyVariationOrderId AND variation.ProjectId=value.ProjectId AND variation.ContractId=value.ContractId AND variation.TenantId=value.TenantId AND variation.IsDeleted=0 AND variation.IsQuantitySurveyGoverned=1
                LEFT JOIN dbo.CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-014' AND decision.IsDeleted=0
                LEFT JOIN dbo.WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0
                LEFT JOIN dbo.WorkflowEntityTypes entityType ON entityType.Id=workflow.EntityTypeId AND entityType.TenantId=value.TenantId AND entityType.IsDeleted=0 AND entityType.IsActive=1 AND entityType.Code='PROJECT_CIVIL_EXTENSION_OF_TIME'
                LEFT JOIN dbo.CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentVersions evidenceVersion ON evidenceVersion.Id=value.EvidenceDocumentVersionId AND evidenceVersion.DocumentRecordId=value.EvidenceDocumentRecordId AND evidenceVersion.TenantId=value.TenantId AND evidenceVersion.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentRecords evidenceRecord ON evidenceRecord.Id=evidenceVersion.DocumentRecordId AND evidenceRecord.TenantId=value.TenantId AND evidenceRecord.IsDeleted=0
                LEFT JOIN dbo.Users submitter ON submitter.Id=value.SubmittedById AND submitter.TenantId=value.TenantId AND submitter.IsActive=1
                WHERE project.Id IS NULL OR eot.Id IS NULL OR contract.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR workflow.Id IS NULL OR entityType.Id IS NULL OR template.Id IS NULL OR evidenceVersion.Id IS NULL OR evidenceRecord.Id IS NULL OR submitter.Id IS NULL
                   OR NOT EXISTS (SELECT 1 FROM dbo.ProjectPackages package WHERE package.TenantId=value.TenantId AND package.ProjectId=value.ProjectId AND package.ContractId=value.ContractId AND package.IsDeleted=0)
                   OR evidenceRecord.MetadataTemplateCode<>template.TemplateCode OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR evidenceVersion.Status<>'Published' OR evidenceRecord.LifecycleStatus<>'Active' OR evidenceRecord.VersionStatus<>'Published' OR evidenceRecord.CurrentVersion<>evidenceVersion.VersionNumber OR evidenceVersion.PublishedAt IS NULL
                   OR (value.QuantitySurveyVariationOrderId IS NOT NULL AND (variation.Id IS NULL OR variation.Status<>'Approved' OR variation.ApprovalStatus<>'Approved' OR variation.DownstreamApplicationStatus<>'Applied'))
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (eot.Status NOT IN ('Draft','UnderReview') OR profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2 OR workflow.IsActive<>1 OR workflow.LifecycleStatus<>1 OR template.IsActive<>1 OR template.PublishedAt IS NULL OR (SELECT COUNT(*) FROM dbo.WorkflowSteps step WHERE step.WorkflowDefinitionId=workflow.Id AND step.TenantId=value.TenantId AND step.IsDeleted=0)<2))
              ) THROW 52145, 'Civil extension-of-time project, QS variation, DMS, workflow, contract, or frozen policy lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilExtensionOfTimeControls_Lifecycle ON dbo.ProjectCivilExtensionOfTimeControls AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id))
                THROW 52146, 'Civil extension-of-time controls cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE
                value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectExtensionOfTimeId<>prior.ProjectExtensionOfTimeId OR value.ContractId<>prior.ContractId OR ISNULL(value.QuantitySurveyVariationOrderId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.QuantitySurveyVariationOrderId,'00000000-0000-0000-0000-000000000000') OR value.ScopeSummary<>prior.ScopeSummary OR value.HasCostImpact<>prior.HasCostImpact OR value.SubmittedById<>prior.SubmittedById OR value.SubmittedAt<>prior.SubmittedAt OR value.EvidenceDocumentRecordId<>prior.EvidenceDocumentRecordId OR value.EvidenceDocumentVersionId<>prior.EvidenceDocumentVersionId OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted)
                THROW 52147, 'Civil extension-of-time subject, evidence and policy lineage is immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE
                (prior.Status='PendingApproval' AND value.Status NOT IN ('PendingApproval','Approved','Rejected')) OR
                (prior.Status IN ('Approved','Rejected') AND value.Status<>prior.Status))
                THROW 52148, 'Invalid Civil extension-of-time lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE
                (value.Status='PendingApproval' AND (value.ApprovalStatus<>'Pending' OR value.ApprovedById IS NOT NULL OR value.ApprovedAt IS NOT NULL OR value.RejectionReason IS NOT NULL)) OR
                (value.Status='Approved' AND (value.ApprovalStatus<>'Approved' OR value.ApprovedById IS NULL OR value.ApprovedAt IS NULL OR value.RejectionReason IS NOT NULL)) OR
                (value.Status='Rejected' AND (value.ApprovalStatus<>'Rejected' OR value.ApprovedById IS NOT NULL OR value.ApprovedAt IS NOT NULL OR LEN(LTRIM(RTRIM(ISNULL(value.RejectionReason,''))))<3)))
                THROW 52149, 'Civil extension-of-time decision state is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilExtensionOfTimeRevisions_Lineage ON dbo.ProjectCivilExtensionOfTimeRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.ProjectCivilExtensionOfTimeControls control ON control.Id=value.ExtensionOfTimeControlId AND control.TenantId=value.TenantId AND control.IsDeleted=0
                LEFT JOIN dbo.Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1
                WHERE control.Id IS NULL OR actor.Id IS NULL OR LEN(value.RequestHash)<>64 OR value.Action NOT IN ('CreateCivilExtensionOfTime','SubmitCivilExtensionOfTime','ApproveCivilExtensionOfTime','RejectCivilExtensionOfTime')
              ) THROW 52150, 'Civil extension-of-time revision lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilExtensionOfTimeRevisions_AppendOnly ON dbo.ProjectCivilExtensionOfTimeRevisions AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              THROW 52151, 'Civil extension-of-time revisions are append-only.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectExtensionOfTimeRequests_CivilControl ON dbo.ProjectExtensionOfTimeRequests AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior JOIN dbo.ProjectCivilExtensionOfTimeControls control ON control.ProjectExtensionOfTimeId=prior.Id AND control.TenantId=prior.TenantId AND control.IsDeleted=0 WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id))
                THROW 52152, 'A governed Civil extension-of-time request cannot be deleted directly.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN dbo.Contracts contract ON contract.Id=value.ContractId AND contract.TenantId=value.TenantId AND contract.IsDeleted=0 AND contract.ContractType='Works' LEFT JOIN dbo.ProjectCivilExtensionOfTimeControls control ON control.ProjectExtensionOfTimeId=value.Id AND control.TenantId=value.TenantId AND control.IsDeleted=0 WHERE NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND value.Status<>'Draft' AND control.Id IS NULL)
                THROW 52153, 'Works extension-of-time requests must use the governed Civil control before review or approval.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id JOIN dbo.Contracts contract ON contract.Id=value.ContractId AND contract.TenantId=value.TenantId AND contract.IsDeleted=0 AND contract.ContractType='Works' LEFT JOIN dbo.ProjectCivilExtensionOfTimeControls control ON control.ProjectExtensionOfTimeId=value.Id AND control.TenantId=value.TenantId AND control.IsDeleted=0 WHERE control.Id IS NULL AND value.Status<>'Draft')
                THROW 52153, 'Works extension-of-time requests must use the governed Civil control before review or approval.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id JOIN dbo.ProjectCivilExtensionOfTimeControls control ON control.ProjectExtensionOfTimeId=value.Id AND control.TenantId=value.TenantId AND control.IsDeleted=0 WHERE
                value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR ISNULL(value.ProjectPhaseId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectPhaseId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ProjectPackageId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectPackageId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ContractId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ContractId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ReferenceNumber,'')<>ISNULL(prior.ReferenceNumber,'') OR value.Title<>prior.Title OR ISNULL(value.Reason,'')<>ISNULL(prior.Reason,'') OR value.RequestedDate<>prior.RequestedDate OR ISNULL(value.DaysRequested,-1)<>ISNULL(prior.DaysRequested,-1) OR ISNULL(value.RevisedCompletionDate,'19000101')<>ISNULL(prior.RevisedCompletionDate,'19000101') OR ISNULL(value.RequestedByName,'')<>ISNULL(prior.RequestedByName,'') )
                THROW 52154, 'The governed Civil extension-of-time subject is immutable outside its control workflow.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN dbo.ProjectCivilExtensionOfTimeControls control ON control.ProjectExtensionOfTimeId=value.Id AND control.TenantId=value.TenantId AND control.IsDeleted=0 WHERE
                (control.Status='PendingApproval' AND value.Status<>'UnderReview') OR (control.Status='Approved' AND (value.Status<>'Approved' OR value.DaysApproved<>value.DaysRequested OR value.DecisionDate IS NULL)) OR (control.Status='Rejected' AND value.Status<>'Rejected'))
                THROW 52155, 'The Projects extension-of-time status must match its governed Civil workflow state.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProjectExtensionOfTimeRequests_CivilControl;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilExtensionOfTimeRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilExtensionOfTimeRevisions_Lineage;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilExtensionOfTimeControls_Lifecycle;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilExtensionOfTimeControls_Lineage;
            DROP TABLE IF EXISTS dbo.ProjectCivilExtensionOfTimeRevisions;
            DROP TABLE IF EXISTS dbo.ProjectCivilExtensionOfTimeControls;
            """);
    }
}
