using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserTenantRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_UserName",
                table: "Users");

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
                name: "TenantId",
                table: "Users");

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

            migrationBuilder.CreateTable(
                name: "UserTenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccessLevel = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GrantedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_UserTenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTenants_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenants_Users_UserId",
                        column: x => x.UserId,
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
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true,
                filter: "[UserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationUserTenant_UsersId",
                table: "ApplicationUserTenant",
                column: "UsersId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_IsDefault",
                table: "UserTenants",
                column: "IsDefault");

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
                name: "IX_UserTenants_UserId_TenantId",
                table: "UserTenants",
                columns: new[] { "UserId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationUserTenant");

            migrationBuilder.DropTable(
                name: "UserTenants");

            migrationBuilder.DropIndex(
                name: "IX_Users_UserName",
                table: "Users");

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

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2251));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2301));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2386));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 18, 17, 13, 30, 772, DateTimeKind.Utc).AddTicks(2389));

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
                name: "IX_Users_TenantId_UserName",
                table: "Users",
                columns: new[] { "TenantId", "UserName" },
                unique: true,
                filter: "[UserName] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId",
                table: "Users",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
