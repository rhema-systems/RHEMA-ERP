using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the Civil Engineering intake overlay for Maintenance schedules, Estate buildings/properties
/// and Helpdesk complaints. The source modules, shared Workflow, central DMS and AuditLogs remain owners.
/// </summary>
public partial class AddCivilEngineeringMaintenanceIntakes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE CivilEngineeringMaintenanceIntakes (
                Id uniqueidentifier NOT NULL PRIMARY KEY, IntakeNumber varchar(40) NOT NULL,
                WorkClassification int NOT NULL, Source int NOT NULL, Urgency int NOT NULL,
                Title nvarchar(300) NOT NULL, Description nvarchar(4000) NOT NULL,
                ProjectId uniqueidentifier NULL, MaintenanceAssetId uniqueidentifier NULL, EstateManagedAssetId uniqueidentifier NULL,
                MaintenanceScheduleId uniqueidentifier NULL, HelpdeskTicketId uniqueidentifier NULL,
                RequesterUserId uniqueidentifier NOT NULL, PriorityLevelId uniqueidentifier NOT NULL,
                CentralDocumentRecordId uniqueidentifier NOT NULL, CentralDocumentVersionId uniqueidentifier NOT NULL,
                ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL,
                WorkflowDefinitionId uniqueidentifier NOT NULL, EvidenceMetadataTemplateId uniqueidentifier NOT NULL,
                EvidenceMetadataTemplateCodeSnapshot varchar(80) NOT NULL, PolicyHash varchar(64) NOT NULL,
                Status varchar(40) NOT NULL, ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
                CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_MaintenanceAsset FOREIGN KEY (MaintenanceAssetId) REFERENCES MaintenanceAssets(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_EstateAsset FOREIGN KEY (EstateManagedAssetId) REFERENCES EstateManagedAssets(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Schedule FOREIGN KEY (MaintenanceScheduleId) REFERENCES MaintenanceSchedules(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_HelpdeskTicket FOREIGN KEY (HelpdeskTicketId) REFERENCES EhcTickets(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Requester FOREIGN KEY (RequesterUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Priority FOREIGN KEY (PriorityLevelId) REFERENCES PriorityLevels(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_DocumentRecord FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_DocumentVersion FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Workflow FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Template FOREIGN KEY (EvidenceMetadataTemplateId) REFERENCES CentralDocumentMetadataTemplates(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakes_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_CivilEngineeringMaintenanceIntakes_Target CHECK ((MaintenanceAssetId IS NOT NULL AND EstateManagedAssetId IS NULL) OR (MaintenanceAssetId IS NULL AND EstateManagedAssetId IS NOT NULL)),
                CONSTRAINT CK_CivilEngineeringMaintenanceIntakes_SourceLink CHECK ((WorkClassification=2 AND Source=1 AND MaintenanceScheduleId IS NOT NULL AND HelpdeskTicketId IS NULL) OR (WorkClassification=3 AND MaintenanceScheduleId IS NULL AND HelpdeskTicketId IS NULL) OR (WorkClassification=5 AND Source IN (2,3) AND MaintenanceScheduleId IS NULL AND HelpdeskTicketId IS NOT NULL)),
                CONSTRAINT CK_CivilEngineeringMaintenanceIntakes_Status CHECK (Status IN ('Logged','AssessmentInProgress','AssessmentReturned','Assessed'))
            );
            CREATE UNIQUE INDEX IX_CivilEngineeringMaintenanceIntakes_TenantId_ClientRequestId ON CivilEngineeringMaintenanceIntakes(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_CivilEngineeringMaintenanceIntakes_TenantId_IntakeNumber ON CivilEngineeringMaintenanceIntakes(TenantId, IntakeNumber) WHERE IsDeleted=0;
            CREATE INDEX IX_CivilEngineeringMaintenanceIntakes_TenantId_Status_WorkClassification ON CivilEngineeringMaintenanceIntakes(TenantId, Status, WorkClassification);
            CREATE INDEX IX_CivilEngineeringMaintenanceIntakes_TenantId_MaintenanceAssetId_EstateManagedAssetId ON CivilEngineeringMaintenanceIntakes(TenantId, MaintenanceAssetId, EstateManagedAssetId);

            CREATE TABLE CivilEngineeringMaintenanceIntakeRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, IntakeId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
                ActorUserId uniqueidentifier NOT NULL, ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL,
                CorrelationId nvarchar(100) NOT NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakeRevisions_Intake FOREIGN KEY (IntakeId) REFERENCES CivilEngineeringMaintenanceIntakes(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakeRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceIntakeRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_CivilEngineeringMaintenanceIntakeRevisions_TenantId_IntakeId_CreatedAt ON CivilEngineeringMaintenanceIntakeRevisions(TenantId, IntakeId, CreatedAt);
            CREATE INDEX IX_CivilEngineeringMaintenanceIntakeRevisions_TenantId_CorrelationId ON CivilEngineeringMaintenanceIntakeRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceIntakes_Lineage ON CivilEngineeringMaintenanceIntakes AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN MaintenanceAssets maintenanceAsset ON maintenanceAsset.Id=value.MaintenanceAssetId AND maintenanceAsset.TenantId=value.TenantId AND maintenanceAsset.IsDeleted=0 AND maintenanceAsset.Status=0
                LEFT JOIN EstateManagedAssets estateAsset ON estateAsset.Id=value.EstateManagedAssetId AND estateAsset.TenantId=value.TenantId AND estateAsset.IsDeleted=0
                LEFT JOIN MaintenanceSchedules schedule ON schedule.Id=value.MaintenanceScheduleId AND schedule.TenantId=value.TenantId AND schedule.IsDeleted=0 AND schedule.IsActive=1 AND schedule.AssetId=value.MaintenanceAssetId
                LEFT JOIN EhcTickets ticket ON ticket.Id=value.HelpdeskTicketId AND ticket.TenantId=value.TenantId AND ticket.IsDeleted=0 AND ticket.TicketType=2 AND ticket.Status<>7
                LEFT JOIN Users requester ON requester.Id=value.RequesterUserId AND requester.TenantId=value.TenantId AND requester.IsActive=1
                LEFT JOIN PriorityLevels priority ON priority.Id=value.PriorityLevelId AND priority.TenantId=value.TenantId AND priority.IsDeleted=0 AND priority.IsActive=1
                LEFT JOIN CentralDocumentRecords documentRecord ON documentRecord.Id=value.CentralDocumentRecordId AND documentRecord.TenantId=value.TenantId AND documentRecord.IsDeleted=0 AND documentRecord.LifecycleStatus='Active' AND documentRecord.VersionStatus='Published'
                LEFT JOIN CentralDocumentVersions documentVersion ON documentVersion.Id=value.CentralDocumentVersionId AND documentVersion.DocumentRecordId=value.CentralDocumentRecordId AND documentVersion.TenantId=value.TenantId AND documentVersion.IsDeleted=0 AND documentVersion.Status='Published' AND documentVersion.PublishedAt IS NOT NULL
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-007' AND decision.IsDeleted=0
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0 AND workflow.IsActive=1 AND workflow.LifecycleStatus=1
                LEFT JOIN WorkflowEntityTypes workflowType ON workflowType.Id=workflow.EntityTypeId AND workflowType.TenantId=value.TenantId AND workflowType.IsDeleted=0 AND workflowType.IsActive=1 AND workflowType.Code='PROJECT_MAINTENANCE_ASSESSMENT'
                LEFT JOIN CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0 AND template.IsActive=1 AND template.PublishedAt IS NOT NULL
                WHERE (value.ProjectId IS NOT NULL AND project.Id IS NULL)
                   OR (value.MaintenanceAssetId IS NOT NULL AND maintenanceAsset.Id IS NULL)
                   OR (value.EstateManagedAssetId IS NOT NULL AND estateAsset.Id IS NULL)
                   OR (value.MaintenanceScheduleId IS NOT NULL AND schedule.Id IS NULL)
                   OR (value.HelpdeskTicketId IS NOT NULL AND ticket.Id IS NULL)
                   OR requester.Id IS NULL OR priority.Id IS NULL OR documentRecord.Id IS NULL OR documentVersion.Id IS NULL
                   OR profile.Id IS NULL OR decision.Id IS NULL OR workflow.Id IS NULL OR workflowType.Id IS NULL OR template.Id IS NULL
                   OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR documentRecord.CurrentVersion<>documentVersion.VersionNumber OR documentRecord.MetadataTemplateCode<>template.TemplateCode
                   OR JSON_VALUE(decision.ValueJson,'$.workflowDefinitionId')<>CONVERT(varchar(36),value.WorkflowDefinitionId)
                   OR JSON_VALUE(decision.ValueJson,'$.metadataTemplateId')<>CONVERT(varchar(36),value.EvidenceMetadataTemplateId)
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR profile.EffectiveFrom>SYSUTCDATETIME() OR (profile.EffectiveTo IS NOT NULL AND profile.EffectiveTo<SYSUTCDATETIME()) OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2 OR NOT EXISTS (SELECT 1 FROM WorkflowSteps step WHERE step.WorkflowDefinitionId=value.WorkflowDefinitionId AND step.TenantId=value.TenantId AND step.IsDeleted=0)))
              ) THROW 52200, 'Civil maintenance intake tenant, source, configuration, workflow, requester, priority, asset/property, or DMS lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceIntakes_Lifecycle ON CivilEngineeringMaintenanceIntakes AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52201, 'Civil maintenance intake records cannot be deleted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE value.TenantId<>prior.TenantId OR value.IntakeNumber<>prior.IntakeNumber OR value.WorkClassification<>prior.WorkClassification
                   OR value.Source<>prior.Source OR value.Urgency<>prior.Urgency OR value.Title<>prior.Title OR value.Description<>prior.Description
                   OR ISNULL(value.ProjectId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.MaintenanceAssetId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.MaintenanceAssetId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.EstateManagedAssetId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.EstateManagedAssetId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.MaintenanceScheduleId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.MaintenanceScheduleId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.HelpdeskTicketId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.HelpdeskTicketId,'00000000-0000-0000-0000-000000000000')
                   OR value.RequesterUserId<>prior.RequesterUserId OR value.PriorityLevelId<>prior.PriorityLevelId
                   OR value.CentralDocumentRecordId<>prior.CentralDocumentRecordId OR value.CentralDocumentVersionId<>prior.CentralDocumentVersionId
                   OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId
                   OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId
                   OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot OR value.PolicyHash<>prior.PolicyHash
                   OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted
              ) THROW 52202, 'Civil maintenance intake identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE NOT ((prior.Status='Logged' AND value.Status IN ('Logged','AssessmentInProgress'))
                    OR (prior.Status='AssessmentInProgress' AND value.Status IN ('AssessmentInProgress','AssessmentReturned','Assessed'))
                    OR (prior.Status='AssessmentReturned' AND value.Status IN ('AssessmentReturned','AssessmentInProgress'))
                    OR (prior.Status='Assessed' AND value.Status='Assessed'))
              ) THROW 52203, 'Invalid Civil maintenance intake lifecycle transition.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceIntakeRevisions_Lineage ON CivilEngineeringMaintenanceIntakeRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN CivilEngineeringMaintenanceIntakes intake ON intake.Id=value.IntakeId AND intake.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE intake.Id IS NULL OR actor.Id IS NULL) THROW 52204, 'Civil maintenance intake revision lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceIntakeRevisions_AppendOnly ON CivilEngineeringMaintenanceIntakeRevisions AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52205, 'Civil maintenance intake revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceIntakeRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceIntakeRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceIntakes_Lifecycle;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceIntakes_Lineage;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceIntakeRevisions;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceIntakes;
            """);
    }
}
