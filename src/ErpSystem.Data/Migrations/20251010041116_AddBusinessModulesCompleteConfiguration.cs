using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessModulesCompleteConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_Tenants_TenantId",
                table: "EmailTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportDataSources_Users_CreatedByUserId",
                table: "ReportDataSources");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExecutions_Tenants_TenantId",
                table: "ReportExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExecutions_Users_UserId",
                table: "ReportExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExports_Tenants_TenantId",
                table: "ReportExports");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExports_Users_UserId",
                table: "ReportExports");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportRoleAssignments_Tenants_TenantId",
                table: "ReportRoleAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_TenantModules_ModuleId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Tenants_TenantId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportSchedules_Tenants_TenantId",
                table: "ReportSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportTemplates_Tenants_TenantId",
                table: "ReportTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAlerts_Tenants_TenantId",
                table: "SecurityAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAlerts_Users_AffectedUserId",
                table: "SecurityAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityMetricsSet_Tenants_TenantId",
                table: "SecurityMetricsSet");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityPolicyViolations_Users_UserId",
                table: "SecurityPolicyViolations");

            migrationBuilder.DropForeignKey(
                name: "FK_ThreatDetections_Users_AffectedUserId",
                table: "ThreatDetections");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReportFavorites_Tenants_TenantId",
                table: "UserReportFavorites");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("373f98d9-f7f9-4f5c-ac18-ad5a2c0b5bc8"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("465cd212-b089-4e3f-90bb-6b520f0b765b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ec91dbc4-54d4-494c-8437-3742a899c097"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f0f54f0d-81c1-4450-8f87-06d829faaab4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f22f6bfb-aa6f-4450-add9-a8e55904eade"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f2f2d485-fd35-4a64-a9a3-11da759feb49"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f8fce369-9a14-4b75-9cdf-e51417fa4df8"));

            migrationBuilder.CreateTable(
                name: "AssetTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    RequiresLocation = table.Column<bool>(type: "bit", nullable: false),
                    RequiresOperatingHours = table.Column<bool>(type: "bit", nullable: false),
                    RequiresMileageTracking = table.Column<bool>(type: "bit", nullable: false),
                    RequiresLicensing = table.Column<bool>(type: "bit", nullable: false),
                    RequiresInspections = table.Column<bool>(type: "bit", nullable: false),
                    SupportsHierarchy = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSpecializedFields = table.Column<bool>(type: "bit", nullable: false),
                    DefaultMaintenanceIntervalDays = table.Column<int>(type: "int", nullable: false, defaultValue: 30),
                    RequiresPreventiveMaintenance = table.Column<bool>(type: "bit", nullable: false),
                    RequiresConditionMonitoring = table.Column<bool>(type: "bit", nullable: false),
                    RequiresSafetyChecks = table.Column<bool>(type: "bit", nullable: false),
                    RequiresLockoutTagout = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPermits = table.Column<bool>(type: "bit", nullable: false),
                    DefaultWorkOrderPriority = table.Column<int>(type: "int", nullable: false),
                    DefaultEstimatedHours = table.Column<double>(type: "float", nullable: false),
                    DefaultWorkInstructions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CustomFieldsConfig = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Countries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Alpha2Code = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Countries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Countries_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    InspectionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ChecklistItems = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionTemplates_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DefaultUnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DefaultSerialTracking = table.Column<bool>(type: "bit", nullable: false),
                    DefaultLotTracking = table.Column<bool>(type: "bit", nullable: false),
                    DefaultRequiresInspection = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryCategories_InventoryCategories_ParentCategoryId",
                        column: x => x.ParentCategoryId,
                        principalTable: "InventoryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceAssetCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAssetCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssetCategories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriorityLevels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    ResponseTimeHours = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriorityLevels_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shifts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Skill",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresCertification = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skill", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skill_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "nvarchar(20)", nullable: false),
                    ScheduledHours = table.Column<double>(type: "float", nullable: false),
                    BreakMinutes = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianShifts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianSkills_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    WarehouseType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warehouses_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    DefaultPriority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderTypes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetTypeFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FieldType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ValidationRules = table.Column<string>(type: "nvarchar(max)", maxLength: 1000, nullable: true),
                    Options = table.Column<string>(type: "nvarchar(max)", maxLength: 2000, nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HelpText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetTypeFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetTypeFields_AssetTypes_AssetTypeId",
                        column: x => x.AssetTypeId,
                        principalTable: "AssetTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssetTypeFields_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StandardCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastPurchaseCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SalePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AvailableStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AllocatedStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OnOrderStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MinimumLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    MaximumLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReorderLevel = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReorderQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SafetyStock = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    SafetyLeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    ItemType = table.Column<int>(type: "int", nullable: false),
                    ABCClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Length = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsSerialTracked = table.Column<bool>(type: "bit", nullable: false),
                    IsLotTracked = table.Column<bool>(type: "bit", nullable: false),
                    IsExpirationTracked = table.Column<bool>(type: "bit", nullable: false),
                    IsLocationTracked = table.Column<bool>(type: "bit", nullable: false),
                    RequiresInspection = table.Column<bool>(type: "bit", nullable: false),
                    ShelfLifeDays = table.Column<int>(type: "int", nullable: true),
                    PrimarySupplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastStockDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastPurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastSaleDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCountDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryItems_InventoryCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "InventoryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SupplierAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PromisedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShippingCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ShippingTerms = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DeliveryWarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeliveryAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Users_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Warehouses_DeliveryWarehouseId",
                        column: x => x.DeliveryWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WarehouseLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LocationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ParentLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsPickingLocation = table.Column<bool>(type: "bit", nullable: false),
                    IsReceivingLocation = table.Column<bool>(type: "bit", nullable: false),
                    MaxWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxVolume = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MaxItems = table.Column<int>(type: "int", nullable: true),
                    CurrentWeight = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrentVolume = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    CurrentItemCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarehouseLocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseLocations_WarehouseLocations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseLocations_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AdjustmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAdjustmentValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustments_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OrderedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExpectedDeliveryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveryNote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReceivedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InspectedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_Users_InspectedById",
                        column: x => x.InspectedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceipts_Users_ReceivedById",
                        column: x => x.ReceivedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocatedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RemainingQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequiredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AllocatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryAllocations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryAllocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryAllocations_Users_AllocatedById",
                        column: x => x.AllocatedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryAllocations_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AllocatedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AvailableQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AverageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastMovementDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCountDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextCountDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CountFrequencyDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryLocations_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InventoryLocations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLocations_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MovementDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReferenceType = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RunningBalance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RunningValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ProcessedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_Users_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockMovements_Users_ProcessedById",
                        column: x => x.ProcessedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockMovements_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "StockAdjustmentItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SystemQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PhysicalQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AdjustmentQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AdjustmentValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockAdjustmentItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentItems_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentItems_StockAdjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalTable: "StockAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockAdjustmentItems_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderReceiptItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AcceptedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    RejectedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderReceiptItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptItems_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptItems_PurchaseOrderReceipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "PurchaseOrderReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptItems_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderReceiptItems_WarehouseLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AssetDowntimes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DowntimeHours = table.Column<double>(type: "float", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EstimatedCostImpact = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetDowntimes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetDowntimes_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssetInspections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OverallResult = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    InspectionData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RecommendedActions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NextInspectionDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsRegulatoryRequired = table.Column<bool>(type: "bit", nullable: false),
                    RegulatoryStandard = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetInspections_InspectionTemplates_InspectionTemplateId",
                        column: x => x.InspectionTemplateId,
                        principalTable: "InspectionTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssetInspections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionDocuments_AssetInspections_InspectionId",
                        column: x => x.InspectionId,
                        principalTable: "AssetInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InspectionDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AttendanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    CheckInTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    CheckOutTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    WorkedHours = table.Column<double>(type: "float", nullable: true),
                    OvertimeHours = table.Column<double>(type: "float", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceRecords_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DepartmentType = table.Column<int>(type: "int", nullable: false),
                    ParentDepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Budget = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_ParentDepartmentId",
                        column: x => x.ParentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Departments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeePositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    MinSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MaxSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    RequiresCertification = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Responsibilities = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Requirements = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeePositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePositions_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeePositions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkStations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkStations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkStations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkStations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionSkillRequirement",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiredLevel = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionSkillRequirement", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionSkillRequirement_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PositionSkillRequirement_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PositionSkillRequirement_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeBiometrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BiometricType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BiometricData = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnrolledDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBiometrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeBiometrics_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeContractDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractType = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PayFrequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkingHoursPerWeek = table.Column<int>(type: "int", nullable: false),
                    VacationDaysPerYear = table.Column<int>(type: "int", nullable: false),
                    SickDaysPerYear = table.Column<int>(type: "int", nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ContractPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeContractDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeContractDetails_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeDependents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Relationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    Occupation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsStudentDependent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDependents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDependents_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeEmergencyContacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Relationship = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AlternatePhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeEmergencyContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeEmergencyContacts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeIdentificationCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DocumentNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocumentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeIdentificationCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeIdentificationCards_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeQualifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QualificationName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Institution = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldOfStudy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeQualifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeQualifications_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CorporateEmployeeID = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    MaritalStatus = table.Column<int>(type: "int", nullable: true),
                    Religion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsFullTime = table.Column<bool>(type: "bit", nullable: false),
                    DateEmployed = table.Column<DateOnly>(type: "date", nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PostalCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TelephoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BusinessNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Extension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ContractType = table.Column<int>(type: "int", nullable: false),
                    ProbationPeriodDays = table.Column<int>(type: "int", nullable: false),
                    ConfirmationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RetirementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PicturePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PositionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StaffStatus = table.Column<int>(type: "int", nullable: false),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BloodType = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ManagerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    BadgeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastPromotionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_Countries_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Countries",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_EmployeePositions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "EmployeePositions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Employees_ManagerId",
                        column: x => x.ManagerId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Employees_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Employees_WorkStations_StationId",
                        column: x => x.StationId,
                        principalTable: "WorkStations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmployeeShiftPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreferenceLevel = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeShiftPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftPreferences_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftPreferences_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeShiftPreferences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillLevel = table.Column<int>(type: "int", nullable: false),
                    AcquiredDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificationExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificationNumber = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CertifyingBody = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSkills_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeSkills_Skill_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skill",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeSkills_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWorkHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompanyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    JobDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Salary = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReasonForLeaving = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SupervisorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SupervisorPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CanContact = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWorkHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeWorkHistories_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeWorkHistories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AssetNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssetCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SerialNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Building = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Floor = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Room = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Criticality = table.Column<int>(type: "int", nullable: false),
                    WarrantyStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarrantyEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    WarrantyProvider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ParentAssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Specifications = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentLinks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Images = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OperatingHours = table.Column<double>(type: "float", nullable: true),
                    LastOperatingHoursUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Mileage = table.Column<double>(type: "float", nullable: true),
                    LastMileageUpdate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LicensePlate = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    VIN = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LastServiceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextServiceDue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FloorArea = table.Column<double>(type: "float", nullable: true),
                    EnergyRating = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Capacity = table.Column<double>(type: "float", nullable: true),
                    CapacityUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PowerRating = table.Column<double>(type: "float", nullable: true),
                    PowerUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_AssetTypes_AssetTypeId",
                        column: x => x.AssetTypeId,
                        principalTable: "AssetTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_MaintenanceAssetCategories_AssetCategoryId",
                        column: x => x.AssetCategoryId,
                        principalTable: "MaintenanceAssetCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_MaintenanceAssets_ParentAssetId",
                        column: x => x.ParentAssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceAssets_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionHeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sections_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sections_Employees_SectionHeadId",
                        column: x => x.SectionHeadId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sections_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShiftAssignments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianAvailabilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AvailabilityType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    RecurrencePattern = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AvailableHours = table.Column<double>(type: "float", nullable: true),
                    CapacityPercentage = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianAvailabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianAvailabilities_Employees_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicianAvailabilities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TeamLeaderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianTeams_Employees_TeamLeaderId",
                        column: x => x.TeamLeaderId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnicianTeams_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserTechnicianSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProficiencyLevel = table.Column<int>(type: "int", nullable: false),
                    CertificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificationExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertifyingBody = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CertificationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTechnicianSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTechnicianSkills_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTechnicianSkills_TechnicianSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "TechnicianSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTechnicianSkills_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScheduleType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntervalValue = table.Column<int>(type: "int", nullable: false),
                    PreferredTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    DayOfWeek = table.Column<int>(type: "int", nullable: true),
                    DayOfMonth = table.Column<int>(type: "int", nullable: true),
                    UsageInterval = table.Column<double>(type: "float", nullable: true),
                    UsageUnit = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ConditionParameters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastGeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedHours = table.Column<double>(type: "float", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WorkOrderTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WorkOrderDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DefaultTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DefaultTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PriorityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaskTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartsTemplate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Employees_DefaultTechnicianId",
                        column: x => x.DefaultTechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_MaintenanceTypes_MaintenanceTypeId",
                        column: x => x.MaintenanceTypeId,
                        principalTable: "MaintenanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_PriorityLevels_PriorityLevelId",
                        column: x => x.PriorityLevelId,
                        principalTable: "PriorityLevels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_TechnicianTeams_DefaultTeamId",
                        column: x => x.DefaultTeamId,
                        principalTable: "TechnicianTeams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianTeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeftDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianTeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianTeamMembers_Employees_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicianTeamMembers_TechnicianTeams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "TechnicianTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicianTeamMembers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PriorityLevelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssignedTeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RequestedStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequestedCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualStartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualCompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EstimatedHours = table.Column<double>(type: "float", nullable: false),
                    ActualHours = table.Column<double>(type: "float", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CauseCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActionCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SafetyRequirements = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiresPermit = table.Column<bool>(type: "bit", nullable: false),
                    RequiresLockout = table.Column<bool>(type: "bit", nullable: false),
                    RequiresConfinedSpaceEntry = table.Column<bool>(type: "bit", nullable: false),
                    ParentWorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MaintenanceScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    CustomFields = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomFieldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrders_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Employees_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Employees_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_MaintenanceAssets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "MaintenanceAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_MaintenanceSchedules_MaintenanceScheduleId",
                        column: x => x.MaintenanceScheduleId,
                        principalTable: "MaintenanceSchedules",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_MaintenanceTypes_MaintenanceTypeId",
                        column: x => x.MaintenanceTypeId,
                        principalTable: "MaintenanceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_PriorityLevels_PriorityLevelId",
                        column: x => x.PriorityLevelId,
                        principalTable: "PriorityLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_TechnicianTeams_AssignedTeamId",
                        column: x => x.AssignedTeamId,
                        principalTable: "TechnicianTeams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_WorkOrderTypes_WorkOrderTypeId",
                        column: x => x.WorkOrderTypeId,
                        principalTable: "WorkOrderTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrders_WorkOrders_ParentWorkOrderId",
                        column: x => x.ParentWorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnicianSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScheduleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ShiftId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsAllDay = table.Column<bool>(type: "bit", nullable: false),
                    IsRecurring = table.Column<bool>(type: "bit", nullable: false),
                    RecurrencePattern = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EstimatedHours = table.Column<double>(type: "float", nullable: false),
                    ActualHours = table.Column<double>(type: "float", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicianSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicianSchedules_Employees_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TechnicianSchedules_TechnicianShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "TechnicianShifts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TechnicianSchedules_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnicianSchedules_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CommentType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsInternal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderComments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderComments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderComments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderDocuments_Employees_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderDocuments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderDocuments_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderLabor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Hours = table.Column<double>(type: "float", nullable: false),
                    HourlyRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LaborType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderLabor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderLabor_Employees_TechnicianId",
                        column: x => x.TechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderLabor_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderLabor_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QuantityRequired = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityAllocated = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityUsed = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PickedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_InventoryAllocations_AllocationId",
                        column: x => x.AllocationId,
                        principalTable: "InventoryAllocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_WarehouseLocations_WarehouseLocationId",
                        column: x => x.WarehouseLocationId,
                        principalTable: "WarehouseLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EstimatedHours = table.Column<double>(type: "float", nullable: false),
                    ActualHours = table.Column<double>(type: "float", nullable: false),
                    AssignedTechnicianId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderTasks_Employees_AssignedTechnicianId",
                        column: x => x.AssignedTechnicianId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderTasks_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderTasks_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7229));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7267));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7269));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7271));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7498));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7513));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7520));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7528));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7538));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7545));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7551));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7557));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7566));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7575));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7581));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7588));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7597));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7608));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7620));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7626));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7671));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7673));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7674));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7675));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7676));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7677));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7678));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7679));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7680));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7681));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7682));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7682));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7683));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7683));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7684));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7685));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7717));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7719));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7719));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7720));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7721));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7721));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7722));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7722));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7723));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7724));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7724));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7725));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7726));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7726));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7727));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7767));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7768));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7770));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7771));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7771));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7772));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7772));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7773));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7774));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7774));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7787));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7788));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7789));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1f33cb93-dce0-453c-b7ed-9efd20a90d05"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7332), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7330), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4f37828e-cf46-4daf-8e6d-7235bb99f404"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7400), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7400), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4fdd066f-98ff-42ea-9a53-ba29abd712b8"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7371), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7371), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5502c101-06bd-4cbd-88db-3290d6371a34"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7386), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7386), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6bcfab55-7e1a-4848-bf86-de7cc2754a03"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7416), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7416), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a6769cc2-0256-4a35-93f4-8452fdb87cb6"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7349), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7349), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e24d1390-2e88-4e91-a7d4-03ef7e877164"), null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7431), null, null, null, null, null, new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7430), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 10, 4, 11, 15, 403, DateTimeKind.Utc).AddTicks(7040));

            migrationBuilder.CreateIndex(
                name: "IX_AssetDowntimes_AssetId",
                table: "AssetDowntimes",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDowntimes_Reason",
                table: "AssetDowntimes",
                column: "Reason");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDowntimes_StartTime",
                table: "AssetDowntimes",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDowntimes_Status",
                table: "AssetDowntimes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDowntimes_TenantId",
                table: "AssetDowntimes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetDowntimes_WorkOrderId",
                table: "AssetDowntimes",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_AssetId",
                table: "AssetInspections",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_InspectionDate",
                table: "AssetInspections",
                column: "InspectionDate");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_InspectionTemplateId",
                table: "AssetInspections",
                column: "InspectionTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_InspectorId",
                table: "AssetInspections",
                column: "InspectorId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_IsRegulatoryRequired",
                table: "AssetInspections",
                column: "IsRegulatoryRequired");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_NextInspectionDue",
                table: "AssetInspections",
                column: "NextInspectionDue");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_OverallResult",
                table: "AssetInspections",
                column: "OverallResult");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_Status",
                table: "AssetInspections",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AssetInspections_TenantId",
                table: "AssetInspections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeFields_AssetTypeId_FieldName",
                table: "AssetTypeFields",
                columns: new[] { "AssetTypeId", "FieldName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeFields_DisplayOrder",
                table: "AssetTypeFields",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeFields_IsActive",
                table: "AssetTypeFields",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypeFields_TenantId",
                table: "AssetTypeFields",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_Code",
                table: "AssetTypes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_IsActive",
                table: "AssetTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AssetTypes_TenantId_Name",
                table: "AssetTypes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId",
                table: "AttendanceRecords",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_TenantId",
                table: "AttendanceRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Countries_TenantId",
                table: "Countries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Code",
                table: "Departments",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentHeadId",
                table: "Departments",
                column: "DepartmentHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentType",
                table: "Departments",
                column: "DepartmentType");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_IsActive",
                table: "Departments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ParentDepartmentId",
                table: "Departments",
                column: "ParentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_TenantId",
                table: "Departments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBiometrics_EmployeeId",
                table: "EmployeeBiometrics",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBiometrics_TenantId",
                table: "EmployeeBiometrics",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContractDetails_EmployeeId",
                table: "EmployeeContractDetails",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeContractDetails_TenantId",
                table: "EmployeeContractDetails",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_EmployeeId",
                table: "EmployeeDependents",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependents_TenantId",
                table: "EmployeeDependents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmergencyContacts_EmployeeId",
                table: "EmployeeEmergencyContacts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmergencyContacts_TenantId",
                table: "EmployeeEmergencyContacts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeIdentificationCards_EmployeeId",
                table: "EmployeeIdentificationCards",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeIdentificationCards_TenantId",
                table: "EmployeeIdentificationCards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_DepartmentId",
                table: "EmployeePositions",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePositions_TenantId",
                table: "EmployeePositions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_EmployeeId",
                table: "EmployeeQualifications",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeQualifications_TenantId",
                table: "EmployeeQualifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CorporateEmployeeID",
                table: "Employees",
                column: "CorporateEmployeeID");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CountryId",
                table: "Employees",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DateEmployed",
                table: "Employees",
                column: "DateEmployed");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId",
                table: "Employees",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmailAddress",
                table: "Employees",
                column: "EmailAddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_EmployeeNumber",
                table: "Employees",
                column: "EmployeeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_IsActive",
                table: "Employees",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ManagerId",
                table: "Employees",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PositionId",
                table: "Employees",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_SectionId",
                table: "Employees",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ShiftId",
                table: "Employees",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_StaffStatus",
                table: "Employees",
                column: "StaffStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_StationId",
                table: "Employees",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TenantId",
                table: "Employees",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftPreferences_EmployeeId",
                table: "EmployeeShiftPreferences",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftPreferences_ShiftId",
                table: "EmployeeShiftPreferences",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeShiftPreferences_TenantId",
                table: "EmployeeShiftPreferences",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSkills_EmployeeId",
                table: "EmployeeSkills",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSkills_SkillId",
                table: "EmployeeSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSkills_TenantId",
                table: "EmployeeSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkHistories_EmployeeId",
                table: "EmployeeWorkHistories",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWorkHistories_TenantId",
                table: "EmployeeWorkHistories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDocuments_DocumentType",
                table: "InspectionDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDocuments_InspectionId",
                table: "InspectionDocuments",
                column: "InspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionDocuments_TenantId",
                table: "InspectionDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_Category",
                table: "InspectionTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_InspectionType",
                table: "InspectionTemplates",
                column: "InspectionType");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_IsActive",
                table: "InspectionTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionTemplates_TenantId",
                table: "InspectionTemplates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocations_AllocatedById",
                table: "InventoryAllocations",
                column: "AllocatedById");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocations_InventoryItemId",
                table: "InventoryAllocations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocations_LocationId",
                table: "InventoryAllocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAllocations_TenantId",
                table: "InventoryAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCategories_Code",
                table: "InventoryCategories",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCategories_IsActive",
                table: "InventoryCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCategories_ParentCategoryId",
                table: "InventoryCategories",
                column: "ParentCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCategories_TenantId",
                table: "InventoryCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ABCClass",
                table: "InventoryItems",
                column: "ABCClass");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CategoryId",
                table: "InventoryItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_IsLotTracked",
                table: "InventoryItems",
                column: "IsLotTracked");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_IsSerialTracked",
                table: "InventoryItems",
                column: "IsSerialTracked");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_ItemCode",
                table: "InventoryItems",
                column: "ItemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Status",
                table: "InventoryItems",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId",
                table: "InventoryItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLocations_InventoryItemId",
                table: "InventoryLocations",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLocations_LocationId",
                table: "InventoryLocations",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLocations_TenantId",
                table: "InventoryLocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetCategories_Code",
                table: "MaintenanceAssetCategories",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetCategories_IsActive",
                table: "MaintenanceAssetCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssetCategories_TenantId",
                table: "MaintenanceAssetCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_AssetCategoryId",
                table: "MaintenanceAssets",
                column: "AssetCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_AssetNumber",
                table: "MaintenanceAssets",
                column: "AssetNumber");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_AssetTypeId",
                table: "MaintenanceAssets",
                column: "AssetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_Criticality",
                table: "MaintenanceAssets",
                column: "Criticality");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_EmployeeId",
                table: "MaintenanceAssets",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_ParentAssetId",
                table: "MaintenanceAssets",
                column: "ParentAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_SerialNumber",
                table: "MaintenanceAssets",
                column: "SerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_Status",
                table: "MaintenanceAssets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAssets_TenantId",
                table: "MaintenanceAssets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_AssetId",
                table: "MaintenanceSchedules",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_DefaultTeamId",
                table: "MaintenanceSchedules",
                column: "DefaultTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_DefaultTechnicianId",
                table: "MaintenanceSchedules",
                column: "DefaultTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_IsActive",
                table: "MaintenanceSchedules",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_LastGeneratedDate",
                table: "MaintenanceSchedules",
                column: "LastGeneratedDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_MaintenanceTypeId",
                table: "MaintenanceSchedules",
                column: "MaintenanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_NextDueDate",
                table: "MaintenanceSchedules",
                column: "NextDueDate");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_PriorityLevelId",
                table: "MaintenanceSchedules",
                column: "PriorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_ScheduleType",
                table: "MaintenanceSchedules",
                column: "ScheduleType");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceSchedules_TenantId",
                table: "MaintenanceSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTypes_Category",
                table: "MaintenanceTypes",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTypes_Code",
                table: "MaintenanceTypes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTypes_IsActive",
                table: "MaintenanceTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTypes_TenantId",
                table: "MaintenanceTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkillRequirement_PositionId",
                table: "PositionSkillRequirement",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkillRequirement_SkillId",
                table: "PositionSkillRequirement",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionSkillRequirement_TenantId",
                table: "PositionSkillRequirement",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityLevels_IsActive",
                table: "PriorityLevels",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityLevels_Level",
                table: "PriorityLevels",
                column: "Level",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriorityLevels_TenantId",
                table: "PriorityLevels",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_InventoryItemId",
                table: "PurchaseOrderItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_TenantId",
                table: "PurchaseOrderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptItems_LocationId",
                table: "PurchaseOrderReceiptItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptItems_PurchaseOrderItemId",
                table: "PurchaseOrderReceiptItems",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptItems_ReceiptId",
                table: "PurchaseOrderReceiptItems",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceiptItems_TenantId",
                table: "PurchaseOrderReceiptItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_InspectedById",
                table: "PurchaseOrderReceipts",
                column: "InspectedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_PurchaseOrderId",
                table: "PurchaseOrderReceipts",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_ReceiptDate",
                table: "PurchaseOrderReceipts",
                column: "ReceiptDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_ReceiptNumber",
                table: "PurchaseOrderReceipts",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_ReceivedById",
                table: "PurchaseOrderReceipts",
                column: "ReceivedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_Status",
                table: "PurchaseOrderReceipts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderReceipts_TenantId",
                table: "PurchaseOrderReceipts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ApprovedById",
                table: "PurchaseOrders",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_DeliveryWarehouseId",
                table: "PurchaseOrders",
                column: "DeliveryWarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderDate",
                table: "PurchaseOrders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_OrderNumber",
                table: "PurchaseOrders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_RequestedById",
                table: "PurchaseOrders",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Status",
                table: "PurchaseOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId",
                table: "PurchaseOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_Code",
                table: "Sections",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_DepartmentId",
                table: "Sections",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_IsActive",
                table: "Sections",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_SectionHeadId",
                table: "Sections",
                column: "SectionHeadId");

            migrationBuilder.CreateIndex(
                name: "IX_Sections_TenantId",
                table: "Sections",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_EmployeeId",
                table: "ShiftAssignments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_ShiftId",
                table: "ShiftAssignments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftAssignments_TenantId",
                table: "ShiftAssignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_TenantId",
                table: "Shifts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Skill_TenantId",
                table: "Skill",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentItems_AdjustmentId",
                table: "StockAdjustmentItems",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentItems_InventoryItemId",
                table: "StockAdjustmentItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentItems_LocationId",
                table: "StockAdjustmentItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustmentItems_TenantId",
                table: "StockAdjustmentItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_AdjustmentDate",
                table: "StockAdjustments",
                column: "AdjustmentDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_AdjustmentNumber",
                table: "StockAdjustments",
                column: "AdjustmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ApprovedById",
                table: "StockAdjustments",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_InventoryItemId",
                table: "StockAdjustments",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_ReasonCode",
                table: "StockAdjustments",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_Status",
                table: "StockAdjustments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_TenantId",
                table: "StockAdjustments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ApprovedById",
                table: "StockMovements",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_InventoryItemId",
                table: "StockMovements",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_LocationId",
                table: "StockMovements",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_MovementDate",
                table: "StockMovements",
                column: "MovementDate");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_MovementType",
                table: "StockMovements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProcessedById",
                table: "StockMovements",
                column: "ProcessedById");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReferenceNumber",
                table: "StockMovements",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ReferenceType",
                table: "StockMovements",
                column: "ReferenceType");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId",
                table: "StockMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAvailabilities_TechnicianId",
                table: "TechnicianAvailabilities",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianAvailabilities_TenantId",
                table: "TechnicianAvailabilities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_ShiftId",
                table: "TechnicianSchedules",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_TechnicianId",
                table: "TechnicianSchedules",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_TenantId",
                table: "TechnicianSchedules",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSchedules_WorkOrderId",
                table: "TechnicianSchedules",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianShifts_TenantId",
                table: "TechnicianShifts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkills_Category",
                table: "TechnicianSkills",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkills_IsActive",
                table: "TechnicianSkills",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianSkills_TenantId",
                table: "TechnicianSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeamMembers_IsActive",
                table: "TechnicianTeamMembers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeamMembers_TeamId",
                table: "TechnicianTeamMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeamMembers_TeamId_TechnicianId",
                table: "TechnicianTeamMembers",
                columns: new[] { "TeamId", "TechnicianId" });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeamMembers_TechnicianId",
                table: "TechnicianTeamMembers",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeamMembers_TenantId",
                table: "TechnicianTeamMembers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeams_Status",
                table: "TechnicianTeams",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeams_TeamLeaderId",
                table: "TechnicianTeams",
                column: "TeamLeaderId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnicianTeams_TenantId",
                table: "TechnicianTeams",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechnicianSkills_CertificationExpiry",
                table: "UserTechnicianSkills",
                column: "CertificationExpiry");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechnicianSkills_EmployeeId",
                table: "UserTechnicianSkills",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechnicianSkills_EmployeeId_SkillId",
                table: "UserTechnicianSkills",
                columns: new[] { "EmployeeId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTechnicianSkills_ProficiencyLevel",
                table: "UserTechnicianSkills",
                column: "ProficiencyLevel");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechnicianSkills_SkillId",
                table: "UserTechnicianSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTechnicianSkills_TenantId",
                table: "UserTechnicianSkills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_IsActive",
                table: "WarehouseLocations",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_LocationCode",
                table: "WarehouseLocations",
                column: "LocationCode");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_ParentLocationId",
                table: "WarehouseLocations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_TenantId",
                table: "WarehouseLocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseLocations_WarehouseId",
                table: "WarehouseLocations",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_IsActive",
                table: "Warehouses",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_TenantId",
                table: "Warehouses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_WarehouseType",
                table: "Warehouses",
                column: "WarehouseType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_CommentType",
                table: "WorkOrderComments",
                column: "CommentType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_CreatedAt",
                table: "WorkOrderComments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_EmployeeId",
                table: "WorkOrderComments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_TenantId",
                table: "WorkOrderComments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderComments_WorkOrderId",
                table: "WorkOrderComments",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDocuments_DocumentType",
                table: "WorkOrderDocuments",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDocuments_TenantId",
                table: "WorkOrderDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDocuments_UploadedAt",
                table: "WorkOrderDocuments",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDocuments_UploadedById",
                table: "WorkOrderDocuments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderDocuments_WorkOrderId",
                table: "WorkOrderDocuments",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderLabor_LaborType",
                table: "WorkOrderLabor",
                column: "LaborType");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderLabor_StartTime",
                table: "WorkOrderLabor",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderLabor_TechnicianId",
                table: "WorkOrderLabor",
                column: "TechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderLabor_TenantId",
                table: "WorkOrderLabor",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderLabor_WorkOrderId",
                table: "WorkOrderLabor",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_AllocationId",
                table: "WorkOrderParts",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_InventoryItemId",
                table: "WorkOrderParts",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_ItemCode",
                table: "WorkOrderParts",
                column: "ItemCode");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_Status",
                table: "WorkOrderParts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_TenantId",
                table: "WorkOrderParts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_WarehouseLocationId",
                table: "WorkOrderParts",
                column: "WarehouseLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_WorkOrderId",
                table: "WorkOrderParts",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ApprovedById",
                table: "WorkOrders",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssetId",
                table: "WorkOrders",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssignedTeamId",
                table: "WorkOrders",
                column: "AssignedTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_AssignedTechnicianId",
                table: "WorkOrders",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_EmployeeId",
                table: "WorkOrders",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenanceScheduleId",
                table: "WorkOrders",
                column: "MaintenanceScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_MaintenanceTypeId",
                table: "WorkOrders",
                column: "MaintenanceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_ParentWorkOrderId",
                table: "WorkOrders",
                column: "ParentWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_PriorityLevelId",
                table: "WorkOrders",
                column: "PriorityLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_RequestedById",
                table: "WorkOrders",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_RequestedCompletionDate",
                table: "WorkOrders",
                column: "RequestedCompletionDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_RequestedStartDate",
                table: "WorkOrders",
                column: "RequestedStartDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_Status",
                table: "WorkOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TenantId",
                table: "WorkOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkOrderNumber",
                table: "WorkOrders",
                column: "WorkOrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_WorkOrderTypeId",
                table: "WorkOrders",
                column: "WorkOrderTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTasks_AssignedTechnicianId",
                table: "WorkOrderTasks",
                column: "AssignedTechnicianId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTasks_Sequence",
                table: "WorkOrderTasks",
                column: "Sequence");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTasks_Status",
                table: "WorkOrderTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTasks_TenantId",
                table: "WorkOrderTasks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTasks_WorkOrderId",
                table: "WorkOrderTasks",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTypes_Code",
                table: "WorkOrderTypes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTypes_IsActive",
                table: "WorkOrderTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTypes_TenantId",
                table: "WorkOrderTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkStations_DepartmentId",
                table: "WorkStations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkStations_TenantId",
                table: "WorkStations",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_Tenants_TenantId",
                table: "EmailTemplates",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportDataSources_Users_CreatedByUserId",
                table: "ReportDataSources",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExecutions_Tenants_TenantId",
                table: "ReportExecutions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExecutions_Users_UserId",
                table: "ReportExecutions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExports_Tenants_TenantId",
                table: "ReportExports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExports_Users_UserId",
                table: "ReportExports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportRoleAssignments_Tenants_TenantId",
                table: "ReportRoleAssignments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_TenantModules_ModuleId",
                table: "Reports",
                column: "ModuleId",
                principalTable: "TenantModules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Tenants_TenantId",
                table: "Reports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportSchedules_Tenants_TenantId",
                table: "ReportSchedules",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportTemplates_Tenants_TenantId",
                table: "ReportTemplates",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAlerts_Tenants_TenantId",
                table: "SecurityAlerts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAlerts_Users_AffectedUserId",
                table: "SecurityAlerts",
                column: "AffectedUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityMetricsSet_Tenants_TenantId",
                table: "SecurityMetricsSet",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityPolicyViolations_Users_UserId",
                table: "SecurityPolicyViolations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ThreatDetections_Users_AffectedUserId",
                table: "ThreatDetections",
                column: "AffectedUserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserReportFavorites_Tenants_TenantId",
                table: "UserReportFavorites",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDowntimes_MaintenanceAssets_AssetId",
                table: "AssetDowntimes",
                column: "AssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AssetDowntimes_WorkOrders_WorkOrderId",
                table: "AssetDowntimes",
                column: "WorkOrderId",
                principalTable: "WorkOrders",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetInspections_Employees_InspectorId",
                table: "AssetInspections",
                column: "InspectorId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AssetInspections_MaintenanceAssets_AssetId",
                table: "AssetInspections",
                column: "AssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceRecords_Employees_EmployeeId",
                table: "AttendanceRecords",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Employees_DepartmentHeadId",
                table: "Departments",
                column: "DepartmentHeadId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeBiometrics_Employees_EmployeeId",
                table: "EmployeeBiometrics",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeContractDetails_Employees_EmployeeId",
                table: "EmployeeContractDetails",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeDependents_Employees_EmployeeId",
                table: "EmployeeDependents",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeEmergencyContacts_Employees_EmployeeId",
                table: "EmployeeEmergencyContacts",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeIdentificationCards_Employees_EmployeeId",
                table: "EmployeeIdentificationCards",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeQualifications_Employees_EmployeeId",
                table: "EmployeeQualifications",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Sections_SectionId",
                table: "Employees",
                column: "SectionId",
                principalTable: "Sections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_Tenants_TenantId",
                table: "EmailTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportDataSources_Users_CreatedByUserId",
                table: "ReportDataSources");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExecutions_Tenants_TenantId",
                table: "ReportExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExecutions_Users_UserId",
                table: "ReportExecutions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExports_Tenants_TenantId",
                table: "ReportExports");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportExports_Users_UserId",
                table: "ReportExports");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportRoleAssignments_Tenants_TenantId",
                table: "ReportRoleAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_TenantModules_ModuleId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_Reports_Tenants_TenantId",
                table: "Reports");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportSchedules_Tenants_TenantId",
                table: "ReportSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportTemplates_Tenants_TenantId",
                table: "ReportTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAlerts_Tenants_TenantId",
                table: "SecurityAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityAlerts_Users_AffectedUserId",
                table: "SecurityAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityMetricsSet_Tenants_TenantId",
                table: "SecurityMetricsSet");

            migrationBuilder.DropForeignKey(
                name: "FK_SecurityPolicyViolations_Users_UserId",
                table: "SecurityPolicyViolations");

            migrationBuilder.DropForeignKey(
                name: "FK_ThreatDetections_Users_AffectedUserId",
                table: "ThreatDetections");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReportFavorites_Tenants_TenantId",
                table: "UserReportFavorites");

            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Employees_DepartmentHeadId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Sections_Employees_SectionHeadId",
                table: "Sections");

            migrationBuilder.DropTable(
                name: "AssetDowntimes");

            migrationBuilder.DropTable(
                name: "AssetTypeFields");

            migrationBuilder.DropTable(
                name: "AttendanceRecords");

            migrationBuilder.DropTable(
                name: "EmployeeBiometrics");

            migrationBuilder.DropTable(
                name: "EmployeeContractDetails");

            migrationBuilder.DropTable(
                name: "EmployeeDependents");

            migrationBuilder.DropTable(
                name: "EmployeeEmergencyContacts");

            migrationBuilder.DropTable(
                name: "EmployeeIdentificationCards");

            migrationBuilder.DropTable(
                name: "EmployeeQualifications");

            migrationBuilder.DropTable(
                name: "EmployeeShiftPreferences");

            migrationBuilder.DropTable(
                name: "EmployeeSkills");

            migrationBuilder.DropTable(
                name: "EmployeeWorkHistories");

            migrationBuilder.DropTable(
                name: "InspectionDocuments");

            migrationBuilder.DropTable(
                name: "InventoryLocations");

            migrationBuilder.DropTable(
                name: "PositionSkillRequirement");

            migrationBuilder.DropTable(
                name: "PurchaseOrderReceiptItems");

            migrationBuilder.DropTable(
                name: "ShiftAssignments");

            migrationBuilder.DropTable(
                name: "StockAdjustmentItems");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "TechnicianAvailabilities");

            migrationBuilder.DropTable(
                name: "TechnicianSchedules");

            migrationBuilder.DropTable(
                name: "TechnicianTeamMembers");

            migrationBuilder.DropTable(
                name: "UserTechnicianSkills");

            migrationBuilder.DropTable(
                name: "WorkOrderComments");

            migrationBuilder.DropTable(
                name: "WorkOrderDocuments");

            migrationBuilder.DropTable(
                name: "WorkOrderLabor");

            migrationBuilder.DropTable(
                name: "WorkOrderParts");

            migrationBuilder.DropTable(
                name: "WorkOrderTasks");

            migrationBuilder.DropTable(
                name: "AssetInspections");

            migrationBuilder.DropTable(
                name: "Skill");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "PurchaseOrderReceipts");

            migrationBuilder.DropTable(
                name: "StockAdjustments");

            migrationBuilder.DropTable(
                name: "TechnicianShifts");

            migrationBuilder.DropTable(
                name: "TechnicianSkills");

            migrationBuilder.DropTable(
                name: "InventoryAllocations");

            migrationBuilder.DropTable(
                name: "WorkOrders");

            migrationBuilder.DropTable(
                name: "InspectionTemplates");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "WarehouseLocations");

            migrationBuilder.DropTable(
                name: "MaintenanceSchedules");

            migrationBuilder.DropTable(
                name: "WorkOrderTypes");

            migrationBuilder.DropTable(
                name: "InventoryCategories");

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropTable(
                name: "MaintenanceAssets");

            migrationBuilder.DropTable(
                name: "MaintenanceTypes");

            migrationBuilder.DropTable(
                name: "PriorityLevels");

            migrationBuilder.DropTable(
                name: "TechnicianTeams");

            migrationBuilder.DropTable(
                name: "AssetTypes");

            migrationBuilder.DropTable(
                name: "MaintenanceAssetCategories");

            migrationBuilder.DropTable(
                name: "Employees");

            migrationBuilder.DropTable(
                name: "Countries");

            migrationBuilder.DropTable(
                name: "EmployeePositions");

            migrationBuilder.DropTable(
                name: "Sections");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "WorkStations");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1f33cb93-dce0-453c-b7ed-9efd20a90d05"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4f37828e-cf46-4daf-8e6d-7235bb99f404"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4fdd066f-98ff-42ea-9a53-ba29abd712b8"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5502c101-06bd-4cbd-88db-3290d6371a34"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6bcfab55-7e1a-4848-bf86-de7cc2754a03"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a6769cc2-0256-4a35-93f4-8452fdb87cb6"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e24d1390-2e88-4e91-a7d4-03ef7e877164"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7367));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7399));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7401));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7402));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7564));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7572));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7575));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7578));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7586));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7591));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7594));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7635));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7641));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7647));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7650));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7653));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7660));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7663));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7666));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7669));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7702));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7708));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7708));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7709));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7710));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7711));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7712));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7713));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7713));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7714));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7715));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7715));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7716));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7717));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7717));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7718));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7744));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7746));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7746));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7747));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7748));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7748));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7749));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7749));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7750));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7750));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7751));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7751));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7752));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7752));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7753));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7791));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7792));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7809));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7810));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7811));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7811));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7812));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7812));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7813));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7814));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7824));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7825));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7825));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("373f98d9-f7f9-4f5c-ac18-ad5a2c0b5bc8"), null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7506), null, null, null, null, null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7506), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("465cd212-b089-4e3f-90bb-6b520f0b765b"), null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7497), null, null, null, null, null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7497), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ec91dbc4-54d4-494c-8437-3742a899c097"), null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7479), null, null, null, null, null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7479), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f0f54f0d-81c1-4450-8f87-06d829faaab4"), null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7459), null, null, null, null, null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7458), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f22f6bfb-aa6f-4450-add9-a8e55904eade"), null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7447), null, null, null, null, null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7444), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f2f2d485-fd35-4a64-a9a3-11da759feb49"), null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7468), null, null, null, null, null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7468), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f8fce369-9a14-4b75-9cdf-e51417fa4df8"), null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7487), null, null, null, null, null, new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7487), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 22, 50, 52, 485, DateTimeKind.Utc).AddTicks(7229));

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_Tenants_TenantId",
                table: "EmailTemplates",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportDataSources_Users_CreatedByUserId",
                table: "ReportDataSources",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExecutions_Tenants_TenantId",
                table: "ReportExecutions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExecutions_Users_UserId",
                table: "ReportExecutions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExports_Tenants_TenantId",
                table: "ReportExports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportExports_Users_UserId",
                table: "ReportExports",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportRoleAssignments_Tenants_TenantId",
                table: "ReportRoleAssignments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_TenantModules_ModuleId",
                table: "Reports",
                column: "ModuleId",
                principalTable: "TenantModules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Reports_Tenants_TenantId",
                table: "Reports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportSchedules_Tenants_TenantId",
                table: "ReportSchedules",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportTemplates_Tenants_TenantId",
                table: "ReportTemplates",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAlerts_Tenants_TenantId",
                table: "SecurityAlerts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityAlerts_Users_AffectedUserId",
                table: "SecurityAlerts",
                column: "AffectedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityMetricsSet_Tenants_TenantId",
                table: "SecurityMetricsSet",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SecurityPolicyViolations_Users_UserId",
                table: "SecurityPolicyViolations",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ThreatDetections_Users_AffectedUserId",
                table: "ThreatDetections",
                column: "AffectedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReportFavorites_Tenants_TenantId",
                table: "UserReportFavorites",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id");
        }
    }
}
