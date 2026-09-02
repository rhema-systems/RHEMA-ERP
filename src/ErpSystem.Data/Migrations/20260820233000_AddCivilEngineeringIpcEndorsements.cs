using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds a Civil Engineering review overlay for QS-owned interim payment certificates.
/// It stores only the review, DMS evidence and audit lineage; QS and Finance remain owners
/// of certificate lifecycle, AP invoices, payment allocation, posting and reversal.
/// </summary>
public partial class AddCivilEngineeringIpcEndorsements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.ProjectCivilIpcEndorsements', N'U') IS NULL
            BEGIN
            CREATE TABLE ProjectCivilIpcEndorsements (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectPaymentCertificateId uniqueidentifier NOT NULL, ProjectId uniqueidentifier NOT NULL,
                Sequence int NOT NULL, ProjectEngineerAssignmentId uniqueidentifier NOT NULL, SubmittedById uniqueidentifier NOT NULL,
                SubmittedAt datetime2 NOT NULL, SubmissionNotes nvarchar(2000) NOT NULL, Status varchar(40) NOT NULL,
                ReviewedById uniqueidentifier NULL, ReviewedAt datetime2 NULL, ReviewNotes nvarchar(2000) NULL,
                EvidenceDocumentRecordId uniqueidentifier NULL, EvidenceDocumentVersionId uniqueidentifier NULL,
                ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL, WorkflowDefinitionId uniqueidentifier NOT NULL,
                EvidenceMetadataTemplateId uniqueidentifier NOT NULL, EvidenceMetadataTemplateCodeSnapshot varchar(80) NOT NULL,
                RequiresDmsEvidence bit NOT NULL, PolicyHash varchar(64) NOT NULL, ClientRequestId uniqueidentifier NOT NULL,
                RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL, LastMutationRequestHash varchar(64) NULL,
                CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Certificate FOREIGN KEY (ProjectPaymentCertificateId) REFERENCES ProjectPaymentCertificates(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Assignment FOREIGN KEY (ProjectEngineerAssignmentId) REFERENCES ProjectCivilProjectEngineerAssignments(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Workflow FOREIGN KEY (WorkflowDefinitionId) REFERENCES WorkflowDefinitions(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Template FOREIGN KEY (EvidenceMetadataTemplateId) REFERENCES CentralDocumentMetadataTemplates(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_EvidenceRecord FOREIGN KEY (EvidenceDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_EvidenceVersion FOREIGN KEY (EvidenceDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Submitter FOREIGN KEY (SubmittedById) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Reviewer FOREIGN KEY (ReviewedById) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsements_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilIpcEndorsements_Sequence CHECK (Sequence > 0),
                CONSTRAINT CK_ProjectCivilIpcEndorsements_Evidence CHECK ((EvidenceDocumentRecordId IS NULL AND EvidenceDocumentVersionId IS NULL) OR (EvidenceDocumentRecordId IS NOT NULL AND EvidenceDocumentVersionId IS NOT NULL)),
                CONSTRAINT CK_ProjectCivilIpcEndorsements_Status CHECK ((Status='AwaitingProjectEngineerReview' AND ReviewedById IS NULL AND ReviewedAt IS NULL AND ReviewNotes IS NULL AND EvidenceDocumentRecordId IS NULL AND EvidenceDocumentVersionId IS NULL) OR (Status='Endorsed' AND ReviewedById IS NOT NULL AND ReviewedAt IS NOT NULL AND ReviewNotes IS NOT NULL AND LEN(LTRIM(RTRIM(ReviewNotes)))>0) OR (Status='ReturnedToProjectsCoordinator' AND ReviewedById IS NOT NULL AND ReviewedAt IS NOT NULL AND ReviewNotes IS NOT NULL AND LEN(LTRIM(RTRIM(ReviewNotes)))>0 AND EvidenceDocumentRecordId IS NULL AND EvidenceDocumentVersionId IS NULL))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilIpcEndorsements_TenantId_ClientRequestId ON ProjectCivilIpcEndorsements(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilIpcEndorsements_TenantId_Certificate_Sequence ON ProjectCivilIpcEndorsements(TenantId, ProjectPaymentCertificateId, Sequence);
            CREATE INDEX IX_ProjectCivilIpcEndorsements_TenantId_ProjectId_Status_SubmittedAt ON ProjectCivilIpcEndorsements(TenantId, ProjectId, Status, SubmittedAt);
            END;

            IF OBJECT_ID(N'dbo.ProjectCivilIpcEndorsementRevisions', N'U') IS NULL
            BEGIN
            CREATE TABLE ProjectCivilIpcEndorsementRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, IpcEndorsementId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL,
                ActorUserId uniqueidentifier NOT NULL, ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL,
                CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL, BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilIpcEndorsementRevisions_Review FOREIGN KEY (IpcEndorsementId) REFERENCES ProjectCivilIpcEndorsements(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsementRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilIpcEndorsementRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilIpcEndorsementRevisions_TenantId_ReviewId_CreatedAt ON ProjectCivilIpcEndorsementRevisions(TenantId, IpcEndorsementId, CreatedAt);
            CREATE INDEX IX_ProjectCivilIpcEndorsementRevisions_TenantId_CorrelationId ON ProjectCivilIpcEndorsementRevisions(TenantId, CorrelationId);
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilIpcEndorsements_Lineage ON ProjectCivilIpcEndorsements AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN ProjectPaymentCertificates certificate ON certificate.Id=value.ProjectPaymentCertificateId AND certificate.TenantId=value.TenantId AND certificate.ProjectId=value.ProjectId AND certificate.IsDeleted=0 AND certificate.QuantitySurveyValuationWorksheetId IS NOT NULL
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.TenantId=value.TenantId AND assignment.ProjectId=value.ProjectId AND assignment.IsActive=1 AND assignment.IsDeleted=0 AND assignment.EffectiveFrom<=SYSUTCDATETIME() AND (assignment.EffectiveTo IS NULL OR assignment.EffectiveTo>=SYSUTCDATETIME())
                LEFT JOIN ProjectMembers engineerMember ON engineerMember.Id=assignment.ProjectMemberId AND engineerMember.TenantId=value.TenantId AND engineerMember.ProjectId=value.ProjectId AND engineerMember.UserId=assignment.AssignedUserId AND engineerMember.IsActive=1 AND engineerMember.IsDeleted=0 AND engineerMember.Role='TDC_PROJECT_ENGINEER'
                LEFT JOIN Users submitter ON submitter.Id=value.SubmittedById AND submitter.TenantId=value.TenantId AND submitter.IsActive=1
                LEFT JOIN Users reviewer ON reviewer.Id=value.ReviewedById AND reviewer.TenantId=value.TenantId AND reviewer.IsActive=1
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions supervision ON supervision.Id=value.ConfigurationDecisionId AND supervision.ProfileId=value.ConfigurationProfileId AND supervision.TenantId=value.TenantId AND supervision.ConfigurationKey='CIV-CFG-005' AND supervision.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions documents ON documents.ProfileId=value.ConfigurationProfileId AND documents.TenantId=value.TenantId AND documents.ConfigurationKey='CIV-CFG-004' AND documents.IsDeleted=0
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsActive=1 AND workflow.IsDeleted=0 AND workflow.LifecycleStatus=1
                LEFT JOIN WorkflowEntityTypes workflowType ON workflowType.Id=workflow.EntityTypeId AND workflowType.TenantId=value.TenantId AND workflowType.IsActive=1 AND workflowType.IsDeleted=0 AND workflowType.Code='QS_PAYMENT_CERTIFICATE'
                LEFT JOIN CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsActive=1 AND template.IsDeleted=0
                LEFT JOIN CentralDocumentRecords evidenceRecord ON evidenceRecord.Id=value.EvidenceDocumentRecordId AND evidenceRecord.TenantId=value.TenantId AND evidenceRecord.IsDeleted=0 AND evidenceRecord.LifecycleStatus='Active' AND evidenceRecord.VersionStatus='Published'
                LEFT JOIN CentralDocumentVersions evidenceVersion ON evidenceVersion.Id=value.EvidenceDocumentVersionId AND evidenceVersion.DocumentRecordId=value.EvidenceDocumentRecordId AND evidenceVersion.TenantId=value.TenantId AND evidenceVersion.IsDeleted=0 AND evidenceVersion.Status='Published' AND evidenceVersion.PublishedAt IS NOT NULL
                WHERE certificate.Id IS NULL OR project.Id IS NULL OR assignment.Id IS NULL OR engineerMember.Id IS NULL OR submitter.Id IS NULL OR profile.Id IS NULL OR supervision.Id IS NULL OR documents.Id IS NULL OR workflow.Id IS NULL OR workflowType.Id IS NULL OR template.Id IS NULL
                   OR certificate.ApprovalWorkflowDefinitionId IS NULL OR certificate.ApprovalWorkflowDefinitionId<>value.WorkflowDefinitionId
                   OR JSON_VALUE(documents.ValueJson,'$.metadataTemplateId')<>CONVERT(varchar(36),value.EvidenceMetadataTemplateId)
                   OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR NOT EXISTS (SELECT 1 FROM UserRoles roleAssignment WHERE roleAssignment.UserId=assignment.AssignedUserId AND EXISTS (SELECT 1 FROM OPENJSON(supervision.ValueJson,'$.projectEngineerRoleIds') roleId WHERE TRY_CONVERT(uniqueidentifier,roleId.[value])=roleAssignment.RoleId))
                   OR NOT EXISTS (SELECT 1 FROM ProjectMembers member JOIN UserRoles roleAssignment ON roleAssignment.UserId=member.UserId WHERE member.TenantId=value.TenantId AND member.ProjectId=value.ProjectId AND member.UserId=value.SubmittedById AND member.IsActive=1 AND member.IsDeleted=0 AND EXISTS (SELECT 1 FROM OPENJSON(supervision.ValueJson,'$.coordinatorRoleIds') roleId WHERE TRY_CONVERT(uniqueidentifier,roleId.[value])=roleAssignment.RoleId))
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR supervision.Status<>2 OR supervision.ApprovalStatus<>1 OR supervision.EvidenceStatus<>2 OR documents.Status<>2 OR documents.ApprovalStatus<>1 OR documents.EvidenceStatus<>2 OR template.PublishedAt IS NULL OR NOT EXISTS (SELECT 1 FROM WorkflowSteps step WHERE step.WorkflowDefinitionId=value.WorkflowDefinitionId AND step.TenantId=value.TenantId AND step.IsDeleted=0)))
                   OR (value.Status='Endorsed' AND (reviewer.Id IS NULL OR reviewer.Id<>assignment.AssignedUserId))
                   OR (value.Status='Endorsed' AND value.EvidenceDocumentVersionId IS NOT NULL AND (evidenceRecord.Id IS NULL OR evidenceVersion.Id IS NULL OR evidenceRecord.CurrentVersion<>evidenceVersion.VersionNumber OR evidenceRecord.MetadataTemplateCode<>template.TemplateCode OR (evidenceRecord.SourceRecordId<>value.ProjectId AND evidenceRecord.SourceRecordId<>value.ProjectPaymentCertificateId)))
                   OR (value.Status='ReturnedToProjectsCoordinator' AND (reviewer.Id IS NULL OR reviewer.Id<>assignment.AssignedUserId))
                   OR (value.Status='Endorsed' AND value.RequiresDmsEvidence=1 AND (value.EvidenceDocumentRecordId IS NULL OR value.EvidenceDocumentVersionId IS NULL))
              ) THROW 52150, 'Civil IPC endorsement tenant, project, configuration, QS workflow, assignment, coordinator, or DMS lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilIpcEndorsements_Lifecycle ON ProjectCivilIpcEndorsements AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id)) THROW 52151, 'Civil IPC endorsement reviews cannot be deleted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE value.TenantId<>prior.TenantId OR value.ProjectPaymentCertificateId<>prior.ProjectPaymentCertificateId OR value.ProjectId<>prior.ProjectId
                   OR value.Sequence<>prior.Sequence OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId OR value.SubmittedById<>prior.SubmittedById
                   OR value.SubmittedAt<>prior.SubmittedAt OR value.SubmissionNotes<>prior.SubmissionNotes OR value.ConfigurationProfileId<>prior.ConfigurationProfileId
                   OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId
                   OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot
                   OR value.RequiresDmsEvidence<>prior.RequiresDmsEvidence OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId
                   OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted
              ) THROW 52152, 'Civil IPC endorsement identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                WHERE NOT (prior.Status='AwaitingProjectEngineerReview' AND ((value.Status='Endorsed' AND value.ReviewedById IS NOT NULL AND value.ReviewedAt IS NOT NULL AND value.ReviewNotes IS NOT NULL) OR (value.Status='ReturnedToProjectsCoordinator' AND value.ReviewedById IS NOT NULL AND value.ReviewedAt IS NOT NULL AND value.ReviewNotes IS NOT NULL AND value.EvidenceDocumentRecordId IS NULL AND value.EvidenceDocumentVersionId IS NULL)))
              ) THROW 52153, 'Invalid Civil IPC endorsement lifecycle transition.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilIpcEndorsementRevisions_Lineage ON ProjectCivilIpcEndorsementRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilIpcEndorsements review ON review.Id=value.IpcEndorsementId AND review.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId WHERE review.Id IS NULL OR actor.Id IS NULL) THROW 52154, 'Civil IPC endorsement revision lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilIpcEndorsementRevisions_AppendOnly ON ProjectCivilIpcEndorsementRevisions AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52155, 'Civil IPC endorsement revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilIpcEndorsementRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilIpcEndorsementRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilIpcEndorsements_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilIpcEndorsements_Lineage;
            DROP TABLE IF EXISTS ProjectCivilIpcEndorsementRevisions;
            DROP TABLE IF EXISTS ProjectCivilIpcEndorsements;
            """);
    }
}
