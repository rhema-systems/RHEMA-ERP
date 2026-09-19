using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0501 governance overlay for existing Projects work items; task feedback remains a later Civil phase.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821170000_AddCivilEngineeringDirectTaskControls")]
public partial class AddCivilEngineeringDirectTaskControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilDirectTaskControls (
              Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, WorkItemId uniqueidentifier NOT NULL, ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
              AssignedToUserId uniqueidentifier NOT NULL, AssignedRoleId uniqueidentifier NOT NULL, AssignedRoleName nvarchar(256) NOT NULL, Urgency int NOT NULL, DueDate datetime2 NOT NULL, Instructions nvarchar(4000) NOT NULL,
              CentralDocumentRecordId uniqueidentifier NULL, CentralDocumentVersionId uniqueidentifier NULL, ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL,
              WorkflowDefinitionId uniqueidentifier NOT NULL, FeedbackMetadataTemplateId uniqueidentifier NOT NULL, FeedbackMetadataTemplateCodeSnapshot nvarchar(120) NOT NULL, PolicyHash varchar(64) NOT NULL,
              WorkflowInstanceId uniqueidentifier NULL, Status varchar(40) NOT NULL, ApprovalStatus varchar(40) NOT NULL, ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_WorkItem FOREIGN KEY (WorkItemId) REFERENCES ProjectWorkItems(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Assignee FOREIGN KEY (AssignedToUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Role FOREIGN KEY (AssignedRoleId) REFERENCES AspNetRoles(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Workflow FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Template FOREIGN KEY (FeedbackMetadataTemplateId) REFERENCES CentralDocumentMetadataTemplates(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Instance FOREIGN KEY (WorkflowInstanceId) REFERENCES WorkflowInstances(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskControls_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilDirectTaskControls_Urgency CHECK (Urgency IN (0,1,2,3)),
              CONSTRAINT CK_ProjectCivilDirectTaskControls_Evidence CHECK ((CentralDocumentRecordId IS NULL AND CentralDocumentVersionId IS NULL) OR (CentralDocumentRecordId IS NOT NULL AND CentralDocumentVersionId IS NOT NULL)),
              CONSTRAINT CK_ProjectCivilDirectTaskControls_Status CHECK (Status IN ('Assigned','InProgress','PendingAcceptance','Accepted','Returned','Cancelled') AND ApprovalStatus IN ('Draft','Pending','Approved','Rejected'))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDirectTaskControls_TenantId_ClientRequestId ON ProjectCivilDirectTaskControls(TenantId,ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilDirectTaskControls_TenantId_WorkItemId ON ProjectCivilDirectTaskControls(TenantId,WorkItemId);
            CREATE INDEX IX_ProjectCivilDirectTaskControls_TenantId_ProjectId_AssignedToUserId_DueDate ON ProjectCivilDirectTaskControls(TenantId,ProjectId,AssignedToUserId,DueDate);

            CREATE TABLE ProjectCivilDirectTaskRevisions (
              Id uniqueidentifier NOT NULL PRIMARY KEY, DirectTaskControlId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
              ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDirectTaskRevisions_Task FOREIGN KEY (DirectTaskControlId) REFERENCES ProjectCivilDirectTaskControls(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilDirectTaskRevisions_TenantId_TaskId_CreatedAt ON ProjectCivilDirectTaskRevisions(TenantId,DirectTaskControlId,CreatedAt);
            CREATE INDEX IX_ProjectCivilDirectTaskRevisions_TenantId_CorrelationId ON ProjectCivilDirectTaskRevisions(TenantId,CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskControls_Lineage ON ProjectCivilDirectTaskControls AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN ProjectWorkItems workItem ON workItem.Id=value.WorkItemId AND workItem.TenantId=value.TenantId AND workItem.ProjectId=value.ProjectId AND workItem.IsDeleted=0 AND workItem.AssignedToUserId=value.AssignedToUserId AND workItem.Status='Assigned'
                LEFT JOIN Users assignee ON assignee.Id=value.AssignedToUserId AND assignee.TenantId=value.TenantId AND assignee.IsActive=1
                LEFT JOIN AspNetRoles role ON role.Id=value.AssignedRoleId AND role.Name=value.AssignedRoleName
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0 AND profile.LifecycleStatus=1
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.IsDeleted=0 AND decision.ConfigurationKey='CIV-CFG-010' AND decision.Status=2 AND decision.ApprovalStatus=1 AND decision.EvidenceStatus=2
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0 AND workflow.IsActive=1 AND workflow.LifecycleStatus=1
                LEFT JOIN WorkflowEntityTypes workflowType ON workflowType.Id=workflow.EntityTypeId AND workflowType.TenantId=value.TenantId AND workflowType.IsDeleted=0 AND workflowType.IsActive=1 AND workflowType.Code='PROJECT_TASK'
                LEFT JOIN CentralDocumentMetadataTemplates template ON template.Id=value.FeedbackMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0 AND template.IsActive=1 AND template.PublishedAt IS NOT NULL AND template.TemplateCode=value.FeedbackMetadataTemplateCodeSnapshot
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published'
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL AND record.CurrentVersion=version.VersionNumber
                WHERE project.Id IS NULL OR workItem.Id IS NULL OR assignee.Id IS NULL OR role.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR workflow.Id IS NULL OR workflowType.Id IS NULL OR template.Id IS NULL
                   OR value.Status<>'Assigned' OR value.ApprovalStatus<>'Draft' OR value.WorkflowInstanceId IS NOT NULL
                   OR JSON_VALUE(decision.ValueJson,'$.workflowDefinitionId')<>CONVERT(varchar(36),value.WorkflowDefinitionId)
                   OR JSON_VALUE(decision.ValueJson,'$.feedbackMetadataTemplateId')<>CONVERT(varchar(36),value.FeedbackMetadataTemplateId)
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(decision.ValueJson,'$.assigneeRoleIds') allowedRole WHERE TRY_CONVERT(uniqueidentifier,allowedRole.value)=value.AssignedRoleId)
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(decision.ValueJson,'$.allowedUrgencies') allowedUrgency WHERE TRY_CONVERT(int,allowedUrgency.value)=value.Urgency)
                   OR NOT EXISTS (SELECT 1 FROM UserRoles userRole WHERE userRole.UserId=value.AssignedToUserId AND userRole.RoleId=value.AssignedRoleId)
                   OR NOT EXISTS (SELECT 1 FROM ProjectMembers member WHERE member.TenantId=value.TenantId AND member.ProjectId=value.ProjectId AND member.UserId=value.AssignedToUserId AND member.Role=value.AssignedRoleName AND member.IsActive=1 AND member.IsDeleted=0)
                   OR (JSON_VALUE(decision.ValueJson,'$.requireDueDate')='true' AND value.DueDate IS NULL)
                   OR (value.CentralDocumentRecordId IS NOT NULL AND (record.Id IS NULL OR version.Id IS NULL))
              ) THROW 52280, 'Civil direct-task tenant, project, assignee, configuration, workflow or DMS lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskControls_Lifecycle ON ProjectCivilDirectTaskControls AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52281, 'Governed Civil direct tasks cannot be deleted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.WorkItemId<>prior.WorkItemId OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash
                   OR value.AssignedToUserId<>prior.AssignedToUserId OR value.AssignedRoleId<>prior.AssignedRoleId OR value.AssignedRoleName<>prior.AssignedRoleName OR value.Urgency<>prior.Urgency OR value.DueDate<>prior.DueDate
                   OR value.Instructions<>prior.Instructions OR ISNULL(value.CentralDocumentRecordId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.CentralDocumentRecordId,'00000000-0000-0000-0000-000000000000')
                   OR ISNULL(value.CentralDocumentVersionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.CentralDocumentVersionId,'00000000-0000-0000-0000-000000000000')
                   OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId
                   OR value.FeedbackMetadataTemplateId<>prior.FeedbackMetadataTemplateId OR value.FeedbackMetadataTemplateCodeSnapshot<>prior.FeedbackMetadataTemplateCodeSnapshot OR value.PolicyHash<>prior.PolicyHash OR value.IsDeleted<>prior.IsDeleted
              ) THROW 52282, 'Civil direct-task assignment content and frozen lineage are immutable; use the controlled reassignment flow.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskRevisions_Lineage ON ProjectCivilDirectTaskRevisions AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilDirectTaskControls task ON task.Id=value.DirectTaskControlId AND task.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE task.Id IS NULL OR actor.Id IS NULL)
                THROW 52283, 'Civil direct-task revision lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskRevisions_AppendOnly ON ProjectCivilDirectTaskRevisions AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52284, 'Civil direct-task revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskControls_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskControls_Lineage;
            DROP TABLE IF EXISTS ProjectCivilDirectTaskRevisions;
            DROP TABLE IF EXISTS ProjectCivilDirectTaskControls;
            """);
    }
}
