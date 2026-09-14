using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionSheetTypesFleetDefaultsAndInspectionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultFleetDefectBillingType",
                table: "MaintenanceSettings",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Repairs");

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultFleetDefectMaintenanceTypeId",
                table: "MaintenanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultFleetDefectPriorityLevelId",
                table: "MaintenanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultFleetDefectWorkOrderTypeId",
                table: "MaintenanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SheetType",
                table: "InspectionTemplates",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "InspectionSheet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultFleetDefectBillingType",
                table: "MaintenanceSettings");

            migrationBuilder.DropColumn(
                name: "DefaultFleetDefectMaintenanceTypeId",
                table: "MaintenanceSettings");

            migrationBuilder.DropColumn(
                name: "DefaultFleetDefectPriorityLevelId",
                table: "MaintenanceSettings");

            migrationBuilder.DropColumn(
                name: "DefaultFleetDefectWorkOrderTypeId",
                table: "MaintenanceSettings");

            migrationBuilder.DropColumn(
                name: "SheetType",
                table: "InspectionTemplates");
        }
    }
}
