using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260707170000_AddWorkflowDelegationScopedWorkflowSteps")]
    public partial class AddWorkflowDelegationScopedWorkflowSteps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowDefinitionId",
                table: "WorkflowDelegations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowStepId",
                table: "WorkflowDelegations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegations_WorkflowDefinitionId",
                table: "WorkflowDelegations",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDelegations_WorkflowStepId",
                table: "WorkflowDelegations",
                column: "WorkflowStepId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowDelegations_WorkflowDefinitionId",
                table: "WorkflowDelegations");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDelegations_WorkflowStepId",
                table: "WorkflowDelegations");

            migrationBuilder.DropColumn(
                name: "WorkflowDefinitionId",
                table: "WorkflowDelegations");

            migrationBuilder.DropColumn(
                name: "WorkflowStepId",
                table: "WorkflowDelegations");
        }
    }
}
