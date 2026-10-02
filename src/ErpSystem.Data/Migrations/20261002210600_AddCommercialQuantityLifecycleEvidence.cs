using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <summary>
    /// Adds stable Inventory UOM identity plus immutable precision evidence to commercial
    /// quantity lines. This migration is intentionally additive and leaves legacy rows null
    /// until they pass through an authoritative lifecycle boundary.
    /// </summary>
    public partial class AddCommercialQuantityLifecycleEvidence : Migration
    {
        private static readonly string[] EvidenceTables =
        {
            "VendorInvoiceLineItem",
            "TenderNegotiationItems",
            "TenderItems",
            "StockMovements",
            "SalesOrderLines",
            "ReturnOrderLines",
            "RequestForQuotationItems",
            "PurchaseRequisitionItems",
            "PurchaseOrderReceiptItems",
            "PurchaseOrderItems",
            "ProcurementReceiptInspectionLines",
            "ProcurementPlanItems",
            "ProcurementFrameworkPriceListLines",
            "ProcurementFrameworkCallOffLines",
            "PhysicalCountItems",
            "InvoiceLineItem",
            "InventoryTransferItems",
            "InventoryRequisitionItems",
            "InventoryIssueVoucherLines",
            "InventoryDisposalLines",
            "GoodsReceiptNoteItems",
            "DeliveryNoteLines"
        };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in EvidenceTables)
            {
                migrationBuilder.AddColumn<string>(
                    name: "UnitOfMeasureCodeSnapshot", table: table,
                    type: "nvarchar(20)", maxLength: 20, nullable: true);
                migrationBuilder.AddColumn<int>(
                    name: "UnitOfMeasureDecimalPlacesSnapshot", table: table,
                    type: "int", nullable: true);
                migrationBuilder.AddColumn<Guid>(
                    name: "UnitOfMeasureId", table: table,
                    type: "uniqueidentifier", nullable: true);
                migrationBuilder.AddColumn<decimal>(
                    name: "UnitOfMeasureRoundingIncrementSnapshot", table: table,
                    type: "decimal(18,4)", nullable: true);
                migrationBuilder.CreateIndex(
                    name: $"IX_{table}_UnitOfMeasureId", table: table,
                    column: "UnitOfMeasureId");
                migrationBuilder.AddForeignKey(
                    name: $"FK_{table}_UnitsOfMeasure_UnitOfMeasureId", table: table,
                    column: "UnitOfMeasureId", principalTable: "UnitsOfMeasure",
                    principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            }

            migrationBuilder.AddColumn<Guid>(
                name: "UnitOfMeasureId", table: "InventoryItems",
                type: "uniqueidentifier", nullable: true);
            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_UnitOfMeasureId", table: "InventoryItems",
                column: "UnitOfMeasureId");
            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_UnitOfMeasureId", table: "InventoryItems",
                column: "UnitOfMeasureId", principalTable: "UnitsOfMeasure",
                principalColumn: "Id", onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddColumn<int>(
                name: "CommercialQuantityDecimalPlaces", table: "AccountTransactions",
                type: "int", nullable: true);
            migrationBuilder.AddColumn<decimal>(
                name: "CommercialQuantityRoundingIncrement", table: "AccountTransactions",
                type: "decimal(18,4)", nullable: true);
            migrationBuilder.AddColumn<string>(
                name: "CommercialUnitOfMeasureCode", table: "AccountTransactions",
                type: "nvarchar(20)", maxLength: 20, nullable: true);
            migrationBuilder.AddColumn<Guid>(
                name: "CommercialUnitOfMeasureId", table: "AccountTransactions",
                type: "uniqueidentifier", nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CommercialQuantityDecimalPlaces", table: "AccountTransactions");
            migrationBuilder.DropColumn(name: "CommercialQuantityRoundingIncrement", table: "AccountTransactions");
            migrationBuilder.DropColumn(name: "CommercialUnitOfMeasureCode", table: "AccountTransactions");
            migrationBuilder.DropColumn(name: "CommercialUnitOfMeasureId", table: "AccountTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_UnitsOfMeasure_UnitOfMeasureId", table: "InventoryItems");
            migrationBuilder.DropIndex(name: "IX_InventoryItems_UnitOfMeasureId", table: "InventoryItems");
            migrationBuilder.DropColumn(name: "UnitOfMeasureId", table: "InventoryItems");

            foreach (var table in EvidenceTables)
            {
                migrationBuilder.DropForeignKey(
                    name: $"FK_{table}_UnitsOfMeasure_UnitOfMeasureId", table: table);
                migrationBuilder.DropIndex(name: $"IX_{table}_UnitOfMeasureId", table: table);
                migrationBuilder.DropColumn(name: "UnitOfMeasureCodeSnapshot", table: table);
                migrationBuilder.DropColumn(name: "UnitOfMeasureDecimalPlacesSnapshot", table: table);
                migrationBuilder.DropColumn(name: "UnitOfMeasureId", table: table);
                migrationBuilder.DropColumn(name: "UnitOfMeasureRoundingIncrementSnapshot", table: table);
            }
        }
    }
}
