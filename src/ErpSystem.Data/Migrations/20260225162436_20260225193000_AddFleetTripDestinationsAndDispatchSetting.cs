using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260225193000_AddFleetTripDestinationsAndDispatchSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequirePredefinedFleetTripDestinationOnDispatch",
                table: "MaintenanceSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "ExpectedHours",
                table: "FleetTrips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExpectedMileage",
                table: "FleetTrips",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FleetTripDestinationId",
                table: "FleetTrips",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FleetTripDestinations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpectedHours = table.Column<double>(type: "float", nullable: true),
                    ExpectedMileage = table.Column<double>(type: "float", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_FleetTripDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTripDestinations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_FleetTripDestinationId",
                table: "FleetTrips",
                column: "FleetTripDestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_FleetTripDestinationId",
                table: "FleetTrips",
                columns: new[] { "TenantId", "FleetTripDestinationId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripDestinations_TenantId",
                table: "FleetTripDestinations",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_FleetTrips_FleetTripDestinations_FleetTripDestinationId",
                table: "FleetTrips",
                column: "FleetTripDestinationId",
                principalTable: "FleetTripDestinations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FleetTrips_FleetTripDestinations_FleetTripDestinationId",
                table: "FleetTrips");

            migrationBuilder.DropTable(
                name: "FleetTripDestinations");

            migrationBuilder.DropIndex(
                name: "IX_FleetTrips_FleetTripDestinationId",
                table: "FleetTrips");

            migrationBuilder.DropIndex(
                name: "IX_FleetTrips_TenantId_FleetTripDestinationId",
                table: "FleetTrips");

            migrationBuilder.DropColumn(
                name: "RequirePredefinedFleetTripDestinationOnDispatch",
                table: "MaintenanceSettings");

            migrationBuilder.DropColumn(
                name: "ExpectedHours",
                table: "FleetTrips");

            migrationBuilder.DropColumn(
                name: "ExpectedMileage",
                table: "FleetTrips");

            migrationBuilder.DropColumn(
                name: "FleetTripDestinationId",
                table: "FleetTrips");
        }
    }
}
