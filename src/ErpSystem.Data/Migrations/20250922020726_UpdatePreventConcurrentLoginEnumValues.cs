using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePreventConcurrentLoginEnumValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Commented out conflicting DeleteData operations - these modules don't exist with these IDs
            // migrationBuilder.DeleteData operations removed to avoid conflicts

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 2, 7, 25, 882, DateTimeKind.Utc).AddTicks(4292));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 2, 7, 25, 882, DateTimeKind.Utc).AddTicks(4330));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 2, 7, 25, 882, DateTimeKind.Utc).AddTicks(4332));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 2, 7, 25, 882, DateTimeKind.Utc).AddTicks(4335));

            // Commented out conflicting InsertData operations - modules with these names already exist
            // migrationBuilder.InsertData operations removed to avoid conflicts

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 2, 7, 25, 882, DateTimeKind.Utc).AddTicks(4084));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("40e48e9c-9bda-4721-8718-1ad97932e650"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4532a345-f79a-4048-899a-43f965766d96"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("665a3c03-da8c-44c9-b373-8ada607a06cd"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("70087f9d-0ae1-481c-a173-a753a77bcc12"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9f803ca9-421d-489c-9082-eeb3706f85b8"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a284f3db-aeec-4488-b503-a859f5623cb3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a9e7b1f5-010c-4632-af4d-c53d7b8b16fe"));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1680));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1746));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1751));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1756));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("52867f8a-7cc7-4abb-bf9c-c85e9920d173"), null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(2050), null, null, null, null, null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(2049), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("6a173c91-dda4-45ff-9396-79c8a5b665eb"), null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(2022), null, null, null, null, null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(2021), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("83df94c4-fbfe-40c6-98b9-35898f67202f"), null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1996), null, null, null, null, null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1995), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("bf22c1ee-8ffc-4dc9-9a61-12721d96b6c2"), null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1908), null, null, null, null, null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1903), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("dbc9730e-8b21-484e-8555-15cca4a9ca24"), null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(2077), null, null, null, null, null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(2077), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("dc2addd6-c6aa-4919-b6be-aacb3b5f0be4"), null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1970), null, null, null, null, null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1969), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ec3a51a3-edbc-4198-8e43-88e92936f7c4"), null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1942), null, null, null, null, null, new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1941), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 1, 58, 58, 476, DateTimeKind.Utc).AddTicks(1275));
        }
    }
}
