using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingToolCheckoutColumnsToDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("18de517a-7bcf-47b3-964f-826ef3a2b2e6"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("27bc55c6-6e31-422a-bc2d-6a5b05dbf3ed"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("3693df8f-2544-4117-a721-9662a7f2263a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("9e2792de-8380-4bc6-b7ab-e8bc30b8133d"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a10b589e-936c-41af-8967-c86dc54bb37a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ebea72c1-8f10-4435-9f05-02e4b136d205"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ee1d9932-b743-4761-b1ee-99db8eb0ea78"));

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5384));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5439));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5442));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5454));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5714));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5726));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5733));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5740));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5757));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5765));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5772));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5779));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5790));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5808));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5815));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5822));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5831));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5845));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5852));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5860));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5907));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5909));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5911));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5912));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5913));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5914));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5915));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5917));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5918));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5919));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5920));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5921));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5922));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5922));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5923));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5924));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5974));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5977));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5978));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5979));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5980));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5980));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5981));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5982));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5983));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5983));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5984));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5985));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5986));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5986));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5987));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6061));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6063));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6064));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6065));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6066));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6067));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6068));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6068));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6069));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6070));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6083));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6090));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(6091));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("18de517a-7bcf-47b3-964f-826ef3a2b2e6"), null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5603), null, null, null, null, null, null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5602), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("27bc55c6-6e31-422a-bc2d-6a5b05dbf3ed"), null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5572), null, null, null, null, null, null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5571), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("3693df8f-2544-4117-a721-9662a7f2263a"), null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5562), null, null, null, null, null, null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5562), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("9e2792de-8380-4bc6-b7ab-e8bc30b8133d"), null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5581), null, null, null, null, null, null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5581), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a10b589e-936c-41af-8967-c86dc54bb37a"), null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5552), null, null, null, null, null, null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5552), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ebea72c1-8f10-4435-9f05-02e4b136d205"), null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5592), null, null, null, null, null, null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5591), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ee1d9932-b743-4761-b1ee-99db8eb0ea78"), null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5539), null, null, null, null, null, null, new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5533), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 16, 1, 32, 35, 735, DateTimeKind.Utc).AddTicks(5051));
        }
    }
}
