using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class _202602090001_AddFleetPhase2AndDistributedLocksAndInspectionTemplateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowPhotos",
                table: "InspectionTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AssetTypes",
                table: "InspectionTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "InspectionTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "EstimatedDuration",
                table: "InspectionTemplates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "InspectionTemplates",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InspectorRoles",
                table: "InspectionTemplates",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "InspectionTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresSignature",
                table: "InspectionTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Version",
                table: "InspectionTemplates",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DistributedLocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LockName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcquiredBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AcquiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastHeartbeatUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DistributedLocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FleetBatteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Spec = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    InstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_FleetBatteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetBatteries_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetBatteries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetExternalRepairs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_FleetExternalRepairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_BusinessPartners_VendorBusinessPartnerId",
                        column: x => x.VendorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetExternalRepairs_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetTripInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectorEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InspectionKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OverallResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InspectionData = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    table.PrimaryKey("PK_FleetTripInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_Employees_InspectorEmployeeId",
                        column: x => x.InspectorEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_InspectionTemplates_InspectionTemplateId",
                        column: x => x.InspectionTemplateId,
                        principalTable: "InspectionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTripInspections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetTyres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Position = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    TreadDepthMm = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    InstalledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RemovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
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
                    table.PrimaryKey("PK_FleetTyres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetTyres_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetTyres_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetVehicleAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssignedFromUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AssignedToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssignmentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_FleetVehicleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetVehicleAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetVehicleAssignments_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetVehicleAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FleetCostEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetExternalRepairId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetFuelTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CostDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CostType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
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
                    table.PrimaryKey("PK_FleetCostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetExternalRepairs_FleetExternalRepairId",
                        column: x => x.FleetExternalRepairId,
                        principalTable: "FleetExternalRepairs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetFuelTransactions_FleetFuelTransactionId",
                        column: x => x.FleetFuelTransactionId,
                        principalTable: "FleetFuelTransactions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetCostEntries_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FleetDefects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VehicleAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FleetTripId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FleetTripInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdditionalData = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_FleetDefects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FleetDefects_Employees_ReportedByEmployeeId",
                        column: x => x.ReportedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetDefects_FleetTripInspections_FleetTripInspectionId",
                        column: x => x.FleetTripInspectionId,
                        principalTable: "FleetTripInspections",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetDefects_FleetTrips_FleetTripId",
                        column: x => x.FleetTripId,
                        principalTable: "FleetTrips",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FleetDefects_MaintenanceAssets_VehicleAssetId",
                        column: x => x.VehicleAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FleetDefects_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FleetDefects_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4076));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4151));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4154));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4156));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4441));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4464));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4472));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4479));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4496));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4507));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4514));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4522));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4533));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4545));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4553));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4561));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4573));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4593));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4612));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4619));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4689));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4691));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4699));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4701));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4702));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4703));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4704));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4705));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4706));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4708));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4708));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4709));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4710));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4711));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4712));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4713));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4781));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4784));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4785));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4786));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4787));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4788));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4789));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4789));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4790));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4791));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4792));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4793));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4794));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4795));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4796));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4868));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4869));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4871));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4872));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4873));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4874));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4875));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4876));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4876));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4877));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4891));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4892));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(4893));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 9, 0, 40, 45, 347, DateTimeKind.Utc).AddTicks(3741));

            migrationBuilder.CreateIndex(
                name: "IX_DistributedLocks_LockName",
                table: "DistributedLocks",
                column: "LockName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_TenantId_SerialNumber_IsDeleted",
                table: "FleetBatteries",
                columns: new[] { "TenantId", "SerialNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetBatteries",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetBatteries_VehicleAssetId",
                table: "FleetBatteries",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetExternalRepairId",
                table: "FleetCostEntries",
                column: "FleetExternalRepairId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetFuelTransactionId",
                table: "FleetCostEntries",
                column: "FleetFuelTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_FleetTripId",
                table: "FleetCostEntries",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_TenantId_CostType_IsDeleted",
                table: "FleetCostEntries",
                columns: new[] { "TenantId", "CostType", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_TenantId_VehicleAssetId_CostDateUtc_IsDeleted",
                table: "FleetCostEntries",
                columns: new[] { "TenantId", "VehicleAssetId", "CostDateUtc", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_VehicleAssetId",
                table: "FleetCostEntries",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetCostEntries_WorkOrderId",
                table: "FleetCostEntries",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_FleetTripId",
                table: "FleetDefects",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_FleetTripInspectionId",
                table: "FleetDefects",
                column: "FleetTripInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_ReportedByEmployeeId",
                table: "FleetDefects",
                column: "ReportedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_TenantId_FleetTripId_IsDeleted",
                table: "FleetDefects",
                columns: new[] { "TenantId", "FleetTripId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetDefects",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_VehicleAssetId",
                table: "FleetDefects",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetDefects_WorkOrderId",
                table: "FleetDefects",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetExternalRepairs",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_TenantId_VendorBusinessPartnerId_IsDeleted",
                table: "FleetExternalRepairs",
                columns: new[] { "TenantId", "VendorBusinessPartnerId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_VehicleAssetId",
                table: "FleetExternalRepairs",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_VendorBusinessPartnerId",
                table: "FleetExternalRepairs",
                column: "VendorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetExternalRepairs_WorkOrderId",
                table: "FleetExternalRepairs",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_FleetTripId",
                table: "FleetTripInspections",
                column: "FleetTripId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_InspectionTemplateId",
                table: "FleetTripInspections",
                column: "InspectionTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_InspectorEmployeeId",
                table: "FleetTripInspections",
                column: "InspectorEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId_FleetTripId_InspectionKind_IsDeleted",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "FleetTripId", "InspectionKind", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTripInspections_TenantId_InspectionTemplateId_IsDeleted",
                table: "FleetTripInspections",
                columns: new[] { "TenantId", "InspectionTemplateId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_TenantId_SerialNumber_IsDeleted",
                table: "FleetTyres",
                columns: new[] { "TenantId", "SerialNumber", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_TenantId_VehicleAssetId_Status_IsDeleted",
                table: "FleetTyres",
                columns: new[] { "TenantId", "VehicleAssetId", "Status", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetTyres_VehicleAssetId",
                table: "FleetTyres",
                column: "VehicleAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_EmployeeId",
                table: "FleetVehicleAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_TenantId_EmployeeId_IsActive",
                table: "FleetVehicleAssignments",
                columns: new[] { "TenantId", "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_TenantId_VehicleAssetId_IsActive",
                table: "FleetVehicleAssignments",
                columns: new[] { "TenantId", "VehicleAssetId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FleetVehicleAssignments_VehicleAssetId",
                table: "FleetVehicleAssignments",
                column: "VehicleAssetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DistributedLocks");

            migrationBuilder.DropTable(
                name: "FleetBatteries");

            migrationBuilder.DropTable(
                name: "FleetCostEntries");

            migrationBuilder.DropTable(
                name: "FleetDefects");

            migrationBuilder.DropTable(
                name: "FleetTyres");

            migrationBuilder.DropTable(
                name: "FleetVehicleAssignments");

            migrationBuilder.DropTable(
                name: "FleetExternalRepairs");

            migrationBuilder.DropTable(
                name: "FleetTripInspections");

            migrationBuilder.DropColumn(
                name: "AllowPhotos",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "AssetTypes",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "EstimatedDuration",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "InspectorRoles",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "RequiresSignature",
                table: "InspectionTemplates");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "InspectionTemplates");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(6953));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7029));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7032));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7035));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7317));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7337));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7345));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7351));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7372));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7380));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7389));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7396));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7406));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7418));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7425));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7442));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7453));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7468));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7491));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7496));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7567));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7570));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7571));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7572));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7573));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7575));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7576));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7577));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7578));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7579));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7580));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7581));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7582));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7583));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7584));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7585));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7648));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7650));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7651));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7652));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7653));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7654));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7654));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7655));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7656));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7657));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7658));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7659));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7660));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7660));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7661));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7748));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7749));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7751));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7752));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7753));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7754));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7755));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7755));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7756));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7757));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7771));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7772));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(7773));

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 8, 20, 48, 32, 362, DateTimeKind.Utc).AddTicks(6566));
        }
    }
}
