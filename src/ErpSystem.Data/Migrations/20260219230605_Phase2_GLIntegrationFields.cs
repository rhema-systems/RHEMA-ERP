using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2_GLIntegrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "VendorInvoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "VendorInvoiceLineItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                table: "VendorInvoiceLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "VendorInvoiceLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                table: "VendorInvoiceLineItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "VendorInvoiceLineItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "VendorInvoiceLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultApAccountId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultArAccountId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultExpenseAccountId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "Suppliers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CostTotal",
                table: "InvoiceLineItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpirationDate",
                table: "InvoiceLineItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryItemId",
                table: "InvoiceLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LocationId",
                table: "InvoiceLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                table: "InvoiceLineItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "InvoiceLineItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "InvoiceLineItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "InvoiceLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ControlAccountCOGSId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ControlAccountGRVAccrualId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultBankAccountId",
                table: "FinanceSettings",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultApAccountId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultArAccountId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultExpenseAccountId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "Customers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                table: "CustomerPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultApAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultArAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultExpenseAccountId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentTermId",
                table: "BusinessPartners",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentTerm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DueDays = table.Column<int>(type: "int", nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DiscountDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ApplicableTo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerm", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTerm_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(356));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(445));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(453));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(458));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(998));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1033));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1051));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1081));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1136));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1159));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1176));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1192));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1223));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1257));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1278));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1333));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1362));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1379));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1395));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1418));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1514));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1520));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1523));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1525));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1527));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1530));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1533));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1535));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1537));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1540));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1542));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1544));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1546));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1548));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1550));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1552));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1640));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1646));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1648));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1650));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1652));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1654));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1656));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1667));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1669));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1671));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1673));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1675));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1677));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1679));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1681));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1794));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1797));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1801));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1804));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1806));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1807));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1809));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1811));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1813));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1815));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1849));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1852));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(1854));

            migrationBuilder.UpdateData(
                table: "UnitTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-1001-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(797));

            migrationBuilder.UpdateData(
                table: "UnitTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-1001-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 19, 23, 5, 59, 983, DateTimeKind.Utc).AddTicks(805));

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoices_PaymentTermId",
                table: "VendorInvoices",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItems_InventoryItemId",
                table: "VendorInvoiceLineItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItems_LocationId",
                table: "VendorInvoiceLineItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItems_WarehouseId",
                table: "VendorInvoiceLineItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PaymentTermId",
                table: "Suppliers",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_InventoryItemId",
                table: "InvoiceLineItems",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_LocationId",
                table: "InvoiceLineItems",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItems_WarehouseId",
                table: "InvoiceLineItems",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_ControlAccountCOGSId",
                table: "FinanceSettings",
                column: "ControlAccountCOGSId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettings_ControlAccountGRVAccrualId",
                table: "FinanceSettings",
                column: "ControlAccountGRVAccrualId");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_PaymentTermId",
                table: "Customers",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_DefaultApAccountId",
                table: "BusinessPartners",
                column: "DefaultApAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_DefaultArAccountId",
                table: "BusinessPartners",
                column: "DefaultArAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_DefaultExpenseAccountId",
                table: "BusinessPartners",
                column: "DefaultExpenseAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessPartners_PaymentTermId",
                table: "BusinessPartners",
                column: "PaymentTermId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTerm_TenantId",
                table: "PaymentTerm",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_DefaultApAccountId",
                table: "BusinessPartners",
                column: "DefaultApAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_DefaultArAccountId",
                table: "BusinessPartners",
                column: "DefaultArAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_Accounts_DefaultExpenseAccountId",
                table: "BusinessPartners",
                column: "DefaultExpenseAccountId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessPartners_PaymentTerm_PaymentTermId",
                table: "BusinessPartners",
                column: "PaymentTermId",
                principalTable: "PaymentTerm",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_PaymentTerm_PaymentTermId",
                table: "Customers",
                column: "PaymentTermId",
                principalTable: "PaymentTerm",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_ControlAccountCOGSId",
                table: "FinanceSettings",
                column: "ControlAccountCOGSId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinanceSettings_Accounts_ControlAccountGRVAccrualId",
                table: "FinanceSettings",
                column: "ControlAccountGRVAccrualId",
                principalTable: "Accounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_InventoryItems_InventoryItemId",
                table: "InvoiceLineItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_WarehouseLocations_LocationId",
                table: "InvoiceLineItems",
                column: "LocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItems_Warehouses_WarehouseId",
                table: "InvoiceLineItems",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_PaymentTerm_PaymentTermId",
                table: "Suppliers",
                column: "PaymentTermId",
                principalTable: "PaymentTerm",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoiceLineItems_InventoryItems_InventoryItemId",
                table: "VendorInvoiceLineItems",
                column: "InventoryItemId",
                principalTable: "InventoryItems",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoiceLineItems_WarehouseLocations_LocationId",
                table: "VendorInvoiceLineItems",
                column: "LocationId",
                principalTable: "WarehouseLocations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoiceLineItems_Warehouses_WarehouseId",
                table: "VendorInvoiceLineItems",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoices_PaymentTerm_PaymentTermId",
                table: "VendorInvoices",
                column: "PaymentTermId",
                principalTable: "PaymentTerm",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_DefaultApAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_DefaultArAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_Accounts_DefaultExpenseAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessPartners_PaymentTerm_PaymentTermId",
                table: "BusinessPartners");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_PaymentTerm_PaymentTermId",
                table: "Customers");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_ControlAccountCOGSId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_FinanceSettings_Accounts_ControlAccountGRVAccrualId",
                table: "FinanceSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_InventoryItems_InventoryItemId",
                table: "InvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_WarehouseLocations_LocationId",
                table: "InvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItems_Warehouses_WarehouseId",
                table: "InvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_PaymentTerm_PaymentTermId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoiceLineItems_InventoryItems_InventoryItemId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoiceLineItems_WarehouseLocations_LocationId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoiceLineItems_Warehouses_WarehouseId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoices_PaymentTerm_PaymentTermId",
                table: "VendorInvoices");

            migrationBuilder.DropTable(
                name: "PaymentTerm");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoices_PaymentTermId",
                table: "VendorInvoices");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoiceLineItems_InventoryItemId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoiceLineItems_LocationId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoiceLineItems_WarehouseId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_PaymentTermId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_InventoryItemId",
                table: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_LocationId",
                table: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItems_WarehouseId",
                table: "InvoiceLineItems");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_ControlAccountCOGSId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_FinanceSettings_ControlAccountGRVAccrualId",
                table: "FinanceSettings");

            migrationBuilder.DropIndex(
                name: "IX_Customers_PaymentTermId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_DefaultApAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_DefaultArAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_DefaultExpenseAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropIndex(
                name: "IX_BusinessPartners_PaymentTermId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "VendorInvoices");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "VendorInvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "DefaultApAccountId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DefaultArAccountId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "DefaultExpenseAccountId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "CostTotal",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "ExpirationDate",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "InventoryItemId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LocationId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "InvoiceLineItems");

            migrationBuilder.DropColumn(
                name: "ControlAccountCOGSId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "ControlAccountGRVAccrualId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "DefaultBankAccountId",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "DefaultApAccountId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DefaultArAccountId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "DefaultExpenseAccountId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                table: "CustomerPayments");

            migrationBuilder.DropColumn(
                name: "DefaultApAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "DefaultArAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "DefaultExpenseAccountId",
                table: "BusinessPartners");

            migrationBuilder.DropColumn(
                name: "PaymentTermId",
                table: "BusinessPartners");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2258));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2323));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2327));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2331));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2707));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2725));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2739));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2749));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2784));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2807));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2818));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2831));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2847));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2861));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2872));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2901));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2919));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2943));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2954));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2972));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3044));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3047));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3049));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3050));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3051));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3054));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3055));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3056));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3058));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3060));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3061));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3062));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3064));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3065));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3066));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3067));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3135));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3138));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3139));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3141));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3142));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3143));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3144));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3146));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3147));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3148));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3149));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3150));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3152));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3153));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3154));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3222));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3224));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3227));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3228));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3229));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3230));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3232));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3233));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3234));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3235));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3256));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3258));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(3259));

            migrationBuilder.UpdateData(
                table: "UnitTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-1001-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2559));

            migrationBuilder.UpdateData(
                table: "UnitTypes",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-1001-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2026, 2, 15, 23, 19, 26, 341, DateTimeKind.Utc).AddTicks(2564));
        }
    }
}
