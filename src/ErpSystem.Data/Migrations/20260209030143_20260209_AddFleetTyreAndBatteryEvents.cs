using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    public partial class _20260209_AddFleetTyreAndBatteryEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FleetBatteryEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetBatteryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_FleetBatteryEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetBatteryEvents_FleetBatteries_FleetBatteryId",
                        column: x => x.FleetBatteryId,
                        principalTable: "FleetBatteries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetBatteryEvents_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetBatteryEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetTyreEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTyreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FromPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToPosition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    TreadDepthMm = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_FleetTyreEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTyreEvents_FleetTyres_FleetTyreId",
                        column: x => x.FleetTyreId,
                        principalTable: "FleetTyres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTyreEvents_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTyreEvents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_FleetBatteryId",
                table: "FleetBatteryEvents",
                column: "FleetBatteryId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_TenantId_FleetBatteryId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents",
                columns: new[] { "TenantId", "FleetBatteryId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetBatteryEvents",
                columns: new[] { "TenantId", "VehicleAssetId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteryEvents_VehicleAssetId",
                table: "FleetBatteryEvents",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_FleetTyreId",
                table: "FleetTyreEvents",
                column: "FleetTyreId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_TenantId_FleetTyreId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents",
                columns: new[] { "TenantId", "FleetTyreId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_TenantId_VehicleAssetId_EventAtUtc_IsDeleted",
                table: "FleetTyreEvents",
                columns: new[] { "TenantId", "VehicleAssetId", "EventAtUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyreEvents_VehicleAssetId",
                table: "FleetTyreEvents",
                column: "VehicleAssetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FleetBatteryEvents");

            migrationBuilder.DropTable(
                name: "FleetTyreEvents");
        }
    }
}

