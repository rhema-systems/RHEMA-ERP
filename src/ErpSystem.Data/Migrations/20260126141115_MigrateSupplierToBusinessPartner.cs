using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateSupplierToBusinessPartner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitionItems_Suppliers_PreferredSupplierId",
                table: "PurchaseRequisitionItems");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("090c51e8-de29-4d06-a607-e3e187300896"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("26e5d47f-a5f3-423b-9a02-2ee54c82f59a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("571fafe8-722a-46b8-b1c7-7d1ab5ebae27"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("63627664-1d25-4ea6-a749-2bc93f8b80af"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("b68ae3f3-d3c7-494c-92a6-648acb21ae3a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("d67ba717-b5a6-4cc8-8cdb-538f90831f31"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("e6f51ce9-292d-412a-b14a-e3f70dc436de"));

            migrationBuilder.RenameColumn(
                name: "PreferredSupplierId",
                table: "PurchaseRequisitions",
                newName: "PreferredBusinessPartnerId");

            migrationBuilder.RenameColumn(
                name: "PreferredSupplierId",
                table: "PurchaseRequisitionItems",
                newName: "PreferredBusinessPartnerId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseRequisitionItems_PreferredSupplierId",
                table: "PurchaseRequisitionItems",
                newName: "IX_PurchaseRequisitionItems_PreferredBusinessPartnerId");

            migrationBuilder.RenameColumn(
                name: "SupplierOrderNumber",
                table: "PurchaseOrders",
                newName: "BusinessPartnerOrderNumber");

            migrationBuilder.RenameColumn(
                name: "SupplierItemCode",
                table: "PurchaseOrderItems",
                newName: "BusinessPartnerItemCode");

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessPartnerId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8740));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8803));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8806));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8809));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9061));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9083));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9091));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9098));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9117));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9125));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9133));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9140));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9151));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9167));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9175));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9182));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9193));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9213));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9225));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9232));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9294));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9296));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9298));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9299));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9300));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9301));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9302));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9303));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9304));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9306));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9307));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9308));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9309));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9310));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9311));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9311));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9368));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9370));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9371));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9372));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9373));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9374));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9375));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9376));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9376));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9377));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9378));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9379));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9380));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9381));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9381));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9451));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9452));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9454));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9455));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9456));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9457));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9458));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9459));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9460));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9460));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9475));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9476));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(9477));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("2ad4a63b-51c8-4c52-9345-5900a2dbdd69"), null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8946), null, null, null, null, null, null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8946), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4a5d0ebd-3dda-458c-b5fc-653d073d6524"), null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8930), null, null, null, null, null, null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8930), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("4a91ddd9-f611-47bb-98f9-43da08fabafc"), null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8914), null, null, null, null, null, null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8914), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7290493d-449d-404e-bf28-278bfb1a2a6b"), null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8922), null, null, null, null, null, null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8922), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("80a0d482-dca3-44a7-abae-30e211215716"), null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8893), null, null, null, null, null, null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8893), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c07120da-d95f-4741-b8f9-0b95ae970425"), null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8939), null, null, null, null, null, null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8938), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("ea9cf344-8362-47a9-96fe-42a7e90c0b94"), null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8883), null, null, null, null, null, null, new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8878), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 26, 14, 11, 11, 618, DateTimeKind.Utc).AddTicks(8422));

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_BusinessPartnerId",
                table: "PurchaseOrders",
                column: "BusinessPartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_BusinessPartners_BusinessPartnerId",
                table: "PurchaseOrders",
                column: "BusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitionItems_BusinessPartners_PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems",
                column: "PreferredBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_BusinessPartners_BusinessPartnerId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequisitionItems_BusinessPartners_PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_BusinessPartnerId",
                table: "PurchaseOrders");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("2ad4a63b-51c8-4c52-9345-5900a2dbdd69"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4a5d0ebd-3dda-458c-b5fc-653d073d6524"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("4a91ddd9-f611-47bb-98f9-43da08fabafc"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7290493d-449d-404e-bf28-278bfb1a2a6b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("80a0d482-dca3-44a7-abae-30e211215716"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c07120da-d95f-4741-b8f9-0b95ae970425"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("ea9cf344-8362-47a9-96fe-42a7e90c0b94"));

            migrationBuilder.DropColumn(
                name: "BusinessPartnerId",
                table: "PurchaseOrders");

            migrationBuilder.RenameColumn(
                name: "PreferredBusinessPartnerId",
                table: "PurchaseRequisitions",
                newName: "PreferredSupplierId");

            migrationBuilder.RenameColumn(
                name: "PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems",
                newName: "PreferredSupplierId");

            migrationBuilder.RenameIndex(
                name: "IX_PurchaseRequisitionItems_PreferredBusinessPartnerId",
                table: "PurchaseRequisitionItems",
                newName: "IX_PurchaseRequisitionItems_PreferredSupplierId");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerOrderNumber",
                table: "PurchaseOrders",
                newName: "SupplierOrderNumber");

            migrationBuilder.RenameColumn(
                name: "BusinessPartnerItemCode",
                table: "PurchaseOrderItems",
                newName: "SupplierItemCode");

            migrationBuilder.AlterColumn<Guid>(
                name: "SupplierId",
                table: "PurchaseOrders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(5941));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6020));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6024));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6028));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6448));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6477));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6490));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6499));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6515));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6525));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6534));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6543));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6558));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6570));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6582));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6591));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6605));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6630));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6644));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6653));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6745));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6749));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6750));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6751));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6753));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6755));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6756));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6757));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6759));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6761));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6762));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6763));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6765));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6766));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6767));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6768));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6841));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6844));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6846));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6847));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6848));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6849));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6850));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6852));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6853));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6854));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6855));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6856));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6857));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6859));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6860));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6955));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6956));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6959));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6960));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6962));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6963));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6964));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6965));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6966));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6968));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6985));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6987));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6988));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("090c51e8-de29-4d06-a607-e3e187300896"), null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6309), null, null, null, null, null, null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6309), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("26e5d47f-a5f3-423b-9a02-2ee54c82f59a"), null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6228), null, null, null, null, null, null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6227), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("571fafe8-722a-46b8-b1c7-7d1ab5ebae27"), null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6158), null, null, null, null, null, null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6153), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("63627664-1d25-4ea6-a749-2bc93f8b80af"), null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6291), null, null, null, null, null, null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6290), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("b68ae3f3-d3c7-494c-92a6-648acb21ae3a"), null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6267), null, null, null, null, null, null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6267), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("d67ba717-b5a6-4cc8-8cdb-538f90831f31"), null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6208), null, null, null, null, null, null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6207), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("e6f51ce9-292d-412a-b14a-e3f70dc436de"), null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6189), null, null, null, null, null, null, new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(6188), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 24, 0, 48, 58, 889, DateTimeKind.Utc).AddTicks(4715));

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Suppliers_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequisitionItems_Suppliers_PreferredSupplierId",
                table: "PurchaseRequisitionItems",
                column: "PreferredSupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id");
        }
    }
}
