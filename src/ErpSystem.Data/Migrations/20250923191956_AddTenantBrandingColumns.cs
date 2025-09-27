using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantBrandingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddColumn<string>(
                name: "CoverImageUrl",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FaviconUrl",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryColor",
                table: "Tenants",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondaryColor",
                table: "Tenants",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: true);

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
                columns: new[] { "CoverImageUrl", "CreatedAt", "FaviconUrl", "PrimaryColor", "SecondaryColor" },
                values: new object[] { null, new DateTime(2025, 9, 23, 19, 19, 55, 277, DateTimeKind.Utc).AddTicks(2385), null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(
                name: "CoverImageUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "FaviconUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PrimaryColor",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SecondaryColor",
                table: "Tenants");

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
    }
}
