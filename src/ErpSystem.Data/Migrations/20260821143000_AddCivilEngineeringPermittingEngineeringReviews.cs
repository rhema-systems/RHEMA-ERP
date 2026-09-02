using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0403 SCE engineering recommendation register; HOD decision remains a later stage.</summary>
public partial class AddCivilEngineeringPermittingEngineeringReviews : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilDevelopmentApprovalEngineeringReviews (
              Id uniqueidentifier NOT NULL PRIMARY KEY, DevelopmentApprovalFileId uniqueidentifier NOT NULL, SourceHandoffId uniqueidentifier NOT NULL,
              ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, ReviewerUserId uniqueidentifier NOT NULL, CommentCategoryId uniqueidentifier NOT NULL,
              ReviewComment nvarchar(4000) NOT NULL, RecommendedOutcome int NOT NULL, CentralDocumentRecordId uniqueidentifier NOT NULL, CentralDocumentVersionId uniqueidentifier NOT NULL,
              WorkflowDefinitionId uniqueidentifier NOT NULL, WorkflowInstanceId uniqueidentifier NULL, Stage int NOT NULL, Status varchar(40) NOT NULL, ApprovalStatus varchar(40) NOT NULL,
              ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL, ReviewedAt datetime2 NOT NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_File FOREIGN KEY (DevelopmentApprovalFileId) REFERENCES ProjectCivilDevelopmentApprovalFiles(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_Handoff FOREIGN KEY (SourceHandoffId) REFERENCES ProjectCivilDevelopmentApprovalFileHandoffs(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_Reviewer FOREIGN KEY (ReviewerUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_Category FOREIGN KEY (CommentCategoryId) REFERENCES ProjectCatalogEntries(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_Workflow FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviews_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalEngineeringReviews_Stage CHECK (Stage IN (0,1,2,3,4)),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalEngineeringReviews_Outcome CHECK (RecommendedOutcome IN (0,1,2,3)),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalEngineeringReviews_Status CHECK (Status IN ('CorrectionRequested','PendingApproval','Approved','Rejected','Draft') AND ApprovalStatus IN ('NotApplicable','Pending','Approved','Rejected','Draft'))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalEngineeringReviews_TenantId_ClientRequestId ON ProjectCivilDevelopmentApprovalEngineeringReviews(TenantId,ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalEngineeringReviews_TenantId_SourceHandoffId ON ProjectCivilDevelopmentApprovalEngineeringReviews(TenantId,SourceHandoffId);
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalEngineeringReviews_TenantId_FileId_Stage ON ProjectCivilDevelopmentApprovalEngineeringReviews(TenantId,DevelopmentApprovalFileId,Stage);

            CREATE TABLE ProjectCivilDevelopmentApprovalEngineeringReviewRevisions (
              Id uniqueidentifier NOT NULL PRIMARY KEY, EngineeringReviewId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
              ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_Review FOREIGN KEY (EngineeringReviewId) REFERENCES ProjectCivilDevelopmentApprovalEngineeringReviews(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_TenantId_ReviewId_CreatedAt ON ProjectCivilDevelopmentApprovalEngineeringReviewRevisions(TenantId,EngineeringReviewId,CreatedAt);
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_TenantId_CorrelationId ON ProjectCivilDevelopmentApprovalEngineeringReviewRevisions(TenantId,CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lineage ON ProjectCivilDevelopmentApprovalEngineeringReviews AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN ProjectCivilDevelopmentApprovalFiles approvalFile ON approvalFile.Id=value.DevelopmentApprovalFileId AND approvalFile.TenantId=value.TenantId AND approvalFile.IsDeleted=0 AND approvalFile.Status=2
                LEFT JOIN ProjectCivilDevelopmentApprovalFileHandoffs handoff ON handoff.Id=value.SourceHandoffId AND handoff.DevelopmentApprovalFileId=value.DevelopmentApprovalFileId AND handoff.TenantId=value.TenantId AND handoff.IsDeleted=0 AND handoff.ToSection=2 AND handoff.RecipientUserId=value.ReviewerUserId
                LEFT JOIN CivilEngineeringConfigurationDecisions permitting ON permitting.Id=approvalFile.PermittingConfigurationDecisionId AND permitting.ProfileId=approvalFile.ConfigurationProfileId AND permitting.TenantId=value.TenantId AND permitting.IsDeleted=0 AND permitting.ConfigurationKey='CIV-CFG-009'
                LEFT JOIN CivilEngineeringConfigurationDecisions documentPolicy ON documentPolicy.Id=approvalFile.DocumentConfigurationDecisionId AND documentPolicy.ProfileId=approvalFile.ConfigurationProfileId AND documentPolicy.TenantId=value.TenantId AND documentPolicy.IsDeleted=0 AND documentPolicy.ConfigurationKey='CIV-CFG-004'
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0 AND workflow.IsActive=1 AND workflow.LifecycleStatus=1
                LEFT JOIN WorkflowEntityTypes workflowType ON workflowType.Id=workflow.EntityTypeId AND workflowType.TenantId=value.TenantId AND workflowType.IsDeleted=0 AND workflowType.IsActive=1 AND workflowType.Code='PROJECT_PERMITTING_REVIEW'
                LEFT JOIN Users reviewer ON reviewer.Id=value.ReviewerUserId AND reviewer.TenantId=value.TenantId AND reviewer.IsActive=1
                LEFT JOIN ProjectCatalogEntries category ON category.Id=value.CommentCategoryId AND category.TenantId=value.TenantId AND category.IsDeleted=0 AND category.IsActive=1 AND category.CatalogType='civil-permitting-comment-categories'
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published' AND record.MetadataTemplateCode=approvalFile.MetadataTemplateCodeSnapshot
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL AND record.CurrentVersion=version.VersionNumber
                WHERE approvalFile.Id IS NULL OR handoff.Id IS NULL OR permitting.Id IS NULL OR documentPolicy.Id IS NULL OR workflow.Id IS NULL OR workflowType.Id IS NULL OR reviewer.Id IS NULL OR category.Id IS NULL OR record.Id IS NULL OR version.Id IS NULL
                   OR JSON_VALUE(permitting.ValueJson,'$.workflowDefinitionId')<>CONVERT(varchar(36),value.WorkflowDefinitionId)
                   OR NOT EXISTS (SELECT 1 FROM UserRoles reviewerRole JOIN AspNetRoles role ON role.Id=reviewerRole.RoleId WHERE reviewerRole.UserId=value.ReviewerUserId AND role.Name='TDC_SUPERVISING_CIVIL_ENGINEER')
                   OR NOT EXISTS (SELECT 1 FROM ProjectMembers member WHERE member.TenantId=value.TenantId AND member.ProjectId=approvalFile.ProjectId AND member.UserId=value.ReviewerUserId AND member.IsDeleted=0)
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(permitting.ValueJson,'$.commentCategoryIds') allowedCategory WHERE TRY_CONVERT(uniqueidentifier,allowedCategory.value)=value.CommentCategoryId)
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(permitting.ValueJson,'$.allowedOutcomes') allowedOutcome WHERE TRY_CONVERT(int,allowedOutcome.value)=value.RecommendedOutcome)
                   OR JSON_VALUE(documentPolicy.ValueJson,'$.requireVersioning')<>'true'
                   OR version.FileSize IS NULL OR version.FileSize<=0 OR version.FileSize>TRY_CONVERT(bigint,JSON_VALUE(documentPolicy.ValueJson,'$.maximumFileSizeMb'))*1048576
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(documentPolicy.ValueJson,'$.allowedFileExtensions') extension WHERE LOWER(extension.value)=LOWER(RIGHT(ISNULL(version.FileName,''),CHARINDEX('.',REVERSE(ISNULL(version.FileName,''))))))
              ) THROW 52260, 'Civil permitting engineering-review tenant, handoff, SCE, configuration, workflow, comment or DMS lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lifecycle ON ProjectCivilDevelopmentApprovalEngineeringReviews AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52261, 'Civil permitting engineering reviews cannot be deleted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE value.TenantId<>prior.TenantId OR value.DevelopmentApprovalFileId<>prior.DevelopmentApprovalFileId OR value.SourceHandoffId<>prior.SourceHandoffId OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash
                   OR value.ReviewerUserId<>prior.ReviewerUserId OR value.CommentCategoryId<>prior.CommentCategoryId OR value.ReviewComment<>prior.ReviewComment OR value.RecommendedOutcome<>prior.RecommendedOutcome OR value.CentralDocumentRecordId<>prior.CentralDocumentRecordId OR value.CentralDocumentVersionId<>prior.CentralDocumentVersionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.ReviewedAt<>prior.ReviewedAt OR value.IsDeleted<>prior.IsDeleted
              ) THROW 52262, 'Civil permitting engineering-review content and frozen lineage are immutable.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE NOT ((prior.Stage=0 AND value.Stage=0) OR (prior.Stage=1 AND value.Stage IN (1,2,3,4)) OR (prior.Stage IN (2,3,4) AND value.Stage=prior.Stage))
              ) THROW 52263, 'Invalid Civil permitting engineering-review lifecycle transition.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_Lineage ON ProjectCivilDevelopmentApprovalEngineeringReviewRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilDevelopmentApprovalEngineeringReviews review ON review.Id=value.EngineeringReviewId AND review.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE review.Id IS NULL OR actor.Id IS NULL) THROW 52264, 'Civil permitting engineering-review revision lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_AppendOnly ON ProjectCivilDevelopmentApprovalEngineeringReviewRevisions AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52265, 'Civil permitting engineering-review revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEngineeringReviewRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalEngineeringReviews_Lineage;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalEngineeringReviewRevisions;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalEngineeringReviews;
            """);
    }
}
