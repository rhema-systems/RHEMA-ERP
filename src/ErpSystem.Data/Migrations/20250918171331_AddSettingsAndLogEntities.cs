using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSettingsAndLogEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0b0e4d1d-ad38-47bf-b283-60654f64bd01"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("22cc6d74-6998-49b1-bfb1-e38a5620b591"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7d65cccf-7a63-439f-b893-2103d811bb0f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("891adcc2-b5ab-44f3-8604-999ae10791c9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("cd1f5db3-26cb-4b61-ae05-ac9e4ae05479"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d8db69d5-988e-44db-b3bd-d539edd394cc"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f96c8ce4-308d-440f-b2e6-f3a59379c027"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Roles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Roles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Resource = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EmailSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SmtpUsername = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SmtpPassword = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    UseTLS = table.Column<bool>(type: "bit", nullable: false),
                    FromAddress = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_EmailSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinLength = table.Column<int>(type: "int", nullable: false),
                    RequireUppercase = table.Column<bool>(type: "bit", nullable: false),
                    RequireLowercase = table.Column<bool>(type: "bit", nullable: false),
                    RequireDigits = table.Column<bool>(type: "bit", nullable: false),
                    RequireSpecialChars = table.Column<bool>(type: "bit", nullable: false),
                    MaxAge = table.Column<int>(type: "int", nullable: true),
                    PreventReuse = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_PasswordPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordPolicies_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SecurityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_SecurityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityLogs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SecurityLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_SystemSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SystemSettings_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2251), null, null });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                columns: new[] { "CreatedAt", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2301), null, null });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                columns: new[] { "CreatedAt", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2386), null, null });

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                columns: new[] { "CreatedAt", "UpdatedAt", "UpdatedBy" },
                values: new object[] { new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2389), null, null });

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("20998378-e35d-4997-b875-f58a88e04a9b"), null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2513), null, null, null, null, null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2512), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("569ad24e-ebe5-4db9-af4f-7953e5b9ced2"), null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2486), null, null, null, null, null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2485), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5e175eaa-fb74-4dba-8018-c70143ec4cea"), null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2527), null, null, null, null, null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2527), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5f01e2f4-9d65-4766-bf69-107b0a405d67"), null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2500), null, null, null, null, null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2500), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("5ffb814f-b4e7-4f6a-b6d5-79015f2ffff2"), null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2453), null, null, null, null, null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2449), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ac4bff78-09e3-451c-9999-00f2c45aef65"), null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2540), null, null, null, null, null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2539), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ba99b2b2-fe9c-4d0b-9ec8-cc9a19c09c72"), null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2470), null, null, null, null, null, new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2469), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2057));

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Resource",
                table: "AuditLogs",
                column: "Resource");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId",
                table: "AuditLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSettings_TenantId",
                table: "EmailSettings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordPolicies_TenantId",
                table: "PasswordPolicies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_Action",
                table: "SecurityLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_IpAddress",
                table: "SecurityLogs",
                column: "IpAddress");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_TenantId",
                table: "SecurityLogs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_Timestamp",
                table: "SecurityLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityLogs_UserId",
                table: "SecurityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_TenantId_Key",
                table: "SystemSettings",
                columns: new[] { "TenantId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "EmailSettings");

            migrationBuilder.DropTable(
                name: "PasswordPolicies");

            migrationBuilder.DropTable(
                name: "SecurityLogs");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("20998378-e35d-4997-b875-f58a88e04a9b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("569ad24e-ebe5-4db9-af4f-7953e5b9ced2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5e175eaa-fb74-4dba-8018-c70143ec4cea"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5f01e2f4-9d65-4766-bf69-107b0a405d67"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("5ffb814f-b4e7-4f6a-b6d5-79015f2ffff2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ac4bff78-09e3-451c-9999-00f2c45aef65"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ba99b2b2-fe9c-4d0b-9ec8-cc9a19c09c72"));

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Roles");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9224));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9267));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9270));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9272));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0b0e4d1d-ad38-47bf-b283-60654f64bd01"), null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9350), null, null, null, null, null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9349), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("22cc6d74-6998-49b1-bfb1-e38a5620b591"), null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9334), null, null, null, null, null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9330), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7d65cccf-7a63-439f-b893-2103d811bb0f"), null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9404), null, null, null, null, null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9403), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("891adcc2-b5ab-44f3-8604-999ae10791c9"), null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9476), null, null, null, null, null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9476), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("cd1f5db3-26cb-4b61-ae05-ac9e4ae05479"), null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9364), null, null, null, null, null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9363), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d8db69d5-988e-44db-b3bd-d539edd394cc"), null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9389), null, null, null, null, null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9389), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f96c8ce4-308d-440f-b2e6-f3a59379c027"), null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9376), null, null, null, null, null, new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9375), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 17, 20, 42, 6, 966, DateTimeKind.Utc).AddTicks(9069));
        }
    }
}
