using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260808124500_AddProjectBoqApprovalPublication")]
public partial class AddProjectBoqApprovalPublication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "WorkflowDefinitionId",
            table: "ProjectBoqVersions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SubmittedById",
            table: "ProjectBoqVersions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "SubmittedAt",
            table: "ProjectBoqVersions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PublishedById",
            table: "ProjectBoqVersions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "PublishedAt",
            table: "ProjectBoqVersions",
            type: "datetime2",
            nullable: true);

        migrationBuilder.DropCheckConstraint(
            name: "CK_ProjectBoqVersions_ApprovalStatus",
            table: "ProjectBoqVersions");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProjectBoqVersions_ApprovalStatus",
            table: "ProjectBoqVersions",
            sql: "[ApprovalStatus] IN ('Draft', 'Pending', 'Approved', 'Rejected')");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProjectBoqVersions_PublishedLifecycle",
            table: "ProjectBoqVersions",
            sql: "[VersionType] <> 2 OR [Status] <> 'Approved' OR ([PublishedAt] IS NOT NULL AND [PublishedById] IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectBoqVersions_TenantId_ProjectId",
            table: "ProjectBoqVersions",
            columns: new[] { "TenantId", "ProjectId" },
            unique: true,
            filter: "[VersionType] = 2 AND [Status] = 'Approved' AND [PublishedAt] IS NOT NULL AND [IsDeleted] = 0");

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProjectBoqVersionLines_ImmutablePublished]
            ON [dbo].[ProjectBoqVersionLines]
            AFTER INSERT, UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM inserted i
                    INNER JOIN [dbo].[ProjectBoqVersions] v ON v.[Id] = i.[ProjectBoqVersionId]
                    WHERE v.[VersionType] = 2 AND v.[PublishedAt] IS NOT NULL
                ) OR EXISTS (
                    SELECT 1
                    FROM deleted d
                    INNER JOIN [dbo].[ProjectBoqVersions] v ON v.[Id] = d.[ProjectBoqVersionId]
                    WHERE v.[VersionType] = 2 AND v.[PublishedAt] IS NOT NULL
                )
                BEGIN
                    THROW 51000, 'Published BoQ version lines are immutable. Create and approve a revision instead.', 1;
                END
            END
            """);

        migrationBuilder.Sql(
            """
            CREATE OR ALTER TRIGGER [dbo].[TR_ProjectBoqVersions_ImmutablePublished]
            ON [dbo].[ProjectBoqVersions]
            AFTER UPDATE, DELETE
            AS
            BEGIN
                SET NOCOUNT ON;

                IF EXISTS (
                    SELECT 1
                    FROM deleted d
                    LEFT JOIN inserted i ON i.[Id] = d.[Id]
                    WHERE d.[VersionType] = 2
                      AND d.[PublishedAt] IS NOT NULL
                      AND (
                          i.[Id] IS NULL
                          OR i.[TenantId] <> d.[TenantId]
                          OR i.[ProjectId] <> d.[ProjectId]
                          OR ISNULL(i.[SourceVersionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SourceVersionId], '00000000-0000-0000-0000-000000000000')
                          OR i.[VersionNumber] <> d.[VersionNumber]
                          OR i.[VersionType] <> d.[VersionType]
                          OR i.[ApprovalStatus] <> d.[ApprovalStatus]
                          OR ISNULL(i.[WorkflowInstanceId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[WorkflowInstanceId], '00000000-0000-0000-0000-000000000000')
                          OR ISNULL(i.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[WorkflowDefinitionId], '00000000-0000-0000-0000-000000000000')
                          OR ISNULL(i.[SubmittedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[SubmittedById], '00000000-0000-0000-0000-000000000000')
                          OR ISNULL(i.[SubmittedAt], '19000101') <> ISNULL(d.[SubmittedAt], '19000101')
                          OR ISNULL(i.[ApprovedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[ApprovedById], '00000000-0000-0000-0000-000000000000')
                          OR ISNULL(i.[ApprovedAt], '19000101') <> ISNULL(d.[ApprovedAt], '19000101')
                          OR ISNULL(i.[PublishedById], '00000000-0000-0000-0000-000000000000') <> ISNULL(d.[PublishedById], '00000000-0000-0000-0000-000000000000')
                          OR ISNULL(i.[PublishedAt], '19000101') <> ISNULL(d.[PublishedAt], '19000101')
                          OR i.[ChangeSummary] <> d.[ChangeSummary]
                          OR i.[SnapshotHash] <> d.[SnapshotHash]
                          OR i.[LineCount] <> d.[LineCount]
                          OR i.[SnapshotAt] <> d.[SnapshotAt]
                          OR i.[IsDeleted] <> d.[IsDeleted]
                          OR (i.[Status] <> d.[Status] AND NOT (d.[Status] = 'Approved' AND i.[Status] = 'Retired'))
                      )
                )
                BEGIN
                    THROW 51000, 'Published BoQ versions are immutable. Only retirement through an approved replacement is permitted.', 1;
                END
            END
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectBoqVersionLines_ImmutablePublished];");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS [dbo].[TR_ProjectBoqVersions_ImmutablePublished];");

        migrationBuilder.DropIndex(
            name: "IX_ProjectBoqVersions_TenantId_ProjectId",
            table: "ProjectBoqVersions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ProjectBoqVersions_PublishedLifecycle",
            table: "ProjectBoqVersions");

        migrationBuilder.DropCheckConstraint(
            name: "CK_ProjectBoqVersions_ApprovalStatus",
            table: "ProjectBoqVersions");

        migrationBuilder.AddCheckConstraint(
            name: "CK_ProjectBoqVersions_ApprovalStatus",
            table: "ProjectBoqVersions",
            sql: "[ApprovalStatus] IN ('Draft', 'PendingApproval', 'Approved', 'Rejected')");

        migrationBuilder.DropColumn(name: "WorkflowDefinitionId", table: "ProjectBoqVersions");
        migrationBuilder.DropColumn(name: "SubmittedById", table: "ProjectBoqVersions");
        migrationBuilder.DropColumn(name: "SubmittedAt", table: "ProjectBoqVersions");
        migrationBuilder.DropColumn(name: "PublishedById", table: "ProjectBoqVersions");
        migrationBuilder.DropColumn(name: "PublishedAt", table: "ProjectBoqVersions");
    }
}
