using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkProcedureCasesToWorkflowInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WorkflowInstanceId",
                table: "ProcedureCases",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureCases_WorkflowInstanceId",
                table: "ProcedureCases",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcedureCases_WorkflowInstanceId",
                table: "ProcedureCases");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "ProcedureCases");
        }
    }
}
