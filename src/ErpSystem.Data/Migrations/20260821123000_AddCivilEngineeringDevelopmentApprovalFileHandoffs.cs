using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>CIV-0402 append-only inter-section routing over the immutable CIV-0401 file register.</summary>
public partial class AddCivilEngineeringDevelopmentApprovalFileHandoffs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilDevelopmentApprovalFileHandoffs (
              Id uniqueidentifier NOT NULL PRIMARY KEY, DevelopmentApprovalFileId uniqueidentifier NOT NULL, SequenceNumber int NOT NULL,
              ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, FromSection int NOT NULL, ToSection int NOT NULL,
              FromUserId uniqueidentifier NOT NULL, RecipientRoleId uniqueidentifier NOT NULL, RecipientUserId uniqueidentifier NOT NULL, CoverNote nvarchar(2000) NULL,
              DueDate datetime2 NOT NULL,
              CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileHandoffs_File FOREIGN KEY (DevelopmentApprovalFileId) REFERENCES ProjectCivilDevelopmentApprovalFiles(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileHandoffs_FromUser FOREIGN KEY (FromUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileHandoffs_Role FOREIGN KEY (RecipientRoleId) REFERENCES AspNetRoles(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileHandoffs_Recipient FOREIGN KEY (RecipientUserId) REFERENCES Users(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalFileHandoffs_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalFileHandoffs_Sequence CHECK (SequenceNumber BETWEEN 1 AND 999999),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalFileHandoffs_Sections CHECK (FromSection BETWEEN 0 AND 6 AND ToSection BETWEEN 0 AND 6 AND FromSection<>ToSection),
              CONSTRAINT CK_ProjectCivilDevelopmentApprovalFileHandoffs_DueDate CHECK (CONVERT(date, DueDate)>=CONVERT(date, CreatedAt))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalFileHandoffs_TenantId_ClientRequestId ON ProjectCivilDevelopmentApprovalFileHandoffs(TenantId,ClientRequestId);
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalFileHandoffs_TenantId_FileId_Sequence ON ProjectCivilDevelopmentApprovalFileHandoffs(TenantId,DevelopmentApprovalFileId,SequenceNumber);
            CREATE INDEX IX_ProjectCivilDevelopmentApprovalFileHandoffs_TenantId_FileId_CreatedAt ON ProjectCivilDevelopmentApprovalFileHandoffs(TenantId,DevelopmentApprovalFileId,CreatedAt);

            CREATE TABLE ProjectCivilDevelopmentApprovalHandoffEvidence (
              Id uniqueidentifier NOT NULL PRIMARY KEY, HandoffId uniqueidentifier NOT NULL, CentralDocumentRecordId uniqueidentifier NOT NULL, CentralDocumentVersionId uniqueidentifier NOT NULL,
              CorrelationId nvarchar(100) NOT NULL,
              CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
              IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalHandoffEvidence_Handoff FOREIGN KEY (HandoffId) REFERENCES ProjectCivilDevelopmentApprovalFileHandoffs(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalHandoffEvidence_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalHandoffEvidence_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
              CONSTRAINT FK_ProjectCivilDevelopmentApprovalHandoffEvidence_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE UNIQUE INDEX IX_ProjectCivilDevelopmentApprovalHandoffEvidence_TenantId_HandoffId_VersionId ON ProjectCivilDevelopmentApprovalHandoffEvidence(TenantId,HandoffId,CentralDocumentVersionId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalFileHandoffs_Lineage ON ProjectCivilDevelopmentApprovalFileHandoffs AFTER INSERT AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN ProjectCivilDevelopmentApprovalFiles approvalFile ON approvalFile.Id=value.DevelopmentApprovalFileId AND approvalFile.TenantId=value.TenantId AND approvalFile.IsDeleted=0 AND approvalFile.Status=2
                LEFT JOIN CivilEngineeringConfigurationDecisions permitting ON permitting.Id=approvalFile.PermittingConfigurationDecisionId AND permitting.ProfileId=approvalFile.ConfigurationProfileId AND permitting.TenantId=value.TenantId AND permitting.IsDeleted=0 AND permitting.ConfigurationKey='CIV-CFG-009'
                LEFT JOIN WorkflowDefinitions workflow ON workflow.Id=approvalFile.WorkflowDefinitionId AND workflow.TenantId=value.TenantId AND workflow.IsDeleted=0 AND workflow.IsActive=1 AND workflow.LifecycleStatus=1
                LEFT JOIN WorkflowEntityTypes workflowType ON workflowType.Id=workflow.EntityTypeId AND workflowType.TenantId=value.TenantId AND workflowType.IsDeleted=0 AND workflowType.IsActive=1 AND workflowType.Code='PROJECT_PERMITTING_REVIEW'
                LEFT JOIN Users sender ON sender.Id=value.FromUserId AND sender.TenantId=value.TenantId AND sender.IsActive=1
                LEFT JOIN AspNetRoles role ON role.Id=value.RecipientRoleId
                LEFT JOIN Users recipient ON recipient.Id=value.RecipientUserId AND recipient.TenantId=value.TenantId AND recipient.IsActive=1
                WHERE approvalFile.Id IS NULL OR permitting.Id IS NULL OR workflow.Id IS NULL OR workflowType.Id IS NULL OR sender.Id IS NULL OR role.Id IS NULL OR recipient.Id IS NULL
                   OR JSON_VALUE(permitting.ValueJson,'$.workflowDefinitionId')<>CONVERT(varchar(36),approvalFile.WorkflowDefinitionId)
                   OR CONVERT(date,value.DueDate)>CONVERT(date,approvalFile.DueDate)
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(permitting.ValueJson,'$.handoffRoleIds') allowedRole WHERE TRY_CONVERT(uniqueidentifier,allowedRole.value)=value.RecipientRoleId)
                   OR NOT EXISTS (SELECT 1 FROM UserRoles userRole WHERE userRole.UserId=value.RecipientUserId AND userRole.RoleId=value.RecipientRoleId)
                   OR NOT EXISTS (SELECT 1 FROM UserRoles senderRole CROSS APPLY OPENJSON(permitting.ValueJson,'$.handoffRoleIds') allowedRole WHERE senderRole.UserId=value.FromUserId AND senderRole.RoleId=TRY_CONVERT(uniqueidentifier,allowedRole.value))
                   OR value.SequenceNumber<>(SELECT ISNULL(MAX(prior.SequenceNumber),0)+1 FROM ProjectCivilDevelopmentApprovalFileHandoffs prior WHERE prior.TenantId=value.TenantId AND prior.DevelopmentApprovalFileId=value.DevelopmentApprovalFileId AND prior.Id<>value.Id AND prior.IsDeleted=0)
                   OR (value.SequenceNumber=1 AND value.FromSection<>0)
                   OR (value.SequenceNumber>1 AND NOT EXISTS (SELECT 1 FROM ProjectCivilDevelopmentApprovalFileHandoffs prior WHERE prior.TenantId=value.TenantId AND prior.DevelopmentApprovalFileId=value.DevelopmentApprovalFileId AND prior.SequenceNumber=value.SequenceNumber-1 AND prior.ToSection=value.FromSection AND prior.RecipientUserId=value.FromUserId AND prior.IsDeleted=0))
              ) THROW 52250, 'Civil development-file handoff tenant, routing, recipient, sequence, configuration or site-inspection lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalFileHandoffs_AppendOnly ON ProjectCivilDevelopmentApprovalFileHandoffs AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52251, 'Civil development-file handoffs are append-only.', 1; END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalHandoffEvidence_Lineage ON ProjectCivilDevelopmentApprovalHandoffEvidence AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                JOIN ProjectCivilDevelopmentApprovalFileHandoffs handoff ON handoff.Id=value.HandoffId AND handoff.TenantId=value.TenantId AND handoff.IsDeleted=0
                JOIN ProjectCivilDevelopmentApprovalFiles approvalFile ON approvalFile.Id=handoff.DevelopmentApprovalFileId AND approvalFile.TenantId=value.TenantId AND approvalFile.IsDeleted=0
                LEFT JOIN CentralDocumentRecords record ON record.Id=value.CentralDocumentRecordId AND record.TenantId=value.TenantId AND record.IsDeleted=0 AND record.LifecycleStatus='Active' AND record.VersionStatus='Published' AND record.MetadataTemplateCode=approvalFile.MetadataTemplateCodeSnapshot
                LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 AND version.Status='Published' AND version.PublishedAt IS NOT NULL AND record.CurrentVersion=version.VersionNumber
                LEFT JOIN CivilEngineeringConfigurationDecisions documentPolicy ON documentPolicy.Id=approvalFile.DocumentConfigurationDecisionId AND documentPolicy.ProfileId=approvalFile.ConfigurationProfileId AND documentPolicy.TenantId=value.TenantId AND documentPolicy.IsDeleted=0 AND documentPolicy.ConfigurationKey='CIV-CFG-004'
                WHERE record.Id IS NULL OR version.Id IS NULL OR documentPolicy.Id IS NULL OR JSON_VALUE(documentPolicy.ValueJson,'$.requireVersioning')<>'true'
                   OR version.FileSize IS NULL OR version.FileSize<=0 OR version.FileSize>TRY_CONVERT(bigint,JSON_VALUE(documentPolicy.ValueJson,'$.maximumFileSizeMb'))*1048576
                   OR NOT EXISTS (SELECT 1 FROM OPENJSON(documentPolicy.ValueJson,'$.allowedFileExtensions') extension WHERE LOWER(extension.value)=LOWER(RIGHT(ISNULL(version.FileName,''),CHARINDEX('.',REVERSE(ISNULL(version.FileName,''))))))
              ) THROW 52252, 'Civil development-file handoff evidence must be a current Published tenant DMS document under the frozen file policy.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilDevelopmentApprovalHandoffEvidence_AppendOnly ON ProjectCivilDevelopmentApprovalHandoffEvidence AFTER UPDATE, DELETE AS
            BEGIN SET NOCOUNT ON; THROW 52253, 'Civil development-file handoff evidence is append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalHandoffEvidence_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalHandoffEvidence_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalFileHandoffs_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilDevelopmentApprovalFileHandoffs_Lineage;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalHandoffEvidence;
            DROP TABLE IF EXISTS ProjectCivilDevelopmentApprovalFileHandoffs;
            """);
    }
}
