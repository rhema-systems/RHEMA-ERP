using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalanceAutoRoutingToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SupplierReturns",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SupplierReturnLineItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "SupplierReturnLineItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SupplierDebitNotes",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SupplierDebitNoteLineItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "SupplierDebitNoteLineItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "SalesOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "SalesOrderLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceId",
                table: "ReturnOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoiceLineItemId",
                table: "ReturnOrderLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "Quotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "QuoteLineItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpeningBalanceAutoRoutingEnabled",
                table: "FinanceSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "FinancePurchaseOrders",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "FinancePurchaseOrderReceiptItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "FinancePurchaseOrderReceiptItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "FinancePurchaseOrderItems",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "FinancePurchaseOrderItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "DeliveryNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "DeliveryNoteLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercentage",
                table: "DeliveryNoteLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                table: "DeliveryNoteLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                table: "DeliveryNoteLines",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                table: "DeliveryNoteLines",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "DeliveryNoteLines",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TaxGroupId",
                table: "SalesOrders",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrderLines_TaxGroupId",
                table: "SalesOrderLines",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrders_InvoiceId",
                table: "ReturnOrders",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReturnOrderLines_InvoiceLineItemId",
                table: "ReturnOrderLines",
                column: "InvoiceLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_TaxGroupId",
                table: "Quotes",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLineItems_TaxGroupId",
                table: "QuoteLineItems",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_TaxGroupId",
                table: "DeliveryNotes",
                column: "TaxGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_TaxGroupId",
                table: "DeliveryNoteLines",
                column: "TaxGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryNoteLines_TaxGroups_TaxGroupId",
                table: "DeliveryNoteLines",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryNotes_TaxGroups_TaxGroupId",
                table: "DeliveryNotes",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuoteLineItems_TaxGroups_TaxGroupId",
                table: "QuoteLineItems",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotes_TaxGroups_TaxGroupId",
                table: "Quotes",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            // Long-lived databases can have the baseline recorded while the legacy AR tables
            // are absent. EnsureCurrentArInvoiceTables creates them later and restores these
            // deferred relationships, so this historical migration must tolerate that state.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[ReturnOrderLines]')
                         AND [name] = N'FK_ReturnOrderLines_InvoiceLineItem_InvoiceLineItemId')
                BEGIN
                    ALTER TABLE [dbo].[ReturnOrderLines] WITH CHECK
                    ADD CONSTRAINT [FK_ReturnOrderLines_InvoiceLineItem_InvoiceLineItemId]
                        FOREIGN KEY ([InvoiceLineItemId]) REFERENCES [dbo].[InvoiceLineItem] ([Id])
                        ON DELETE NO ACTION;
                END

                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[ReturnOrders]')
                         AND [name] = N'FK_ReturnOrders_Invoices_InvoiceId')
                BEGIN
                    ALTER TABLE [dbo].[ReturnOrders] WITH CHECK
                    ADD CONSTRAINT [FK_ReturnOrders_Invoices_InvoiceId]
                        FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id])
                        ON DELETE NO ACTION;
                END
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrderLines_TaxGroups_TaxGroupId",
                table: "SalesOrderLines",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SalesOrders_TaxGroups_TaxGroupId",
                table: "SalesOrders",
                column: "TaxGroupId",
                principalTable: "TaxGroups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryNoteLines_TaxGroups_TaxGroupId",
                table: "DeliveryNoteLines");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryNotes_TaxGroups_TaxGroupId",
                table: "DeliveryNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_QuoteLineItems_TaxGroups_TaxGroupId",
                table: "QuoteLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Quotes_TaxGroups_TaxGroupId",
                table: "Quotes");

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FK_ReturnOrderLines_InvoiceLineItem_InvoiceLineItemId]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ReturnOrderLines]
                    DROP CONSTRAINT [FK_ReturnOrderLines_InvoiceLineItem_InvoiceLineItemId];

                IF OBJECT_ID(N'[dbo].[FK_ReturnOrders_Invoices_InvoiceId]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[ReturnOrders]
                    DROP CONSTRAINT [FK_ReturnOrders_Invoices_InvoiceId];
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrderLines_TaxGroups_TaxGroupId",
                table: "SalesOrderLines");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesOrders_TaxGroups_TaxGroupId",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TaxGroupId",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrderLines_TaxGroupId",
                table: "SalesOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_ReturnOrders_InvoiceId",
                table: "ReturnOrders");

            migrationBuilder.DropIndex(
                name: "IX_ReturnOrderLines_InvoiceLineItemId",
                table: "ReturnOrderLines");

            migrationBuilder.DropIndex(
                name: "IX_Quotes_TaxGroupId",
                table: "Quotes");

            migrationBuilder.DropIndex(
                name: "IX_QuoteLineItems_TaxGroupId",
                table: "QuoteLineItems");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryNotes_TaxGroupId",
                table: "DeliveryNotes");

            migrationBuilder.DropIndex(
                name: "IX_DeliveryNoteLines_TaxGroupId",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SupplierReturns");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SupplierReturnLineItems");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "SupplierReturnLineItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SupplierDebitNotes");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "SupplierDebitNoteLineItems");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "SupplierDebitNoteLineItems");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "SalesOrderLines");

            migrationBuilder.DropColumn(
                name: "InvoiceId",
                table: "ReturnOrders");

            migrationBuilder.DropColumn(
                name: "InvoiceLineItemId",
                table: "ReturnOrderLines");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "Quotes");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "QuoteLineItems");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceAutoRoutingEnabled",
                table: "FinanceSettings");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "FinancePurchaseOrders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "FinancePurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "FinancePurchaseOrderReceiptItems");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "FinancePurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "FinancePurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "DeliveryNotes");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "DiscountPercentage",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                table: "DeliveryNoteLines");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                table: "DeliveryNoteLines");
        }
    }
}
