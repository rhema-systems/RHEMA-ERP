using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260611110000_AddFinancePurchaseOrderPaymentTerm")]
    public partial class AddFinancePurchaseOrderPaymentTerm : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FinancePurchaseOrders]', N'U') IS NOT NULL
                BEGIN
                    IF COL_LENGTH(N'dbo.FinancePurchaseOrders', N'PaymentTermId') IS NULL
                        ALTER TABLE [dbo].[FinancePurchaseOrders] ADD [PaymentTermId] uniqueidentifier NULL;

                    IF NOT EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[FinancePurchaseOrders]')
                          AND [name] = N'IX_FinancePurchaseOrders_PaymentTermId')
                        CREATE INDEX [IX_FinancePurchaseOrders_PaymentTermId]
                            ON [dbo].[FinancePurchaseOrders] ([PaymentTermId]);

                    IF OBJECT_ID(N'[dbo].[PaymentTerms]', N'U') IS NOT NULL
                       AND NOT EXISTS (
                           SELECT 1 FROM sys.foreign_keys
                           WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinancePurchaseOrders]')
                             AND [name] = N'FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId')
                        ALTER TABLE [dbo].[FinancePurchaseOrders]
                            ADD CONSTRAINT [FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId]
                            FOREIGN KEY ([PaymentTermId]) REFERENCES [dbo].[PaymentTerms] ([Id]);
                END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[FinancePurchaseOrders]', N'U') IS NOT NULL
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM sys.foreign_keys
                        WHERE [parent_object_id] = OBJECT_ID(N'[dbo].[FinancePurchaseOrders]')
                          AND [name] = N'FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId')
                        ALTER TABLE [dbo].[FinancePurchaseOrders]
                            DROP CONSTRAINT [FK_FinancePurchaseOrders_PaymentTerms_PaymentTermId];

                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE [object_id] = OBJECT_ID(N'[dbo].[FinancePurchaseOrders]')
                          AND [name] = N'IX_FinancePurchaseOrders_PaymentTermId')
                        DROP INDEX [IX_FinancePurchaseOrders_PaymentTermId]
                            ON [dbo].[FinancePurchaseOrders];

                    IF COL_LENGTH(N'dbo.FinancePurchaseOrders', N'PaymentTermId') IS NOT NULL
                        ALTER TABLE [dbo].[FinancePurchaseOrders] DROP COLUMN [PaymentTermId];
                END;
                """);
        }
    }
}
