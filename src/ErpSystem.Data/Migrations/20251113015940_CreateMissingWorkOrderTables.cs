using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateMissingWorkOrderTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkOrderParts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    QuantityRequired = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityAllocated = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantityReturned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WarehouseLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PickedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkOrderParts_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkOrderTools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsAllocated = table.Column<bool>(type: "bit", nullable: false),
                    AllocationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderTools", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderTools_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkOrderTools_InventoryItems_ToolId",
                        column: x => x.ToolId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_WorkOrderId",
                table: "WorkOrderParts",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderParts_InventoryItemId",
                table: "WorkOrderParts",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTools_WorkOrderId",
                table: "WorkOrderTools",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderTools_ToolId",
                table: "WorkOrderTools",
                column: "ToolId");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("15d6f670-35eb-45cf-81b1-af7117a49878"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("7742f242-d778-486f-97e5-d2d2da2e5251"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("81bd91b4-24b6-4deb-ac67-f6a77ea9de0c"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("90e4b89c-b53b-4a69-9fcf-77821b2bb781"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("a24104de-fcec-47b9-8f15-2c351126f12a"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("c5d2d69c-2a5f-498a-ad4e-9d30d2913093"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("cc7009e3-eae4-42a0-aa9d-901f970d5dd3"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(6764));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7063));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7079));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7087));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7924));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8005));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8081));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8104));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8151));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8192));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8210));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8227));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8254));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8294));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8318));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8374));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8413));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8460));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8494));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8514));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8696));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8703));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8706));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8710));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8713));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8719));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8723));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8726));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8730));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8734));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8738));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8742));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8745));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8749));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8752));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8755));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8875));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8881));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8885));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8888));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8912));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8916));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8919));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8923));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8926));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8929));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8933));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8937));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8940));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8944));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(8947));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9139));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9144));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9149));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9152));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9155));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9159));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9163));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9166));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9170));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9173));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9222));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9226));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(9230));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("01222e8f-a311-414b-b3fd-0e5b68cfc98b"), null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7584), null, null, null, null, null, null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7582), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("08a386d8-7832-4af0-9b09-adfc0ec7b893"), null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7329), null, null, null, null, null, null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7321), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("23712d6c-8dc7-4955-822f-d7abc10ff382"), null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7463), null, null, null, null, null, null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7461), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("426b2283-6c84-4fda-a159-969b2416e78b"), null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7699), null, null, null, null, null, null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7697), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("511a8d40-64e3-45ac-986c-afc8e815b917"), null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7645), null, null, null, null, null, null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7643), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("8b376924-95a8-4199-bef1-b9256876acb2"), null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7403), null, null, null, null, null, null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7401), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("f100481e-b8c8-4f54-8dcb-b1df9ebf1ddd"), null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7523), null, null, null, null, null, null, new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(7521), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 13, 1, 59, 30, 520, DateTimeKind.Utc).AddTicks(5881));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderParts");

            migrationBuilder.DropTable(
                name: "WorkOrderTools");

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("01222e8f-a311-414b-b3fd-0e5b68cfc98b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("08a386d8-7832-4af0-9b09-adfc0ec7b893"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("23712d6c-8dc7-4955-822f-d7abc10ff382"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("426b2283-6c84-4fda-a159-969b2416e78b"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("511a8d40-64e3-45ac-986c-afc8e815b917"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("8b376924-95a8-4199-bef1-b9256876acb2"));

            migrationBuilder.DeleteData(
                table: "TenantModules",
                keyColumn: "Id",
                keyValue: new Guid("f100481e-b8c8-4f54-8dcb-b1df9ebf1ddd"));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3558));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3674));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3679));

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3682));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4140));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4170));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4179));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4188));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4202));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4211));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4218));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4225));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4237));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4248));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4256));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4279));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4291));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4330));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4345));

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4353));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4430));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4434));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4436));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4437));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4438));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4440));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4442));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4443));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4444));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4446));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4447));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4448));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4449));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4450));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4451));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000001") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4452));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4543));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4545));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4546));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000004"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4547));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4548));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000006"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4549));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4550));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000008"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4551));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4552));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4553));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4554));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4555));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4556));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000015"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4557));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000002") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4558));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4617));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000003"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4619));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000005"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4621));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4622));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4623));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4624));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4625));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4626));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000014"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4627));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000016"), new Guid("00000000-0000-0000-0000-000000000003") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4628));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4643));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000009"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4644));

            migrationBuilder.UpdateData(
                table: "RolePermissions",
                keyColumns: new[] { "PermissionId", "RoleId" },
                keyValues: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), new Guid("00000000-0000-0000-0000-000000000004") },
                column: "GrantedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(4645));

            migrationBuilder.InsertData(
                table: "TenantModules",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CreatedBy", "CreatedById", "DeletedAt", "DeletedBy", "Description", "DisabledDate", "EnabledDate", "IsDeleted", "LastModifiedById", "ModuleName", "Status", "TenantId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { new Guid("15d6f670-35eb-45cf-81b1-af7117a49878"), null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3898), null, null, null, null, null, null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3898), false, null, "HR", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("7742f242-d778-486f-97e5-d2d2da2e5251"), null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3974), null, null, null, null, null, null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3973), false, null, "Marketing", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("81bd91b4-24b6-4deb-ac67-f6a77ea9de0c"), null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3991), null, null, null, null, null, null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3991), false, null, "WorkflowEngine", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("90e4b89c-b53b-4a69-9fcf-77821b2bb781"), null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3953), null, null, null, null, null, null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3952), false, null, "Inventory", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("a24104de-fcec-47b9-8f15-2c351126f12a"), null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3918), null, null, null, null, null, null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3918), false, null, "Sales", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("c5d2d69c-2a5f-498a-ad4e-9d30d2913093"), null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3935), null, null, null, null, null, null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3934), false, null, "Procurement", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null },
                    { new Guid("cc7009e3-eae4-42a0-aa9d-901f970d5dd3"), null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3837), null, null, null, null, null, null, new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(3829), false, null, "Finance", 1, new Guid("00000000-0000-0000-0000-000000000001"), null, null }
                });

            migrationBuilder.UpdateData(
                table: "Tenants",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2025, 11, 9, 9, 11, 37, 390, DateTimeKind.Utc).AddTicks(2824));
        }
    }
}
