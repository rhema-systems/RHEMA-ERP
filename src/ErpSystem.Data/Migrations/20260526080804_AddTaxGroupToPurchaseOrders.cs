using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxGroupToPurchaseOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "VendorInvoiceLineItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "VendorInvoice",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "InvoiceLineItem",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "InvoiceLineItem",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "FinancePurchaseOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "FinancePurchaseOrderItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoiceLineItem_TaxGroupId",
                table: "VendorInvoiceLineItem",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorInvoice_TaxGroupId",
                table: "VendorInvoice",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TaxGroupId",
                table: "Invoices",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItem_TaxGroupId",
                table: "InvoiceLineItem",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrders_TaxGroupId",
                table: "FinancePurchaseOrders",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancePurchaseOrderItems_TaxGroupId",
                table: "FinancePurchaseOrderItems",
                column: "TaxGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancePurchaseOrderItems_TaxGroups_TaxGroupId",
                table: "FinancePurchaseOrderItems",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FinancePurchaseOrders_TaxGroups_TaxGroupId",
                table: "FinancePurchaseOrders",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceLineItem_TaxGroups_TaxGroupId",
                table: "InvoiceLineItem",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_TaxGroups_TaxGroupId",
                table: "Invoices",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoice_TaxGroups_TaxGroupId",
                table: "VendorInvoice",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorInvoiceLineItem_TaxGroups_TaxGroupId",
                table: "VendorInvoiceLineItem",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FinancePurchaseOrderItems_TaxGroups_TaxGroupId",
                table: "FinancePurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_FinancePurchaseOrders_TaxGroups_TaxGroupId",
                table: "FinancePurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceLineItem_TaxGroups_TaxGroupId",
                table: "InvoiceLineItem");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_TaxGroups_TaxGroupId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoice_TaxGroups_TaxGroupId",
                table: "VendorInvoice");

            migrationBuilder.DropForeignKey(
                name: "FK_VendorInvoiceLineItem_TaxGroups_TaxGroupId",
                table: "VendorInvoiceLineItem");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoiceLineItem_TaxGroupId",
                table: "VendorInvoiceLineItem");

            migrationBuilder.DropIndex(
                name: "IX_VendorInvoice_TaxGroupId",
                table: "VendorInvoice");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TaxGroupId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceLineItem_TaxGroupId",
                table: "InvoiceLineItem");

            migrationBuilder.DropIndex(
                name: "IX_FinancePurchaseOrders_TaxGroupId",
                table: "FinancePurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_FinancePurchaseOrderItems_TaxGroupId",
                table: "FinancePurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "VendorInvoiceLineItem");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "VendorInvoice");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "InvoiceLineItem");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "InvoiceLineItem");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "FinancePurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "FinancePurchaseOrderItems");
        }
    }
}
