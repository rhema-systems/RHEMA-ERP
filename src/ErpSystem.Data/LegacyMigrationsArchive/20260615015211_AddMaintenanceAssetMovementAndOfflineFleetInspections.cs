using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceAssetMovementAndOfflineFleetInspections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FleetTripInspections_FleetTrips_FleetTripId",
                table: "FleetTripInspections");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentProjectId",
                table: "MaintenanceAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentSiteLocationId",
                table: "MaintenanceAssets",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoCreateWorkOrderOnFailure",
                table: "InspectionTemplates",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailureMaintenanceTypeId",
                table: "InspectionTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailurePriorityLevelId",
                table: "InspectionTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FailureWorkOrderTypeId",
                table: "InspectionTemplates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "FleetTripId",
                table: "FleetTripInspections",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTime>(
                name: "CapturedOfflineAtUtc",
                table: "FleetTripInspections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientSubmissionId",
                table: "FleetTripInspections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SyncedAtUtc",
                table: "FleetTripInspections",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleAssetId",
                table: "FleetTripInspections",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE inspection
                SET inspection.VehicleAssetId = trip.VehicleAssetId
                FROM FleetTripInspections inspection
                INNER JOIN FleetTrips trip ON trip.Id = inspection.FleetTripId
                WHERE inspection.VehicleAssetId IS NULL;

                IF EXISTS (SELECT 1 FROM FleetTripInspections WHERE VehicleAssetId IS NULL)
                    THROW 51000, 'Unable to backfill VehicleAssetId for one or more fleet trip inspections.', 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "VehicleAssetId",
                table: "FleetTripInspections",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "MaintenanceAssetMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromProjectName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ToProjectName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    FromSiteLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToSiteLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FromSiteLocationName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ToSiteLocationName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    FromLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ToLocation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MovementType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceAssetMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssetMovements_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssetMovements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_CurrentProjectId",
                table: "MaintenanceAssets",
                column: "CurrentProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_CurrentSiteLocationId",
                table: "MaintenanceAssets",
                column: "CurrentSiteLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId_ClientSubmissionId",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "ClientSubmissionId" },
                unique: true,
                filter: "[ClientSubmissionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId_VehicleAssetId_StartedAtUtc_IsDeleted",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "VehicleAssetId", "StartedAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_VehicleAssetId",
                table: "FleetTripInspections",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetMovements_AssetId",
                table: "MaintenanceAssetMovements",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetMovements_EffectiveDate",
                table: "MaintenanceAssetMovements",
                column: "EffectiveDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetMovements_TenantId",
                table: "MaintenanceAssetMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetMovements_ToProjectId",
                table: "MaintenanceAssetMovements",
                column: "ToProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetMovements_ToSiteLocationId",
                table: "MaintenanceAssetMovements",
                column: "ToSiteLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTripInspections_FleetTrips_FleetTripId",
                table: "FleetTripInspections",
                column: "FleetTripId",
                principalTable: "FleetTrips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTripInspections_MaintenanceAssets_VehicleAssetId",
                table: "FleetTripInspections",
                column: "VehicleAssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceAssets_Locations_CurrentSiteLocationId",
                table: "MaintenanceAssets",
                column: "CurrentSiteLocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceAssets_Projects_CurrentProjectId",
                table: "MaintenanceAssets",
                column: "CurrentProjectId",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FleetTripInspections_FleetTrips_FleetTripId",
                table: "FleetTripInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_FleetTripInspections_MaintenanceAssets_VehicleAssetId",
                table: "FleetTripInspections");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceAssets_Locations_CurrentSiteLocationId",
                table: "MaintenanceAssets");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceAssets_Projects_CurrentProjectId",
                table: "MaintenanceAssets");

            migrationBuilder.DropTable(
                name: "MaintenanceAssetMovements");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceAssets_CurrentProjectId",
                table: "MaintenanceAssets");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceAssets_CurrentSiteLocationId",
                table: "MaintenanceAssets");

            migrationBuilder.DropIndex(
                name: "IX_FleetTripInspections_TenantId_ClientSubmissionId",
                table: "FleetTripInspections");

            migrationBuilder.DropIndex(
                name: "IX_FleetTripInspections_TenantId_VehicleAssetId_StartedAtUtc_IsDeleted",
                table: "FleetTripInspections");

            migrationBuilder.DropIndex(
                name: "IX_FleetTripInspections_VehicleAssetId",
                table: "FleetTripInspections");

            migrationBuilder.DropColumn(
                name: "CurrentProjectId",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "CurrentSiteLocationId",
                table: "MaintenanceAssets");

            migrationBuilder.DropColumn(
                name: "AutoCreateWorkOrderOnFailure",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "FailureMaintenanceTypeId",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "FailurePriorityLevelId",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "FailureWorkOrderTypeId",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "CapturedOfflineAtUtc",
                table: "FleetTripInspections");

            migrationBuilder.DropColumn(
                name: "ClientSubmissionId",
                table: "FleetTripInspections");

            migrationBuilder.DropColumn(
                name: "SyncedAtUtc",
                table: "FleetTripInspections");

            migrationBuilder.DropColumn(
                name: "VehicleAssetId",
                table: "FleetTripInspections");

            migrationBuilder.AlterColumn<Guid>(
                name: "FleetTripId",
                table: "FleetTripInspections",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTripInspections_FleetTrips_FleetTripId",
                table: "FleetTripInspections",
                column: "FleetTripId",
                principalTable: "FleetTrips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
