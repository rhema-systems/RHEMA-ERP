using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(ApplicationDbContext))]
    [Migration("20260703120000_AddArInvoiceEarlyPaymentDiscountFields")]
    public partial class AddArInvoiceEarlyPaymentDiscountFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'EarlyPaymentDiscountPercentage') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices]
                        ADD [EarlyPaymentDiscountPercentage] decimal(18,4) NOT NULL
                            CONSTRAINT [DF_Invoices_EarlyPaymentDiscountPercentage] DEFAULT 0;
                END

                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'EarlyPaymentDiscountDueDate') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices]
                        ADD [EarlyPaymentDiscountDueDate] datetime2 NULL;
                END

                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'EarlyPaymentDiscountAmount') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices]
                        ADD [EarlyPaymentDiscountAmount] decimal(18,2) NOT NULL
                            CONSTRAINT [DF_Invoices_EarlyPaymentDiscountAmount] DEFAULT 0;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'EarlyPaymentDiscountAmount') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[dbo].[DF_Invoices_EarlyPaymentDiscountAmount]', N'D') IS NOT NULL
                        ALTER TABLE [dbo].[Invoices] DROP CONSTRAINT [DF_Invoices_EarlyPaymentDiscountAmount];

                    ALTER TABLE [dbo].[Invoices] DROP COLUMN [EarlyPaymentDiscountAmount];
                END

                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'EarlyPaymentDiscountDueDate') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices] DROP COLUMN [EarlyPaymentDiscountDueDate];
                END

                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'EarlyPaymentDiscountPercentage') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[dbo].[DF_Invoices_EarlyPaymentDiscountPercentage]', N'D') IS NOT NULL
                        ALTER TABLE [dbo].[Invoices] DROP CONSTRAINT [DF_Invoices_EarlyPaymentDiscountPercentage];

                    ALTER TABLE [dbo].[Invoices] DROP COLUMN [EarlyPaymentDiscountPercentage];
                END
                """);
        }
    }
}
