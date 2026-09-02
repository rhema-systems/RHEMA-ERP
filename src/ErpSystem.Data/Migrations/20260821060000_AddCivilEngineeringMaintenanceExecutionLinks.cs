using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Civil lineage only. Maintenance remains authoritative for JobCards and WorkOrders.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821060000_AddCivilEngineeringMaintenanceExecutionLinks")]
public partial class AddCivilEngineeringMaintenanceExecutionLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE CivilEngineeringMaintenanceExecutionLinks (
                Id uniqueidentifier NOT NULL PRIMARY KEY, HandoffId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL,
                MaintenanceAssetId uniqueidentifier NOT NULL, JobCardId uniqueidentifier NULL, WorkOrderId uniqueidentifier NULL,
                LinkMode varchar(30) NOT NULL, Stage varchar(40) NOT NULL, Status varchar(30) NOT NULL,
                LastOwnerStatusSummary nvarchar(1000) NULL, LastRevalidatedAt datetime2 NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
                LastMutationClientRequestId uniqueidentifier NULL, LastMutationRequestHash varchar(64) NULL,
                CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL,
                DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinks_Handoff FOREIGN KEY (HandoffId) REFERENCES CivilEngineeringMaintenanceCostingHandoffs(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinks_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinks_Asset FOREIGN KEY (MaintenanceAssetId) REFERENCES MaintenanceAssets(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinks_JobCard FOREIGN KEY (JobCardId) REFERENCES JobCard(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinks_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinks_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_CivilEngineeringMaintenanceExecutionLinks_Mode CHECK (LinkMode IN ('CreateJobCard','LinkExisting')),
                CONSTRAINT CK_CivilEngineeringMaintenanceExecutionLinks_Target CHECK (JobCardId IS NOT NULL OR WorkOrderId IS NOT NULL),
                CONSTRAINT CK_CivilEngineeringMaintenanceExecutionLinks_Stage CHECK ((Stage='AwaitingJobCardApproval' AND Status='Pending') OR (Stage IN ('AwaitingWorkOrder','WorkInProgress') AND Status='Active') OR (Stage='Completed' AND Status='Completed') OR (Stage='Blocked' AND Status='Blocked'))
            );
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceExecutionLinks_Tenant_Handoff ON CivilEngineeringMaintenanceExecutionLinks(TenantId, HandoffId);
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceExecutionLinks_Tenant_ClientRequest ON CivilEngineeringMaintenanceExecutionLinks(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceExecutionLinks_Tenant_JobCard ON CivilEngineeringMaintenanceExecutionLinks(TenantId, JobCardId) WHERE JobCardId IS NOT NULL AND IsDeleted=0;
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceExecutionLinks_Tenant_WorkOrder ON CivilEngineeringMaintenanceExecutionLinks(TenantId, WorkOrderId) WHERE WorkOrderId IS NOT NULL AND IsDeleted=0;
            CREATE INDEX IX_CivilEngineeringMaintenanceExecutionLinks_Tenant_Project_Stage ON CivilEngineeringMaintenanceExecutionLinks(TenantId, ProjectId, Stage);
            """);
        migrationBuilder.Sql("""
            CREATE TABLE CivilEngineeringMaintenanceExecutionLinkRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ExecutionLinkId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
                FromStage varchar(40) NULL, ToStage varchar(40) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
                ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, Reason nvarchar(2000) NULL,
                CorrelationId nvarchar(100) NOT NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL,
                DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinkRevisions_Link FOREIGN KEY (ExecutionLinkId) REFERENCES CivilEngineeringMaintenanceExecutionLinks(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinkRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceExecutionLinkRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_CivilEngineeringMaintenanceExecutionLinkRevisions_Tenant_Link_CreatedAt ON CivilEngineeringMaintenanceExecutionLinkRevisions(TenantId, ExecutionLinkId, CreatedAt);
            CREATE INDEX IX_CivilEngineeringMaintenanceExecutionLinkRevisions_Tenant_Correlation ON CivilEngineeringMaintenanceExecutionLinkRevisions(TenantId, CorrelationId);
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceExecutionLinks_Lineage ON CivilEngineeringMaintenanceExecutionLinks AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN CivilEngineeringMaintenanceCostingHandoffs handoff ON handoff.Id=value.HandoffId AND handoff.TenantId=value.TenantId AND handoff.IsDeleted=0
                LEFT JOIN CivilEngineeringMaintenanceAssessments assessment ON assessment.Id=handoff.AssessmentId AND assessment.TenantId=value.TenantId AND assessment.IsDeleted=0
                LEFT JOIN CivilEngineeringMaintenanceIntakes intake ON intake.Id=assessment.IntakeId AND intake.TenantId=value.TenantId AND intake.IsDeleted=0
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN MaintenanceAssets asset ON asset.Id=value.MaintenanceAssetId AND asset.TenantId=value.TenantId AND asset.IsDeleted=0
                LEFT JOIN JobCard jobCard ON jobCard.Id=value.JobCardId AND jobCard.TenantId=value.TenantId AND jobCard.IsDeleted=0
                LEFT JOIN WorkOrders workOrder ON workOrder.Id=value.WorkOrderId AND workOrder.TenantId=value.TenantId AND workOrder.IsDeleted=0
                WHERE handoff.Id IS NULL OR assessment.Id IS NULL OR intake.Id IS NULL OR project.Id IS NULL OR asset.Id IS NULL
                  OR handoff.ProjectId<>value.ProjectId OR intake.MaintenanceAssetId<>value.MaintenanceAssetId
                  OR handoff.Stage<>'Awarded' OR handoff.Status<>'Awarded' OR handoff.ApprovalStatus<>'Approved'
                  OR (value.JobCardId IS NOT NULL AND (jobCard.Id IS NULL OR jobCard.AssetId<>value.MaintenanceAssetId))
                  OR (value.WorkOrderId IS NOT NULL AND (workOrder.Id IS NULL OR workOrder.AssetId<>value.MaintenanceAssetId))
                  OR (value.JobCardId IS NOT NULL AND value.WorkOrderId IS NOT NULL AND workOrder.JobCardId IS NOT NULL AND workOrder.JobCardId<>value.JobCardId)
                  OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND value.LinkMode='CreateJobCard' AND value.JobCardId IS NULL)
              ) THROW 52230, 'Civil Maintenance execution tenant, awarded scope, project/asset, or linked owner lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceExecutionLinks_Lifecycle ON CivilEngineeringMaintenanceExecutionLinks AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52231, 'Civil Maintenance execution links cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                         WHERE value.TenantId<>prior.TenantId OR value.HandoffId<>prior.HandoffId OR value.ProjectId<>prior.ProjectId OR value.MaintenanceAssetId<>prior.MaintenanceAssetId OR ISNULL(CONVERT(varchar(36),value.JobCardId),'')<>ISNULL(CONVERT(varchar(36),prior.JobCardId),'') OR value.LinkMode<>prior.LinkMode OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted OR (prior.WorkOrderId IS NOT NULL AND ISNULL(CONVERT(varchar(36),value.WorkOrderId),'')<>CONVERT(varchar(36),prior.WorkOrderId)))
                THROW 52232, 'Civil Maintenance execution identity and frozen link lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                         WHERE NOT ((prior.Stage='AwaitingJobCardApproval' AND value.Stage IN ('AwaitingWorkOrder','WorkInProgress','Blocked')) OR (prior.Stage='AwaitingWorkOrder' AND value.Stage IN ('WorkInProgress','Completed','Blocked')) OR (prior.Stage='WorkInProgress' AND value.Stage IN ('Completed','Blocked')) OR (prior.Stage='Blocked' AND value.Stage IN ('AwaitingJobCardApproval','AwaitingWorkOrder','WorkInProgress','Completed')) OR value.Stage=prior.Stage))
                THROW 52233, 'Invalid Civil Maintenance execution-link lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                         WHERE value.Stage=prior.Stage AND value.Status<>prior.Status)
                THROW 52234, 'Civil Maintenance execution status may change only through an allowed owner-state transition.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceExecutionLinkRevisions_Lineage ON CivilEngineeringMaintenanceExecutionLinkRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN CivilEngineeringMaintenanceExecutionLinks linkValue ON linkValue.Id=value.ExecutionLinkId AND linkValue.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE linkValue.Id IS NULL OR actor.Id IS NULL) THROW 52235, 'Civil Maintenance execution-link revision lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceExecutionLinkRevisions_AppendOnly ON CivilEngineeringMaintenanceExecutionLinkRevisions AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON; THROW 52236, 'Civil Maintenance execution-link revisions are append-only.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceExecutionLinkRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceExecutionLinkRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceExecutionLinks_Lifecycle;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceExecutionLinks_Lineage;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceExecutionLinkRevisions;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceExecutionLinks;
            """);
    }
}
