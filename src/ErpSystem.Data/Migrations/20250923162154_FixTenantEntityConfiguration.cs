using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixTenantEntityConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Tenants_TenantId1",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId1",
                table: "Users");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("1da24716-fd56-44b0-8d2c-b4e7d6850c9b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4643d21c-6433-49b7-9026-66c7039d7351"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a7673475-7689-4253-81ec-62894c352cb9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ac1534db-0b3a-4f1a-8da4-600e8d3609ae"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("acdad383-4d21-4f5b-9fc3-2e16220d7102"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c5f28f1e-92ea-43c7-883c-03ed0485b45b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("df39c1d7-5c64-415d-9485-fe50d7c4f821"));

            migrationBuilder.DropColumn(
                name: "TenantId1",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(7629));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(7713));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(7719));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(7725));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("36ca7275-9ce5-4f00-a912-aa63425f0ac2"), null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(7972), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(7965), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("44b5c7b7-464e-457f-bbf6-6edf2fb04053"), null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8163), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8163), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("64b55479-2ed0-4613-9dcc-4c3db0d38fcb"), null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8137), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8136), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6ad704b8-2d8f-4894-a2ac-7aaf8b61c21e"), null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8103), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8102), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a73e51bb-eb0f-42e7-ba22-ac8d50a7fe8c"), null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8077), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8076), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c59f5e2c-a983-444c-ad52-613f0444fe98"), null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8047), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8046), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("db83ef08-fe98-4236-8cf4-1834cfa077d9"), null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8010), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(8009), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 21, 53, 84, DateTimeKind.Utc).AddTicks(7267));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("36ca7275-9ce5-4f00-a912-aa63425f0ac2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("44b5c7b7-464e-457f-bbf6-6edf2fb04053"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("64b55479-2ed0-4613-9dcc-4c3db0d38fcb"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6ad704b8-2d8f-4894-a2ac-7aaf8b61c21e"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a73e51bb-eb0f-42e7-ba22-ac8d50a7fe8c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c59f5e2c-a983-444c-ad52-613f0444fe98"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("db83ef08-fe98-4236-8cf4-1834cfa077d9"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId1",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(477));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(546));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(549));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(552));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("1da24716-fd56-44b0-8d2c-b4e7d6850c9b"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(671), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(671), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4643d21c-6433-49b7-9026-66c7039d7351"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(854), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(854), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a7673475-7689-4253-81ec-62894c352cb9"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(649), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(644), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ac1534db-0b3a-4f1a-8da4-600e8d3609ae"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(874), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(873), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("acdad383-4d21-4f5b-9fc3-2e16220d7102"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(710), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(709), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c5f28f1e-92ea-43c7-883c-03ed0485b45b"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(689), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("df39c1d7-5c64-415d-9485-fe50d7c4f821"), null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(891), null, null, null, null, null, new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(891), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 23, 16, 10, 6, 229, DateTimeKind.Utc).AddTicks(207));

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId1",
                table: "Users",
                column: "TenantId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Tenants_TenantId1",
                table: "Users",
                column: "TenantId1",
                principalTable: "Tenants",
                principalColumn: "Id");
        }
    }
}
