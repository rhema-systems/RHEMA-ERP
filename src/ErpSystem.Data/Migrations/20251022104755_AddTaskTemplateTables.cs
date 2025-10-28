using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskTemplateTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_Employees_RequestedById",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_MaintenanceAssets_AssetId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_MaintenanceTypes_MaintenanceTypeId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_PriorityLevels_PriorityLevelId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCardApprovalStep_Employees_ApproverId",
                table: "JobCardApprovalStep");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCardComment_Employees_CommentById",
                table: "JobCardComment");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCardDocument_Employees_UploadedById",
                table: "JobCardDocument");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_GeneratedWorkOrderId",
                table: "JobCard");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("41fa25e2-da85-4bf0-8cc4-e506be470c59"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4dd0d8cb-5658-43ad-9713-0488e7015b59"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("545f68cc-8470-4146-ad05-edb915f64fa0"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("56251be0-d276-4b03-9091-97a7b04c6299"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8bbc8292-dce5-4c50-acfa-f806b348ab08"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("daab7151-1423-4d43-be63-5b0d5d59bf44"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("db2bf769-9728-4c6d-a836-3b186e4bc1cc"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8481));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8521));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8527));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8528));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8770));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8779));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8784));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8788));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8797));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8803));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8807));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8811));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8817));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8824));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8829));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8833));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8842));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8855));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8859));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8863));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8915));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8916));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8917));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8918));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8918));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8926));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8927));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8928));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8929));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8930));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8930));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8931));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8932));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8932));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8933));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8933));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9197));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9199));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9200));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9200));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9201));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9201));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9202));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9202));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9203));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9204));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9204));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9205));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9205));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9206));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9206));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9275));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9276));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9278));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9279));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9279));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9280));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9280));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9281));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9282));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9282));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9292));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9293));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(9294));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("0e20fda3-56ce-4480-8459-6057e63605c9"), null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8597), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8594), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("14affeff-982f-4596-884c-55196a1d8f92"), null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8615), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8615), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("90193f22-152e-4dc9-a008-ab38ba0296ec"), null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8686), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8686), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ccc95590-dea3-4c05-b1bd-9c7572c80027"), null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8631), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8631), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("dbb78c12-45a4-44c1-9655-0057fc51203b"), null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8648), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8647), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e421a076-4ddb-4056-95d9-e77bfd72bd8b"), null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8700), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8699), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e5b1d674-354a-4cb0-9760-2268e10a6488"), null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8662), null, null, null, null, null, null, new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8662), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 10, 47, 51, 621, DateTimeKind.Utc).AddTicks(8216));

            migrationBuilder.CreateIndex(
                name: "IX_JobCardDocument_DocumentType",
                table: "JobCardDocument",
                column: "DocumentType");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardDocument_UploadedDate",
                table: "JobCardDocument",
                column: "UploadedDate");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardComment_CommentDate",
                table: "JobCardComment",
                column: "CommentDate");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardComment_CommentType",
                table: "JobCardComment",
                column: "CommentType");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardApprovalStep_Status",
                table: "JobCardApprovalStep",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobCardApprovalStep_StepOrder",
                table: "JobCardApprovalStep",
                column: "StepOrder");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_ApprovalStatus",
                table: "JobCard",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_GeneratedWorkOrderId",
                table: "JobCard",
                column: "GeneratedWorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_JobCardNumber",
                table: "JobCard",
                column: "JobCardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_JobCardStatus",
                table: "JobCard",
                column: "JobCardStatus");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_RequestedDate",
                table: "JobCard",
                column: "RequestedDate");

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_RequiredCompletionDate",
                table: "JobCard",
                column: "RequiredCompletionDate");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_Employees_RequestedById",
                table: "JobCard",
                column: "RequestedById",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_MaintenanceAssets_AssetId",
                table: "JobCard",
                column: "AssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_MaintenanceTypes_MaintenanceTypeId",
                table: "JobCard",
                column: "MaintenanceTypeId",
                principalTable: "MaintenanceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_PriorityLevels_PriorityLevelId",
                table: "JobCard",
                column: "PriorityLevelId",
                principalTable: "PriorityLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCardApprovalStep_Employees_ApproverId",
                table: "JobCardApprovalStep",
                column: "ApproverId",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCardComment_Employees_CommentById",
                table: "JobCardComment",
                column: "CommentById",
                principalTable: "Employees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCardDocument_Employees_UploadedById",
                table: "JobCardDocument",
                column: "UploadedById",
                principalTable: "Employees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_Employees_RequestedById",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_MaintenanceAssets_AssetId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_MaintenanceTypes_MaintenanceTypeId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCard_PriorityLevels_PriorityLevelId",
                table: "JobCard");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCardApprovalStep_Employees_ApproverId",
                table: "JobCardApprovalStep");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCardComment_Employees_CommentById",
                table: "JobCardComment");

            migrationBuilder.DropForeignKey(
                name: "FK_JobCardDocument_Employees_UploadedById",
                table: "JobCardDocument");

            migrationBuilder.DropIndex(
                name: "IX_JobCardDocument_DocumentType",
                table: "JobCardDocument");

            migrationBuilder.DropIndex(
                name: "IX_JobCardDocument_UploadedDate",
                table: "JobCardDocument");

            migrationBuilder.DropIndex(
                name: "IX_JobCardComment_CommentDate",
                table: "JobCardComment");

            migrationBuilder.DropIndex(
                name: "IX_JobCardComment_CommentType",
                table: "JobCardComment");

            migrationBuilder.DropIndex(
                name: "IX_JobCardApprovalStep_Status",
                table: "JobCardApprovalStep");

            migrationBuilder.DropIndex(
                name: "IX_JobCardApprovalStep_StepOrder",
                table: "JobCardApprovalStep");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_ApprovalStatus",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_GeneratedWorkOrderId",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_JobCardNumber",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_JobCardStatus",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_RequestedDate",
                table: "JobCard");

            migrationBuilder.DropIndex(
                name: "IX_JobCard_RequiredCompletionDate",
                table: "JobCard");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("0e20fda3-56ce-4480-8459-6057e63605c9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("14affeff-982f-4596-884c-55196a1d8f92"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("90193f22-152e-4dc9-a008-ab38ba0296ec"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ccc95590-dea3-4c05-b1bd-9c7572c80027"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("dbb78c12-45a4-44c1-9655-0057fc51203b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e421a076-4ddb-4056-95d9-e77bfd72bd8b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e5b1d674-354a-4cb0-9760-2268e10a6488"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5709));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5763));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5766));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5767));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5970));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5987));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5993));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5998));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6012));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6018));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6023));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6027));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6034));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6042));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6047));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6051));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6058));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6071));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6076));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6080));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6112));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6116));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6117));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6118));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6118));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6119));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6120));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6121));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6121));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6122));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6123));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6124));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6124));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6125));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6125));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6126));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6192));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6194));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6195));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6195));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6196));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6197));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6197));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6198));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6198));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6199));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6199));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6200));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6201));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6201));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6206));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6236));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6237));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6239));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6239));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6240));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6241));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6241));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6242));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6242));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6243));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6256));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6257));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(6258));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("41fa25e2-da85-4bf0-8cc4-e506be470c59"), null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5842), null, null, null, null, null, null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5842), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4dd0d8cb-5658-43ad-9713-0488e7015b59"), null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5866), null, null, null, null, null, null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5866), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("545f68cc-8470-4146-ad05-edb915f64fa0"), null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5828), null, null, null, null, null, null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5826), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("56251be0-d276-4b03-9091-97a7b04c6299"), null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5878), null, null, null, null, null, null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5878), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8bbc8292-dce5-4c50-acfa-f806b348ab08"), null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5901), null, null, null, null, null, null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5901), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("daab7151-1423-4d43-be63-5b0d5d59bf44"), null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5890), null, null, null, null, null, null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5890), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("db2bf769-9728-4c6d-a836-3b186e4bc1cc"), null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5854), null, null, null, null, null, null, new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5854), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 20, 18, 19, 10, 53, DateTimeKind.Utc).AddTicks(5551));

            migrationBuilder.CreateIndex(
                name: "IX_JobCard_GeneratedWorkOrderId",
                table: "JobCard",
                column: "GeneratedWorkOrderId",
                unique: true,
                filter: "[GeneratedWorkOrderId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_Employees_RequestedById",
                table: "JobCard",
                column: "RequestedById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_MaintenanceAssets_AssetId",
                table: "JobCard",
                column: "AssetId",
                principalTable: "MaintenanceAssets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_MaintenanceTypes_MaintenanceTypeId",
                table: "JobCard",
                column: "MaintenanceTypeId",
                principalTable: "MaintenanceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCard_PriorityLevels_PriorityLevelId",
                table: "JobCard",
                column: "PriorityLevelId",
                principalTable: "PriorityLevels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCardApprovalStep_Employees_ApproverId",
                table: "JobCardApprovalStep",
                column: "ApproverId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCardComment_Employees_CommentById",
                table: "JobCardComment",
                column: "CommentById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_JobCardDocument_Employees_UploadedById",
                table: "JobCardDocument",
                column: "UploadedById",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
