using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>Civil completion evidence/directions only; Maintenance and Finance remain authoritative.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821063000_AddCivilEngineeringMaintenanceCompletionControls")]
public partial class AddCivilEngineeringMaintenanceCompletionControls : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE CivilEngineeringMaintenanceCompletionControls (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ExecutionLinkId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL,
                MaintenanceAssetId uniqueidentifier NOT NULL, JobCardId uniqueidentifier NULL, WorkOrderId uniqueidentifier NULL,
                CivilEngineerUserId uniqueidentifier NOT NULL, SupervisingCivilEngineerUserId uniqueidentifier NOT NULL, HodUserId uniqueidentifier NOT NULL,
                Stage varchar(40) NOT NULL, Status varchar(30) NOT NULL, CompletionSummary nvarchar(4000) NOT NULL,
                CompletionDocumentRecordId uniqueidentifier NOT NULL, CompletionDocumentVersionId uniqueidentifier NOT NULL, CompletionReportedAt datetime2 NOT NULL,
                SceReviewedById uniqueidentifier NULL, SceReviewedAt datetime2 NULL, HodReviewedById uniqueidentifier NULL, HodReviewedAt datetime2 NULL,
                InspectionStatus varchar(30) NOT NULL, InspectionDirectionDocumentRecordId uniqueidentifier NULL, InspectionDirectionDocumentVersionId uniqueidentifier NULL,
                InspectionOutcomeDocumentRecordId uniqueidentifier NULL, InspectionOutcomeDocumentVersionId uniqueidentifier NULL, InspectionOutcomeNote nvarchar(2000) NULL,
                InspectionRecordedById uniqueidentifier NULL, InspectionRecordedAt datetime2 NULL,
                PaymentDirectionStatus varchar(30) NOT NULL, PaymentDirectionDocumentRecordId uniqueidentifier NULL, PaymentDirectionDocumentVersionId uniqueidentifier NULL,
                PaymentDirectionNote nvarchar(2000) NULL, PaymentDirectedById uniqueidentifier NULL, PaymentDirectedAt datetime2 NULL,
                ClosureDocumentRecordId uniqueidentifier NULL, ClosureDocumentVersionId uniqueidentifier NULL, ClosedById uniqueidentifier NULL, ClosedAt datetime2 NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL,
                LastMutationRequestHash varchar(64) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL,
                TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_ExecutionLink FOREIGN KEY (ExecutionLinkId) REFERENCES CivilEngineeringMaintenanceExecutionLinks(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_Asset FOREIGN KEY (MaintenanceAssetId) REFERENCES MaintenanceAssets(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_JobCard FOREIGN KEY (JobCardId) REFERENCES JobCard(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_WorkOrder FOREIGN KEY (WorkOrderId) REFERENCES WorkOrders(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_CivilEngineer FOREIGN KEY (CivilEngineerUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_Sce FOREIGN KEY (SupervisingCivilEngineerUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_Hod FOREIGN KEY (HodUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionControls_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_CivilEngineeringMaintenanceCompletionControls_Stage CHECK (Stage IN ('SceReview','HodReview','AwaitingInspectionDirection','InspectionInProgress','AwaitingPaymentDirection','AwaitingClosure','RemediationRequired','Closed','Returned') AND Status IN ('Pending','Active','Returned','Blocked','Closed')),
                CONSTRAINT CK_CivilEngineeringMaintenanceCompletionControls_Inspection CHECK (InspectionStatus IN ('NotDirected','Directed','Passed','Failed')),
                CONSTRAINT CK_CivilEngineeringMaintenanceCompletionControls_Payment CHECK (PaymentDirectionStatus IN ('NotDirected','Directed')),
                CONSTRAINT CK_CivilEngineeringMaintenanceCompletionControls_Evidence CHECK (CompletionDocumentRecordId IS NOT NULL AND CompletionDocumentVersionId IS NOT NULL AND (InspectionDirectionDocumentRecordId IS NULL AND InspectionDirectionDocumentVersionId IS NULL OR InspectionDirectionDocumentRecordId IS NOT NULL AND InspectionDirectionDocumentVersionId IS NOT NULL) AND (InspectionOutcomeDocumentRecordId IS NULL AND InspectionOutcomeDocumentVersionId IS NULL OR InspectionOutcomeDocumentRecordId IS NOT NULL AND InspectionOutcomeDocumentVersionId IS NOT NULL) AND (PaymentDirectionDocumentRecordId IS NULL AND PaymentDirectionDocumentVersionId IS NULL OR PaymentDirectionDocumentRecordId IS NOT NULL AND PaymentDirectionDocumentVersionId IS NOT NULL) AND (ClosureDocumentRecordId IS NULL AND ClosureDocumentVersionId IS NULL OR ClosureDocumentRecordId IS NOT NULL AND ClosureDocumentVersionId IS NOT NULL))
            );
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceCompletionControls_Tenant_ExecutionLink ON CivilEngineeringMaintenanceCompletionControls(TenantId,ExecutionLinkId) WHERE IsDeleted=0;
            CREATE UNIQUE INDEX UX_CivilEngineeringMaintenanceCompletionControls_Tenant_Request ON CivilEngineeringMaintenanceCompletionControls(TenantId,ClientRequestId);
            CREATE INDEX IX_CivilEngineeringMaintenanceCompletionControls_Project_Stage ON CivilEngineeringMaintenanceCompletionControls(TenantId,ProjectId,Stage);

            CREATE TABLE CivilEngineeringMaintenanceCompletionRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, CompletionControlId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
                FromStage nvarchar(40) NULL, ToStage nvarchar(40) NOT NULL, ActorUserId uniqueidentifier NOT NULL, ActorName nvarchar(300) NOT NULL,
                ActorRoles nvarchar(500) NULL, Reason nvarchar(2000) NULL, CorrelationId nvarchar(100) NOT NULL,
                BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionRevisions_Control FOREIGN KEY (CompletionControlId) REFERENCES CivilEngineeringMaintenanceCompletionControls(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceCompletionRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_CivilEngineeringMaintenanceCompletionRevisions_Control_Created ON CivilEngineeringMaintenanceCompletionRevisions(TenantId,CompletionControlId,CreatedAt);
            CREATE INDEX IX_CivilEngineeringMaintenanceCompletionRevisions_Correlation ON CivilEngineeringMaintenanceCompletionRevisions(TenantId,CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCompletionControls_Lineage ON CivilEngineeringMaintenanceCompletionControls AFTER INSERT, UPDATE AS BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN CivilEngineeringMaintenanceExecutionLinks link ON link.Id=value.ExecutionLinkId AND link.TenantId=value.TenantId AND link.IsDeleted=0 LEFT JOIN CivilEngineeringMaintenanceCostingHandoffs handoff ON handoff.Id=link.HandoffId AND handoff.TenantId=value.TenantId AND handoff.IsDeleted=0 LEFT JOIN CivilEngineeringMaintenanceAssessments assessment ON assessment.Id=handoff.AssessmentId AND assessment.TenantId=value.TenantId AND assessment.IsDeleted=0 WHERE link.Id IS NULL OR handoff.Id IS NULL OR assessment.Id IS NULL OR link.ProjectId<>value.ProjectId OR link.MaintenanceAssetId<>value.MaintenanceAssetId OR handoff.Stage<>'Awarded' OR handoff.Status<>'Awarded' OR handoff.ApprovalStatus<>'Approved' OR assessment.CivilEngineerUserId<>value.CivilEngineerUserId OR assessment.SupervisingCivilEngineerUserId<>value.SupervisingCivilEngineerUserId OR assessment.HodUserId<>value.HodUserId)
                THROW 52240, 'Civil completion control lineage must match its awarded execution link and frozen assessment assignments.', 1;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN JobCard jobCard ON jobCard.Id=value.JobCardId AND jobCard.TenantId=value.TenantId AND jobCard.IsDeleted=0 LEFT JOIN WorkOrders workOrder ON workOrder.Id=value.WorkOrderId AND workOrder.TenantId=value.TenantId AND workOrder.IsDeleted=0 WHERE (value.JobCardId IS NOT NULL AND (jobCard.Id IS NULL OR jobCard.AssetId<>value.MaintenanceAssetId)) OR (value.WorkOrderId IS NOT NULL AND (workOrder.Id IS NULL OR workOrder.AssetId<>value.MaintenanceAssetId)))
                THROW 52241, 'Civil completion control owner records must match the frozen Maintenance asset.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.ExecutionLinkId<>prior.ExecutionLinkId OR value.ProjectId<>prior.ProjectId OR value.MaintenanceAssetId<>prior.MaintenanceAssetId OR ISNULL(CONVERT(varchar(36),value.JobCardId),'')<>ISNULL(CONVERT(varchar(36),prior.JobCardId),'') OR ISNULL(CONVERT(varchar(36),value.WorkOrderId),'')<>ISNULL(CONVERT(varchar(36),prior.WorkOrderId),'') OR value.CivilEngineerUserId<>prior.CivilEngineerUserId OR value.SupervisingCivilEngineerUserId<>prior.SupervisingCivilEngineerUserId OR value.HodUserId<>prior.HodUserId)
                THROW 52242, 'Civil completion-control source, owner and assignment lineage is immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE (value.CompletionDocumentRecordId<>prior.CompletionDocumentRecordId OR value.CompletionDocumentVersionId<>prior.CompletionDocumentVersionId) AND NOT (prior.Stage='Returned' AND value.Stage='SceReview'))
                THROW 52243, 'Completion-report evidence can change only during a controlled returned-report resubmission.', 1;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCompletionControls_Lifecycle ON CivilEngineeringMaintenanceCompletionControls AFTER UPDATE AS BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.Stage<>prior.Stage AND NOT ((prior.Stage='SceReview' AND value.Stage IN ('HodReview','Returned')) OR (prior.Stage='Returned' AND value.Stage='SceReview') OR (prior.Stage='HodReview' AND value.Stage='AwaitingInspectionDirection') OR (prior.Stage='AwaitingInspectionDirection' AND value.Stage='InspectionInProgress') OR (prior.Stage='InspectionInProgress' AND value.Stage IN ('AwaitingPaymentDirection','RemediationRequired')) OR (prior.Stage='AwaitingPaymentDirection' AND value.Stage='AwaitingClosure') OR (prior.Stage='AwaitingClosure' AND value.Stage='Closed')))
                THROW 52244, 'Invalid Civil completion-control lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE (value.InspectionStatus='Passed' AND value.Stage<>'AwaitingPaymentDirection') OR (value.InspectionStatus='Failed' AND value.Stage<>'RemediationRequired') OR (value.PaymentDirectionStatus='Directed' AND value.Stage NOT IN ('AwaitingClosure','Closed')) OR (value.Stage='Closed' AND (value.InspectionStatus<>'Passed' OR value.PaymentDirectionStatus<>'Directed' OR value.ClosedAt IS NULL)))
                THROW 52245, 'Civil completion-control directions and closure state are inconsistent.', 1;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCompletionRevisions_Lineage ON CivilEngineeringMaintenanceCompletionRevisions AFTER INSERT AS BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN CivilEngineeringMaintenanceCompletionControls control ON control.Id=value.CompletionControlId AND control.TenantId=value.TenantId AND control.IsDeleted=0 WHERE control.Id IS NULL)
                THROW 52246, 'Civil completion history must belong to an active completion control in the same tenant.', 1;
            END
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceCompletionRevisions_AppendOnly ON CivilEngineeringMaintenanceCompletionRevisions AFTER UPDATE, DELETE AS BEGIN
              THROW 52247, 'Civil completion history is append-only.', 1;
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS CivilEngineeringMaintenanceCompletionRevisions; DROP TABLE IF EXISTS CivilEngineeringMaintenanceCompletionControls;");
    }
}
