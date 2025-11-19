using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToolManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("32715b1c-6308-45dc-9f6d-448600185404"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("53ff0afe-b958-44aa-ae42-7dcf80863fa9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("625a4223-2641-484b-8f38-34a3108cd02f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("792f5e49-edd7-4058-a615-f114756619e4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("98b86b4b-4c2b-4d3c-99f7-2dea8263a072"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("99901b55-7bdd-4787-a80b-6a5c16b11e0a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("df25c4a7-3a5d-42fc-874f-354023468c0a"));

            migrationBuilder.CreateTable(
                name: "MaintenanceTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CurrentLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HomeLocation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LastMaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextMaintenanceDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastCalibrationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextCalibrationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentValue = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DailyRentalRate = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalUsageDays = table.Column<int>(type: "int", nullable: false),
                    LastUsedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RequiresCertification = table.Column<bool>(type: "bit", nullable: false),
                    RequiresTraining = table.Column<bool>(type: "bit", nullable: false),
                    SafetyNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DocumentPaths = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_MaintenanceTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceTools_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ToolCheckouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckedOutById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    JobCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CheckoutDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpectedReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActualReturnDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckedInById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CheckoutNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReturnNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConditionOnCheckout = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ConditionOnReturn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DamageReported = table.Column<bool>(type: "bit", nullable: false),
                    DamageDescription = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DamageCost = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
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
                    table.PrimaryKey("PK_ToolCheckouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ToolCheckouts_Users_CheckedInById",
                        column: x => x.CheckedInById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ToolCheckouts_Users_CheckedOutById",
                        column: x => x.CheckedOutById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_ToolCheckouts_JobCard_JobCardId",
                        column: x => x.JobCardId,
                        principalTable: "JobCard",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ToolCheckouts_MaintenanceTools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "MaintenanceTools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ToolCheckouts_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ToolCheckouts_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsAllocated = table.Column<bool>(type: "bit", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_WorkOrderTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderTools_MaintenanceTools_ToolId",
                        column: x => x.ToolId,
                        principalTable: "MaintenanceTools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkOrderTools_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkOrderTools_ToolCheckouts_CheckoutId",
                        column: x => x.CheckoutId,
                        principalTable: "ToolCheckouts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkOrderTools_WorkOrders_WorkOrderId",
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
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(6886));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(6965));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(6968));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(6980));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7301));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7313));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7322));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7330));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7343));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7351));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7359));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7365));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7377));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7395));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7405));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7412));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7424));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7445));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7453));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7460));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7519));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7522));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7524));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7526));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7532));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7534));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7535));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7536));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7537));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7538));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7539));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7540));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8515));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8519));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8520));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8521));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8522));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8523));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8524));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8526));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8527));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8530));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8613));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8615));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8617));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8618));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8619));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8620));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8621));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8622));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8623));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8624));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8677));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8679));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(8680));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("41bb04e8-d100-4dd9-98e7-9b00c2e04be3"), null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7106), null, null, null, null, null, null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7106), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("46a888e8-db2b-49f0-bbdd-ce8c959ef05b"), null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7179), null, null, null, null, null, null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7179), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6d4602cb-e572-4545-8b42-b618b4e57a9d"), null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7195), null, null, null, null, null, null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7195), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6d7587b1-f5a3-40da-9220-7f89720edc97"), null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7126), null, null, null, null, null, null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7126), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("80adfaf6-9de4-4e86-8a32-19927b15bb1c"), null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7144), null, null, null, null, null, null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7143), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("cb27c167-37cb-4dc2-ab9f-e9b2f9a9d0d0"), null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7161), null, null, null, null, null, null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7160), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e681378f-10ef-4abe-a8a0-73a35554b82c"), null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7078), null, null, null, null, null, null, new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(7074), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 4, 13, 10, 49, 633, DateTimeKind.Utc).AddTicks(6551));

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTools_TenantId",
                table: "MaintenanceTools",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckouts_CheckedInById",
                table: "ToolCheckouts",
                column: "CheckedInById");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckouts_CheckedOutById",
                table: "ToolCheckouts",
                column: "CheckedOutById");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckouts_JobCardId",
                table: "ToolCheckouts",
                column: "JobCardId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckouts_TenantId",
                table: "ToolCheckouts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckouts_ToolId",
                table: "ToolCheckouts",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolCheckouts_WorkOrderId",
                table: "ToolCheckouts",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTools_CheckoutId",
                table: "WorkOrderTools",
                column: "CheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTools_TenantId",
                table: "WorkOrderTools",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTools_ToolId",
                table: "WorkOrderTools",
                column: "ToolId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTools_WorkOrderId",
                table: "WorkOrderTools",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderTools");

            migrationBuilder.DropTable(
                name: "ToolCheckouts");

            migrationBuilder.DropTable(
                name: "MaintenanceTools");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("41bb04e8-d100-4dd9-98e7-9b00c2e04be3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("46a888e8-db2b-49f0-bbdd-ce8c959ef05b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6d4602cb-e572-4545-8b42-b618b4e57a9d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6d7587b1-f5a3-40da-9220-7f89720edc97"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("80adfaf6-9de4-4e86-8a32-19927b15bb1c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("cb27c167-37cb-4dc2-ab9f-e9b2f9a9d0d0"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e681378f-10ef-4abe-a8a0-73a35554b82c"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3342));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3440));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3444));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3447));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3949));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3974));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3983));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3991));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4005));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4014));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4023));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4031));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4044));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4125));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4134));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4141));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4153));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4183));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4214));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4227));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4419));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4427));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4429));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4430));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4432));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4434));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4435));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4445));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4447));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5386));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5389));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5390));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5391));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5392));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5393));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5394));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5395));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5396));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5397));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5398));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5399));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5400));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5401));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5402));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5567));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5569));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5571));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5572));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5573));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5574));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5575));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5576));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5582));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5583));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5603));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5606));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("32715b1c-6308-45dc-9f6d-448600185404"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3736), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3736), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("53ff0afe-b958-44aa-ae42-7dcf80863fa9"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3680), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3679), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("625a4223-2641-484b-8f38-34a3108cd02f"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3698), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3697), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("792f5e49-edd7-4058-a615-f114756619e4"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3660), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3659), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("98b86b4b-4c2b-4d3c-99f7-2dea8263a072"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3634), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3628), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("99901b55-7bdd-4787-a80b-6a5c16b11e0a"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3755), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3755), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("df25c4a7-3a5d-42fc-874f-354023468c0a"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3715), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3715), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(2196));
        }
    }
}
