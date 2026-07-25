using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalanceInvoiceFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'IsOpeningBalance') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoice] ADD [IsOpeningBalance] bit NOT NULL CONSTRAINT [DF_VendorInvoice_IsOpeningBalance] DEFAULT CAST(0 AS bit);
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoices]', N'IsOpeningBalance') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoices] ADD [IsOpeningBalance] bit NOT NULL CONSTRAINT [DF_VendorInvoices_IsOpeningBalance] DEFAULT CAST(0 AS bit);
                END

                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'IsOpeningBalance') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices] ADD [IsOpeningBalance] bit NOT NULL CONSTRAINT [DF_Invoices_IsOpeningBalance] DEFAULT CAST(0 AS bit);
                END

                IF OBJECT_ID(N'[dbo].[Invoice]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoice]', N'IsOpeningBalance') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoice] ADD [IsOpeningBalance] bit NOT NULL CONSTRAINT [DF_Invoice_IsOpeningBalance] DEFAULT CAST(0 AS bit);
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[VendorInvoice]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoice]', N'IsOpeningBalance') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoice] DROP CONSTRAINT IF EXISTS [DF_VendorInvoice_IsOpeningBalance];
                    ALTER TABLE [dbo].[VendorInvoice] DROP COLUMN [IsOpeningBalance];
                END

                IF OBJECT_ID(N'[dbo].[VendorInvoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[VendorInvoices]', N'IsOpeningBalance') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[VendorInvoices] DROP CONSTRAINT IF EXISTS [DF_VendorInvoices_IsOpeningBalance];
                    ALTER TABLE [dbo].[VendorInvoices] DROP COLUMN [IsOpeningBalance];
                END

                IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoices]', N'IsOpeningBalance') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoices] DROP CONSTRAINT IF EXISTS [DF_Invoices_IsOpeningBalance];
                    ALTER TABLE [dbo].[Invoices] DROP COLUMN [IsOpeningBalance];
                END

                IF OBJECT_ID(N'[dbo].[Invoice]', N'U') IS NOT NULL
                    AND COL_LENGTH(N'[dbo].[Invoice]', N'IsOpeningBalance') IS NOT NULL
                BEGIN
                    ALTER TABLE [dbo].[Invoice] DROP CONSTRAINT IF EXISTS [DF_Invoice_IsOpeningBalance];
                    ALTER TABLE [dbo].[Invoice] DROP COLUMN [IsOpeningBalance];
                END
                """);
        }
    }
}
