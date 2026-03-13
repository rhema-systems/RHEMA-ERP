using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectDeliverableWorkflowGovernance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalApprovalNotes",
                table: "ProjectDeliverables",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExternalApprovedAt",
                table: "ProjectDeliverables",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalApprovedById",
                table: "ProjectDeliverables",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalApprovalNotes",
                table: "ProjectDeliverables");

            migrationBuilder.DropColumn(
                name: "ExternalApprovedAt",
                table: "ProjectDeliverables");

            migrationBuilder.DropColumn(
                name: "ExternalApprovedById",
                table: "ProjectDeliverables");
        }
    }
}
