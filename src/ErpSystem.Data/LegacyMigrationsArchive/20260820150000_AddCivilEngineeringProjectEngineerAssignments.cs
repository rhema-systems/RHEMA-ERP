using System;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Persists the governed Civil Engineering Project Engineer appointment and its immutable revision trail.
/// The Project, membership, user, and frozen CIV-CFG-005 references remain authoritative in their shared owners.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260820150000_AddCivilEngineeringProjectEngineerAssignments")]
public partial class AddCivilEngineeringProjectEngineerAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectCivilProjectEngineerAssignments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectMemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                SourceCivilRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ProjectRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Authority = table.Column<int>(type: "int", nullable: false),
                EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                EffectiveTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                ConfigurationProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ConfigurationDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PolicyHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RequestHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectCivilProjectEngineerAssignments", x => x.Id);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignments_CivilEngineeringConfigurationDecisions_ConfigurationDecisionId", x => x.ConfigurationDecisionId, "CivilEngineeringConfigurationDecisions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignments_CivilEngineeringConfigurationProfiles_ConfigurationProfileId", x => x.ConfigurationProfileId, "CivilEngineeringConfigurationProfiles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignments_ProjectMembers_ProjectMemberId", x => x.ProjectMemberId, "ProjectMembers", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignments_Projects_ProjectId", x => x.ProjectId, "Projects", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignments_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignments_Users_AssignedUserId", x => x.AssignedUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ProjectCivilProjectEngineerAssignmentRevisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AssignmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ActorName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                ActorRoles = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                BeforeJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AfterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectCivilProjectEngineerAssignmentRevisions", x => x.Id);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignmentRevisions_ProjectCivilProjectEngineerAssignments_AssignmentId", x => x.AssignmentId, "ProjectCivilProjectEngineerAssignments", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignmentRevisions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ProjectCivilProjectEngineerAssignmentRevisions_Users_ActorUserId", x => x.ActorUserId, "Users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_AssignedUserId", "ProjectCivilProjectEngineerAssignments", "AssignedUserId");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_ConfigurationDecisionId", "ProjectCivilProjectEngineerAssignments", "ConfigurationDecisionId");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_ConfigurationProfileId", "ProjectCivilProjectEngineerAssignments", "ConfigurationProfileId");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_ProjectMemberId", "ProjectCivilProjectEngineerAssignments", "ProjectMemberId");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_ProjectId", "ProjectCivilProjectEngineerAssignments", "ProjectId");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_TenantId_AssignedUserId_IsActive", "ProjectCivilProjectEngineerAssignments", new[] { "TenantId", "AssignedUserId", "IsActive" });
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_TenantId_ClientRequestId", "ProjectCivilProjectEngineerAssignments", new[] { "TenantId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_TenantId_ProjectId_EffectiveFrom", "ProjectCivilProjectEngineerAssignments", new[] { "TenantId", "ProjectId", "EffectiveFrom" });
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignments_TenantId_ProjectId_IsActive", "ProjectCivilProjectEngineerAssignments", new[] { "TenantId", "ProjectId", "IsActive" }, unique: true, filter: "[IsDeleted] = 0 AND [IsActive] = 1");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignmentRevisions_ActorUserId", "ProjectCivilProjectEngineerAssignmentRevisions", "ActorUserId");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignmentRevisions_AssignmentId", "ProjectCivilProjectEngineerAssignmentRevisions", "AssignmentId");
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignmentRevisions_TenantId_AssignmentId_CreatedAt", "ProjectCivilProjectEngineerAssignmentRevisions", new[] { "TenantId", "AssignmentId", "CreatedAt" });
        migrationBuilder.CreateIndex("IX_ProjectCivilProjectEngineerAssignmentRevisions_TenantId_CorrelationId", "ProjectCivilProjectEngineerAssignmentRevisions", new[] { "TenantId", "CorrelationId" });

        migrationBuilder.Sql("""
            ALTER TABLE ProjectCivilProjectEngineerAssignments ADD CONSTRAINT CK_ProjectCivilProjectEngineerAssignments_Period CHECK ([EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]);
            ALTER TABLE ProjectCivilProjectEngineerAssignments ADD CONSTRAINT CK_ProjectCivilProjectEngineerAssignments_Authority CHECK ([Authority] IN (0,1,2));
            ALTER TABLE ProjectCivilProjectEngineerAssignments ADD CONSTRAINT CK_ProjectCivilProjectEngineerAssignments_ActivePeriod CHECK (([IsActive] = 1 AND [EffectiveTo] IS NULL) OR ([IsActive] = 0 AND [EffectiveTo] IS NOT NULL));
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilProjectEngineerAssignments_Lineage
            ON ProjectCivilProjectEngineerAssignments
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted value
                    LEFT JOIN Projects project ON project.Id = value.ProjectId AND project.TenantId = value.TenantId AND project.IsDeleted = 0
                    LEFT JOIN ProjectMembers member ON member.Id = value.ProjectMemberId AND member.TenantId = value.TenantId AND member.ProjectId = value.ProjectId AND member.UserId = value.AssignedUserId AND member.Role = 'TDC_PROJECT_ENGINEER' AND member.IsDeleted = 0
                    LEFT JOIN Users assignedUser ON assignedUser.Id = value.AssignedUserId AND assignedUser.TenantId = value.TenantId
                    LEFT JOIN CivilEngineeringConfigurationProfiles profile ON profile.Id = value.ConfigurationProfileId AND profile.TenantId = value.TenantId AND profile.IsDeleted = 0
                    LEFT JOIN CivilEngineeringConfigurationDecisions decision ON decision.Id = value.ConfigurationDecisionId AND decision.TenantId = value.TenantId AND decision.ProfileId = value.ConfigurationProfileId AND decision.ConfigurationKey = 'CIV-CFG-005' AND decision.IsDeleted = 0
                    WHERE project.Id IS NULL OR member.Id IS NULL OR assignedUser.Id IS NULL OR profile.Id IS NULL OR decision.Id IS NULL
                       OR (value.IsActive = 1 AND (member.IsActive = 0 OR assignedUser.IsActive = 0))
                       OR (NOT EXISTS (SELECT 1 FROM deleted prior WHERE prior.Id = value.Id)
                           AND (profile.LifecycleStatus <> 1 OR decision.Status <> 2 OR decision.ApprovalStatus <> 1 OR decision.EvidenceStatus <> 2)))
                BEGIN
                    THROW 52001, 'Civil Project Engineer tenant, project membership, active user, or CIV-CFG-005 policy lineage is invalid.', 1;
                END;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilProjectEngineerAssignments_Lifecycle
            ON ProjectCivilProjectEngineerAssignments
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM deleted WHERE NOT EXISTS (SELECT 1 FROM inserted WHERE inserted.Id = deleted.Id))
                    THROW 52002, 'Civil Project Engineer appointments cannot be deleted.', 1;
                IF EXISTS (
                    SELECT 1 FROM inserted value JOIN deleted prior ON prior.Id = value.Id
                    WHERE value.TenantId <> prior.TenantId OR value.ProjectId <> prior.ProjectId
                       OR value.ProjectMemberId <> prior.ProjectMemberId OR value.AssignedUserId <> prior.AssignedUserId
                       OR value.SourceCivilRole <> prior.SourceCivilRole OR value.ProjectRole <> prior.ProjectRole
                       OR value.Authority <> prior.Authority OR value.EffectiveFrom <> prior.EffectiveFrom
                       OR value.ConfigurationProfileId <> prior.ConfigurationProfileId OR value.ConfigurationDecisionId <> prior.ConfigurationDecisionId
                       OR value.PolicyHash <> prior.PolicyHash OR value.ClientRequestId <> prior.ClientRequestId
                       OR value.RequestHash <> prior.RequestHash OR value.CorrelationId <> prior.CorrelationId
                       OR value.IsDeleted <> prior.IsDeleted
                       OR prior.IsActive = 0
                       OR value.IsActive <> 0 OR value.EffectiveTo IS NULL OR value.EffectiveTo < value.EffectiveFrom)
                    THROW 52003, 'Civil Project Engineer appointment identity and lifecycle are immutable; an active appointment may only be ended.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilProjectEngineerAssignmentRevisions_Lineage
            ON ProjectCivilProjectEngineerAssignmentRevisions
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (
                    SELECT 1 FROM inserted value
                    LEFT JOIN ProjectCivilProjectEngineerAssignments assignment ON assignment.Id = value.AssignmentId AND assignment.TenantId = value.TenantId AND assignment.IsDeleted = 0
                    LEFT JOIN Users actor ON actor.Id = value.ActorUserId AND actor.TenantId = value.TenantId
                    WHERE assignment.Id IS NULL OR actor.Id IS NULL)
                    THROW 52004, 'Civil Project Engineer revision tenant, appointment, or actor lineage is invalid.', 1;
            END;
            """);

        migrationBuilder.Sql("""
            CREATE OR ALTER TRIGGER TR_ProjectCivilProjectEngineerAssignmentRevisions_AppendOnly
            ON ProjectCivilProjectEngineerAssignmentRevisions
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;
                THROW 52005, 'Civil Project Engineer appointment revisions are append-only.', 1;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilProjectEngineerAssignmentRevisions_AppendOnly;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilProjectEngineerAssignmentRevisions_Lineage;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilProjectEngineerAssignments_Lifecycle;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS TR_ProjectCivilProjectEngineerAssignments_Lineage;");
        migrationBuilder.DropTable(name: "ProjectCivilProjectEngineerAssignmentRevisions");
        migrationBuilder.DropTable(name: "ProjectCivilProjectEngineerAssignments");
    }
}
