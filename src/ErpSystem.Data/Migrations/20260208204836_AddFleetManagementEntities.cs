using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFleetManagementEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FleetComplianceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComplianceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCritical = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DocumentLinks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastDueSoonReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastOverdueReminderSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_FleetComplianceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetComplianceItems_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetComplianceItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetTrips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DriverEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Purpose = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Origin = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Destination = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PlannedStartAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualEndAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DispatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DispatchedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartMileage = table.Column<double>(type: "float", nullable: true),
                    EndMileage = table.Column<double>(type: "float", nullable: true),
                    StartOperatingHours = table.Column<double>(type: "float", nullable: true),
                    EndOperatingHours = table.Column<double>(type: "float", nullable: true),
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
                    table.PrimaryKey("PK_FleetTrips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTrips_Employees_DriverEmployeeId",
                        column: x => x.DriverEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FleetTrips_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetTrips_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetFuelTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FuelledAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MileageAtFuel = table.Column<double>(type: "float", nullable: true),
                    OperatingHoursAtFuel = table.Column<double>(type: "float", nullable: true),
                    VendorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReceiptReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_FleetFuelTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetFuelTransactions_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FleetFuelTransactions_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetFuelTransactions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId_ExpiryDate",
                table: "FleetComplianceItems",
                columns: new[] { "TenantId", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId",
                table: "FleetComplianceItems",
                columns: new[] { "TenantId", "VehicleAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_TenantId_VehicleAssetId_IsCritical_ExpiryDate",
                table: "FleetComplianceItems",
                columns: new[] { "TenantId", "VehicleAssetId", "IsCritical", "ExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetComplianceItems_VehicleAssetId",
                table: "FleetComplianceItems",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_FleetTripId",
                table: "FleetFuelTransactions",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_TenantId_FleetTripId",
                table: "FleetFuelTransactions",
                columns: new[] { "TenantId", "FleetTripId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_TenantId_VehicleAssetId_FuelledAt",
                table: "FleetFuelTransactions",
                columns: new[] { "TenantId", "VehicleAssetId", "FuelledAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetFuelTransactions_VehicleAssetId",
                table: "FleetFuelTransactions",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_DriverEmployeeId",
                table: "FleetTrips",
                column: "DriverEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_DriverEmployeeId",
                table: "FleetTrips",
                columns: new[] { "TenantId", "DriverEmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_RequestedByUserId",
                table: "FleetTrips",
                columns: new[] { "TenantId", "RequestedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_Status",
                table: "FleetTrips",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_TenantId_VehicleAssetId",
                table: "FleetTrips",
                columns: new[] { "TenantId", "VehicleAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTrips_VehicleAssetId",
                table: "FleetTrips",
                column: "VehicleAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FleetComplianceItems");

            migrationBuilder.DropTable(
                name: "FleetFuelTransactions");

            migrationBuilder.DropTable(
                name: "FleetTrips");
        }
    }
}

