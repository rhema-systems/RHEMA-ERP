using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyToolCheckoutColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop legacy columns from ToolCheckouts that are no longer used by the EF model
            // and were causing NOT NULL insert errors (Condition, Quantity, CheckedOutAt, IsReturned, IsActive).
            // We first drop any default constraints on these columns before dropping the columns themselves.
            migrationBuilder.Sql(@"
DECLARE @sql NVARCHAR(MAX) = N'';

-- Drop default constraints on the legacy columns if they exist
SELECT @sql = @sql + 'ALTER TABLE [dbo].[ToolCheckouts] DROP CONSTRAINT [' + dc.name + '];' + CHAR(13)
FROM sys.default_constraints AS dc
INNER JOIN sys.columns AS c ON c.default_object_id = dc.object_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.ToolCheckouts')
  AND c.name IN ('Condition', 'Quantity', 'CheckedOutAt', 'IsReturned', 'IsActive');

IF @sql <> N''
    EXEC sp_executesql @sql;

-- Now drop the columns if they still exist
IF COL_LENGTH('dbo.ToolCheckouts', 'Condition') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] DROP COLUMN [Condition];
END

IF COL_LENGTH('dbo.ToolCheckouts', 'Quantity') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] DROP COLUMN [Quantity];
END

IF COL_LENGTH('dbo.ToolCheckouts', 'CheckedOutAt') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] DROP COLUMN [CheckedOutAt];
END

IF COL_LENGTH('dbo.ToolCheckouts', 'IsReturned') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] DROP COLUMN [IsReturned];
END

IF COL_LENGTH('dbo.ToolCheckouts', 'IsActive') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] DROP COLUMN [IsActive];
END

");
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("04e8fe6c-c146-42d3-85ee-c67b9ad735fb"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("16e14419-3663-46a0-babd-f96dc6a7529b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("359c537b-1a85-4773-9c24-2f268a24acfa"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a19acac7-418e-4be6-a1dc-d3e5762965a7"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a99de392-3f49-47b2-ba53-86ea1446a5db"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b52db09c-90ab-4cfa-b26c-ac72ce900a21"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("de2c230c-6b87-48ab-877c-f65506e5ad69"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5530));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5591));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5594));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5596));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5882));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5892));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5900));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5906));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5925));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5940));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5945));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5952));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5961));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5974));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5981));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5988));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6000));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6015));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6021));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6027));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6079));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6081));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6082));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6083));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6084));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6086));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6087));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6088));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6089));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6090));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6091));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6092));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6092));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6093));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6094));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6095));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6147));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6150));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6151));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6151));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6152));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6153));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6154));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6154));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6155));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6156));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6157));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6157));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6158));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6159));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6160));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6228));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6230));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6232));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6233));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6234));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6234));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6235));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6236));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6237));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6237));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6250));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6251));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(6252));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0bb0c2f3-cbbd-4120-aff1-e8bd0ef84b11"), null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5694), null, null, null, null, null, null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5693), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3dea18f8-6d6e-4b06-9589-8340c652dd2f"), null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5724), null, null, null, null, null, null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5724), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7f27571a-4095-47a0-bb09-68dac92a4111"), null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5709), null, null, null, null, null, null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5708), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b6313e5f-fd8f-4048-ab60-4836ff15cb78"), null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5754), null, null, null, null, null, null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5753), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d957c522-9634-490e-8225-bca7fec17d74"), null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5677), null, null, null, null, null, null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5672), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e15e3cdb-8933-4b49-97f2-d8085e6d2d87"), null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5767), null, null, null, null, null, null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5767), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f0081224-8f97-48ea-95df-801c02e64c8b"), null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5740), null, null, null, null, null, null, new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5739), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 18, 17, 55, 29, 814, DateTimeKind.Utc).AddTicks(5213));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate legacy columns in case of migration rollback.
            // Definitions approximate original schema and are for rollback compatibility only.
            migrationBuilder.Sql(@"
IF COL_LENGTH('dbo.ToolCheckouts', 'Condition') IS NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] ADD [Condition] NVARCHAR(50) NOT NULL DEFAULT('Good');
END

IF COL_LENGTH('dbo.ToolCheckouts', 'Quantity') IS NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] ADD [Quantity] INT NOT NULL DEFAULT(1);
END

IF COL_LENGTH('dbo.ToolCheckouts', 'CheckedOutAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] ADD [CheckedOutAt] DATETIME2 NOT NULL DEFAULT(SYSUTCDATETIME());
END

IF COL_LENGTH('dbo.ToolCheckouts', 'IsReturned') IS NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] ADD [IsReturned] BIT NOT NULL DEFAULT(0);
END

IF COL_LENGTH('dbo.ToolCheckouts', 'IsActive') IS NULL
BEGIN
    ALTER TABLE [dbo].[ToolCheckouts] ADD [IsActive] BIT NOT NULL DEFAULT(1);
END
");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0bb0c2f3-cbbd-4120-aff1-e8bd0ef84b11"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3dea18f8-6d6e-4b06-9589-8340c652dd2f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7f27571a-4095-47a0-bb09-68dac92a4111"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b6313e5f-fd8f-4048-ab60-4836ff15cb78"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d957c522-9634-490e-8225-bca7fec17d74"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e15e3cdb-8933-4b49-97f2-d8085e6d2d87"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f0081224-8f97-48ea-95df-801c02e64c8b"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3102));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3162));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3164));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3167));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3403));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3424));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3434));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3441));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3458));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3466));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3473));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3481));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3491));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3503));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3512));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3548));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3559));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3571));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3584));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3590));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3634));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3636));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3638));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3638));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3639));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3641));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3642));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3643));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3644));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3645));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3646));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3647));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3648));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3649));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3657));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3658));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3707));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3709));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3710));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3711));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3712));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3713));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3714));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3714));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3715));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3716));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3717));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3718));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3719));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3720));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3720));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3799));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3800));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3802));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3803));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3804));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3805));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3806));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3806));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3807));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3808));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3820));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3822));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3823));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("04e8fe6c-c146-42d3-85ee-c67b9ad735fb"), null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3270), null, null, null, null, null, null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3270), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("16e14419-3663-46a0-babd-f96dc6a7529b"), null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3233), null, null, null, null, null, null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3230), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("359c537b-1a85-4773-9c24-2f268a24acfa"), null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3256), null, null, null, null, null, null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3255), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a19acac7-418e-4be6-a1dc-d3e5762965a7"), null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3263), null, null, null, null, null, null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3263), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a99de392-3f49-47b2-ba53-86ea1446a5db"), null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3279), null, null, null, null, null, null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3279), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b52db09c-90ab-4cfa-b26c-ac72ce900a21"), null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3297), null, null, null, null, null, null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3296), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("de2c230c-6b87-48ab-877c-f65506e5ad69"), null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3248), null, null, null, null, null, null, new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(3247), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 22, 11, 51, 455, DateTimeKind.Utc).AddTicks(2783));
        }
    }
}
