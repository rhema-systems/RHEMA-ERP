using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateSecurityTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_Roles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_Tenants_TenantId",
                table: "UserTenants");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_Users_UserId",
                table: "UserTenants");

            migrationBuilder.DropTable(
                name: "ApplicationUserTenant");

            migrationBuilder.DropIndex(
                name: "IX_UserTenants_TenantId",
                table: "UserTenants");

            migrationBuilder.DropIndex(
                name: "IX_UserTenants_UserId",
                table: "UserTenants");

            migrationBuilder.DropIndex(
                name: "IX_UserTenants_UserId_IsDefault",
                table: "UserTenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Name",
                table: "Tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                table: "Roles");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0e337286-7ab4-4c72-a560-97ec2b5c6c0b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4dc88d64-defd-4318-99a1-e761d8eacdbe"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("52899860-3157-41ba-b39a-6eb144d215bd"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7be66c35-082a-4a42-bb94-6365b1fcb4d9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8b9f8059-d463-4f01-8b7e-8cc80112aea2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a140e87e-8615-40b7-a8f6-c894294c5c68"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d6acc58b-1e8f-44f4-b44b-64dceaa7f1e9"));

            migrationBuilder.RenameTable(
                name: "Roles",
                newName: "AspNetRoles");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReactivatedAt",
                table: "UserTenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "UserTenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StatusChangedBy",
                table: "UserTenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                table: "UserTenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId1",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowSelfRegistration",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DefaultPriority",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAutoSelection",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForInternalUsers",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultForPublicUsers",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicRegistrationDomains",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireEmailVerification",
                table: "Tenants",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "UserAudience",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "WelcomeMessage",
                table: "Tenants",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Securities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PasswordMinLength = table.Column<int>(type: "int", nullable: false),
                    PasswordRequireUppercase = table.Column<bool>(type: "bit", nullable: false),
                    PasswordRequireLowercase = table.Column<bool>(type: "bit", nullable: false),
                    PasswordRequireDigits = table.Column<bool>(type: "bit", nullable: false),
                    PasswordRequireSpecialChars = table.Column<bool>(type: "bit", nullable: false),
                    PasswordMaxAge = table.Column<int>(type: "int", nullable: true),
                    PasswordPreventReuse = table.Column<int>(type: "int", nullable: true),
                    CaptchaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CaptchaProvider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecaptchaSiteKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RecaptchaSecretKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HCaptchaSiteKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HCaptchaSecretKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    RateLimitLoginMaxAttempts = table.Column<int>(type: "int", nullable: false),
                    RateLimitLoginWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    RateLimitLoginBlockDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    RateLimitRegisterMaxAttempts = table.Column<int>(type: "int", nullable: false),
                    RateLimitRegisterWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    RateLimitRegisterBlockDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    RateLimitForgotPasswordMaxAttempts = table.Column<int>(type: "int", nullable: false),
                    RateLimitForgotPasswordWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    RateLimitForgotPasswordBlockDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    SessionTimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    JwtTokenLifetimeMinutes = table.Column<int>(type: "int", nullable: false),
                    MaxFailedLoginAttempts = table.Column<int>(type: "int", nullable: false),
                    AccountLockoutMinutes = table.Column<int>(type: "int", nullable: false),
                    PreventConcurrentLogin = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_Securities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Securities_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2125));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2165));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2168));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2170));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("5b3f921d-2398-4f29-823e-a3b2e8599967"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2305), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2304), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("89bf11b7-396a-4ad4-8a1b-edf0415b92db"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2265), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2264), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c849318a-78b5-4352-8cc0-e54051706710"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2249), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2248), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c8b67e30-c868-4102-bf56-9d8b0656769d"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2316), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2316), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("cd04d659-03db-43fe-bdfd-67d44972bca3"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2232), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2228), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ea3115bb-a58d-4c0d-92f3-1eebaf5280bd"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2278), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2278), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fdfcf06f-57df-47d2-abae-83d44d1b9a97"), null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2291), null, null, null, null, null, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(2291), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "AllowSelfRegistration", "CreatedAt", "DefaultPriority", "EnableAutoSelection", "IsDefaultForInternalUsers", "IsDefaultForPublicUsers", "PublicRegistrationDomains", "RequireEmailVerification", "UserAudience", "WelcomeMessage" },
                values: new object[] { false, new DateTime(2025, 9, 23, 12, 51, 42, 23, DateTimeKind.Utc).AddTicks(1929), 10, false, false, false, null, false, 2, null });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_TenantId_Status",
                table: "UserTenants",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId",
                table: "Users",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId1",
                table: "Users",
                column: "TenantId1");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Domain",
                table: "Tenants",
                column: "Domain",
                unique: true,
                filter: "[Domain] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Securities_TenantId",
                table: "Securities",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_AspNetRoles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId1",
                table: "Users",
                column: "TenantId1",
                principalTable: "Tenants",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_Tenants_TenantId",
                table: "UserTenants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_Users_UserId",
                table: "UserTenants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRoles_AspNetRoles_RoleId",
                table: "UserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId1",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_Tenants_TenantId",
                table: "UserTenants");

            migrationBuilder.DropForeignKey(
                name: "FK_UserTenants_Users_UserId",
                table: "UserTenants");

            migrationBuilder.DropTable(
                name: "Securities");

            migrationBuilder.DropIndex(
                name: "IX_UserTenants_TenantId_Status",
                table: "UserTenants");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Domain",
                table: "Tenants");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AspNetRoles",
                table: "AspNetRoles");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5b3f921d-2398-4f29-823e-a3b2e8599967"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("89bf11b7-396a-4ad4-8a1b-edf0415b92db"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c849318a-78b5-4352-8cc0-e54051706710"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c8b67e30-c868-4102-bf56-9d8b0656769d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("cd04d659-03db-43fe-bdfd-67d44972bca3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ea3115bb-a58d-4c0d-92f3-1eebaf5280bd"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fdfcf06f-57df-47d2-abae-83d44d1b9a97"));

            migrationBuilder.DropColumn(
                name: "ReactivatedAt",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "StatusChangedBy",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId1",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AllowSelfRegistration",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DefaultPriority",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EnableAutoSelection",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsDefaultForInternalUsers",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "IsDefaultForPublicUsers",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PublicRegistrationDomains",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "RequireEmailVerification",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UserAudience",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "WelcomeMessage",
                table: "Tenants");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "Roles");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                table: "Roles",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ApplicationUserTenant",
                columns: table => new
                {
                    AccessibleTenantsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationUserTenant", x => new { x.AccessibleTenantsId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_ApplicationUserTenant_Tenants_AccessibleTenantsId",
                        column: x => x.AccessibleTenantsId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApplicationUserTenant_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8206));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8242));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8244));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8246));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0e337286-7ab4-4c72-a560-97ec2b5c6c0b"), null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8317), null, null, null, null, null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8317), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4dc88d64-defd-4318-99a1-e761d8eacdbe"), null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8300), null, null, null, null, null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8297), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("52899860-3157-41ba-b39a-6eb144d215bd"), null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8366), null, null, null, null, null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8366), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7be66c35-082a-4a42-bb94-6365b1fcb4d9"), null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8378), null, null, null, null, null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8378), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8b9f8059-d463-4f01-8b7e-8cc80112aea2"), null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8341), null, null, null, null, null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8341), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a140e87e-8615-40b7-a8f6-c894294c5c68"), null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8354), null, null, null, null, null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8353), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d6acc58b-1e8f-44f4-b44b-64dceaa7f1e9"), null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8329), null, null, null, null, null, new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8329), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 19, 20, 14, 55, 983, DateTimeKind.Utc).AddTicks(8053));

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_TenantId",
                table: "UserTenants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_UserId",
                table: "UserTenants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_UserId_IsDefault",
                table: "UserTenants",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                table: "Tenants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserTenant_UsersId",
                table: "ApplicationUserTenant",
                column: "UsersId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_Roles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRoles_Roles_RoleId",
                table: "UserRoles",
                column: "RoleId",
                principalTable: "Roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_Tenants_TenantId",
                table: "UserTenants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_Users_UserId",
                table: "UserTenants",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
