using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class InventoryIssueActualReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryIssueVoucherActions_ActionType",
                table: "InventoryIssueVoucherActions");

            migrationBuilder.AddColumn<int>(
                name: "ReceiptSequence",
                table: "InventoryIssueVouchers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptIdempotencyKey",
                table: "InventoryIssueVoucherActions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptPayloadHash",
                table: "InventoryIssueVoucherActions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventoryIssueVoucherReceiptLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryIssueVoucherActionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventoryIssueVoucherLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
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
                    table.PrimaryKey("PK_InventoryIssueVoucherReceiptLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryIssueVoucherReceiptLines_Quantity", "[ReceivedQuantity] > 0");
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherReceiptLines_InventoryIssueVoucherActions_InventoryIssueVoucherActionId",
                        column: x => x.InventoryIssueVoucherActionId,
                        principalTable: "InventoryIssueVoucherActions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherReceiptLines_InventoryIssueVoucherLines_InventoryIssueVoucherLineId",
                        column: x => x.InventoryIssueVoucherLineId,
                        principalTable: "InventoryIssueVoucherLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryIssueVoucherReceiptLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryIssueVouchers_ReceiptSequence",
                table: "InventoryIssueVouchers",
                sql: "[ReceiptSequence] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherActions_TenantId_InventoryIssueVoucherId_ReceiptIdempotencyKey",
                table: "InventoryIssueVoucherActions",
                columns: new[] { "TenantId", "InventoryIssueVoucherId", "ReceiptIdempotencyKey" },
                unique: true,
                filter: "[ReceiptIdempotencyKey] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryIssueVoucherActions_ActionType",
                table: "InventoryIssueVoucherActions",
                sql: "[ActionType] IN (1,2,3)");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherReceiptLines_InventoryIssueVoucherActionId",
                table: "InventoryIssueVoucherReceiptLines",
                column: "InventoryIssueVoucherActionId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherReceiptLines_InventoryIssueVoucherLineId",
                table: "InventoryIssueVoucherReceiptLines",
                column: "InventoryIssueVoucherLineId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherReceiptLines_TenantId_InventoryIssueVoucherActionId_InventoryIssueVoucherLineId",
                table: "InventoryIssueVoucherReceiptLines",
                columns: new[] { "TenantId", "InventoryIssueVoucherActionId", "InventoryIssueVoucherLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryIssueVoucherReceiptLines_TenantId_InventoryIssueVoucherLineId",
                table: "InventoryIssueVoucherReceiptLines",
                columns: new[] { "TenantId", "InventoryIssueVoucherLineId" });

            InventoryIssueReceiptGuards.Install(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            InventoryIssueReceiptGuards.Uninstall(migrationBuilder);

            migrationBuilder.DropTable(
                name: "InventoryIssueVoucherReceiptLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryIssueVouchers_ReceiptSequence",
                table: "InventoryIssueVouchers");

            migrationBuilder.DropIndex(
                name: "IX_InventoryIssueVoucherActions_TenantId_InventoryIssueVoucherId_ReceiptIdempotencyKey",
                table: "InventoryIssueVoucherActions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryIssueVoucherActions_ActionType",
                table: "InventoryIssueVoucherActions");

            migrationBuilder.DropColumn(
                name: "ReceiptSequence",
                table: "InventoryIssueVouchers");

            migrationBuilder.DropColumn(
                name: "ReceiptIdempotencyKey",
                table: "InventoryIssueVoucherActions");

            migrationBuilder.DropColumn(
                name: "ReceiptPayloadHash",
                table: "InventoryIssueVoucherActions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryIssueVoucherActions_ActionType",
                table: "InventoryIssueVoucherActions",
                sql: "[ActionType] IN (1,2)");
        }
    }
}
