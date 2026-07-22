using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionTemplateFleetMobileAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAssetCategoryId",
                table: "InspectionTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignedAssetId",
                table: "InspectionTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FleetInspectionKind",
                table: "InspectionTemplates",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Any");

            migrationBuilder.AddColumn<bool>(
                name: "IsQrEnabled",
                table: "InspectionTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "MobileOfflineEnabled",
                table: "InspectionTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "QrPayloadVersion",
                table: "InspectionTemplates",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "TemplateScope",
                table: "InspectionTemplates",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "General");

            migrationBuilder.Sql("""
                UPDATE InspectionTemplates
                SET TemplateScope = N'Fleet',
                    FleetInspectionKind = N'Any'
                WHERE IsDeleted = 0
                  AND (
                      Category = N'Fleet'
                      OR InspectionType = N'Fleet'
                  )
                """);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_TenantId_AssignedAssetCategoryId_IsDeleted",
                table: "InspectionTemplates",
                columns: new[] { "TenantId", "AssignedAssetCategoryId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_TenantId_AssignedAssetId_IsDeleted",
                table: "InspectionTemplates",
                columns: new[] { "TenantId", "AssignedAssetId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_TenantId_TemplateScope_FleetInspectionKind_IsActive_IsDeleted",
                table: "InspectionTemplates",
                columns: new[] { "TenantId", "TemplateScope", "FleetInspectionKind", "IsActive", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_TenantId_TemplateScope_IsActive_IsDeleted",
                table: "InspectionTemplates",
                columns: new[] { "TenantId", "TemplateScope", "IsActive", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionTemplates_TenantId_AssignedAssetCategoryId_IsDeleted",
                table: "InspectionTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InspectionTemplates_TenantId_AssignedAssetId_IsDeleted",
                table: "InspectionTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InspectionTemplates_TenantId_TemplateScope_FleetInspectionKind_IsActive_IsDeleted",
                table: "InspectionTemplates");

            migrationBuilder.DropIndex(
                name: "IX_InspectionTemplates_TenantId_TemplateScope_IsActive_IsDeleted",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "AssignedAssetCategoryId",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "AssignedAssetId",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "FleetInspectionKind",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "IsQrEnabled",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "MobileOfflineEnabled",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "QrPayloadVersion",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "TemplateScope",
                table: "InspectionTemplates");
        }
    }
}
