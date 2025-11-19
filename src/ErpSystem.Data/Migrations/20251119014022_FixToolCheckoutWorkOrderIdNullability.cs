using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixToolCheckoutWorkOrderIdNullability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Explicitly make ToolCheckouts.WorkOrderId nullable for existing databases
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'WorkOrderId' AND Object_ID = Object_ID(N'dbo.ToolCheckouts'))
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] ALTER COLUMN [WorkOrderId] UNIQUEIDENTIFIER NULL;
END
");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0ab4039d-1def-4121-ac36-648c3caa87a2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0c5fd25b-39e1-4684-b29b-1fa5ad7d22d3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("152fba20-b06a-4eeb-a361-0239c3f6d4eb"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4baa16e9-a09b-4b97-8d1b-f591aded5d6f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7d2170e2-7dfc-4f5a-8fa4-fdd6bc74c332"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("842a02e9-f9f7-4225-bdc8-5511371b430d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c85e4234-acd4-406c-94b2-db260333d672"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2354));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2423));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2426));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2428));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2732));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2743));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2750));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2757));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2768));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2778));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2785));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2791));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2802));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2812));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2819));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2826));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2837));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2859));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2867));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2873));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2926));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2929));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2930));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2931));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2932));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2939));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2940));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2941));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2942));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2943));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2944));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2945));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2946));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2947));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2948));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2948));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2991));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2993));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2994));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2995));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2996));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2997));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2997));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2998));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2999));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3000));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3000));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3001));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3002));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3003));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3003));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3062));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3064));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3066));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3067));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3068));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3068));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3069));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3070));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3071));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3072));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3085));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3086));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(3087));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("3e4477be-a9dd-45ce-88ab-6185836b23c1"), null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2571), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2570), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3fa539b8-7ffa-46c8-a5e0-9874da191bb3"), null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2535), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2534), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("63bcbe36-c2b1-459a-9431-e73613a9a880"), null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2553), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2552), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("663d27f4-b35a-45a3-9370-8c33d460740e"), null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2515), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2511), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("bd87eeea-c96c-49d1-be7b-3a1122662ce7"), null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2615), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2615), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("cf6aed88-e39a-4251-996d-3663ad2169b1"), null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2632), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2631), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d8b098b7-ed1b-46f7-b0e5-7576d32ebbcb"), null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2590), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2590), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 40, 20, 413, DateTimeKind.Utc).AddTicks(2089));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert ToolCheckouts.WorkOrderId to NOT NULL (may fail if NULLs exist)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.columns WHERE Name = N'WorkOrderId' AND Object_ID = Object_ID(N'dbo.ToolCheckouts'))
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] ALTER COLUMN [WorkOrderId] UNIQUEIDENTIFIER NOT NULL;
END
");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3e4477be-a9dd-45ce-88ab-6185836b23c1"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3fa539b8-7ffa-46c8-a5e0-9874da191bb3"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("63bcbe36-c2b1-459a-9431-e73613a9a880"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("663d27f4-b35a-45a3-9370-8c33d460740e"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("bd87eeea-c96c-49d1-be7b-3a1122662ce7"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("cf6aed88-e39a-4251-996d-3663ad2169b1"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d8b098b7-ed1b-46f7-b0e5-7576d32ebbcb"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(2862));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(2940));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(2943));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(2945));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3210));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3236));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3242));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3248));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3259));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3267));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3273));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3281));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3291));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3301));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3308));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3325));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3337));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3361));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3376));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3383));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3441));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3445));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3455));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3456));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3457));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3458));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3460));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3461));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3462));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3463));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3464));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3464));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3465));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3523));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3526));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3527));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3527));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3530));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3531));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3532));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3533));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3534));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3534));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3535));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3536));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3607));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3608));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3610));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3611));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3612));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3613));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3614));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3615));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3616));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3617));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3630));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3631));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3632));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0ab4039d-1def-4121-ac36-648c3caa87a2"), null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3040), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3040), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("0c5fd25b-39e1-4684-b29b-1fa5ad7d22d3"), null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3031), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3030), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("152fba20-b06a-4eeb-a361-0239c3f6d4eb"), null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3076), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3076), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4baa16e9-a09b-4b97-8d1b-f591aded5d6f"), null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3084), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3084), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7d2170e2-7dfc-4f5a-8fa4-fdd6bc74c332"), null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3057), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3056), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("842a02e9-f9f7-4225-bdc8-5511371b430d"), null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3017), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3014), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c85e4234-acd4-406c-94b2-db260333d672"), null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3048), null, null, null, null, null, null, new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(3048), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 19, 1, 36, 15, 235, DateTimeKind.Utc).AddTicks(2519));
        }
    }
}
