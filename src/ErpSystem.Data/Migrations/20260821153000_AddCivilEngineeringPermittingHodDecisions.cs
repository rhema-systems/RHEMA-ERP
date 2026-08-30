using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0404 immutable HOD decision evidence over the existing shared permitting-review workflow.</summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260821153000_AddCivilEngineeringPermittingHodDecisions")]
public partial class AddCivilEngineeringPermittingHodDecisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilDevelopmentApprovalEngineeringReviewDecisions (
              Id uniqueidentifier NOT NULL PRIMARY KEY, EngineeringReviewId uniqueidentifier NOT NULL, ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
              Outcome int NOT NULL, Reason nvarchar(2000) NULL, DecidedById uniqueidentifier NOT NULL, WorkflowInstanceId uniqueidentifier NOT NULL, WorkflowAction varchar(30) NOT NULL, WorkflowOutcome varchar(30) NOT NULL,
              CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
              CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Review FOREIGN KEY (EngineeringReviewId) REFERENCES ProjectCivilDevelopmentApprovalEngineeringReviews(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Decider FOREIGN KEY (DecidedById) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_WorkflowInstance FOREIGN KEY (WorkflowInstanceId) REFERENCES WorkflowInstances(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Outcome CHECK (Outcome IN (0,1,2))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_TenantId_ClientRequestId ON ProjectCivilDevelopmentApprovalEngineeringReviewDecisions(TenantId,ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_TenantId_ReviewId ON ProjectCivilDevelopmentApprovalEngineeringReviewDecisions(TenantId,EngineeringReviewId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Lineage ON ProjectCivilDevelopmentApprovalEngineeringReviewDecisions AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN ProjectCivilDevelopmentApprovalEngineeringReviews review ON review.Id=value.EngineeringReviewId AND review.TenantId=value.TenantId AND review.IsDeleted=0 AND review.Stage=1 AND review.Status='PendingApproval' AND review.ApprovalStatus='Pending' AND review.WorkflowInstanceId=value.WorkflowInstanceId
                LEFT JOIN ProjectCivilDevelopmentApprovalFiles approvalFile ON approvalFile.Id=review.DevelopmentApprovalFileId AND approvalFile.TenantId=value.TenantId AND approvalFile.IsDeleted=0
                LEFT JOIN Users decider ON decider.Id=value.DecidedById AND decider.TenantId=value.TenantId AND decider.IsActive=1
                LEFT JOIN WorkflowInstances instance ON instance.Id=value.WorkflowInstanceId AND instance.TenantId=value.TenantId AND instance.EntityId=review.Id AND instance.WorkflowDefinitionId=review.WorkflowDefinitionId AND instance.IsDeleted=0
                LEFT JOIN WorkflowEntityTypes entityType ON entityType.Id=instance.EntityTypeId AND entityType.TenantId=value.TenantId AND entityType.IsDeleted=0 AND entityType.Code='PROJECT_PERMITTING_REVIEW'
                WHERE review.Id IS NULL OR approvalFile.Id IS NULL OR decider.Id IS NULL OR instance.Id IS NULL OR entityType.Id IS NULL
                   OR NOT EXISTS (SELECT 1 FROM UserRoles userRole JOIN AspNetRoles role ON role.Id=userRole.RoleId WHERE userRole.UserId=value.DecidedById AND role.Name='TDC_HEAD_OF_CIVIL_ENGINEERING')
                   OR NOT EXISTS (SELECT 1 FROM ProjectMembers member WHERE member.TenantId=value.TenantId AND member.ProjectId=approvalFile.ProjectId AND member.UserId=value.DecidedById AND member.IsDeleted=0)
                   OR NOT EXISTS (SELECT 1 FROM ProjectCivilDevelopmentApprovalFileHandoffs handoff WHERE handoff.TenantId=value.TenantId AND handoff.DevelopmentApprovalFileId=review.DevelopmentApprovalFileId AND handoff.IsDeleted=0 AND handoff.SequenceNumber=(SELECT MAX(lastHandoff.SequenceNumber) FROM ProjectCivilDevelopmentApprovalFileHandoffs lastHandoff WHERE lastHandoff.TenantId=value.TenantId AND lastHandoff.DevelopmentApprovalFileId=review.DevelopmentApprovalFileId AND lastHandoff.IsDeleted=0) AND handoff.ToSection=3 AND handoff.RecipientUserId=value.DecidedById)
                   OR NOT ((value.Outcome=0 AND value.WorkflowAction='Approve' AND value.WorkflowOutcome='Approved') OR (value.Outcome IN (1,2) AND value.WorkflowAction='Reject' AND value.WorkflowOutcome='Rejected'))
              ) THROW 52270, 'Civil permitting HOD-decision tenant, handoff, workflow, role or outcome lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_AppendOnly ON ProjectCivilDevelopmentApprovalEngineeringReviewDecisions AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52271, 'Civil permitting HOD decisions are append-only.', 1; END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEngineeringReviews_HodDecisionProjection ON ProjectCivilDevelopmentApprovalEngineeringReviews AFTER UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE prior.Stage=1 AND value.Stage IN (2,3,4)
                  AND (NOT EXISTS (SELECT 1 FROM ProjectCivilDevelopmentApprovalEngineeringReviewDecisions decision WHERE decision.TenantId=value.TenantId AND decision.EngineeringReviewId=value.Id AND decision.WorkflowInstanceId=value.WorkflowInstanceId AND ((value.Stage=2 AND decision.Outcome=0) OR (value.Stage=3 AND decision.Outcome=1) OR (value.Stage=4 AND decision.Outcome=2)))
                       OR (value.Stage=2 AND (value.Status<>'Approved' OR value.ApprovalStatus<>'Approved'))
                       OR (value.Stage IN (3,4) AND (value.Status<>'Rejected' OR value.ApprovalStatus<>'Rejected')))
              ) THROW 52272, 'A final Civil permitting recommendation status requires its matching immutable HOD decision.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEngineeringReviews_HodDecisionProjection;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEngineeringReviewDecisions_Lineage;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalEngineeringReviewDecisions;
            """);
    }
}
