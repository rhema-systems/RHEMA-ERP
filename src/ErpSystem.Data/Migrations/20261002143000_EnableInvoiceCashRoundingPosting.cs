using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002143000_EnableInvoiceCashRoundingPosting")]
public partial class EnableInvoiceCashRoundingPosting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceSettings_InvoiceRoundingReadiness",
            table: "FinanceSettings");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceSettings_InvoiceRoundingReadiness",
            table: "FinanceSettings",
            sql: "[InvoiceRoundingEnabled] = 0 OR ([InvoiceRoundingIncrement] IS NOT NULL AND [InvoiceRoundingIncrement] > 0 AND [InvoiceRoundingGainAccountId] IS NOT NULL AND [InvoiceRoundingLossAccountId] IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_FinanceSettings_InvoiceRoundingReadiness",
            table: "FinanceSettings");

        migrationBuilder.AddCheckConstraint(
            name: "CK_FinanceSettings_InvoiceRoundingReadiness",
            table: "FinanceSettings",
            sql: "[InvoiceRoundingEnabled] = 0");
    }
}
