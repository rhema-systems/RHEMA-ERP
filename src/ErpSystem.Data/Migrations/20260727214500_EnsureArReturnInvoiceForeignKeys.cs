using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260727214500_EnsureArReturnInvoiceForeignKeys")]
    public partial class EnsureArReturnInvoiceForeignKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[ReturnOrders]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[ReturnOrders]', N'InvoiceId') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
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

                IF OBJECT_ID(N'[dbo].[ReturnOrderLines]', N'U') IS NOT NULL
                   AND COL_LENGTH(N'[dbo].[ReturnOrderLines]', N'InvoiceLineItemId') IS NOT NULL
                   AND OBJECT_ID(N'[dbo].[InvoiceLineItem]', N'U') IS NOT NULL
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
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- This forward-only repair intentionally retains existing foreign keys.
                -- They may predate this migration and protect production data.
                """);
        }
    }
}
