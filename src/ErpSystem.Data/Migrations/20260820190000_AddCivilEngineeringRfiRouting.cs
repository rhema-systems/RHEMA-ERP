using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds controlled Civil RFI routing around the existing ProjectRfis header.
/// It deliberately reuses Projects, Business Partners, Workflow, DMS and AuditLogs.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260820190000_AddCivilEngineeringRfiRouting")]
public partial class AddCivilEngineeringRfiRouting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE ProjectCivilRfiRoutings (
                Id uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL, ProjectRfiId uniqueidentifier NOT NULL,
                ProjectEngineerAssignmentId uniqueidentifier NOT NULL, ProjectManagerUserId uniqueidentifier NOT NULL, ExternalBusinessPartnerId uniqueidentifier NOT NULL,
                ConfigurationProfileId uniqueidentifier NOT NULL, ConfigurationDecisionId uniqueidentifier NOT NULL, WorkflowDefinitionId uniqueidentifier NOT NULL,
                WorkflowInstanceId uniqueidentifier NULL, PolicyHash varchar(64) NOT NULL, Status nvarchar(40) NOT NULL, ApprovalStatus nvarchar(30) NOT NULL,
                ApprovedById uniqueidentifier NULL, ApprovedAt datetime2 NULL, RejectionReason nvarchar(2000) NULL,
                ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL, LastMutationClientRequestId uniqueidentifier NULL, LastMutationRequestHash varchar(64) NULL,
                CorrelationId nvarchar(100) NOT NULL, RowVersion rowversion NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilRfiRoutings_Project FOREIGN KEY (ProjectId) REFERENCES Projects(Id),
                CONSTRAINT FK_ProjectCivilRfiRoutings_Rfi FOREIGN KEY (ProjectRfiId) REFERENCES ProjectRfis(Id),
                CONSTRAINT FK_ProjectCivilRfiRoutings_EngineerAssignment FOREIGN KEY (ProjectEngineerAssignmentId) REFERENCES ProjectCivilProjectEngineerAssignments(Id),
                CONSTRAINT FK_ProjectCivilRfiRoutings_Profile FOREIGN KEY (ConfigurationProfileId) REFERENCES CivilEngineeringConfigurationProfiles(Id),
                CONSTRAINT FK_ProjectCivilRfiRoutings_Decision FOREIGN KEY (ConfigurationDecisionId) REFERENCES CivilEngineeringConfigurationDecisions(Id),
                CONSTRAINT FK_ProjectCivilRfiRoutings_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilRfiRoutings_Status CHECK (Status IN ('AwaitingProjectEngineerResponse','AwaitingProjectManagerApproval','ReturnedToProjectEngineer','Answered','Closed'))
            );
            CREATE UNIQUE INDEX IX_ProjectCivilRfiRoutings_TenantId_ProjectRfiId ON ProjectCivilRfiRoutings(TenantId, ProjectRfiId);
            CREATE UNIQUE INDEX IX_ProjectCivilRfiRoutings_TenantId_ClientRequestId ON ProjectCivilRfiRoutings(TenantId, ClientRequestId);
            CREATE INDEX IX_ProjectCivilRfiRoutings_TenantId_ProjectId_Status ON ProjectCivilRfiRoutings(TenantId, ProjectId, Status);

            CREATE TABLE ProjectCivilRfiEvidence (
                Id uniqueidentifier NOT NULL PRIMARY KEY, RoutingId uniqueidentifier NOT NULL, CentralDocumentRecordId uniqueidentifier NOT NULL,
                CentralDocumentVersionId uniqueidentifier NOT NULL, EvidenceRole nvarchar(40) NOT NULL, LinkedByUserId uniqueidentifier NOT NULL, LinkedAt datetime2 NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilRfiEvidence_Routing FOREIGN KEY (RoutingId) REFERENCES ProjectCivilRfiRoutings(Id),
                CONSTRAINT FK_ProjectCivilRfiEvidence_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilRfiEvidence_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilRfiEvidence_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE UNIQUE INDEX IX_ProjectCivilRfiEvidence_TenantId_RoutingId_CentralDocumentVersionId_EvidenceRole ON ProjectCivilRfiEvidence(TenantId, RoutingId, CentralDocumentVersionId, EvidenceRole);

            CREATE TABLE ProjectCivilRfiResponses (
                Id uniqueidentifier NOT NULL PRIMARY KEY, RoutingId uniqueidentifier NOT NULL, ClientRequestId uniqueidentifier NOT NULL, RequestHash varchar(64) NOT NULL,
                Sequence int NOT NULL, ResponseText nvarchar(4000) NOT NULL, RespondedByUserId uniqueidentifier NOT NULL,
                CentralDocumentRecordId uniqueidentifier NOT NULL, CentralDocumentVersionId uniqueidentifier NOT NULL, CorrelationId nvarchar(100) NOT NULL,
                CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL, CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL,
                CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL, IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilRfiResponses_Routing FOREIGN KEY (RoutingId) REFERENCES ProjectCivilRfiRoutings(Id),
                CONSTRAINT FK_ProjectCivilRfiResponses_Record FOREIGN KEY (CentralDocumentRecordId) REFERENCES CentralDocumentRecords(Id),
                CONSTRAINT FK_ProjectCivilRfiResponses_Version FOREIGN KEY (CentralDocumentVersionId) REFERENCES CentralDocumentVersions(Id),
                CONSTRAINT FK_ProjectCivilRfiResponses_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
                CONSTRAINT CK_ProjectCivilRfiResponses_Sequence CHECK (Sequence > 0)
            );
            CREATE UNIQUE INDEX IX_ProjectCivilRfiResponses_TenantId_RoutingId_Sequence ON ProjectCivilRfiResponses(TenantId, RoutingId, Sequence);
            CREATE UNIQUE INDEX IX_ProjectCivilRfiResponses_TenantId_RoutingId_ClientRequestId ON ProjectCivilRfiResponses(TenantId, RoutingId, ClientRequestId);

            CREATE TABLE ProjectCivilRfiRevisions (
                Id uniqueidentifier NOT NULL PRIMARY KEY, RoutingId uniqueidentifier NOT NULL, Action nvarchar(100) NOT NULL, ActorUserId uniqueidentifier NOT NULL,
                ActorName nvarchar(300) NOT NULL, ActorRoles nvarchar(500) NULL, CorrelationId nvarchar(100) NOT NULL, Reason nvarchar(2000) NULL,
                BeforeJson nvarchar(max) NULL, AfterJson nvarchar(max) NOT NULL, CreatedAt datetime2 NOT NULL, UpdatedAt datetime2 NULL,
                CreatedBy nvarchar(max) NULL, UpdatedBy nvarchar(max) NULL, CreatedById uniqueidentifier NULL, LastModifiedById uniqueidentifier NULL,
                IsDeleted bit NOT NULL, DeletedAt datetime2 NULL, DeletedBy nvarchar(max) NULL, TenantId uniqueidentifier NOT NULL,
                CONSTRAINT FK_ProjectCivilRfiRevisions_Routing FOREIGN KEY (RoutingId) REFERENCES ProjectCivilRfiRoutings(Id),
                CONSTRAINT FK_ProjectCivilRfiRevisions_Tenant FOREIGN KEY (TenantId) REFERENCES Tenants(Id)
            );
            CREATE INDEX IX_ProjectCivilRfiRevisions_TenantId_RoutingId_CreatedAt ON ProjectCivilRfiRevisions(TenantId, RoutingId, CreatedAt);
            CREATE INDEX IX_ProjectCivilRfiRevisions_TenantId_CorrelationId ON ProjectCivilRfiRevisions(TenantId, CorrelationId);
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilRfiRoutings_Lineage ON ProjectCivilRfiRoutings AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (
                SELECT 1 FROM inserted value
                LEFT JOIN Projects project ON project.Id=value.ProjectId AND project.TenantId=value.TenantId AND project.IsDeleted=0 AND project.ExternalPortalAccessEnabled=1
                LEFT JOIN ProjectRfis rfi ON rfi.Id=value.ProjectRfiId AND rfi.ProjectId=value.ProjectId AND rfi.TenantId=value.TenantId AND rfi.IsDeleted=0 AND rfi.CivilDesignCaseId IS NULL
                LEFT JOIN ProjectCivilProjectEngineerAssignments assignment ON assignment.Id=value.ProjectEngineerAssignmentId AND assignment.ProjectId=value.ProjectId AND assignment.TenantId=value.TenantId AND assignment.IsActive=1 AND assignment.IsDeleted=0
                LEFT JOIN Users manager ON manager.Id=value.ProjectManagerUserId AND manager.TenantId=value.TenantId AND manager.IsActive=1
                LEFT JOIN BusinessPartners partner ON partner.Id=value.ExternalBusinessPartnerId AND partner.TenantId=value.TenantId AND partner.IsDeleted=0 AND partner.IsActive=1
                LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id=value.ConfigurationProfileId AND profile.TenantId=value.TenantId AND profile.IsDeleted=0
                LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id=value.ConfigurationDecisionId AND decision.ProfileId=value.ConfigurationProfileId AND decision.TenantId=value.TenantId AND decision.ConfigurationKey='CIV-CFG-005' AND decision.IsDeleted=0
                WHERE project.Id IS NULL OR rfi.Id IS NULL OR assignment.Id IS NULL OR manager.Id IS NULL OR partner.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL
                   OR project.ProjectManagerId<>value.ProjectManagerUserId
                   OR (project.BusinessPartnerId<>value.ExternalBusinessPartnerId AND NOT EXISTS (SELECT 1 FROM ProjectExternalAccessPolicies policy WHERE policy.TenantId=value.TenantId AND policy.ProjectId=value.ProjectId AND policy.BusinessPartnerId=value.ExternalBusinessPartnerId AND policy.IsDeleted=0 AND policy.CanComment=1 AND policy.ArtifactType='Project'))
                   OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id=value.Id) AND (profile.LifecycleStatus<>1 OR decision.Status<>2 OR decision.ApprovalStatus<>1 OR decision.EvidenceStatus<>2)) )
                THROW 52111, 'Civil RFI tenant, project, actor, contractor/consultant, or CIV-CFG-005 lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilRfiRoutings_Lifecycle ON ProjectCivilRfiRoutings AFTER UPDATE, DELETE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id=deleted.Id)) THROW 52112, 'Civil RFI routing cannot be deleted.', 1;
              IF EXISTS (SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id=value.Id WHERE value.TenantId<>prior.TenantId OR value.ProjectId<>prior.ProjectId OR value.ProjectRfiId<>prior.ProjectRfiId OR value.ProjectEngineerAssignmentId<>prior.ProjectEngineerAssignmentId OR value.ProjectManagerUserId<>prior.ProjectManagerUserId OR value.ExternalBusinessPartnerId<>prior.ExternalBusinessPartnerId OR value.ConfigurationProfileId<>prior.ConfigurationProfileId OR value.ConfigurationDecisionId<>prior.ConfigurationDecisionId OR value.WorkflowDefinitionId<>prior.WorkflowDefinitionId OR value.PolicyHash<>prior.PolicyHash OR value.ClientRequestId<>prior.ClientRequestId OR value.RequestHash<>prior.RequestHash OR value.IsDeleted<>prior.IsDeleted)
                THROW 52113, 'Civil RFI routing identity and frozen policy lineage are immutable.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilRfiEvidence_Lineage ON ProjectCivilRfiEvidence AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilRfiRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0 LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 LEFT JOIN Users actor ON actor.Id=value.LinkedByUserId AND actor.TenantId=value.TenantId WHERE routing.Id IS NULL OR version.Id IS NULL OR actor.Id IS NULL)
                THROW 52114, 'Civil RFI DMS evidence lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilRfiResponses_Lineage ON ProjectCivilRfiResponses AFTER INSERT, UPDATE AS
            BEGIN
              SET NOCOUNT ON;
              IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilRfiRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId AND routing.IsDeleted=0 LEFT JOIN Users actor ON actor.Id=value.RespondedByUserId AND actor.TenantId=value.TenantId LEFT JOIN CentralDocumentVersions version ON version.Id=value.CentralDocumentVersionId AND version.DocumentRecordId=value.CentralDocumentRecordId AND version.TenantId=value.TenantId AND version.IsDeleted=0 WHERE routing.Id IS NULL OR actor.Id IS NULL OR version.Id IS NULL)
                THROW 52115, 'Civil RFI response lineage is invalid.', 1;
            END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilRfiResponses_AppendOnly ON ProjectCivilRfiResponses AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 52116, 'Civil RFI responses are append-only.', 1; END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilRfiRevisions_Lineage ON ProjectCivilRfiRevisions AFTER INSERT, UPDATE AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted value LEFT JOIN ProjectCivilRfiRoutings routing ON routing.Id=value.RoutingId AND routing.TenantId=value.TenantId LEFT JOIN Users actor ON actor.Id=value.ActorUserId AND actor.TenantId=value.TenantId WHERE routing.Id IS NULL OR actor.Id IS NULL) THROW 52116, 'Civil RFI revision lineage is invalid.', 1; END;
            """);
        migrationBuilder.Sql("""
            CREATE TRIGGER TR_ProjectCivilRfiRevisions_AppendOnly ON ProjectCivilRfiRevisions AFTER UPDATE, DELETE AS BEGIN SET NOCOUNT ON; THROW 52116, 'Civil RFI revisions are append-only.', 1; END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS TR_ProjectCivilRfiRevisions_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilRfiRevisions_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilRfiResponses_AppendOnly;
            DROP TRIGGER IF EXISTS TR_ProjectCivilRfiResponses_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilRfiEvidence_Lineage;
            DROP TRIGGER IF EXISTS TR_ProjectCivilRfiRoutings_Lifecycle;
            DROP TRIGGER IF EXISTS TR_ProjectCivilRfiRoutings_Lineage;
            DROP TABLE IF EXISTS ProjectCivilRfiRevisions;
            DROP TABLE IF EXISTS ProjectCivilRfiResponses;
            DROP TABLE IF EXISTS ProjectCivilRfiEvidence;
            DROP TABLE IF EXISTS ProjectCivilRfiRoutings;
            """);
    }
}
