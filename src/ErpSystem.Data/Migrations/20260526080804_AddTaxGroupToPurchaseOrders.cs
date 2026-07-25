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

            // Some long-lived tenant databases recorded the initial baseline without retaining
            // the legacy AR source tables. The later EnsureCurrentArInvoiceTables migration
            // creates the current tables in that case, so this historical upgrade must only
            // alter legacy tables when they are actually present.
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[Invoices]', N'TaxGroupId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices] ADD [TaxGroupId] uniqueidentifier NULL;
                END

                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[InvoiceLineItem]', N'TaxAmount') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[InvoiceLineItem]
                        ADD [TaxAmount] decimal(18,2) NOT NULL
                            CONSTRAINT [DF_InvoiceLineItem_TaxAmount] DEFAULT 0;
                    END

                    IF COL_LENGTH(N'[dbo].[InvoiceLineItem]', N'TaxGroupId') IS NULL
                    BEGIN
                        ALTER TABLE [dbo].[InvoiceLineItem] ADD [TaxGroupId] uniqueidentifier NULL;
                    END
                END
                """);

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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[Invoices]', N'TaxGroupId') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[Invoices]')
                         AND [name] = N'IX_Invoices_TaxGroupId')
                BEGIN
                    CREATE INDEX [IX_Invoices_TaxGroupId]
                        ON [dbo].[Invoices] ([TaxGroupId]);
                END

                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[InvoiceLineItem]', N'TaxGroupId') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[InvoiceLineItem]')
                         AND [name] = N'IX_InvoiceLineItem_TaxGroupId')
                BEGIN
                    CREATE INDEX [IX_InvoiceLineItem_TaxGroupId]
                        ON [dbo].[InvoiceLineItem] ([TaxGroupId]);
                END
                """);

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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[TaxGroups]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[Invoices]')
                         AND [name] = N'FK_Invoices_TaxGroups_TaxGroupId')
                BEGIN
                    ALTER TABLE [dbo].[Invoices] WITH CHECK
                    ADD CONSTRAINT [FK_Invoices_TaxGroups_TaxGroupId]
                        FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]);
                END

                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[TaxGroups]', N'U') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[InvoiceLineItem]')
                         AND [name] = N'FK_InvoiceLineItem_TaxGroups_TaxGroupId')
                BEGIN
                    ALTER TABLE [dbo].[InvoiceLineItem] WITH CHECK
                    ADD CONSTRAINT [FK_InvoiceLineItem_TaxGroups_TaxGroupId]
                        FOREIGN KEY ([TaxGroupId]) REFERENCES [dbo].[TaxGroups] ([Id]);
                END
                """);

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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FK_InvoiceLineItem_TaxGroups_TaxGroupId]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[InvoiceLineItem]
                    DROP CONSTRAINT [FK_InvoiceLineItem_TaxGroups_TaxGroupId];

                IF OBJECT_ID(N'[dbo].[FK_Invoices_TaxGroups_TaxGroupId]', N'F') IS NOT NULL
                    ALTER TABLE [dbo].[Invoices]
                    DROP CONSTRAINT [FK_Invoices_TaxGroups_TaxGroupId];
                """);

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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[Invoices]')
                         AND [name] = N'IX_Invoices_TaxGroupId')
                    DROP INDEX [IX_Invoices_TaxGroupId] ON [dbo].[Invoices];

                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[InvoiceLineItem]')
                         AND [name] = N'IX_InvoiceLineItem_TaxGroupId')
                    DROP INDEX [IX_InvoiceLineItem_TaxGroupId] ON [dbo].[InvoiceLineItem];
                """);

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

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[Invoices]', N'TaxGroupId') IS NOT NULL
                    ALTER TABLE [dbo].[Invoices] DROP COLUMN [TaxGroupId];

                IF OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'[dbo].[InvoiceLineItem]', N'TaxAmount') IS NOT NULL
                    BEGIN
                        IF OBJECT_ID(N'[dbo].[DF_InvoiceLineItem_TaxAmount]', N'D') IS NOT NULL
                            ALTER TABLE [dbo].[InvoiceLineItem]
                            DROP CONSTRAINT [DF_InvoiceLineItem_TaxAmount];
                        ALTER TABLE [dbo].[InvoiceLineItem] DROP COLUMN [TaxAmount];
                    END

                    IF COL_LENGTH(N'[dbo].[InvoiceLineItem]', N'TaxGroupId') IS NOT NULL
                        ALTER TABLE [dbo].[InvoiceLineItem] DROP COLUMN [TaxGroupId];
                END
                """);

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "FinancePurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                table: "FinancePurchaseOrderItems");
        }
    }
}
