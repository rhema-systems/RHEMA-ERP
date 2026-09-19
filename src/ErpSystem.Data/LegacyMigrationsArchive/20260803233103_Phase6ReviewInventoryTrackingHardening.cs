using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase6ReviewInventoryTrackingHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "StockAdjustmentItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "StockAdjustmentItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManufactureDate",
                table: "StockAdjustmentItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptScanTrackingLinesJson",
                table: "InventoryTransferItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShipmentScanTrackingLinesJson",
                table: "InventoryTransferItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedPayloadHash",
                table: "InventoryTrackingExceptions",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedPayloadHash",
                table: "InventoryNegativeStockOverrides",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "InventoryDisposalLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScanTrackingLinesJson",
                table: "GoodsReceiptNoteItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "StockAdjustmentItems");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "StockAdjustmentItems");

            migrationBuilder.DropColumn(
                name: "ManufactureDate",
                table: "StockAdjustmentItems");

            migrationBuilder.DropColumn(
                name: "ReceiptScanTrackingLinesJson",
                table: "InventoryTransferItems");

            migrationBuilder.DropColumn(
                name: "ShipmentScanTrackingLinesJson",
                table: "InventoryTransferItems");

            migrationBuilder.DropColumn(
                name: "ApprovedPayloadHash",
                table: "InventoryTrackingExceptions");

            migrationBuilder.DropColumn(
                name: "ApprovedPayloadHash",
                table: "InventoryNegativeStockOverrides");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "InventoryDisposalLines");

            migrationBuilder.DropColumn(
                name: "ScanTrackingLinesJson",
                table: "GoodsReceiptNoteItems");
        }
    }
}
