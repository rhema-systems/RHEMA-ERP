using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("01c2efeb-4436-47cc-8901-ff0fd9e99cd4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("21f83ba1-8c0d-4cb2-a261-d816817913f1"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3906592d-eda5-4422-882c-6333b48b303a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("73ca1e48-49d7-4db4-9e9c-cf4b1bbbaef2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8e9dcf6c-5654-4c8f-8cf8-7bf8545930e8"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b04f99fc-9f67-4b17-8b1e-1c8a3e10d7b3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("bbe225a4-f21a-4664-b352-fb56e377dd0b"));

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsSystemPermission = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GrantedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5186));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5224));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5227));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5229));

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "Id", "Category", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisplayName", "IsDeleted", "IsSystemPermission", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "User Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5639), null, null, null, "View user accounts and details", "View Users", false, true, "users.read", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "User Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5648), null, null, null, "Create new user accounts", "Create Users", false, true, "users.create", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "User Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5653), null, null, null, "Edit existing user accounts", "Update Users", false, true, "users.update", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "User Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5657), null, null, null, "Delete user accounts", "Delete Users", false, true, "users.delete", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "Role Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5667), null, null, null, "View role definitions", "View Roles", false, true, "roles.read", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "Role Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5675), null, null, null, "Create new roles", "Create Roles", false, true, "roles.create", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000007"), "Role Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5688), null, null, null, "Edit existing roles", "Update Roles", false, true, "roles.update", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000008"), "Role Management", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5693), null, null, null, "Delete roles", "Delete Roles", false, true, "roles.delete", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "Dashboard & Reports", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5702), null, null, null, "Access main dashboard", "View Dashboard", false, true, "dashboard.read", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000010"), "Dashboard & Reports", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5709), null, null, null, "Access reporting features", "View Reports", false, true, "reports.read", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000011"), "Dashboard & Reports", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5715), null, null, null, "Generate custom reports", "Create Reports", false, true, "reports.create", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000012"), "Dashboard & Reports", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5725), null, null, null, "Access analytics data", "View Analytics", false, true, "analytics.read", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000013"), "System Administration", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5733), null, null, null, "Access admin interface", "View Admin", false, true, "admin.read", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000014"), "System Administration", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5748), null, null, null, "View system settings", "View Settings", false, true, "settings.read", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000015"), "System Administration", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5753), null, null, null, "Modify system settings", "Update Settings", false, true, "settings.update", null, null },
                    { new Guid("00000000-0000-0000-0000-000000000016"), "System Administration", new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5757), null, null, null, "Access audit trail", "View Audit Logs", false, true, "audit.read", null, null }
                });

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0eb1e162-c834-4090-9ddd-c63b918689b5"), null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5443), null, null, null, null, null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5438), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("21db1bb8-c582-402b-9030-d460d9b2c364"), null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5526), null, null, null, null, null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5525), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("40580bfa-bd22-40b5-9e7c-2ec0eb767d16"), null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5543), null, null, null, null, null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5542), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5db01044-f9b1-4eaa-9c60-179f968ea9d6"), null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5557), null, null, null, null, null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5557), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7a949ede-0f99-41bd-be45-48e62669c0d1"), null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5475), null, null, null, null, null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5475), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7c9c9cc5-be45-469e-a215-5b05030d2c2c"), null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5492), null, null, null, null, null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5492), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("9068b023-0439-43d1-8bc5-56b0b1848e13"), null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5509), null, null, null, null, null, new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5508), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5048));

            migrationBuilder.InsertData(
                table: "RolePermissions",
                columns: new[] { "PermissionId", "RoleId", "GrantedAt", "GrantedBy" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5802), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5804), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5805), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5806), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5807), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5809), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5810), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5811), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5811), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5813), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5814), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5815), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5816), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5816), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5817), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5818), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5851), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5854), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5855), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5855), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5856), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5857), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5858), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5858), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5859), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5860), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5861), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5861), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5862), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5863), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5864), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5896), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5897), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5904), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5905), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5906), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5907), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5907), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5908), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5909), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5910), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5923), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5924), "System" },
                    { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2025, 9, 25, 22, 27, 33, 72, DateTimeKind.Utc).AddTicks(5925), "System" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Category",
                table: "Permissions",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Name",
                table: "Permissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0eb1e162-c834-4090-9ddd-c63b918689b5"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("21db1bb8-c582-402b-9030-d460d9b2c364"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("40580bfa-bd22-40b5-9e7c-2ec0eb767d16"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5db01044-f9b1-4eaa-9c60-179f968ea9d6"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7a949ede-0f99-41bd-be45-48e62669c0d1"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7c9c9cc5-be45-469e-a215-5b05030d2c2c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9068b023-0439-43d1-8bc5-56b0b1848e13"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2528));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2568));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2570));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2573));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("01c2efeb-4436-47cc-8901-ff0fd9e99cd4"), null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2640), null, null, null, null, null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2640), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("21f83ba1-8c0d-4cb2-a261-d816817913f1"), null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2679), null, null, null, null, null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2679), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3906592d-eda5-4422-882c-6333b48b303a"), null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2623), null, null, null, null, null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2620), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("73ca1e48-49d7-4db4-9e9c-cf4b1bbbaef2"), null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2667), null, null, null, null, null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2667), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8e9dcf6c-5654-4c8f-8cf8-7bf8545930e8"), null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2705), null, null, null, null, null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2704), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b04f99fc-9f67-4b17-8b1e-1c8a3e10d7b3"), null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2693), null, null, null, null, null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2692), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("bbe225a4-f21a-4664-b352-fb56e377dd0b"), null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2654), null, null, null, null, null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2653), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2385));
        }
    }
}
