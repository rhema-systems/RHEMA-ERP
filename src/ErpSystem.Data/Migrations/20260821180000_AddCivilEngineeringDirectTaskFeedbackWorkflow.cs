using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0502 feedback, completion and acceptance lifecycle for the governed CIV-0501 task overlay.</summary>
public partial class AddCivilEngineeringDirectTaskFeedbackWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE ProjectCivilDirectTaskControls ADD
              ProgressPercent decimal(5,2) NOT NULL CONSTRAINT DF_ProjectCivilDirectTaskControls_ProgressPercent DEFAULT 0,
              AcknowledgedById uniqueidentifier NULL,
              AcknowledgedAt datetime2 NULL,
              CompletedById uniqueidentifier NULL,
              CompletedAt datetime2 NULL,
              AcceptedById uniqueidentifier NULL,
              AcceptedAt datetime2 NULL,
              LastFeedbackClientRequestId uniqueidentifier NULL,
              LastFeedbackRequestHash varchar(64) NULL,
              CONSTRAINT CK_ProjectCivilDirectTaskControls_Progress CHECK (ProgressPercent >= 0 AND ProgressPercent <= 100);
            """);

        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilDirectTaskFeedbackEntries (
              Id uniqueidentifier NOT NULL PRIMARY KEY,
              DirectTaskControlId uniqueidentifier NOT NULL,
              ClientRequestId uniqueidentifier NOT NULL,
              RequestHash varchar(64) NOT NULL,
              Sequence int NOT NULL,
              Action int NOT NULL,
              ProgressPercent decimal(5,2) NULL,
              Message nvarchar(2000) NULL,
              ActorUserId uniqueidentifier NOT NULL,
              CentralDocumentRecordId uniqueidentifier NULL,
              CentralDocumentVersionId uniqueidentifier NULL,
              WorkflowOutcome varchar(40) NULL,
              CorrelationId nvarchar(100) NOT NULL,
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
              CONSTRAINT FK_ProjectCivilDirectTaskFeedbackEntries_Task FOREIGN KEY (DirectTaskControlId) REFERENCES ProjectCivilDirectTaskControls(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskFeedbackEntries_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskFeedbackEntries_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskFeedbackEntries_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilDirectTaskFeedbackEntries_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilDirectTaskFeedbackEntries_Action CHECK (Action IN (0,1,2,3,4)),
              CONSTRAINT CK_ProjectCivilDirectTaskFeedbackEntries_Progress CHECK (ProgressPercent IS NULL OR (ProgressPercent >= 0 AND ProgressPercent <= 100)),
              CONSTRAINT CK_ProjectCivilDirectTaskFeedbackEntries_Evidence CHECK ((CentralDocumentRecordId IS NULL AND CentralDocumentVersionId IS NULL) OR (CentralDocumentRecordId IS NOT NULL AND CentralDocumentVersionId IS NOT NULL))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDirectTaskFeedbackEntries_TenantId_TaskId_ClientRequestId ON ProjectCivilDirectTaskFeedbackEntries(TenantId,DirectTaskControlId,ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilDirectTaskFeedbackEntries_TenantId_TaskId_Sequence ON ProjectCivilDirectTaskFeedbackEntries(TenantId,DirectTaskControlId,Sequence);
            CREATE INDEX IX_ProjectCivilDirectTaskFeedbackEntries_TenantId_TaskId_CreatedAt ON ProjectCivilDirectTaskFeedbackEntries(TenantId,DirectTaskControlId,CreatedAt);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskFeedbackEntries_Lineage ON ProjectCivilDirectTaskFeedbackEntries AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN ProjectCivilDirectTaskControls task ON task.Id=value.DirectTaskControlId AND task.TenantId=value.TenantId AND task.IsDeleted=0
                LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.MetadataTemplateCode=task.FeedbackMetadataTemplateCodeSnapshot
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL AND record.CurrentVersion=version.VersionNumber
                WHERE task.Id IS NULL OR actor.Id IS NULL
                   OR value.Sequence < 1
                   OR (value.CentralDocumentRecordId IS NOT NULL AND (record.Id IS NULL OR version.Id IS NULL))
              ) THROW 52285, 'Civil direct-task feedback lineage or DMS evidence is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskFeedbackEntries_AppendOnly ON ProjectCivilDirectTaskFeedbackEntries AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52286, 'Civil direct-task feedback entries are append-only.', 1; END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDirectTaskControls_FeedbackLifecycle ON ProjectCivilDirectTaskControls AFTER UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE (value.Status<>prior.Status OR value.ApprovalStatus<>prior.ApprovalStatus OR value.ProgressPercent<>prior.ProgressPercent
                       OR ISNULL(value.AcknowledgedAt,'19000101')<>ISNULL(prior.AcknowledgedAt,'19000101')
                       OR ISNULL(value.CompletedAt,'19000101')<>ISNULL(prior.CompletedAt,'19000101')
                       OR ISNULL(value.AcceptedAt,'19000101')<>ISNULL(prior.AcceptedAt,'19000101')
                       OR ISNULL(value.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000'))
                  AND (value.LastFeedbackClientRequestId IS NULL OR value.LastFeedbackClientRequestId=prior.LastFeedbackClientRequestId
                       OR NOT EXISTS (SELECT 1 FROM ProjectCivilDirectTaskFeedbackEntries feedback WHERE feedback.TenantId=value.TenantId AND feedback.DirectTaskControlId=value.Id AND feedback.ClientRequestId=value.LastFeedbackClientRequestId AND feedback.IsDeleted=0))
              ) THROW 52287, 'Civil direct-task lifecycle changes require an append-only governed feedback event.', 1;

              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE (prior.Status='Assigned' AND value.Status NOT IN ('Assigned','InProgress','Cancelled'))
                   OR (prior.Status='InProgress' AND value.Status NOT IN ('InProgress','PendingAcceptance','Cancelled'))
                   OR (prior.Status='PendingAcceptance' AND value.Status NOT IN ('PendingAcceptance','Accepted','Returned','Cancelled'))
                   OR (prior.Status='Returned' AND value.Status NOT IN ('Returned','InProgress','PendingAcceptance','Cancelled'))
                   OR (prior.Status='Accepted' AND value.Status<>'Accepted')
                   OR (prior.Status='Cancelled' AND value.Status<>'Cancelled')
              ) THROW 52288, 'Invalid Civil direct-task lifecycle transition.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskControls_FeedbackLifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskFeedbackEntries_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDirectTaskFeedbackEntries_Lineage;
            DROP TABLE IF EXISTS ProjectCivilDirectTaskFeedbackEntries;
            ALTER TABLE ProjectCivilDirectTaskControls DROP CONSTRAINT IF EXISTS CK_ProjectCivilDirectTaskControls_Progress;
            ALTER TABLE ProjectCivilDirectTaskControls DROP CONSTRAINT IF EXISTS DF_ProjectCivilDirectTaskControls_ProgressPercent;
            ALTER TABLE ProjectCivilDirectTaskControls DROP COLUMN IF EXISTS ProgressPercent, AcknowledgedById, AcknowledgedAt, CompletedById, CompletedAt, AcceptedById, AcceptedAt, LastFeedbackClientRequestId, LastFeedbackRequestHash;
            """);
    }
}
