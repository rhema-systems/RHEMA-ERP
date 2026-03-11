using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectExternalPortalAndContractMilestoneSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExternalCollaborationEnabled",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ExternalPortalAccessEnabled",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsExternalVisible",
                table: "ProjectDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ContractMilestoneId",
                table: "ProjectBillingSchedules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_ExternalPortalAccessEnabled_BusinessPartnerId",
                table: "Projects",
                columns: new[] { "TenantId", "ExternalPortalAccessEnabled", "BusinessPartnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectDocuments_ProjectId_IsExternalVisible",
                table: "ProjectDocuments",
                columns: new[] { "ProjectId", "IsExternalVisible" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBillingSchedules_ContractId_ContractMilestoneId",
                table: "ProjectBillingSchedules",
                columns: new[] { "ContractId", "ContractMilestoneId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_TenantId_ExternalPortalAccessEnabled_BusinessPartnerId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectDocuments_ProjectId_IsExternalVisible",
                table: "ProjectDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBillingSchedules_ContractId_ContractMilestoneId",
                table: "ProjectBillingSchedules");

            migrationBuilder.DropColumn(
                name: "ExternalCollaborationEnabled",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ExternalPortalAccessEnabled",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsExternalVisible",
                table: "ProjectDocuments");

            migrationBuilder.DropColumn(
                name: "ContractMilestoneId",
                table: "ProjectBillingSchedules");
        }
    }
}
