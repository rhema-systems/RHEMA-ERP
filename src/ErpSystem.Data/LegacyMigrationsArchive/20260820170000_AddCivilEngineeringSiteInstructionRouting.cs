using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds a Civil governance envelope around the pre-existing Projects site-instruction owner.
/// It never duplicates the instruction, contractor, document, workflow, or project records.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260820170000_AddCivilEngineeringSiteInstructionRouting")]
public partial class AddCivilEngineeringSiteInstructionRouting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilSiteInstructionRoutings (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, ProjectSiteInstructionId uniqueidentifier NOT NULL,
                ProjectEngineerAssignmentId uniqueidentifier NOT NULL, ProjectManagerUserId uniqueidentifier NOT NULL, ContractorBusinessPartnerId uniqueidentifier NOT NULL,
                ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL, WorkflowDefinitionId uniqueidentifier NOT NULL,
                WorkflowInstanceId uniqueidentifier NULL, PolicyHash varchar(64) NOT NULL, Status nvarchar(40) NOT NULL, ApprovalStatus nvarchar(30) NOT NULL,
                ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL,
                LastMutationRequestHash varchar(64) NULL, CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_Projects_ProjectId FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_ProjectSiteInstructions_ProjectSiteInstructionId FOREIGN KEY (ProjectSiteInstructionId) REFERENCES ProjectSiteInstructions(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_ProjectEngineerAssignments_ProjectEngineerAssignmentId FOREIGN KEY (ProjectEngineerAssignmentId) REFERENCES ProjectCivilProjectEngineerAssignments(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_CivilProfiles_ConfigurationProfileId FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_CivilDecisions_ConfigurationDecisionId FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionRoutings_Tenants_TenantId FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilSiteInstructionRoutings_Status CHECK (Status IN ('PendingApproval','AwaitingContractorAcknowledgement','ContractorResponded','Closed','Rejected'))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilSiteInstructionRoutings_TenantId_ProjectSiteInstructionId ON ProjectCivilSiteInstructionRoutings(TenantId, ProjectSiteInstructionId);
            CREATE UNIQUE INDEX IX_ProjectCivilSiteInstructionRoutings_TenantId_ClientRequestId ON ProjectCivilSiteInstructionRoutings(TenantId, ClientRequestId);
            CREATE INDEX IX_ProjectCivilSiteInstructionRoutings_TenantId_ProjectId_Status ON ProjectCivilSiteInstructionRoutings(TenantId, ProjectId, Status);

            CREATE TABLE ProjectCivilSiteInstructionEvidence (
                Id uniqueidentifier NOT NULL PRIMARY KEY, RoutingId uniqueidentifier NOT NULL, CentralDocumentRecordId uniqueidentifier NOT NULL,
                CentralDocumentVersionId uniqueidentifier NOT NULL, EvidenceRole nvarchar(40) NOT NULL, LinkedByUserId uniqueidentifier NOT NULL,
                LinkedAt datetime2 NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL,
                UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL,
                DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilSiteInstructionEvidence_Routing FOREIGN KEY (RoutingId) REFERENCES ProjectCivilSiteInstructionRoutings(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionEvidence_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionEvidence_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionEvidence_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE UNIQUE INDEX IX_ProjectCivilSiteInstructionEvidence_TenantId_RoutingId_CentralDocumentVersionId_EvidenceRole ON ProjectCivilSiteInstructionEvidence(TenantId, RoutingId, CentralDocumentVersionId, EvidenceRole);

            CREATE TABLE ProjectCivilSiteInstructionResponses (
                Id uniqueidentifier NOT NULL PRIMARY KEY, RoutingId uniqueidentifier NOT NULL, ClientRequestId uniqueidentifier NOT NULL, Sequence int NOT NULL,
                Action nvarchar(40) NOT NULL, Message nvarchar(4000) NOT NULL, ActorUserId uniqueidentifier NOT NULL, BusinessPartnerId uniqueidentifier NULL,
                CentralDocumentRecordId uniqueidentifier NULL, CentralDocumentVersionId uniqueidentifier NULL, CorrelationId nvarchar(100) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL,
                DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilSiteInstructionResponses_Routing FOREIGN KEY (RoutingId) REFERENCES ProjectCivilSiteInstructionRoutings(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionResponses_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionResponses_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionResponses_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilSiteInstructionResponses_Sequence CHECK (Sequence > 0)
            );
            CREATE UNIQUE INDEX IX_ProjectCivilSiteInstructionResponses_TenantId_RoutingId_Sequence ON ProjectCivilSiteInstructionResponses(TenantId, RoutingId, Sequence);
            CREATE UNIQUE INDEX IX_ProjectCivilSiteInstructionResponses_TenantId_RoutingId_ClientRequestId ON ProjectCivilSiteInstructionResponses(TenantId, RoutingId, ClientRequestId);

            CREATE TABLE ProjectCivilSiteInstructionRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, RoutingId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
                ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL,
                BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilSiteInstructionRevisions_Routing FOREIGN KEY (RoutingId) REFERENCES ProjectCivilSiteInstructionRoutings(Id),
                CONSTRAINT FK_ProjectCivilSiteInstructionRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilSiteInstructionRevisions_TenantId_RoutingId_CreatedAt ON ProjectCivilSiteInstructionRevisions(TenantId, RoutingId, CreatedAt);
            CREATE INDEX IX_ProjectCivilSiteInstructionRevisions_TenantId_CorrelationId ON ProjectCivilSiteInstructionRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilSiteInstructionRoutings_Lineage ON ProjectCivilSiteInstructionRoutings AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0
                LEFT JOIN ProjectSiteInstructions instruction ON instruction.Id=value.ProjectSiteInstructionId AND instruction.ProjectId=value.ProjectId AND instruction.TenantId=value.TenantId AND instruction.IsDeleted=0 AND instruction.InstructionType='EngineerInstruction'
                LEFT JOIN ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.ProjectId=value.ProjectId AND assignment.TenantId=value.TenantId AND assignment.IsDeleted=0
                LEFT JOIN Users manager ON manager.Id=value.ProjectManagerUserId AND manager.TenantId=value.TenantId AND manager.IsActive=1
                LEFT JOIN BusinessPartners contractor ON contractor.Id=value.ContractorBusinessPartnerId AND contractor.TenantId=value.TenantId AND contractor.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-005' AND decision.IsDeleted=0
                WHERE project.Id IS NULL OR instruction.Id IS NULL OR assignment.Id IS NULL OR manager.Id IS NULL OR contractor.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL
                   OR project.ProjectManagerId<>value.ProjectManagerUserId OR project.BusinessPartnerId<>value.ContractorBusinessPartnerId
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2)) )
                THROW 52011, 'Civil site-instruction tenant, Project Engineer, Project Manager, contractor, or CIV-CFG-005 lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilSiteInstructionRoutings_Lifecycle ON ProjectCivilSiteInstructionRoutings AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id)) THROW 52012, 'Civil site-instruction routing cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectSiteInstructionId<>prior.ProjectSiteInstructionId OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId OR value.ProjectManagerUserId<>prior.ProjectManagerUserId OR value.ContractorBusinessPartnerId<>prior.ContractorBusinessPartnerId OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted)
                THROW 52013, 'Civil site-instruction routing identity and frozen policy lineage are immutable.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilSiteInstructionEvidence_Lineage ON ProjectCivilSiteInstructionEvidence AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilSiteInstructionRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0 LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 WHERE routing.Id IS NULL OR version.Id IS NULL)
                THROW 52014, 'Civil site-instruction DMS evidence lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilSiteInstructionResponses_Lineage ON ProjectCivilSiteInstructionResponses AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilSiteInstructionRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0 LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId LEFT JOIN CentralDocumentVersions version ON value.CentralDocumentVersionId IS NULL OR (version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0) WHERE routing.Id IS NULL OR actor.Id IS NULL OR (value.BusinessPartnerId IS NOT NULL AND value.BusinessPartnerId<>routing.ContractorBusinessPartnerId) OR (value.CentralDocumentVersionId IS NULL AND value.CentralDocumentRecordId IS NOT NULL) OR (value.CentralDocumentVersionId IS NOT NULL AND version.Id IS NULL) OR value.Action NOT IN ('ContractorAcknowledged','ContractorResponded'))
                THROW 52015, 'Civil site-instruction contractor response lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilSiteInstructionResponses_AppendOnly ON ProjectCivilSiteInstructionResponses AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 52016, 'Civil site-instruction contractor responses are append-only.', 1; END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilSiteInstructionRevisions_Lineage ON ProjectCivilSiteInstructionRevisions AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilSiteInstructionRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId WHERE routing.Id IS NULL OR actor.Id IS NULL) THROW 52016, 'Civil site-instruction revision lineage is invalid.', 1; END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilSiteInstructionRevisions_AppendOnly ON ProjectCivilSiteInstructionRevisions AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 52016, 'Civil site-instruction revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilSiteInstructionRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilSiteInstructionRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilSiteInstructionResponses_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilSiteInstructionResponses_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilSiteInstructionEvidence_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilSiteInstructionRoutings_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilSiteInstructionRoutings_Lineage;
            DROP TABLE IF EXISTS ProjectCivilSiteInstructionRevisions;
            DROP TABLE IF EXISTS ProjectCivilSiteInstructionResponses;
            DROP TABLE IF EXISTS ProjectCivilSiteInstructionEvidence;
            DROP TABLE IF EXISTS ProjectCivilSiteInstructionRoutings;
            """);
    }
}
