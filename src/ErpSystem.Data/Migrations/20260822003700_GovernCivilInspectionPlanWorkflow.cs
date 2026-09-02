using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Routes Civil inspection plans through the existing PROJECT_QUALITY_TEST workflow
/// before scheduled execution. Central Workflow, Security, Projects, DMS and Notifications
/// remain the authoritative owners; this migration only freezes their governed references.
/// </summary>
public partial class GovernCivilInspectionPlanWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET NOCOUNT ON;
            ALTER TABLE dbo.ProjectCivilInspectionControls ADD
                WorkflowDefinitionId uniqueidentifier NULL,
                WorkflowInstanceId uniqueidentifier NULL,
                PlanApprovalStatus varchar(30) NOT NULL CONSTRAINT DF_ProjectCivilInspectionControls_PlanApprovalStatus DEFAULT('Approved'),
                PlanSubmittedById uniqueidentifier NULL,
                PlanSubmittedAt datetime2 NULL,
                PlanApprovedById uniqueidentifier NULL,
                PlanApprovedAt datetime2 NULL,
                PlanRejectionReason nvarchar(2000) NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE control
               SET WorkflowDefinitionId=TRY_CONVERT(uniqueidentifier,JSON_VALUE(decision.ValueJson,'$.workflowDefinitionId')),
                   PlanSubmittedById=control.CreatedById,
                   PlanSubmittedAt=control.CreatedAt
              FROM dbo.ProjectCivilInspectionControls control
              JOIN dbo.CivilEngineeringConfigurationDecisions decision
                ON decision.Id=control.QualityConfigurationDecisionId
               AND decision.TenantId=control.TenantId
               AND decision.ConfigurationKey='CIV-CFG-011';

            IF EXISTS (SELECT 1 FROM dbo.ProjectCivilInspectionControls WHERE WorkflowDefinitionId IS NULL)
                THROW 52162, 'Existing Civil inspections cannot be bound to their frozen CIV-CFG-011 workflow definition.', 1;

            ALTER TABLE dbo.ProjectCivilInspectionControls ALTER COLUMN WorkflowDefinitionId uniqueidentifier NOT NULL;
            ALTER TABLE dbo.ProjectCivilInspectionControls ADD
                CONSTRAINT FK_ProjectCivilInspectionControls_WorkflowDefinition FOREIGN KEY (WorkflowDefinitionId) REFERENCES dbo.WorkflowDefinitions(Id),
                CONSTRAINT FK_ProjectCivilInspectionControls_WorkflowInstance FOREIGN KEY (WorkflowInstanceId) REFERENCES dbo.WorkflowInstances(Id),
                CONSTRAINT FK_ProjectCivilInspectionControls_PlanSubmittedBy FOREIGN KEY (PlanSubmittedById) REFERENCES dbo.Users(Id),
                CONSTRAINT FK_ProjectCivilInspectionControls_PlanApprovedBy FOREIGN KEY (PlanApprovedById) REFERENCES dbo.Users(Id);

            CREATE UNIQUE INDEX IX_ProjectCivilInspectionControls_TenantId_WorkflowInstanceId
                ON dbo.ProjectCivilInspectionControls(TenantId,WorkflowInstanceId)
                WHERE WorkflowInstanceId IS NOT NULL AND IsDeleted=0;
            CREATE INDEX IX_ProjectCivilInspectionControls_TenantId_ProjectId_PlanApprovalStatus
                ON dbo.ProjectCivilInspectionControls(TenantId,ProjectId,PlanApprovalStatus,ScheduledAt);

            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT CK_ProjectCivilInspectionControls_Stage;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT CK_ProjectCivilInspectionControls_Status;
            ALTER TABLE dbo.ProjectCivilInspectionControls ADD
                CONSTRAINT CK_ProjectCivilInspectionControls_Stage CHECK (Stage IN ('PendingApproval','Scheduled','CorrectiveActionRequired','ReinspectionScheduled','Passed','Closed','Rejected')),
                CONSTRAINT CK_ProjectCivilInspectionControls_Status CHECK (Status IN ('PendingApproval','Scheduled','Active','Blocked','Passed','Closed','Rejected')),
                CONSTRAINT CK_ProjectCivilInspectionControls_PlanApproval CHECK (PlanApprovalStatus IN ('Pending','Approved','Rejected'));
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionControls_Workflow ON dbo.ProjectCivilInspectionControls AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1
                  FROM inserted value
                  LEFT JOIN dbo.WorkflowDefinitions definition ON definition.Id=value.WorkflowDefinitionId AND definition.TenantId=value.TenantId AND definition.IsDeleted=0
                  LEFT JOIN dbo.WorkflowEntityTypes entityType ON entityType.Id=definition.EntityTypeId AND entityType.TenantId=value.TenantId AND entityType.IsDeleted=0 AND entityType.IsActive=1 AND entityType.Code='PROJECT_QUALITY_TEST'
                  LEFT JOIN dbo.WorkflowInstances instance ON instance.Id=value.WorkflowInstanceId AND instance.TenantId=value.TenantId AND instance.WorkflowDefinitionId=value.WorkflowDefinitionId AND instance.EntityId=value.Id AND instance.EntityTypeId=entityType.Id AND instance.IsDeleted=0
                 WHERE definition.Id IS NULL OR entityType.Id IS NULL
                    OR (value.WorkflowInstanceId IS NOT NULL AND instance.Id IS NULL)
                    OR (value.PlanApprovalStatus='Pending' AND (value.Stage<>'PendingApproval' OR value.Status<>'PendingApproval' OR value.WorkflowInstanceId IS NULL OR value.PlanSubmittedById IS NULL OR value.PlanSubmittedAt IS NULL OR value.PlanApprovedById IS NOT NULL OR value.PlanApprovedAt IS NOT NULL OR value.PlanRejectionReason IS NOT NULL))
                    OR (
                      value.PlanApprovalStatus='Approved'
                      AND (
                        (value.WorkflowInstanceId IS NULL AND (value.Stage IN ('PendingApproval','Rejected') OR value.PlanApprovedById IS NOT NULL OR value.PlanApprovedAt IS NOT NULL OR value.PlanRejectionReason IS NOT NULL))
                        OR
                        (value.WorkflowInstanceId IS NOT NULL AND (value.Stage IN ('PendingApproval','Rejected') OR value.PlanApprovedById IS NULL OR value.PlanApprovedAt IS NULL OR value.PlanRejectionReason IS NOT NULL))
                      )
                    )
                    OR (value.PlanApprovalStatus='Rejected' AND (value.Stage<>'Rejected' OR value.Status<>'Rejected' OR value.WorkflowInstanceId IS NULL OR value.PlanApprovedById IS NOT NULL OR value.PlanApprovedAt IS NOT NULL OR LEN(LTRIM(RTRIM(ISNULL(value.PlanRejectionReason,''))))<3))
              ) THROW 52162, 'Civil inspection plan workflow or approval lineage is invalid.', 1;

              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                 WHERE value.WorkflowDefinitionId<>prior.WorkflowDefinitionId
                    OR ISNULL(value.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')
                    OR ISNULL(value.PlanSubmittedById,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.PlanSubmittedById,'00000000-0000-0000-0000-000000000000')
                    OR ISNULL(value.PlanSubmittedAt,'19000101')<>ISNULL(prior.PlanSubmittedAt,'19000101')
                    OR (prior.PlanApprovalStatus<>'Pending' AND (value.PlanApprovalStatus<>prior.PlanApprovalStatus OR ISNULL(value.PlanApprovedById,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.PlanApprovedById,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.PlanApprovedAt,'19000101')<>ISNULL(prior.PlanApprovedAt,'19000101') OR ISNULL(value.PlanRejectionReason,'')<>ISNULL(prior.PlanRejectionReason,'')))
              ) THROW 52162, 'Civil inspection plan workflow binding and completed decision are immutable.', 1;

              IF EXISTS (
                SELECT 1 FROM inserted value
                JOIN deleted prior ON prior.Id=value.Id
                JOIN dbo.CivilEngineeringConfigurationDecisions decision ON decision.Id=value.QualityConfigurationDecisionId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-011'
                WHERE prior.PlanApprovalStatus='Pending' AND value.PlanApprovalStatus IN ('Approved','Rejected')
                  AND (
                    value.LastModifiedById IS NULL
                    OR value.LastModifiedById=value.CreatedById
                    OR value.LastModifiedById=value.InspectorUserId
                    OR NOT EXISTS (
                      SELECT 1 FROM dbo.ProjectMembers member
                      JOIN dbo.UserRoles roleAssignment ON roleAssignment.UserId=member.UserId
                      WHERE member.TenantId=value.TenantId AND member.ProjectId=value.ProjectId AND member.UserId=value.LastModifiedById AND member.IsActive=1 AND member.IsDeleted=0
                        AND EXISTS (
                          SELECT 1 FROM OPENJSON(decision.ValueJson,'$.reviewerRoleIds') configuredRole
                          WHERE TRY_CONVERT(uniqueidentifier,configuredRole.[value])=roleAssignment.RoleId
                        )
                    )
                  )
              ) THROW 52162, 'Civil inspection plan decisions require an independent configured project reviewer.', 1;
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
                  (prior.Stage='PendingApproval' AND NOT ((value.Stage='Scheduled' AND value.Status='Scheduled' AND value.PlanApprovalStatus='Approved') OR (value.Stage='Rejected' AND value.Status='Rejected' AND value.PlanApprovalStatus='Rejected'))) OR
                  (prior.Stage='Scheduled' AND NOT ((value.Stage='Passed' AND value.Status='Passed') OR (value.Stage='CorrectiveActionRequired' AND value.Status='Blocked'))) OR
                  (prior.Stage='CorrectiveActionRequired' AND NOT (value.Stage='ReinspectionScheduled' AND value.Status='Active')) OR
                  (prior.Stage='ReinspectionScheduled' AND NOT ((value.Stage='Passed' AND value.Status='Passed') OR (value.Stage='CorrectiveActionRequired' AND value.Status='Blocked'))) OR
                  (prior.Stage='Passed' AND NOT (value.Stage='Closed' AND value.Status='Closed')) OR
                  (prior.Stage IN ('Closed','Rejected') AND value.Stage<>prior.Stage))
                THROW 52081, 'Invalid Civil inspection lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE
                 ((prior.Stage='PendingApproval' AND value.Stage='Scheduled') AND (value.PlanApprovedById IS NULL OR value.PlanApprovedById<>value.LastModifiedById OR value.PlanApprovedAt IS NULL)) OR
                 ((prior.Stage='PendingApproval' AND value.Stage='Rejected') AND (value.PlanApprovedById IS NOT NULL OR value.PlanApprovedAt IS NOT NULL OR LEN(LTRIM(RTRIM(ISNULL(value.PlanRejectionReason,''))))<3)) OR
                 ((prior.Stage='Scheduled' AND value.Stage IN ('Passed','CorrectiveActionRequired')) AND (value.LastModifiedById<>value.InspectorUserId OR value.InspectedAt IS NULL OR value.InspectionDocumentRecordId IS NULL OR value.InspectionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.Findings,''))))<3)) OR
                 ((prior.Stage='CorrectiveActionRequired' AND value.Stage='ReinspectionScheduled') AND (value.LastModifiedById<>value.InspectorUserId OR value.ReinspectionInspectorUserId IS NULL OR value.ReinspectionInspectorUserId=value.InspectorUserId OR value.CorrectiveActionRecordedAt IS NULL OR value.CorrectiveActionDocumentRecordId IS NULL OR value.CorrectiveActionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.CorrectiveAction,''))))<3)) OR
                 ((prior.Stage='ReinspectionScheduled' AND value.Stage IN ('Passed','CorrectiveActionRequired')) AND (value.LastModifiedById<>value.ReinspectionInspectorUserId OR value.ReinspectedAt IS NULL OR value.ReinspectionDocumentRecordId IS NULL OR value.ReinspectionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.Findings,''))))<3)) OR
                 ((prior.Stage='Passed' AND value.Stage='Closed') AND (value.ClosedById IS NULL OR value.ClosedById<>value.LastModifiedById OR value.ClosedById IN (value.CreatedById,value.InspectorUserId,ISNULL(value.ReinspectionInspectorUserId,'00000000-0000-0000-0000-000000000000')) OR value.ClosedAt IS NULL OR value.ClosureDocumentRecordId IS NULL OR value.ClosureDocumentVersionId IS NULL)))
                THROW 52082, 'Civil inspection actor, workflow approval, independent reinspection, evidence, or closure lineage is invalid.', 1;
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
                WHERE control.Id IS NULL OR actor.Id IS NULL OR LEN(value.RequestHash)<>64 OR value.Action NOT IN ('CreateCivilInspection','ApproveCivilInspectionPlan','RejectCivilInspectionPlan','ApproveCivilInspection','RejectCivilInspection','SubmitCivilDefectReinspection','ApproveCivilDefectClosure','RejectCivilDefectClosure','CloseCivilInspection')
              ) THROW 52083, 'Civil inspection revision lineage is invalid.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM dbo.ProjectCivilInspectionControls WHERE WorkflowInstanceId IS NOT NULL OR PlanApprovalStatus<>'Approved' OR Stage IN ('PendingApproval','Rejected') OR PlanApprovedById IS NOT NULL OR PlanApprovedAt IS NOT NULL OR PlanRejectionReason IS NOT NULL)
                THROW 52163, 'Rollback is blocked because governed Civil inspection-plan workflow history exists.', 1;

            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilInspectionControls_Workflow;
            DROP INDEX IF EXISTS IX_ProjectCivilInspectionControls_TenantId_WorkflowInstanceId ON dbo.ProjectCivilInspectionControls;
            DROP INDEX IF EXISTS IX_ProjectCivilInspectionControls_TenantId_ProjectId_PlanApprovalStatus ON dbo.ProjectCivilInspectionControls;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT CK_ProjectCivilInspectionControls_PlanApproval;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT CK_ProjectCivilInspectionControls_Stage;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT CK_ProjectCivilInspectionControls_Status;
            ALTER TABLE dbo.ProjectCivilInspectionControls ADD
                CONSTRAINT CK_ProjectCivilInspectionControls_Stage CHECK (Stage IN ('Scheduled','CorrectiveActionRequired','ReinspectionScheduled','Passed','Closed')),
                CONSTRAINT CK_ProjectCivilInspectionControls_Status CHECK (Status IN ('Scheduled','Active','Blocked','Passed','Closed'));
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT FK_ProjectCivilInspectionControls_WorkflowDefinition;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT FK_ProjectCivilInspectionControls_WorkflowInstance;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT FK_ProjectCivilInspectionControls_PlanSubmittedBy;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT FK_ProjectCivilInspectionControls_PlanApprovedBy;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP CONSTRAINT DF_ProjectCivilInspectionControls_PlanApprovalStatus;
            ALTER TABLE dbo.ProjectCivilInspectionControls DROP COLUMN WorkflowDefinitionId,WorkflowInstanceId,PlanApprovalStatus,PlanSubmittedById,PlanSubmittedAt,PlanApprovedById,PlanApprovedAt,PlanRejectionReason;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionControls_Lifecycle ON dbo.ProjectCivilInspectionControls AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id)) THROW 52079, 'Civil inspection controls cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.QualityCheckpointId<>prior.QualityCheckpointId OR value.PlanningGisValidationId<>prior.PlanningGisValidationId OR value.InspectorUserId<>prior.InspectorUserId OR value.Purpose<>prior.Purpose OR value.ScheduledAt<>prior.ScheduledAt OR value.SpatialReferenceSnapshot<>prior.SpatialReferenceSnapshot OR ISNULL(value.BoundaryCoordinatesSnapshot,'')<>ISNULL(prior.BoundaryCoordinatesSnapshot,'') OR value.PlanDocumentRecordId<>prior.PlanDocumentRecordId OR value.PlanDocumentVersionId<>prior.PlanDocumentVersionId OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.SupervisionConfigurationDecisionId<>prior.SupervisionConfigurationDecisionId OR value.QualityConfigurationDecisionId<>prior.QualityConfigurationDecisionId OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted) THROW 52080, 'Civil inspection identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE (prior.Stage='Scheduled' AND NOT ((value.Stage='Passed' AND value.Status='Passed') OR (value.Stage='CorrectiveActionRequired' AND value.Status='Blocked'))) OR (prior.Stage='CorrectiveActionRequired' AND NOT (value.Stage='ReinspectionScheduled' AND value.Status='Active')) OR (prior.Stage='ReinspectionScheduled' AND NOT ((value.Stage='Passed' AND value.Status='Passed') OR (value.Stage='CorrectiveActionRequired' AND value.Status='Blocked'))) OR (prior.Stage='Passed' AND NOT (value.Stage='Closed' AND value.Status='Closed')) OR (prior.Stage='Closed' AND value.Stage<>prior.Stage)) THROW 52081, 'Invalid Civil inspection lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE ((prior.Stage='Scheduled' AND value.Stage IN ('Passed','CorrectiveActionRequired')) AND (value.LastModifiedById<>value.InspectorUserId OR value.InspectedAt IS NULL OR value.InspectionDocumentRecordId IS NULL OR value.InspectionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.Findings,''))))<3)) OR ((prior.Stage='CorrectiveActionRequired' AND value.Stage='ReinspectionScheduled') AND (value.LastModifiedById<>value.InspectorUserId OR value.ReinspectionInspectorUserId IS NULL OR value.ReinspectionInspectorUserId=value.InspectorUserId OR value.CorrectiveActionRecordedAt IS NULL OR value.CorrectiveActionDocumentRecordId IS NULL OR value.CorrectiveActionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.CorrectiveAction,''))))<3)) OR ((prior.Stage='ReinspectionScheduled' AND value.Stage IN ('Passed','CorrectiveActionRequired')) AND (value.LastModifiedById<>value.ReinspectionInspectorUserId OR value.ReinspectedAt IS NULL OR value.ReinspectionDocumentRecordId IS NULL OR value.ReinspectionDocumentVersionId IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.Findings,''))))<3)) OR ((prior.Stage='Passed' AND value.Stage='Closed') AND (value.ClosedById IS NULL OR value.ClosedById<>value.LastModifiedById OR value.ClosedById IN (value.CreatedById,value.InspectorUserId,ISNULL(value.ReinspectionInspectorUserId,'00000000-0000-0000-0000-000000000000')) OR value.ClosedAt IS NULL OR value.ClosureDocumentRecordId IS NULL OR value.ClosureDocumentVersionId IS NULL))) THROW 52082, 'Civil inspection actor, independent reinspection, evidence, or closure lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilInspectionRevisions_Lineage ON dbo.ProjectCivilInspectionRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN dbo.ProjectCivilInspectionControls control ON control.Id=value.InspectionControlId AND control.TenantId=value.TenantId AND control.IsDeleted=0 LEFT JOIN dbo.Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE control.Id IS NULL OR actor.Id IS NULL OR LEN(value.RequestHash)<>64 OR value.Action NOT IN ('CreateCivilInspection','ApproveCivilInspection','RejectCivilInspection','SubmitCivilDefectReinspection','ApproveCivilDefectClosure','RejectCivilDefectClosure','CloseCivilInspection')) THROW 52083, 'Civil inspection revision lineage is invalid.', 1;
            END;
            """);
    }
}
