using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPreventConcurrentLoginToSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("32cd4bd4-bbbe-4e58-b999-caa126feff07"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("774dac6f-5088-412b-b34d-e06da70a104c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("86c5b817-d90a-4270-a372-7541b9096d47"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("aeb4d33c-0dc0-4ef6-b1de-e72d06705454"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c69ec18f-45c6-428d-b97a-00ee418e7348"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d8bbaacd-b7f9-4cb2-9efa-619bbee5165a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ee44b8b6-b861-45e7-9c57-1f18c7105642"));

            migrationBuilder.AddColumn<int>(
                name: "PreventConcurrentLogin",
                table: "Securities",
                type: "int",
                nullable: false,
                defaultValue: 0);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("52867f8a-7cc7-4abb-bf9c-c85e9920d173"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("6a173c91-dda4-45ff-9396-79c8a5b665eb"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("83df94c4-fbfe-40c6-98b9-35898f67202f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("bf22c1ee-8ffc-4dc9-9a61-12721d96b6c2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("dbc9730e-8b21-484e-8555-15cca4a9ca24"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("dc2addd6-c6aa-4919-b6be-aacb3b5f0be4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ec3a51a3-edbc-4198-8e43-88e92936f7c4"));

            migrationBuilder.DropColumn(
                name: "PreventConcurrentLogin",
                table: "Securities");

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3428));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3469));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3472));

            migrationBuilder.UpdateData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3475));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("32cd4bd4-bbbe-4e58-b999-caa126feff07"), null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3591), null, null, null, null, null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3588), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("774dac6f-5088-412b-b34d-e06da70a104c"), null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3624), null, null, null, null, null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3623), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("86c5b817-d90a-4270-a372-7541b9096d47"), null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3680), null, null, null, null, null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3680), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("aeb4d33c-0dc0-4ef6-b1de-e72d06705454"), null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3668), null, null, null, null, null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3667), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c69ec18f-45c6-428d-b97a-00ee418e7348"), null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3609), null, null, null, null, null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3609), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d8bbaacd-b7f9-4cb2-9efa-619bbee5165a"), null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3654), null, null, null, null, null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3653), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ee44b8b6-b861-45e7-9c57-1f18c7105642"), null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3641), null, null, null, null, null, new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3640), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 9, 22, 0, 15, 23, 389, DateTimeKind.Utc).AddTicks(3242));
        }
    }
}
