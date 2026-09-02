using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0605 controlled Civil historical-data staging; it does not post into owner modules.</summary>
public partial class AddCivilEngineeringMigrationWorkbench : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilMigrationBatches (
              Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
              SourceType int NOT NULL, SourceRegisterReference nvarchar(180) NOT NULL, Status int NOT NULL,
              ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL, ReconciliationEvidenceTemplateId uniqueidentifier NOT NULL,
              ReconciliationEvidenceTemplateCodeSnapshot nvarchar(120) NOT NULL, PolicyHash varchar(64) NOT NULL, RecordCount int NOT NULL, ErrorCount int NOT NULL,
              SubmittedByUserId uniqueidentifier NOT NULL, ReconciledByUserId uniqueidentifier NULL, ReconciledAt datetime2 NULL,
              ReconciliationDocumentRecordId uniqueidentifier NULL, ReconciliationDocumentVersionId uniqueidentifier NULL, ReconciliationDeclaration nvarchar(2000) NULL,
              SignedOffByUserId uniqueidentifier NULL, SignedOffAt datetime2 NULL, SignOffDeclaration nvarchar(2000) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilMigrationBatches_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_Template FOREIGN KEY (ReconciliationEvidenceTemplateId) REFERENCES CentralDocumentMetadataTemplates(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_ReconciliationRecord FOREIGN KEY (ReconciliationDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_ReconciliationVersion FOREIGN KEY (ReconciliationDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_Submitter FOREIGN KEY (SubmittedByUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_Reconciler FOREIGN KEY (ReconciledByUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_SignOff FOREIGN KEY (SignedOffByUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilMigrationBatches_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilMigrationBatches_Status CHECK (Status IN (0,1,2,3)),
              CONSTRAINT CK_ProjectCivilMigrationBatches_Counts CHECK (RecordCount > 0 AND ErrorCount >= 0 AND ErrorCount <= RecordCount),
              CONSTRAINT CK_ProjectCivilMigrationBatches_ReconciliationEvidence CHECK ((ReconciliationDocumentRecordId IS NULL AND ReconciliationDocumentVersionId IS NULL) OR (ReconciliationDocumentRecordId IS NOT NULL AND ReconciliationDocumentVersionId IS NOT NULL)),
              CONSTRAINT CK_ProjectCivilMigrationBatches_State CHECK (
                (Status IN (0,1) AND ReconciledByUserId IS NULL AND ReconciledAt IS NULL AND ReconciliationDocumentRecordId IS NULL AND ReconciliationDocumentVersionId IS NULL AND ReconciliationDeclaration IS NULL AND SignedOffByUserId IS NULL AND SignedOffAt IS NULL AND SignOffDeclaration IS NULL)
                OR (Status=2 AND ReconciledByUserId IS NOT NULL AND ReconciledAt IS NOT NULL AND ReconciliationDocumentRecordId IS NOT NULL AND ReconciliationDocumentVersionId IS NOT NULL AND LEN(LTRIM(RTRIM(ReconciliationDeclaration)))>=5 AND SignedOffByUserId IS NULL AND SignedOffAt IS NULL AND SignOffDeclaration IS NULL)
                OR (Status=3 AND ReconciledByUserId IS NOT NULL AND ReconciledAt IS NOT NULL AND ReconciliationDocumentRecordId IS NOT NULL AND ReconciliationDocumentVersionId IS NOT NULL AND LEN(LTRIM(RTRIM(ReconciliationDeclaration)))>=5 AND SignedOffByUserId IS NOT NULL AND SignedOffAt IS NOT NULL AND LEN(LTRIM(RTRIM(SignOffDeclaration)))>=5)
              )
            );
            CREATE UNIQUE INDEX IX_ProjectCivilMigrationBatches_TenantId_ClientRequestId ON ProjectCivilMigrationBatches(TenantId,ClientRequestId);
            CREATE INDEX IX_ProjectCivilMigrationBatches_TenantId_ProjectId_Status_CreatedAt ON ProjectCivilMigrationBatches(TenantId,ProjectId,Status,CreatedAt);

            CREATE TABLE ProjectCivilMigrationRecords (
              Id uniqueidentifier NOT NULL PRIMARY KEY, MigrationBatchId uniqueidentifier NOT NULL, Sequence int NOT NULL, RecordType int NOT NULL,
              SourceReference nvarchar(180) NOT NULL, Title nvarchar(250) NOT NULL, RecordDate datetime2 NULL, CentralDocumentRecordId uniqueidentifier NULL,
              CentralDocumentVersionId uniqueidentifier NULL, PhysicalFileReference nvarchar(500) NULL, ValidationStatus nvarchar(24) NOT NULL, ValidationMessage nvarchar(2000) NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilMigrationRecords_Batch FOREIGN KEY (MigrationBatchId) REFERENCES ProjectCivilMigrationBatches(Id),
              CONSTRAINT FK_ProjectCivilMigrationRecords_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilMigrationRecords_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilMigrationRecords_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilMigrationRecords_Type CHECK (RecordType IN (0,1,2,3,4,5,6,7)),
              CONSTRAINT CK_ProjectCivilMigrationRecords_Evidence CHECK ((CentralDocumentRecordId IS NULL AND CentralDocumentVersionId IS NULL) OR (CentralDocumentRecordId IS NOT NULL AND CentralDocumentVersionId IS NOT NULL)),
              CONSTRAINT CK_ProjectCivilMigrationRecords_PhysicalReference CHECK ((RecordType=7 AND PhysicalFileReference IS NOT NULL) OR RecordType<>7)
            );
            CREATE UNIQUE INDEX IX_ProjectCivilMigrationRecords_TenantId_BatchId_Sequence ON ProjectCivilMigrationRecords(TenantId,MigrationBatchId,Sequence);
            CREATE UNIQUE INDEX IX_ProjectCivilMigrationRecords_TenantId_BatchId_SourceReference ON ProjectCivilMigrationRecords(TenantId,MigrationBatchId,SourceReference);

            CREATE TABLE ProjectCivilMigrationValidationIssues (
              Id uniqueidentifier NOT NULL PRIMARY KEY, MigrationBatchId uniqueidentifier NOT NULL, MigrationRecordId uniqueidentifier NULL, Sequence int NULL,
              Code nvarchar(80) NOT NULL, Severity nvarchar(24) NOT NULL, Message nvarchar(2000) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilMigrationValidationIssues_Batch FOREIGN KEY (MigrationBatchId) REFERENCES ProjectCivilMigrationBatches(Id),
              CONSTRAINT FK_ProjectCivilMigrationValidationIssues_Record FOREIGN KEY (MigrationRecordId) REFERENCES ProjectCivilMigrationRecords(Id),
              CONSTRAINT FK_ProjectCivilMigrationValidationIssues_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilMigrationValidationIssues_TenantId_BatchId_Sequence_Code ON ProjectCivilMigrationValidationIssues(TenantId,MigrationBatchId,Sequence,Code);

            CREATE TABLE ProjectCivilMigrationRevisions (
              Id uniqueidentifier NOT NULL PRIMARY KEY, MigrationBatchId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
              ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL,
              BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilMigrationRevisions_Batch FOREIGN KEY (MigrationBatchId) REFERENCES ProjectCivilMigrationBatches(Id),
              CONSTRAINT FK_ProjectCivilMigrationRevisions_Actor FOREIGN KEY (ActorUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilMigrationRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilMigrationRevisions_TenantId_BatchId_CreatedAt ON ProjectCivilMigrationRevisions(TenantId,MigrationBatchId,CreatedAt);
            CREATE INDEX IX_ProjectCivilMigrationRevisions_TenantId_CorrelationId ON ProjectCivilMigrationRevisions(TenantId,CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationBatches_Lineage ON ProjectCivilMigrationBatches AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN Users submitter ON submitter.Id=value.SubmittedByUserId AND submitter.TenantId=value.TenantId AND submitter.IsActive=1
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0 AND profile.LifecycleStatus=1
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.IsDeleted=0 AND decision.ConfigurationKey='CIV-CFG-013' AND decision.Status=2 AND decision.ApprovalStatus=1 AND decision.EvidenceStatus=2
                LEFT JOIN CentralDocumentMetadataTemplates template ON template.Id=value.ReconciliationEvidenceTemplateId AND template.TenantId=value.TenantId AND template.IsDeleted=0 AND template.IsActive=1 AND template.PublishedAt IS NOT NULL AND template.TemplateCode=value.ReconciliationEvidenceTemplateCodeSnapshot
                WHERE project.Id IS NULL OR submitter.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL OR template.Id IS NULL
                   OR JSON_VALUE(decision.ValueJson,'$.reconciliationEvidenceTemplateId')<>CONVERT(varchar(36),value.ReconciliationEvidenceTemplateId)
                   OR JSON_VALUE(decision.ValueJson,'$.requireStaging')<>'true' OR JSON_VALUE(decision.ValueJson,'$.requireReconciliation')<>'true' OR JSON_VALUE(decision.ValueJson,'$.requireSignedAcceptance')<>'true'
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(decision.ValueJson,'$.sourceTypes') source WHERE TRY_CONVERT(int,source.value)=value.SourceType OR LOWER(source.value)=CASE value.SourceType WHEN 0 THEN 'spreadsheet' WHEN 1 THEN 'documentregister' WHEN 2 THEN 'shareddrive' WHEN 3 THEN 'physicalfileregister' WHEN 4 THEN 'legacydatabase' END)
              ) THROW 52292, 'Civil migration batch tenant, project, configuration or source lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationBatches_Lifecycle ON ProjectCivilMigrationBatches AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted prior WHERE NOT EXISTS (SELECT 1 FROM inserted value WHERE value.Id=prior.Id)) THROW 52293, 'Civil migration batches cannot be deleted.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.ReconciliationDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published'
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.ReconciliationDocumentVersionId AND version.DocumentRecordId=value.ReconciliationDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL AND record.CurrentVersion=version.VersionNumber
                WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash
                   OR value.SourceType<>prior.SourceType OR value.SourceRegisterReference<>prior.SourceRegisterReference OR value.ConfigurationProfileId<>prior.ConfigurationProfileId
                   OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.ReconciliationEvidenceTemplateId<>prior.ReconciliationEvidenceTemplateId
                   OR value.ReconciliationEvidenceTemplateCodeSnapshot<>prior.ReconciliationEvidenceTemplateCodeSnapshot OR value.PolicyHash<>prior.PolicyHash OR value.RecordCount<>prior.RecordCount OR value.ErrorCount<>prior.ErrorCount OR value.SubmittedByUserId<>prior.SubmittedByUserId OR value.IsDeleted<>prior.IsDeleted
                   OR (prior.Status=1 AND value.Status=2 AND (value.ReconciledByUserId IS NULL OR value.ReconciledByUserId=value.SubmittedByUserId OR value.ReconciledAt IS NULL OR record.Id IS NULL OR version.Id IS NULL))
                   OR (prior.Status=1 AND value.Status=2 AND (value.SignedOffByUserId IS NOT NULL OR value.SignedOffAt IS NOT NULL OR value.SignOffDeclaration IS NOT NULL))
                   OR (prior.Status=2 AND value.Status=3 AND (value.SignedOffByUserId IS NULL OR value.SignedOffByUserId=value.SubmittedByUserId OR value.SignedOffByUserId=value.ReconciledByUserId OR value.SignedOffAt IS NULL OR value.ReconciledByUserId<>prior.ReconciledByUserId OR value.ReconciledAt<>prior.ReconciledAt OR value.ReconciliationDocumentRecordId<>prior.ReconciliationDocumentRecordId OR value.ReconciliationDocumentVersionId<>prior.ReconciliationDocumentVersionId OR value.ReconciliationDeclaration<>prior.ReconciliationDeclaration))
                   OR (prior.Status<>value.Status AND NOT (prior.Status=1 AND value.Status=2) AND NOT (prior.Status=2 AND value.Status=3))
                   OR (prior.Status=value.Status AND (ISNULL(value.ReconciledByUserId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ReconciledByUserId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ReconciledAt,'19000101')<>ISNULL(prior.ReconciledAt,'19000101') OR ISNULL(value.ReconciliationDocumentRecordId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ReconciliationDocumentRecordId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ReconciliationDocumentVersionId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ReconciliationDocumentVersionId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ReconciliationDeclaration,'')<>ISNULL(prior.ReconciliationDeclaration,'') OR ISNULL(value.SignedOffByUserId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.SignedOffByUserId,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.SignedOffAt,'19000101')<>ISNULL(prior.SignedOffAt,'19000101') OR ISNULL(value.SignOffDeclaration,'')<>ISNULL(prior.SignOffDeclaration,'')))
              ) THROW 52294, 'Civil migration batch state, DMS evidence or independent sign-off lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationRecords_Lineage ON ProjectCivilMigrationRecords AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN ProjectCivilMigrationBatches batch ON batch.Id=value.MigrationBatchId AND batch.TenantId=value.TenantId AND batch.IsDeleted=0
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published'
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL AND record.CurrentVersion=version.VersionNumber
                WHERE batch.Id IS NULL OR (value.RecordType<>7 AND version.Id IS NULL AND batch.Status<>0) OR (value.CentralDocumentVersionId IS NOT NULL AND (record.Id IS NULL OR version.Id IS NULL))
              ) THROW 52295, 'Civil migration record, central-DMS or batch lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationRecords_AppendOnly ON ProjectCivilMigrationRecords AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52296, 'Civil migration records are append-only.', 1; END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationValidationIssues_Lineage ON ProjectCivilMigrationValidationIssues AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN ProjectCivilMigrationBatches batch ON batch.Id=value.MigrationBatchId AND batch.TenantId=value.TenantId AND batch.IsDeleted=0
                LEFT JOIN ProjectCivilMigrationRecords record ON record.Id=value.MigrationRecordId AND record.MigrationBatchId=value.MigrationBatchId AND record.TenantId=value.TenantId
                WHERE batch.Id IS NULL OR batch.Status<>0 OR (value.MigrationRecordId IS NOT NULL AND record.Id IS NULL)
              ) THROW 52299, 'Civil migration validation-issue lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationValidationIssues_AppendOnly ON ProjectCivilMigrationValidationIssues AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52300, 'Civil migration validation issues are append-only.', 1; END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationRevisions_Lineage ON ProjectCivilMigrationRevisions AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilMigrationBatches batch ON batch.Id=value.MigrationBatchId AND batch.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1 WHERE batch.Id IS NULL OR actor.Id IS NULL)
                THROW 52297, 'Civil migration revision lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilMigrationRevisions_AppendOnly ON ProjectCivilMigrationRevisions AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52298, 'Civil migration revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationValidationIssues_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationValidationIssues_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationRecords_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationRecords_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationBatches_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilMigrationBatches_Lineage;
            DROP TABLE IF EXISTS ProjectCivilMigrationRevisions;
            DROP TABLE IF EXISTS ProjectCivilMigrationValidationIssues;
            DROP TABLE IF EXISTS ProjectCivilMigrationRecords;
            DROP TABLE IF EXISTS ProjectCivilMigrationBatches;
            """);
    }
}
