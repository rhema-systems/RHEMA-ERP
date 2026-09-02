using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Completes the existing Civil site-instruction envelope with immutable successor
/// lineage, a contractor-response engineering review and controlled closure. The
/// existing Projects instruction, Procurement contract, Business Partner, DMS,
/// Workflow and Audit owners remain authoritative.
/// </summary>
public partial class HardenCivilEngineeringSiteInstructionLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings ADD
              ContractId uniqueidentifier NULL,
              InstructionVersion int NOT NULL CONSTRAINT DF_ProjectCivilSiteInstructionRoutings_InstructionVersion DEFAULT (1),
              SupersedesRoutingId uniqueidentifier NULL,
              ContractorResponseReviewedById uniqueidentifier NULL,
              ContractorResponseReviewedAt datetime2 NULL,
              ContractorResponseReviewReason nvarchar(2000) NULL,
              ClosedById uniqueidentifier NULL,
              ClosedAt datetime2 NULL;
            ALTER TABLE dbo.ProjectCivilSiteInstructionResponses ADD
              RequestHash varchar(64) NOT NULL CONSTRAINT DF_ProjectCivilSiteInstructionResponses_RequestHash DEFAULT ('');
            """);

        // SQL Server compiles a batch before it executes it, so these dependent
        // constraints and indexes must run after the new columns above exist.
        migrationBuilder.Sql("""
            SET ANSI_NULLS ON;
            SET QUOTED_IDENTIFIER ON;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP CONSTRAINT CK_ProjectCivilSiteInstructionRoutings_Status;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings ADD CONSTRAINT CK_ProjectCivilSiteInstructionRoutings_Status
              CHECK (Status IN ('PendingApproval','AwaitingContractorAcknowledgement','ContractorResponded','AwaitingEngineeringReview','AwaitingEngineeringFollowUp','Closed','Rejected','Superseded'));
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings ADD CONSTRAINT CK_ProjectCivilSiteInstructionRoutings_InstructionVersion CHECK (InstructionVersion > 0);
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings ADD CONSTRAINT CK_ProjectCivilSiteInstructionRoutings_NoSelfSupersession CHECK (SupersedesRoutingId IS NULL OR SupersedesRoutingId <> Id);
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings ADD CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_Contracts_ContractId FOREIGN KEY (ContractId) REFERENCES dbo.Contracts(Id);
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings ADD CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_SupersedesRoutingId FOREIGN KEY (SupersedesRoutingId) REFERENCES dbo.ProjectCivilSiteInstructionRoutings(Id);
            CREATE UNIQUE INDEX IX_ProjectCivilSiteInstructionRoutings_TenantId_SupersedesRoutingId
              ON dbo.ProjectCivilSiteInstructionRoutings(TenantId, SupersedesRoutingId)
              WHERE SupersedesRoutingId IS NOT NULL AND IsDeleted = 0;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionRoutings_Lineage ON dbo.ProjectCivilSiteInstructionRoutings AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN dbo.ProjectSiteInstructions instruction ON instruction.Id=value.ProjectSiteInstructionId AND instruction.ProjectId=value.ProjectId AND instruction.TenantId=value.TenantId AND instruction.IsDeleted=0 AND instruction.InstructionType='EngineerInstruction'
                LEFT JOIN dbo.ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.ProjectId=value.ProjectId AND assignment.TenantId=value.TenantId AND assignment.IsDeleted=0
                LEFT JOIN dbo.Users manager ON manager.Id=value.ProjectManagerUserId AND manager.TenantId=value.TenantId AND manager.IsActive=1
                LEFT JOIN dbo.BusinessPartners contractor ON contractor.Id=value.ContractorBusinessPartnerId AND contractor.TenantId=value.TenantId AND contractor.IsDeleted=0
                LEFT JOIN dbo.Contracts contract ON contract.Id=value.ContractId AND contract.TenantId=value.TenantId AND contract.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-005' AND decision.IsDeleted=0
                LEFT JOIN dbo.ProjectCivilSiteInstructionRoutings superseded ON superseded.Id=value.SupersedesRoutingId AND superseded.TenantId=value.TenantId AND superseded.ProjectId=value.ProjectId AND superseded.IsDeleted=0
                WHERE project.Id IS NULL OR instruction.Id IS NULL OR assignment.Id IS NULL OR manager.Id IS NULL OR contractor.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL
                   OR project.ProjectManagerId<>value.ProjectManagerUserId OR project.BusinessPartnerId<>value.ContractorBusinessPartnerId
                   OR (value.ContractId IS NOT NULL AND (contract.Id IS NULL OR contract.Status<>'Active' OR contract.BusinessPartnerId<>value.ContractorBusinessPartnerId))
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND value.ContractId IS NOT NULL AND project.ContractId<>value.ContractId)
                   OR (value.SupersedesRoutingId IS NOT NULL AND (superseded.Id IS NULL OR value.InstructionVersion<>superseded.InstructionVersion+1))
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2)) )
                THROW 52071, 'Civil site-instruction project, contract, contractor, policy, or successor lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionRoutings_Lifecycle ON dbo.ProjectCivilSiteInstructionRoutings AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id))
                THROW 52072, 'Civil site-instruction routing cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectSiteInstructionId<>prior.ProjectSiteInstructionId OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId OR value.ProjectManagerUserId<>prior.ProjectManagerUserId OR value.ContractorBusinessPartnerId<>prior.ContractorBusinessPartnerId OR ISNULL(value.ContractId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ContractId,'00000000-0000-0000-0000-000000000000') OR value.InstructionVersion<>prior.InstructionVersion OR ISNULL(value.SupersedesRoutingId,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.SupersedesRoutingId,'00000000-0000-0000-0000-000000000000') OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted)
                THROW 52073, 'Civil site-instruction routing identity and frozen policy lineage are immutable.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE
                    (prior.Status='PendingApproval' AND value.Status NOT IN ('PendingApproval','AwaitingContractorAcknowledgement','Rejected')) OR
                    (prior.Status IN ('AwaitingContractorAcknowledgement','ContractorResponded') AND value.Status NOT IN ('AwaitingContractorAcknowledgement','AwaitingEngineeringReview','Superseded')) OR
                    (prior.Status='AwaitingEngineeringReview' AND value.Status NOT IN ('AwaitingEngineeringReview','AwaitingEngineeringFollowUp','AwaitingContractorAcknowledgement','Superseded')) OR
                    (prior.Status='AwaitingEngineeringFollowUp' AND value.Status NOT IN ('AwaitingEngineeringFollowUp','AwaitingContractorAcknowledgement','Closed','Superseded')) OR
                    (prior.Status IN ('Closed','Rejected','Superseded') AND value.Status<>prior.Status))
                THROW 52074, 'Invalid Civil site-instruction lifecycle transition.', 1;
              IF EXISTS (
                SELECT 1 FROM inserted value
                JOIN deleted prior ON prior.Id=value.Id
                LEFT JOIN dbo.ProjectCivilProjectEngineerAssignments engineer ON engineer.Id=value.ProjectEngineerAssignmentId AND engineer.TenantId=value.TenantId AND engineer.IsDeleted=0
                WHERE
                  ((prior.Status='AwaitingEngineeringReview' AND value.Status IN ('AwaitingEngineeringFollowUp','AwaitingContractorAcknowledgement'))
                    AND (engineer.Id IS NULL OR value.ContractorResponseReviewedById IS NULL OR value.ContractorResponseReviewedById<>engineer.AssignedUserId OR value.ContractorResponseReviewedAt IS NULL OR LEN(LTRIM(RTRIM(ISNULL(value.ContractorResponseReviewReason,''))))<3))
                  OR
                  ((prior.Status<>'AwaitingEngineeringReview' OR value.Status NOT IN ('AwaitingEngineeringFollowUp','AwaitingContractorAcknowledgement'))
                    AND (ISNULL(value.ContractorResponseReviewedById,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ContractorResponseReviewedById,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ContractorResponseReviewedAt,'19000101')<>ISNULL(prior.ContractorResponseReviewedAt,'19000101') OR ISNULL(value.ContractorResponseReviewReason,'')<>ISNULL(prior.ContractorResponseReviewReason,'')))
                  OR
                  ((prior.Status='AwaitingEngineeringFollowUp' AND value.Status='Closed')
                    AND (engineer.Id IS NULL OR value.ClosedById IS NULL OR value.ClosedById<>engineer.AssignedUserId OR value.ClosedAt IS NULL))
                  OR
                  ((prior.Status<>'AwaitingEngineeringFollowUp' OR value.Status<>'Closed')
                    AND (ISNULL(value.ClosedById,'00000000-0000-0000-0000-000000000000')<>ISNULL(prior.ClosedById,'00000000-0000-0000-0000-000000000000') OR ISNULL(value.ClosedAt,'19000101')<>ISNULL(prior.ClosedAt,'19000101')))
              )
                THROW 52075, 'Civil site-instruction engineering review and closure lineage is invalid or immutable.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionEvidence_Lineage ON dbo.ProjectCivilSiteInstructionEvidence AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.ProjectCivilSiteInstructionRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentRecords document ON document.Id=version.DocumentRecordId AND document.TenantId=value.TenantId AND document.IsDeleted=0
                WHERE routing.Id IS NULL OR version.Id IS NULL OR document.Id IS NULL OR version.Status<>'Published' OR document.LifecycleStatus<>'Active' OR document.VersionStatus<>'Published' OR document.CurrentVersion<>version.VersionNumber OR version.PublishedAt IS NULL OR document.SourceRecordId<>routing.ProjectId)
                THROW 52077, 'Civil site-instruction evidence must be current Published central-DMS evidence linked to the Project.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionResponses_Lineage ON dbo.ProjectCivilSiteInstructionResponses AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.ProjectCivilSiteInstructionRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0
                LEFT JOIN dbo.Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId AND actor.IsActive=1
                LEFT JOIN dbo.ProjectCivilProjectEngineerAssignments engineer ON engineer.Id=routing.ProjectEngineerAssignmentId AND engineer.TenantId=value.TenantId AND engineer.IsDeleted=0
                LEFT JOIN dbo.CentralDocumentVersions version ON value.CentralDocumentVersionId IS NULL OR (version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0)
                LEFT JOIN dbo.CentralDocumentRecords document ON document.Id=version.DocumentRecordId AND document.TenantId=value.TenantId AND document.IsDeleted=0
                WHERE routing.Id IS NULL OR actor.Id IS NULL OR LEN(value.RequestHash)<>64
                   OR (value.CentralDocumentVersionId IS NULL AND value.CentralDocumentRecordId IS NOT NULL)
                   OR (value.CentralDocumentVersionId IS NOT NULL AND (version.Id IS NULL OR document.Id IS NULL OR version.Status<>'Published' OR document.LifecycleStatus<>'Active' OR document.VersionStatus<>'Published' OR document.CurrentVersion<>version.VersionNumber OR version.PublishedAt IS NULL OR NOT (document.SourceRecordId=routing.ProjectId OR document.SourceRecordId=routing.ProjectSiteInstructionId)))
                   OR (value.Action IN ('ContractorAcknowledged','ContractorResponded') AND (value.BusinessPartnerId<>routing.ContractorBusinessPartnerId OR (value.Action='ContractorResponded' AND value.CentralDocumentVersionId IS NULL)))
                   OR (value.Action IN ('EngineeringResponseAccepted','EngineeringResponseReturned','EngineeringFollowUp','InstructionClosed') AND (value.BusinessPartnerId IS NOT NULL OR engineer.AssignedUserId<>value.ActorUserId OR value.CentralDocumentVersionId IS NULL))
                   OR value.Action NOT IN ('ContractorAcknowledged','ContractorResponded','EngineeringResponseAccepted','EngineeringResponseReturned','EngineeringFollowUp','InstructionClosed'))
                THROW 52076, 'Civil site-instruction response, actor, DMS or request lineage is invalid.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilSiteInstructionResponses_Lineage;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilSiteInstructionEvidence_Lineage;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilSiteInstructionRoutings_Lifecycle;
            DROP TRIGGER IF EXISTS dbo.TR_ProjectCivilSiteInstructionRoutings_Lineage;
            DROP INDEX IF EXISTS IX_ProjectCivilSiteInstructionRoutings_TenantId_SupersedesRoutingId ON dbo.ProjectCivilSiteInstructionRoutings;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP CONSTRAINT IF EXISTS FK_ProjectCivilSiteInstructionRoutings_SupersedesRoutingId;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP CONSTRAINT IF EXISTS FK_ProjectCivilSiteInstructionRoutings_Contracts_ContractId;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP CONSTRAINT IF EXISTS CK_ProjectCivilSiteInstructionRoutings_NoSelfSupersession;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP CONSTRAINT IF EXISTS CK_ProjectCivilSiteInstructionRoutings_InstructionVersion;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP CONSTRAINT IF EXISTS CK_ProjectCivilSiteInstructionRoutings_Status;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings ADD CONSTRAINT CK_ProjectCivilSiteInstructionRoutings_Status CHECK (Status IN ('PendingApproval','AwaitingContractorAcknowledgement','ContractorResponded','Closed','Rejected'));
            ALTER TABLE dbo.ProjectCivilSiteInstructionResponses DROP CONSTRAINT IF EXISTS DF_ProjectCivilSiteInstructionResponses_RequestHash;
            ALTER TABLE dbo.ProjectCivilSiteInstructionResponses DROP COLUMN RequestHash;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP CONSTRAINT IF EXISTS DF_ProjectCivilSiteInstructionRoutings_InstructionVersion;
            ALTER TABLE dbo.ProjectCivilSiteInstructionRoutings DROP COLUMN ContractId, InstructionVersion, SupersedesRoutingId, ContractorResponseReviewedById, ContractorResponseReviewedAt, ContractorResponseReviewReason, ClosedById, ClosedAt;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionRoutings_Lineage ON dbo.ProjectCivilSiteInstructionRoutings AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN dbo.Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN dbo.ProjectSiteInstructions instruction ON instruction.Id=value.ProjectSiteInstructionId AND instruction.ProjectId=value.ProjectId AND instruction.TenantId=value.TenantId AND instruction.IsDeleted=0 AND instruction.InstructionType='EngineerInstruction'
                LEFT JOIN dbo.ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.ProjectId=value.ProjectId AND assignment.TenantId=value.TenantId AND assignment.IsDeleted=0
                LEFT JOIN dbo.Users manager ON manager.Id=value.ProjectManagerUserId AND manager.TenantId=value.TenantId AND manager.IsActive=1
                LEFT JOIN dbo.BusinessPartners contractor ON contractor.Id=value.ContractorBusinessPartnerId AND contractor.TenantId=value.TenantId AND contractor.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN dbo.CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-005' AND decision.IsDeleted=0
                WHERE project.Id IS NULL OR instruction.Id IS NULL OR assignment.Id IS NULL OR manager.Id IS NULL OR contractor.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL
                   OR project.ProjectManagerId<>value.ProjectManagerUserId OR project.BusinessPartnerId<>value.ContractorBusinessPartnerId
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2)) )
                THROW 52011, 'Civil site-instruction tenant, Project Engineer, Project Manager, contractor, or CIV-CFG-005 lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionRoutings_Lifecycle ON dbo.ProjectCivilSiteInstructionRoutings AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id)) THROW 52012, 'Civil site-instruction routing cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectSiteInstructionId<>prior.ProjectSiteInstructionId OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId OR value.ProjectManagerUserId<>prior.ProjectManagerUserId OR value.ContractorBusinessPartnerId<>prior.ContractorBusinessPartnerId OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted)
                THROW 52013, 'Civil site-instruction routing identity and frozen policy lineage are immutable.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionEvidence_Lineage ON dbo.ProjectCivilSiteInstructionEvidence AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN dbo.ProjectCivilSiteInstructionRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0 LEFT JOIN dbo.CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 WHERE routing.Id IS NULL OR version.Id IS NULL)
                THROW 52014, 'Civil site-instruction DMS evidence lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER dbo.TR_ProjectCivilSiteInstructionResponses_Lineage ON dbo.ProjectCivilSiteInstructionResponses AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN dbo.ProjectCivilSiteInstructionRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0 LEFT JOIN dbo.Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId LEFT JOIN dbo.CentralDocumentVersions version ON value.CentralDocumentVersionId IS NULL OR (version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0) WHERE routing.Id IS NULL OR actor.Id IS NULL OR (value.BusinessPartnerId IS NOT NULL AND value.BusinessPartnerId<>routing.ContractorBusinessPartnerId) OR (value.CentralDocumentVersionId IS NULL AND value.CentralDocumentRecordId IS NOT NULL) OR (value.CentralDocumentVersionId IS NOT NULL AND version.Id IS NULL) OR value.Action NOT IN ('ContractorAcknowledged','ContractorResponded'))
                THROW 52015, 'Civil site-instruction contractor response lineage is invalid.', 1;
            END;
            """);
    }
}
