using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

public partial class AddCivilEngineeringMaintenanceAssessments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE CivilEngineeringMaintenanceAssessments (
                Id uniqueidentifier NOT NULL PRIMARY KEY, IntakeId uniqueidentifier NOT NULL,
                HodUserId uniqueidentifier NOT NULL, SupervisingCivilEngineerUserId uniqueidentifier NOT NULL, CivilEngineerUserId uniqueidentifier NULL,
                CurrentAssigneeUserId uniqueidentifier NULL, CurrentDueAt datetime2 NULL, DefectCategoryId uniqueidentifier NULL,
                SiteAssessment nvarchar(4000) NULL, ScopeRecommendation nvarchar(4000) NULL, RemedyRecommendation nvarchar(4000) NULL,
                EstimatedCost decimal(18,2) NULL, CentralDocumentRecordId uniqueidentifier NULL, CentralDocumentVersionId uniqueidentifier NULL,
                WorkflowInstanceId uniqueidentifier NULL, PolicyHash varchar(64) NOT NULL, Stage varchar(40) NOT NULL, Status varchar(30) NOT NULL,
                ApprovalStatus varchar(30) NOT NULL, ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL,
                LastMutationRequestHash varchar(64) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Intake FOREIGN KEY (IntakeId) REFERENCES CivilEngineeringMaintenanceIntakes(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Hod FOREIGN KEY (HodUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Sce FOREIGN KEY (SupervisingCivilEngineerUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_CivilEngineer FOREIGN KEY (CivilEngineerUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Assignee FOREIGN KEY (CurrentAssigneeUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Defect FOREIGN KEY (DefectCategoryId) REFERENCES ProjectCatalogEntries(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessments_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_CivilEngineeringMaintenanceAssessments_Stage CHECK (Stage IN ('SceAssignment','CivilEngineerAssessment','SceAssessmentReview','HodFinalReview','Approved','Rejected')),
                CONSTRAINT CK_CivilEngineeringMaintenanceAssessments_Status CHECK (Status IN ('InProgress','PendingApproval','Approved','Rejected') AND ApprovalStatus IN ('Draft','Pending','Approved','Rejected')),
                CONSTRAINT CK_CivilEngineeringMaintenanceAssessments_Assignment CHECK (HodUserId<>SupervisingCivilEngineerUserId AND (CivilEngineerUserId IS NULL OR (CivilEngineerUserId<>HodUserId AND CivilEngineerUserId<>SupervisingCivilEngineerUserId))),
                CONSTRAINT CK_CivilEngineeringMaintenanceAssessments_Evidence CHECK ((CentralDocumentRecordId IS NULL AND CentralDocumentVersionId IS NULL) OR (CentralDocumentRecordId IS NOT NULL AND CentralDocumentVersionId IS NOT NULL))
            );
            CREATE UNIQUE INDEX IX_CivilEngineeringMaintenanceAssessments_TenantId_IntakeId ON CivilEngineeringMaintenanceAssessments(TenantId, IntakeId);
            CREATE UNIQUE INDEX IX_CivilEngineeringMaintenanceAssessments_TenantId_ClientRequestId ON CivilEngineeringMaintenanceAssessments(TenantId, ClientRequestId);
            CREATE INDEX IX_CivilEngineeringMaintenanceAssessments_TenantId_CurrentAssigneeUserId_Stage ON CivilEngineeringMaintenanceAssessments(TenantId, CurrentAssigneeUserId, Stage);
            CREATE TABLE CivilEngineeringMaintenanceAssessmentRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, AssessmentId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
                FromStage varchar(40) NULL, ToStage varchar(40) NOT NULL, ActorUserId uniqueidentifier NOT NULL, ActorName nvarchar(300) NOT NULL,
                ActorRoles nvarchar(500) NULL, Reason nvarchar(2000) NULL, CorrelationId nvarchar(100) NOT NULL,
                BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessmentRevisions_Assessment FOREIGN KEY (AssessmentId) REFERENCES CivilEngineeringMaintenanceAssessments(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessmentRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_CivilEngineeringMaintenanceAssessmentRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_CivilEngineeringMaintenanceAssessmentRevisions_TenantId_AssessmentId_CreatedAt ON CivilEngineeringMaintenanceAssessmentRevisions(TenantId, AssessmentId, CreatedAt);
            CREATE INDEX IX_CivilEngineeringMaintenanceAssessmentRevisions_TenantId_CorrelationId ON CivilEngineeringMaintenanceAssessmentRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceAssessments_Lineage ON CivilEngineeringMaintenanceAssessments AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN CivilEngineeringMaintenanceIntakes intake ON intake.Id=value.IntakeId AND intake.TenantId=value.TenantId AND intake.IsDeleted=0
                LEFT JOIN Users hod ON hod.Id=value.HodUserId AND hod.TenantId=value.TenantId AND hod.IsActive=1
                LEFT JOIN Users sce ON sce.Id=value.SupervisingCivilEngineerUserId AND sce.TenantId=value.TenantId AND sce.IsActive=1
                LEFT JOIN Users engineer ON engineer.Id=value.CivilEngineerUserId AND engineer.TenantId=value.TenantId AND engineer.IsActive=1
                LEFT JOIN Users assignee ON assignee.Id=value.CurrentAssigneeUserId AND assignee.TenantId=value.TenantId AND assignee.IsActive=1
                LEFT JOIN ProjectCatalogEntries defect ON defect.Id=value.DefectCategoryId AND defect.TenantId=value.TenantId AND defect.IsDeleted=0 AND defect.IsActive=1 AND defect.CatalogType='civil-defect-categories'
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published'
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL
                WHERE intake.Id IS NULL OR hod.Id IS NULL OR sce.Id IS NULL
                  OR (value.CivilEngineerUserId IS NOT NULL AND engineer.Id IS NULL)
                  OR (value.CurrentAssigneeUserId IS NOT NULL AND assignee.Id IS NULL)
                  OR (value.DefectCategoryId IS NOT NULL AND defect.Id IS NULL)
                  OR (value.CentralDocumentVersionId IS NOT NULL AND (record.Id IS NULL OR version.Id IS NULL OR record.CurrentVersion<>version.VersionNumber OR record.MetadataTemplateCode<>intake.EvidenceMetadataTemplateCodeSnapshot))
                  OR value.PolicyHash<>intake.PolicyHash
                  OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (
                        intake.Status NOT IN ('Logged','AssessmentInProgress','AssessmentReturned') OR value.Stage<>'SceAssignment' OR value.Status<>'InProgress' OR value.ApprovalStatus<>'Draft'
                        OR value.CivilEngineerUserId IS NOT NULL OR value.CurrentAssigneeUserId<>value.SupervisingCivilEngineerUserId OR value.WorkflowInstanceId IS NOT NULL
                  ))
              ) THROW 52210, 'Civil maintenance assessment tenant, intake, user, DMS, defect-category, or frozen-policy lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceAssessments_Lifecycle ON CivilEngineeringMaintenanceAssessments AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52211, 'Civil maintenance assessment records cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.IntakeId<>prior.IntakeId OR value.HodUserId<>prior.HodUserId OR value.SupervisingCivilEngineerUserId<>prior.SupervisingCivilEngineerUserId OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted) THROW 52212, 'Civil maintenance assessment identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.Stage='SceAssignment' AND value.Stage='CivilEngineerAssessment') OR (prior.Stage='CivilEngineerAssessment' AND value.Stage='SceAssessmentReview') OR (prior.Stage='SceAssessmentReview' AND value.Stage IN ('CivilEngineerAssessment','HodFinalReview')) OR (prior.Stage='HodFinalReview' AND value.Stage IN ('Approved','Rejected')) OR value.Stage=prior.Stage)) THROW 52213, 'Invalid Civil maintenance assessment lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.Stage=prior.Stage AND (ISNULL(CONVERT(varchar(36),value.CivilEngineerUserId),'')<>ISNULL(CONVERT(varchar(36),prior.CivilEngineerUserId),'') OR ISNULL(CONVERT(varchar(36),value.CurrentAssigneeUserId),'')<>ISNULL(CONVERT(varchar(36),prior.CurrentAssigneeUserId),'') OR ISNULL(CONVERT(varchar(33),value.CurrentDueAt,126),'')<>ISNULL(CONVERT(varchar(33),prior.CurrentDueAt,126),'') OR ISNULL(CONVERT(varchar(36),value.DefectCategoryId),'')<>ISNULL(CONVERT(varchar(36),prior.DefectCategoryId),'') OR ISNULL(value.SiteAssessment,'')<>ISNULL(prior.SiteAssessment,'') OR ISNULL(value.ScopeRecommendation,'')<>ISNULL(prior.ScopeRecommendation,'') OR ISNULL(value.RemedyRecommendation,'')<>ISNULL(prior.RemedyRecommendation,'') OR ISNULL(value.EstimatedCost,-1)<>ISNULL(prior.EstimatedCost,-1) OR ISNULL(CONVERT(varchar(36),value.CentralDocumentRecordId),'')<>ISNULL(CONVERT(varchar(36),prior.CentralDocumentRecordId),'') OR ISNULL(CONVERT(varchar(36),value.CentralDocumentVersionId),'')<>ISNULL(CONVERT(varchar(36),prior.CentralDocumentVersionId),'') OR ISNULL(CONVERT(varchar(36),value.WorkflowInstanceId),'')<>ISNULL(CONVERT(varchar(36),prior.WorkflowInstanceId),'') OR value.Status<>prior.Status OR value.ApprovalStatus<>prior.ApprovalStatus OR ISNULL(CONVERT(varchar(36),value.ApprovedById),'')<>ISNULL(CONVERT(varchar(36),prior.ApprovedById),'') OR ISNULL(CONVERT(varchar(33),value.ApprovedAt,126),'')<>ISNULL(CONVERT(varchar(33),prior.ApprovedAt,126),'') OR ISNULL(value.RejectionReason,'')<>ISNULL(prior.RejectionReason,'') OR ISNULL(CONVERT(varchar(36),value.LastMutationClientRequestId),'')<>ISNULL(CONVERT(varchar(36),prior.LastMutationClientRequestId),'') OR ISNULL(value.LastMutationRequestHash,'')<>ISNULL(prior.LastMutationRequestHash,'') OR value.CorrelationId<>prior.CorrelationId OR value.CreatedAt<>prior.CreatedAt OR ISNULL(value.CreatedBy,'')<>ISNULL(prior.CreatedBy,'') OR ISNULL(CONVERT(varchar(36),value.CreatedById),'')<>ISNULL(CONVERT(varchar(36),prior.CreatedById),''))) THROW 52214, 'Civil maintenance assessment content may change only through an allowed lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value WHERE (value.Stage IN ('SceAssignment','CivilEngineerAssessment') AND (value.Status<>'InProgress' OR value.ApprovalStatus<>'Draft')) OR (value.Stage IN ('SceAssessmentReview','HodFinalReview') AND (value.Status<>'PendingApproval' OR value.ApprovalStatus<>'Pending')) OR (value.Stage='Approved' AND (value.Status<>'Approved' OR value.ApprovalStatus<>'Approved' OR value.ApprovedById IS NULL OR value.ApprovedAt IS NULL)) OR (value.Stage='Rejected' AND (value.Status<>'Rejected' OR value.ApprovalStatus<>'Rejected'))) THROW 52215, 'Civil maintenance assessment stage and approval status are inconsistent.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceAssessmentRevisions_Lineage ON CivilEngineeringMaintenanceAssessmentRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN CivilEngineeringMaintenanceAssessments assessment ON assessment.Id=value.AssessmentId AND assessment.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE assessment.Id IS NULL OR actor.Id IS NULL) THROW 52216, 'Civil maintenance assessment revision lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_CivilEngineeringMaintenanceAssessmentRevisions_AppendOnly ON CivilEngineeringMaintenanceAssessmentRevisions AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON; THROW 52217, 'Civil maintenance assessment revisions are append-only.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceAssessmentRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceAssessmentRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceAssessments_Lifecycle;
            DROP TRIGGER IF EXISTS TR_CivilEngineeringMaintenanceAssessments_Lineage;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceAssessmentRevisions;
            DROP TABLE IF EXISTS CivilEngineeringMaintenanceAssessments;
            """);
    }
}
