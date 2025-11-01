using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobCardIdToWorkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSkills_Skill_SkillId",
                table: "EmployeeSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_Skill_SkillId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropForeignKey(
                name: "FK_Skill_Tenants_TenantId",
                table: "Skill");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Skill",
                table: "Skill");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("346353c7-1906-4fdd-9ccf-1ada1dc40b07"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("51b927e4-fafc-4bfb-a823-422c30f274a2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9149b205-e539-4054-86ac-8fecf16c20a4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("af4d6be9-5747-4cf3-8271-a1df15f8d2dd"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b18c6a31-0279-46a4-a827-213d104e4f25"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("fd0d432a-8a11-414e-8e43-c4c0d62c6b12"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ff109964-77cd-4211-a477-3f5751734d8a"));

            migrationBuilder.RenameTable(
                name: "Skill",
                newName: "Skills");

            migrationBuilder.RenameIndex(
                name: "IX_Skill_TenantId",
                table: "Skills",
                newName: "IX_Skills_TenantId");

            migrationBuilder.AddColumn<Guid>(
                name: "JobCardId",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Skills",
                table: "Skills",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3342));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3440));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3444));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3447));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3949));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3974));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3983));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3991));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4005));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4014));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4023));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4031));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4044));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4125));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4134));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4141));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4153));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4183));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4214));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4227));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4419));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4427));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4429));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4430));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4432));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4434));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4435));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4445));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(4447));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5386));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5389));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5390));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5391));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5392));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5393));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5394));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5395));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5396));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5397));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5398));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5399));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5400));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5401));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5402));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5567));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5569));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5571));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5572));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5573));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5574));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5575));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5576));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5582));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5583));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5603));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5604));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(5606));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("32715b1c-6308-45dc-9f6d-448600185404"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3736), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3736), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("53ff0afe-b958-44aa-ae42-7dcf80863fa9"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3680), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3679), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("625a4223-2641-484b-8f38-34a3108cd02f"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3698), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3697), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("792f5e49-edd7-4058-a615-f114756619e4"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3660), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3659), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("98b86b4b-4c2b-4d3c-99f7-2dea8263a072"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3634), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3628), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("99901b55-7bdd-4787-a80b-6a5c16b11e0a"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3755), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3755), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("df25c4a7-3a5d-42fc-874f-354023468c0a"), null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3715), null, null, null, null, null, null, new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(3715), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 30, 0, 45, 8, 559, DateTimeKind.Utc).AddTicks(2196));

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_JobCardId",
                table: "WorkOrders",
                column: "JobCardId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSkills_Skills_SkillId",
                table: "EmployeeSkills",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirement_Skills_SkillId",
                table: "PositionSkillRequirement",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Skills_Tenants_TenantId",
                table: "Skills",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkOrders_JobCard_JobCardId",
                table: "WorkOrders",
                column: "JobCardId",
                principalTable: "JobCard",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmployeeSkills_Skills_SkillId",
                table: "EmployeeSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_PositionSkillRequirement_Skills_SkillId",
                table: "PositionSkillRequirement");

            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Tenants_TenantId",
                table: "Skills");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkOrders_JobCard_JobCardId",
                table: "WorkOrders");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_JobCardId",
                table: "WorkOrders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Skills",
                table: "Skills");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("32715b1c-6308-45dc-9f6d-448600185404"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("53ff0afe-b958-44aa-ae42-7dcf80863fa9"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("625a4223-2641-484b-8f38-34a3108cd02f"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("792f5e49-edd7-4058-a615-f114756619e4"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("98b86b4b-4c2b-4d3c-99f7-2dea8263a072"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("99901b55-7bdd-4787-a80b-6a5c16b11e0a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("df25c4a7-3a5d-42fc-874f-354023468c0a"));

            migrationBuilder.DropColumn(
                name: "JobCardId",
                table: "WorkOrders");

            migrationBuilder.RenameTable(
                name: "Skills",
                newName: "Skill");

            migrationBuilder.RenameIndex(
                name: "IX_Skills_TenantId",
                table: "Skill",
                newName: "IX_Skill_TenantId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Skill",
                table: "Skill",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1034));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1103));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1106));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1108));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1355));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1378));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1384));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1389));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1404));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1410));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1414));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1418));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1427));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1434));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1439));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1442));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1449));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1461));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1465));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1469));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1514));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1520));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1521));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1521));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1522));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1523));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1524));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1526));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1527));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1528));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1529));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1530));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1643));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1645));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1646));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1647));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1647));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1648));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1648));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1649));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1650));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1650));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1651));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1658));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1658));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1659));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1659));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1746));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1746));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1748));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1748));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1749));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1750));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1750));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1751));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1751));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1752));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1763));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1764));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1765));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("346353c7-1906-4fdd-9ccf-1ada1dc40b07"), null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1250), null, null, null, null, null, null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1249), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("51b927e4-fafc-4bfb-a823-422c30f274a2"), null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1212), null, null, null, null, null, null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1212), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("9149b205-e539-4054-86ac-8fecf16c20a4"), null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1224), null, null, null, null, null, null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1224), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("af4d6be9-5747-4cf3-8271-a1df15f8d2dd"), null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1182), null, null, null, null, null, null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1179), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b18c6a31-0279-46a4-a827-213d104e4f25"), null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1237), null, null, null, null, null, null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1236), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("fd0d432a-8a11-414e-8e43-c4c0d62c6b12"), null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1198), null, null, null, null, null, null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1198), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ff109964-77cd-4211-a477-3f5751734d8a"), null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1261), null, null, null, null, null, null, new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(1261), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 10, 22, 18, 6, 21, 656, DateTimeKind.Utc).AddTicks(767));

            migrationBuilder.AddForeignKey(
                name: "FK_EmployeeSkills_Skill_SkillId",
                table: "EmployeeSkills",
                column: "SkillId",
                principalTable: "Skill",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PositionSkillRequirement_Skill_SkillId",
                table: "PositionSkillRequirement",
                column: "SkillId",
                principalTable: "Skill",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Skill_Tenants_TenantId",
                table: "Skill",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
