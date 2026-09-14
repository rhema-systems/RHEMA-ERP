using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the Projects-owned Civil weekly-supervision register. DMS files, Workflow execution,
/// notification delivery and audit logging remain owned by their central services.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260820213000_AddCivilEngineeringWeeklySupervisionReports")]
public partial class AddCivilEngineeringWeeklySupervisionReports : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilWeeklySupervisionReports (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, ProjectEngineerAssignmentId uniqueidentifier NOT NULL,
                WeekStart datetime2 NOT NULL, WeekEnd datetime2 NOT NULL, OverallProgressPercent decimal(18,2) NULL,
                MaterialUsageSummary nvarchar(4000) NULL, SafetyNotes nvarchar(4000) NULL, TestSummary nvarchar(4000) NULL,
                DueAt datetime2 NOT NULL, EscalatedAt datetime2 NULL, ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL,
                WorkflowDefinitionId uniqueidentifier NOT NULL, EvidenceMetadataTemplateId uniqueidentifier NOT NULL, EvidenceMetadataTemplateCodeSnapshot varchar(80) NOT NULL,
                WorkflowInstanceId uniqueidentifier NULL, PolicyHash varchar(64) NOT NULL, Status nvarchar(30) NOT NULL, ApprovalStatus nvarchar(30) NOT NULL,
                ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL, LastMutationRequestHash varchar(64) NULL,
                CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Assignment FOREIGN KEY (ProjectEngineerAssignmentId) REFERENCES ProjectCivilProjectEngineerAssignments(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Template FOREIGN KEY (EvidenceMetadataTemplateId) REFERENCES CentralDocumentMetadataTemplates(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Approver FOREIGN KEY (ApprovedById) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReports_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilWeeklySupervisionReports_Week CHECK (DATEDIFF(day, WeekStart, WeekEnd) = 6 AND DATEDIFF(day, CONVERT(date, '19000101', 112), CONVERT(date, WeekStart)) % 7 = 0),
                CONSTRAINT CK_ProjectCivilWeeklySupervisionReports_Status CHECK ((Status='PendingApproval' AND ApprovalStatus='Pending' AND ApprovedById IS NULL AND ApprovedAt IS NULL AND RejectionReason IS NULL) OR (Status='Approved' AND ApprovalStatus='Approved' AND ApprovedById IS NOT NULL AND ApprovedAt IS NOT NULL AND RejectionReason IS NULL) OR (Status='Rejected' AND ApprovalStatus='Rejected' AND ApprovedById IS NULL AND ApprovedAt IS NULL AND RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(RejectionReason)))>0))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilWeeklySupervisionReports_TenantId_ClientRequestId ON ProjectCivilWeeklySupervisionReports(TenantId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilWeeklySupervisionReports_TenantId_ProjectId_WeekStart ON ProjectCivilWeeklySupervisionReports(TenantId, ProjectId, WeekStart) WHERE IsDeleted=0;
            CREATE INDEX IX_ProjectCivilWeeklySupervisionReports_TenantId_Status_DueAt ON ProjectCivilWeeklySupervisionReports(TenantId, Status, DueAt);

            CREATE TABLE ProjectCivilWeeklySupervisionActivities (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ReportId uniqueidentifier NOT NULL, Sequence int NOT NULL, ActivityCategoryId uniqueidentifier NOT NULL,
                ActorType varchar(30) NOT NULL, ContractorBusinessPartnerId uniqueidentifier NULL, Description nvarchar(4000) NOT NULL, ProgressPercent decimal(18,2) NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilWeeklySupervisionActivities_Report FOREIGN KEY (ReportId) REFERENCES ProjectCivilWeeklySupervisionReports(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionActivities_Category FOREIGN KEY (ActivityCategoryId) REFERENCES ProjectCatalogEntries(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionActivities_Contractor FOREIGN KEY (ContractorBusinessPartnerId) REFERENCES BusinessPartners(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionActivities_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilWeeklySupervisionActivities_Actor CHECK (ActorType IN ('Contractor','LabourGang') AND ((ActorType='Contractor' AND ContractorBusinessPartnerId IS NOT NULL) OR (ActorType='LabourGang' AND ContractorBusinessPartnerId IS NULL))),
                CONSTRAINT CK_ProjectCivilWeeklySupervisionActivities_Progress CHECK (ProgressPercent IS NULL OR (ProgressPercent>=0 AND ProgressPercent<=100))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilWeeklySupervisionActivities_TenantId_ReportId_Sequence ON ProjectCivilWeeklySupervisionActivities(TenantId, ReportId, Sequence);

            CREATE TABLE ProjectCivilWeeklySupervisionEvidence (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ReportId uniqueidentifier NOT NULL, CentralDocumentRecordId uniqueidentifier NOT NULL, CentralDocumentVersionId uniqueidentifier NOT NULL,
                EvidenceRole varchar(30) NOT NULL, LinkedByUserId uniqueidentifier NOT NULL, LinkedAt datetime2 NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilWeeklySupervisionEvidence_Report FOREIGN KEY (ReportId) REFERENCES ProjectCivilWeeklySupervisionReports(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionEvidence_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionEvidence_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionEvidence_Linker FOREIGN KEY (LinkedByUserId) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionEvidence_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilWeeklySupervisionEvidence_Role CHECK (EvidenceRole IN ('Report','Photo','Test','Review'))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilWeeklySupervisionEvidence_TenantId_ReportId_CentralDocumentVersionId ON ProjectCivilWeeklySupervisionEvidence(TenantId, ReportId, CentralDocumentVersionId);

            CREATE TABLE ProjectCivilWeeklySupervisionReviews (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ReportId uniqueidentifier NOT NULL, ClientRequestId uniqueidentifier NOT NULL, Sequence int NOT NULL,
                Action varchar(30) NOT NULL, Outcome varchar(30) NOT NULL, Comment nvarchar(2000) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
                CentralDocumentRecordId uniqueidentifier NULL, CentralDocumentVersionId uniqueidentifier NULL, CorrelationId nvarchar(100) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReviews_Report FOREIGN KEY (ReportId) REFERENCES ProjectCivilWeeklySupervisionReports(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReviews_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReviews_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReviews_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionReviews_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilWeeklySupervisionReviews_Evidence CHECK ((CentralDocumentRecordId IS NULL AND CentralDocumentVersionId IS NULL) OR (CentralDocumentRecordId IS NOT NULL AND CentralDocumentVersionId IS NOT NULL))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilWeeklySupervisionReviews_TenantId_ReportId_ClientRequestId ON ProjectCivilWeeklySupervisionReviews(TenantId, ReportId, ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilWeeklySupervisionReviews_TenantId_ReportId_Sequence ON ProjectCivilWeeklySupervisionReviews(TenantId, ReportId, Sequence);

            CREATE TABLE ProjectCivilWeeklySupervisionRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ReportId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
                ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL,
                BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilWeeklySupervisionRevisions_Report FOREIGN KEY (ReportId) REFERENCES ProjectCivilWeeklySupervisionReports(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
                CONSTRAINT FK_ProjectCivilWeeklySupervisionRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilWeeklySupervisionRevisions_TenantId_ReportId_CreatedAt ON ProjectCivilWeeklySupervisionRevisions(TenantId, ReportId, CreatedAt);
            CREATE INDEX IX_ProjectCivilWeeklySupervisionRevisions_TenantId_CorrelationId ON ProjectCivilWeeklySupervisionRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilWeeklySupervisionReports_Lineage ON ProjectCivilWeeklySupervisionReports AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.ProjectId=value.ProjectId AND assignment.TenantId=value.TenantId AND assignment.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-006' AND decision.IsDeleted=0
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=value.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0
                LEFT JOIN CentralDocumentMetadataTemplates template ON template.Id=value.EvidenceMetadataTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0
                WHERE project.Id IS NULL OR assignment.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR workflow.Id IS NULL OR template.Id IS NULL
                   OR value.EvidenceMetadataTemplateCodeSnapshot<>template.TemplateCode
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (assignment.IsActive<>1 OR profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2 OR workflow.IsActive<>1 OR workflow.LifecycleStatus<>1 OR template.IsActive<>1 OR template.PublishedAt IS NULL
                       OR NOT EXISTS (SELECT 1 FROM WorkflowEntityTypes entityType WHERE entityType.Id=workflow.EntityTypeId AND entityType.TenantId=value.TenantId AND entityType.IsActive=1 AND entityType.IsDeleted=0 AND entityType.Code='PROJECT_WEEKLY_REPORT')
                       OR (SELECT COUNT(*) FROM WorkflowSteps step WHERE step.WorkflowDefinitionId=workflow.Id AND step.TenantId=value.TenantId AND step.IsDeleted=0)<2))
              ) THROW 52130, 'Civil weekly-supervision tenant, Project Engineer, workflow, template, or CIV-CFG-006 lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilWeeklySupervisionReports_Lifecycle ON ProjectCivilWeeklySupervisionReports AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52131, 'Civil weekly-supervision reports cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId OR value.WeekStart<>prior.WeekStart OR value.WeekEnd<>prior.WeekEnd OR ISNULL(value.OverallProgressPercent,-1)<>ISNULL(prior.OverallProgressPercent,-1) OR ISNULL(value.MaterialUsageSummary,'')<>ISNULL(prior.MaterialUsageSummary,'') OR ISNULL(value.SafetyNotes,'')<>ISNULL(prior.SafetyNotes,'') OR ISNULL(value.TestSummary,'')<>ISNULL(prior.TestSummary,'') OR value.DueAt<>prior.DueAt OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.EvidenceMetadataTemplateId<>prior.EvidenceMetadataTemplateId OR value.EvidenceMetadataTemplateCodeSnapshot<>prior.EvidenceMetadataTemplateCodeSnapshot OR ISNULL(value.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.WorkflowInstanceId,'00000000-0000-0000-0000-000000000000') OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted) THROW 52132, 'Civil weekly-supervision identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.Status='PendingApproval' AND prior.ApprovalStatus='Pending' AND ((value.Status='PendingApproval' AND value.ApprovalStatus='Pending' AND value.ApprovedById IS NULL AND value.ApprovedAt IS NULL AND value.RejectionReason IS NULL) OR (value.Status='Approved' AND value.ApprovalStatus='Approved' AND value.ApprovedById IS NOT NULL AND value.ApprovedAt IS NOT NULL AND value.RejectionReason IS NULL) OR (value.Status='Rejected' AND value.ApprovalStatus='Rejected' AND value.ApprovedById IS NULL AND value.ApprovedAt IS NULL AND value.RejectionReason IS NOT NULL AND LEN(LTRIM(RTRIM(value.RejectionReason)))>0))) OR (value.Status=prior.Status AND value.ApprovalStatus=prior.ApprovalStatus AND ISNULL(value.ApprovedById,'00000000-0000-0000-0000-000000000000')=ISNULL(prior.ApprovedById,'00000000-0000-0000-0000-000000000000') AND ISNULL(value.ApprovedAt,'19000101')=ISNULL(prior.ApprovedAt,'19000101') AND ISNULL(value.RejectionReason,'')=ISNULL(prior.RejectionReason,'')))) THROW 52133, 'Invalid Civil weekly-supervision lifecycle transition.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE NOT ((prior.EscalatedAt IS NULL AND value.EscalatedAt IS NULL) OR (prior.EscalatedAt IS NULL AND value.EscalatedAt IS NOT NULL AND value.Status='PendingApproval' AND value.ApprovalStatus='Pending') OR (prior.EscalatedAt=value.EscalatedAt))) THROW 52134, 'Civil weekly-supervision overdue escalation is immutable once queued.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilWeeklySupervisionActivities_Lineage ON ProjectCivilWeeklySupervisionActivities AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) OR EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id) THROW 52135, 'Civil weekly-supervision activities are append-only.', 1;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilWeeklySupervisionReports report ON report.Id=value.ReportId AND report.TenantId=value.TenantId LEFT JOIN ProjectCatalogEntries category ON category.Id=value.ActivityCategoryId AND category.TenantId=value.TenantId AND category.IsActive=1 AND category.IsDeleted=0 AND category.CatalogType='civil-weekly-activity-categories' LEFT JOIN BusinessPartners contractor ON contractor.Id=value.ContractorBusinessPartnerId AND contractor.TenantId=value.TenantId WHERE report.Id IS NULL OR category.Id IS NULL OR value.Sequence<1 OR (value.ActorType='Contractor' AND (contractor.Id IS NULL OR contractor.IsDeleted=1 OR contractor.IsActive<>1 OR contractor.IsBlacklisted=1))) THROW 52136, 'Civil weekly-supervision activity lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilWeeklySupervisionEvidence_Lineage ON ProjectCivilWeeklySupervisionEvidence AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) OR EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id) THROW 52137, 'Civil weekly-supervision evidence is append-only.', 1;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilWeeklySupervisionReports report ON report.Id=value.ReportId AND report.TenantId=value.TenantId LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published' LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL LEFT JOIN Users linker ON linker.Id=value.LinkedByUserId AND linker.TenantId=value.TenantId AND linker.IsActive=1 WHERE report.Id IS NULL OR record.Id IS NULL OR version.Id IS NULL OR linker.Id IS NULL OR record.CurrentVersion<>version.VersionNumber OR record.MetadataTemplateCode<>report.EvidenceMetadataTemplateCodeSnapshot) THROW 52138, 'Civil weekly-supervision DMS evidence lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilWeeklySupervisionReviews_Lineage ON ProjectCivilWeeklySupervisionReviews AFTER INSERT, UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) OR EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id) THROW 52139, 'Civil weekly-supervision reviews are append-only.', 1;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilWeeklySupervisionReports report ON report.Id=value.ReportId AND report.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published' LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL WHERE report.Id IS NULL OR actor.Id IS NULL OR value.Sequence<1 OR (value.CentralDocumentVersionId IS NOT NULL AND (record.Id IS NULL OR version.Id IS NULL OR record.CurrentVersion<>version.VersionNumber OR record.MetadataTemplateCode<>report.EvidenceMetadataTemplateCodeSnapshot))) THROW 52140, 'Civil weekly-supervision review lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilWeeklySupervisionRevisions_Lineage ON ProjectCivilWeeklySupervisionRevisions AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilWeeklySupervisionReports report ON report.Id=value.ReportId AND report.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId WHERE report.Id IS NULL OR actor.Id IS NULL) THROW 52141, 'Civil weekly-supervision revision lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilWeeklySupervisionRevisions_AppendOnly ON ProjectCivilWeeklySupervisionRevisions AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 52142, 'Civil weekly-supervision revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilWeeklySupervisionRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilWeeklySupervisionRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilWeeklySupervisionReviews_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilWeeklySupervisionEvidence_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilWeeklySupervisionActivities_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilWeeklySupervisionReports_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilWeeklySupervisionReports_Lineage;
            DROP TABLE IF EXISTS ProjectCivilWeeklySupervisionRevisions;
            DROP TABLE IF EXISTS ProjectCivilWeeklySupervisionReviews;
            DROP TABLE IF EXISTS ProjectCivilWeeklySupervisionEvidence;
            DROP TABLE IF EXISTS ProjectCivilWeeklySupervisionActivities;
            DROP TABLE IF EXISTS ProjectCivilWeeklySupervisionReports;
            """);
    }
}
