using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameDataSourcesToReportDataSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DataSources_Users_CreatedByUserId",
                table: "DataSources");

            migrationBuilder.DropForeignKey(
                name: "FK_DataSourceUsageLogs_DataSources_DataSourceId",
                table: "DataSourceUsageLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DataSourceUsageLogs",
                table: "DataSourceUsageLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DataSources",
                table: "DataSources");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("24ba4dff-6e1d-4d8c-8c48-9418623192e9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("362e283e-77ae-4fc2-8944-da2277cc3ef6"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7583e5a4-c339-4754-92bc-e5104221e664"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("85837a5e-79bd-46dd-af8d-b53270d8b279"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("be0fd170-67a9-461b-904f-32d3735d68f6"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c3bf06b5-8f92-4f97-9d5b-ada7235494f9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d5a224dd-7b8f-4b82-986f-fc59f3fd976d"));

            migrationBuilder.RenameTable(
                name: "DataSourceUsageLogs",
                newName: "ReportDataSourceUsageLogs");

            migrationBuilder.RenameTable(
                name: "DataSources",
                newName: "ReportDataSources");

            migrationBuilder.RenameIndex(
                name: "IX_DataSourceUsageLogs_UserId",
                table: "ReportDataSourceUsageLogs",
                newName: "IX_ReportDataSourceUsageLogs_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_DataSourceUsageLogs_TenantId",
                table: "ReportDataSourceUsageLogs",
                newName: "IX_ReportDataSourceUsageLogs_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_DataSourceUsageLogs_Success",
                table: "ReportDataSourceUsageLogs",
                newName: "IX_ReportDataSourceUsageLogs_Success");

            migrationBuilder.RenameIndex(
                name: "IX_DataSourceUsageLogs_OperationType",
                table: "ReportDataSourceUsageLogs",
                newName: "IX_ReportDataSourceUsageLogs_OperationType");

            migrationBuilder.RenameIndex(
                name: "IX_DataSourceUsageLogs_DataSourceId",
                table: "ReportDataSourceUsageLogs",
                newName: "IX_ReportDataSourceUsageLogs_DataSourceId");

            migrationBuilder.RenameIndex(
                name: "IX_DataSourceUsageLogs_AccessedAt",
                table: "ReportDataSourceUsageLogs",
                newName: "IX_ReportDataSourceUsageLogs_AccessedAt");

            migrationBuilder.RenameIndex(
                name: "IX_DataSources_Type",
                table: "ReportDataSources",
                newName: "IX_ReportDataSources_Type");

            migrationBuilder.RenameIndex(
                name: "IX_DataSources_TenantId",
                table: "ReportDataSources",
                newName: "IX_ReportDataSources_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_DataSources_Name",
                table: "ReportDataSources",
                newName: "IX_ReportDataSources_Name");

            migrationBuilder.RenameIndex(
                name: "IX_DataSources_LastUsed",
                table: "ReportDataSources",
                newName: "IX_ReportDataSources_LastUsed");

            migrationBuilder.RenameIndex(
                name: "IX_DataSources_IsActive",
                table: "ReportDataSources",
                newName: "IX_ReportDataSources_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_DataSources_CreatedByUserId",
                table: "ReportDataSources",
                newName: "IX_ReportDataSources_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_DataSources_CreatedAt",
                table: "ReportDataSources",
                newName: "IX_ReportDataSources_CreatedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReportDataSourceUsageLogs",
                table: "ReportDataSourceUsageLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReportDataSources",
                table: "ReportDataSources",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7558));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7592));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7594));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7595));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7767));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7774));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7806));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7811));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7824));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7829));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7832));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7838));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7845));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7852));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7856));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7860));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7866));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7869));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7872));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7874));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7907));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7909));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7910));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7910));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7911));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7913));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7913));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7914));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7915));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7916));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7916));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7917));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7917));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7918));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7919));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7919));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7946));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7948));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7949));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7949));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7950));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7950));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7951));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7952));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7952));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7953));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7953));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7954));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7978));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7979));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7979));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8008));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8009));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8010));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8011));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8011));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8012));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8013));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8013));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8014));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8014));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8026));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8027));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(8027));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("30a3e6c3-87ed-46e7-a197-6ec766bc99d0"), null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7709), null, null, null, null, null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7708), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("445c5e8b-44e6-420d-b9ef-43ab9768b10f"), null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7655), null, null, null, null, null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7654), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("49ab0eaf-963c-4e4c-a837-9a5a7bb90c2d"), null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7641), null, null, null, null, null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7639), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("53aaeb39-7aff-4874-8283-2aff8ccf32ed"), null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7689), null, null, null, null, null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7688), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("78fa0448-3fc8-48bb-9f9b-ed667d0bcd5c"), null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7678), null, null, null, null, null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7678), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b6914bd5-8e5a-4d15-94a0-a7f3b9246838"), null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7700), null, null, null, null, null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7699), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c748e9f7-570b-43f4-a006-14cc95c134c0"), null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7667), null, null, null, null, null, new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7667), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 4, 9, 28, 253, DateTimeKind.Utc).AddTicks(7419));

            migrationBuilder.AddForeignKey(
                name: "FK_ReportDataSources_Users_CreatedByUserId",
                table: "ReportDataSources",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportDataSourceUsageLogs_ReportDataSources_DataSourceId",
                table: "ReportDataSourceUsageLogs",
                column: "DataSourceId",
                principalTable: "ReportDataSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportDataSources_Users_CreatedByUserId",
                table: "ReportDataSources");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportDataSourceUsageLogs_ReportDataSources_DataSourceId",
                table: "ReportDataSourceUsageLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReportDataSourceUsageLogs",
                table: "ReportDataSourceUsageLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReportDataSources",
                table: "ReportDataSources");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("30a3e6c3-87ed-46e7-a197-6ec766bc99d0"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("445c5e8b-44e6-420d-b9ef-43ab9768b10f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("49ab0eaf-963c-4e4c-a837-9a5a7bb90c2d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("53aaeb39-7aff-4874-8283-2aff8ccf32ed"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("78fa0448-3fc8-48bb-9f9b-ed667d0bcd5c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b6914bd5-8e5a-4d15-94a0-a7f3b9246838"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c748e9f7-570b-43f4-a006-14cc95c134c0"));

            migrationBuilder.RenameTable(
                name: "ReportDataSourceUsageLogs",
                newName: "DataSourceUsageLogs");

            migrationBuilder.RenameTable(
                name: "ReportDataSources",
                newName: "DataSources");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSourceUsageLogs_UserId",
                table: "DataSourceUsageLogs",
                newName: "IX_DataSourceUsageLogs_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSourceUsageLogs_TenantId",
                table: "DataSourceUsageLogs",
                newName: "IX_DataSourceUsageLogs_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSourceUsageLogs_Success",
                table: "DataSourceUsageLogs",
                newName: "IX_DataSourceUsageLogs_Success");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSourceUsageLogs_OperationType",
                table: "DataSourceUsageLogs",
                newName: "IX_DataSourceUsageLogs_OperationType");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSourceUsageLogs_DataSourceId",
                table: "DataSourceUsageLogs",
                newName: "IX_DataSourceUsageLogs_DataSourceId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSourceUsageLogs_AccessedAt",
                table: "DataSourceUsageLogs",
                newName: "IX_DataSourceUsageLogs_AccessedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSources_Type",
                table: "DataSources",
                newName: "IX_DataSources_Type");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSources_TenantId",
                table: "DataSources",
                newName: "IX_DataSources_TenantId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSources_Name",
                table: "DataSources",
                newName: "IX_DataSources_Name");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSources_LastUsed",
                table: "DataSources",
                newName: "IX_DataSources_LastUsed");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSources_IsActive",
                table: "DataSources",
                newName: "IX_DataSources_IsActive");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSources_CreatedByUserId",
                table: "DataSources",
                newName: "IX_DataSources_CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ReportDataSources_CreatedAt",
                table: "DataSources",
                newName: "IX_DataSources_CreatedAt");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DataSourceUsageLogs",
                table: "DataSourceUsageLogs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DataSources",
                table: "DataSources",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5281));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5332));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5334));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5336));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5692));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5768));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5774));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5784));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5793));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5806));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5811));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5817));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5826));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5834));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5840));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5845));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5853));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5858));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5869));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5874));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5919));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5921));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5922));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5923));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5923));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5925));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5926));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5926));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5927));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5928));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5929));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5930));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5930));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5931));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5932));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5932));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5979));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5981));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5982));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5983));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5983));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6027));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6028));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6029));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6029));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6030));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6031));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6031));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6032));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6033));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6033));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6161));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6163));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6167));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6168));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6169));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6169));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6170));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6171));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6171));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6172));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6192));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6193));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(6194));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("24ba4dff-6e1d-4d8c-8c48-9418623192e9"), null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5613), null, null, null, null, null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5612), false, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("362e283e-77ae-4fc2-8944-da2277cc3ef6"), null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5548), null, null, null, null, null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5548), false, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7583e5a4-c339-4754-92bc-e5104221e664"), null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5601), null, null, null, null, null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5600), false, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("85837a5e-79bd-46dd-af8d-b53270d8b279"), null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5588), null, null, null, null, null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5587), false, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("be0fd170-67a9-461b-904f-32d3735d68f6"), null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5574), null, null, null, null, null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5574), false, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c3bf06b5-8f92-4f97-9d5b-ada7235494f9"), null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5519), null, null, null, null, null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5517), false, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d5a224dd-7b8f-4b82-986f-fc59f3fd976d"), null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5562), null, null, null, null, null, new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5562), false, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 8, 3, 30, 50, 772, DateTimeKind.Utc).AddTicks(5093));

            migrationBuilder.AddForeignKey(
                name: "FK_DataSources_Users_CreatedByUserId",
                table: "DataSources",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DataSourceUsageLogs_DataSources_DataSourceId",
                table: "DataSourceUsageLogs",
                column: "DataSourceId",
                principalTable: "DataSources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
