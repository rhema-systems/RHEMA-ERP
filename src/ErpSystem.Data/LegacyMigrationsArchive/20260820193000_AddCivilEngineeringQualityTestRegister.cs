using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the Projects-owned Civil quality-test register. It stores governed references only;
/// DMS, Workflow, QS payment certificates, business partners and audit remain central owners.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260820193000_AddCivilEngineeringQualityTestRegister")]
public partial class AddCivilEngineeringQualityTestRegister : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilQualityTestReports (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, ProjectPhaseId uniqueidentifier NULL,
                ProjectPackageId uniqueidentifier NULL, ProjectPaymentCertificateId uniqueidentifier NULL, SourceBusinessPartnerId uniqueidentifier NULL,
                TestCategory int NOT NULL, SourceType varchar(30) NOT NULL, ReportReference varchar(100) NOT NULL, TestedAt datetime2 NOT NULL,
                ResultStatus varchar(30) NOT NULL, ResultSummary nvarchar(2000) NOT NULL, ReviewerUserId uniqueidentifier NOT NULL,
                CentralDocumentRecordId uniqueidentifier NOT NULL, CentralDocumentVersionId uniqueidentifier NOT NULL,
                EndorsementDocumentRecordId uniqueidentifier NULL, EndorsementDocumentVersionId uniqueidentifier NULL,
                ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL, WorkflowDefinitionId uniqueidentifier NOT NULL,
                EvidenceMetadataTemplateId uniqueidentifier NOT NULL, EvidenceMetadataTemplateCodeSnapshot varchar(80) NOT NULL, WorkflowInstanceId uniqueidentifier NULL,
                PolicyHash varchar(64) NOT NULL, Status nvarchar(30) NOT NULL, ApprovalStatus nvarchar(30) NOT NULL, AcceptanceBlocked bit NOT NULL,
                ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL,
                LastMutationRequestHash varchar(64) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilQualityTestReports_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Phase FOREIGN KEY (ProjectPhaseId) REFERENCES ProjectPhases(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Package FOREIGN KEY (ProjectPackageId) REFERENCES ProjectPackages(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Certificate FOREIGN KEY (ProjectPaymentCertificateId) REFERENCES ProjectPaymentCertificates(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_SourcePartner FOREIGN KEY (SourceBusinessPartnerId) REFERENCES BusinessPartners(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Template FOREIGN KEY (EvidenceMetadataTemplateId) REFERENCES CentralDocumentMetadataTemplates(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_EvidenceRecord FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_EvidenceVersion FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_EndorsementRecord FOREIGN KEY (EndorsementDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_EndorsementVersion FOREIGN KEY (EndorsementDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Reviewer FOREIGN KEY (ReviewerUserId) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Approver FOREIGN KEY (ApprovedById) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilQualityTestReports_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilQualityTestReports_Status CHECK ((Status='PendingApproval' AND ApprovalStatus='Pending' AND ApprovedById IS NULL AND ApprovedAt IS NULL AND RejectionReason IS NULL) OR (Status='Approved' AND ApprovalStatus='Approved' AND ApprovedById IS NOT NULL AND ApprovedAt IS NOT NULL AND RejectionReason IS NULL) OR (Status='Rejected' AND ApprovalStatus='Rejected' AND ApprovedById IS NULL AND ApprovedAt IS NULL AND RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(RejectionReason)))>0)),
                CONSTRAINT CK_ProjectCivilQualityTestReports_Source CHECK (SourceType IN ('Laboratory','Contractor','Consultant','Internal') AND ((SourceType='Internal' AND SourceBusinessPartnerId IS NULL) OR (SourceType<>'Internal' AND SourceBusinessPartnerId IS NOT NULL))),
                CONSTRAINT CK_ProjectCivilQualityTestReports_Result CHECK (ResultStatus IN ('Pass','Fail','Inconclusive')),
                CONSTRAINT CK_ProjectCivilQualityTestReports_Endorsement CHECK ((EndorsementDocumentRecordId IS NULL AND EndorsementDocumentVersionId IS NULL) OR (EndorsementDocumentRecordId IS NOT NULL AND EndorsementDocumentVersionId IS NOT NULL))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilQualityTestReports_TenantId_ClientRequestId ON ProjectCivilQualityTestReports(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilQualityTestReports_TenantId_ProjectId_ReportReference ON ProjectCivilQualityTestReports(TenantId, ProjectId, ReportReference) WHERE IsDeleted=0;
            CREATE INDEX IX_ProjectCivilQualityTestReports_TenantId_ProjectId_Status_TestedAt ON ProjectCivilQualityTestReports(TenantId, ProjectId, Status, TestedAt);
            CREATE INDEX IX_ProjectCivilQualityTestReports_TenantId_ReviewerUserId_Status ON ProjectCivilQualityTestReports(TenantId, ReviewerUserId, Status);

            CREATE TABLE ProjectCivilQualityTestRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, TestReportId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
                ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL,
                BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilQualityTestRevisions_Report FOREIGN KEY (TestReportId) REFERENCES ProjectCivilQualityTestReports(Id),
                CONSTRAINT FK_ProjectCivilQualityTestRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilQualityTestRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilQualityTestRevisions_TenantId_TestReportId_CreatedAt ON ProjectCivilQualityTestRevisions(TenantId, TestReportId, CreatedAt);
            CREATE INDEX IX_ProjectCivilQualityTestRevisions_TenantId_CorrelationId ON ProjectCivilQualityTestRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilQualityTestReports_Lineage ON ProjectCivilQualityTestReports AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN ProjectPhases phase ON phase.Id=value.ProjectPhaseId AND phase.ProjectId=value.ProjectId AND phase.TenantId=value.TenantId AND phase.IsDeleted=0
                LEFT JOIN ProjectPackages package ON package.Id=value.ProjectPackageId AND package.ProjectId=value.ProjectId AND package.TenantId=value.TenantId AND package.IsDeleted=0
                LEFT JOIN ProjectPaymentCertificates certificate ON certificate.Id=value.ProjectPaymentCertificateId AND certificate.ProjectId=value.ProjectId AND certificate.TenantId=value.TenantId AND certificate.IsDeleted=0 AND certificate.Status<>'Cancelled'
                LEFT JOIN BusinessPartners sourcePartner ON sourcePartner.Id=value.SourceBusinessPartnerId AND sourcePartner.TenantId=value.TenantId
                LEFT JOIN Users reviewer ON reviewer.Id=value.ReviewerUserId AND reviewer.TenantId=value.TenantId AND reviewer.IsActive=1
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-011' AND decision.IsDeleted=0
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0
                LEFT JOIN CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0
                LEFT JOIN CentralDocumentRecords evidenceRecord ON evidenceRecord.Id=value.CentralDocumentRecordId AND evidenceRecord.TenantId=value.TenantId AND evidenceRecord.IsDeleted=0
                LEFT JOIN CentralDocumentVersions evidenceVersion ON evidenceVersion.Id=value.CentralDocumentVersionId AND evidenceVersion.DocumentRecordId=value.CentralDocumentRecordId AND evidenceVersion.TenantId=value.TenantId AND evidenceVersion.IsDeleted=0
                LEFT JOIN CentralDocumentRecords endorsementRecord ON endorsementRecord.Id=value.EndorsementDocumentRecordId AND endorsementRecord.TenantId=value.TenantId AND endorsementRecord.IsDeleted=0 AND endorsementRecord.LifecycleStatus='Active' AND endorsementRecord.VersionStatus='Published'
                LEFT JOIN CentralDocumentVersions endorsementVersion ON endorsementVersion.Id=value.EndorsementDocumentVersionId AND endorsementVersion.DocumentRecordId=value.EndorsementDocumentRecordId AND endorsementVersion.TenantId=value.TenantId AND endorsementVersion.IsDeleted=0 AND endorsementVersion.Status='Published' AND endorsementVersion.PublishedAt IS NOT NULL
                WHERE project.Id IS NULL OR reviewer.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR workflow.Id IS NULL OR template.Id IS NULL OR evidenceRecord.Id IS NULL OR evidenceVersion.Id IS NULL
                   OR evidenceRecord.MetadataTemplateCode<>template.TemplateCode OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR (value.ProjectPhaseId IS NOT NULL AND phase.Id IS NULL) OR (value.ProjectPackageId IS NOT NULL AND package.Id IS NULL) OR (value.ProjectPaymentCertificateId IS NOT NULL AND certificate.Id IS NULL)
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND value.SourceType<>'Internal' AND (sourcePartner.Id IS NULL OR sourcePartner.IsDeleted=1 OR sourcePartner.IsActive<>1 OR sourcePartner.IsBlacklisted=1))
                   OR (value.EndorsementDocumentVersionId IS NOT NULL AND (endorsementRecord.Id IS NULL OR endorsementVersion.Id IS NULL OR endorsementRecord.CurrentVersion<>endorsementVersion.VersionNumber OR endorsementRecord.MetadataTemplateCode<>template.TemplateCode))
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2 OR workflow.IsActive<>1 OR workflow.LifecycleStatus<>1 OR template.IsActive<>1 OR template.PublishedAt IS NULL OR evidenceRecord.LifecycleStatus<>'Active' OR evidenceRecord.VersionStatus<>'Published' OR evidenceRecord.CurrentVersion<>evidenceVersion.VersionNumber OR evidenceVersion.Status<>'Published' OR evidenceVersion.PublishedAt IS NULL
                       OR NOT EXISTS (SELECT 1 FROM WorkflowEntityTypes entityType WHERE entityType.Id=workflow.EntityTypeId AND entityType.TenantId=value.TenantId AND entityType.IsActive=1 AND entityType.IsDeleted=0 AND entityType.Code='PROJECT_QUALITY_TEST')
                       OR NOT EXISTS (SELECT 1 FROM WorkflowSteps step WHERE step.WorkflowDefinitionId=workflow.Id AND step.TenantId=value.TenantId AND step.IsDeleted=0)
                       OR NOT EXISTS (SELECT 1 FROM ProjectMembers member JOIN UserRoles roleAssignment ON roleAssignment.UserId=member.UserId WHERE member.TenantId=value.TenantId AND member.ProjectId=value.ProjectId AND member.UserId=value.ReviewerUserId AND member.IsActive=1 AND member.IsDeleted=0 AND EXISTS (SELECT 1 FROM OPENJSON(decision.ValueJson, '$.reviewerRoleIds') configuredRole WHERE TRY_CONVERT(uniqueidentifier, configuredRole.[value])=roleAssignment.RoleId))))
              ) THROW 52121, 'Civil quality-test tenant, project, DMS, source, reviewer, or CIV-CFG-011 lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilQualityTestReports_Lifecycle ON ProjectCivilQualityTestReports AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id)) THROW 52122, 'Civil quality-test reports cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR ISNULL(value.ProjectPhaseId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectPhaseId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ProjectPackageId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectPackageId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ProjectPaymentCertificateId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ProjectPaymentCertificateId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.SourceBusinessPartnerId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.SourceBusinessPartnerId,'00000000-0000-0000-0000-000000000000') OR value.TestCategory<>prior.TestCategory OR value.SourceType<>prior.SourceType OR value.ReportReference<>prior.ReportReference OR value.TestedAt<>prior.TestedAt OR value.ResultStatus<>prior.ResultStatus OR value.ResultSummary<>prior.ResultSummary OR value.ReviewerUserId<>prior.ReviewerUserId OR value.CentralDocumentRecordId<>prior.CentralDocumentRecordId OR value.CentralDocumentVersionId<>prior.CentralDocumentVersionId OR (((ISNULL(value.EndorsementDocumentRecordId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.EndorsementDocumentRecordId,'00000000-0000-0000-0000-000000000000')) OR (ISNULL(value.EndorsementDocumentVersionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.EndorsementDocumentVersionId,'00000000-0000-0000-0000-000000000000'))) AND NOT (prior.Status='PendingApproval' AND prior.ApprovalStatus='Pending' AND value.Status IN ('Approved','Rejected') AND value.ApprovalStatus IN ('Approved','Rejected'))) OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot OR ISNULL(value.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000') OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.AcceptanceBlocked<>prior.AcceptanceBlocked OR value.IsDeleted<>prior.IsDeleted) THROW 52123, 'Civil quality-test identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.Status='PendingApproval' AND prior.ApprovalStatus='Pending' AND ((value.Status='Approved' AND value.ApprovalStatus='Approved' AND value.ApprovedById IS NOT NULL AND value.ApprovedAt IS NOT NULL AND value.RejectionReason IS NULL) OR (value.Status='Rejected' AND value.ApprovalStatus='Rejected' AND value.ApprovedById IS NULL AND value.ApprovedAt IS NULL AND value.RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(value.RejectionReason)))>0))) OR (value.Status=prior.Status AND value.ApprovalStatus=prior.ApprovalStatus AND ISNULL(value.ApprovedById,'00000000-0000-0000-0000-000000000000')=ISNULL(prior.ApprovedById,'00000000-0000-0000-0000-000000000000') AND ISNULL(value.ApprovedAt,'19000101')=ISNULL(prior.ApprovedAt,'19000101') AND ISNULL(value.RejectionReason,'')=ISNULL(prior.RejectionReason,'')))) THROW 52124, 'Invalid Civil quality-test lifecycle transition.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilQualityTestRevisions_Lineage ON ProjectCivilQualityTestRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilQualityTestReports report ON report.Id=value.TestReportId AND report.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId WHERE report.Id IS NULL OR actor.Id IS NULL) THROW 52124, 'Civil quality-test revision lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilQualityTestRevisions_AppendOnly ON ProjectCivilQualityTestRevisions AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 52124, 'Civil quality-test revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilQualityTestRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilQualityTestRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilQualityTestReports_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilQualityTestReports_Lineage;
            DROP TABLE IF EXISTS ProjectCivilQualityTestRevisions;
            DROP TABLE IF EXISTS ProjectCivilQualityTestReports;
            """);
    }
}
