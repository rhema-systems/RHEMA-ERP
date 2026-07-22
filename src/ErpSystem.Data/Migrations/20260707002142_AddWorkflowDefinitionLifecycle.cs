using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDefinitionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_TenantId_Name",
                table: "WorkflowDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "ChangeSummary",
                table: "WorkflowDefinitions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefinitionKey",
                table: "WorkflowDefinitions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "LifecycleStatus",
                table: "WorkflowDefinitions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "WorkflowDefinitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedById",
                table: "WorkflowDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetiredAt",
                table: "WorkflowDefinitions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RetiredById",
                table: "WorkflowDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesDefinitionId",
                table: "WorkflowDefinitions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [WorkflowDefinitions]
                SET [DefinitionKey] = [Id],
                    [LifecycleStatus] = CASE
                        WHEN [IsActive] = 1 THEN 1
                        WHEN EXISTS (
                            SELECT 1
                            FROM [WorkflowInstances] wi
                            WHERE wi.[WorkflowDefinitionId] = [WorkflowDefinitions].[Id]
                        ) THEN 2
                        ELSE 0
                    END,
                    [PublishedAt] = CASE
                        WHEN [IsActive] = 1 THEN COALESCE([UpdatedAt], [CreatedAt])
                        ELSE NULL
                    END,
                    [RetiredAt] = CASE
                        WHEN [IsActive] = 0 AND EXISTS (
                            SELECT 1
                            FROM [WorkflowInstances] wi
                            WHERE wi.[WorkflowDefinitionId] = [WorkflowDefinitions].[Id]
                        ) THEN COALESCE([UpdatedAt], [CreatedAt])
                        ELSE NULL
                    END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_LifecycleStatus",
                table: "WorkflowDefinitions",
                column: "LifecycleStatus");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TenantId_DefinitionKey_Version",
                table: "WorkflowDefinitions",
                columns: new[] { "TenantId", "DefinitionKey", "Version" },
                unique: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_LifecycleStatus",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_TenantId_DefinitionKey_Version",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "ChangeSummary",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "DefinitionKey",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "LifecycleStatus",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedById",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "RetiredAt",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "RetiredById",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "SupersedesDefinitionId",
                table: "WorkflowDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TenantId_Name",
                table: "WorkflowDefinitions",
                columns: new[] { "TenantId", "Name" });
        }
    }
}
