using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Retains the approved daily exchange-rate record used by governed AP and AR
/// opening invoices. The columns remain nullable so ordinary and historical
/// invoices keep their existing contract.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260822231500_AddOpeningInvoiceExchangeRateEvidence")]
public sealed class AddOpeningInvoiceExchangeRateEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "ExchangeRateId",
            table: "VendorInvoice",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ExchangeRateId",
            table: "Invoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_VendorInvoice_TenantId_ExchangeRateId",
            table: "VendorInvoice",
            columns: new[] { "TenantId", "ExchangeRateId" });

        migrationBuilder.CreateIndex(
            name: "IX_VendorInvoice_ExchangeRateId",
            table: "VendorInvoice",
            column: "ExchangeRateId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_TenantId_ExchangeRateId",
            table: "Invoices",
            columns: new[] { "TenantId", "ExchangeRateId" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_ExchangeRateId",
            table: "Invoices",
            column: "ExchangeRateId");

        migrationBuilder.AddForeignKey(
            name: "FK_VendorInvoice_ExchangeRates_ExchangeRateId",
            table: "VendorInvoice",
            column: "ExchangeRateId",
            principalTable: "ExchangeRates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Invoices_ExchangeRates_ExchangeRateId",
            table: "Invoices",
            column: "ExchangeRateId",
            principalTable: "ExchangeRates",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_VendorInvoice_ExchangeRates_ExchangeRateId",
            table: "VendorInvoice");

        migrationBuilder.DropForeignKey(
            name: "FK_Invoices_ExchangeRates_ExchangeRateId",
            table: "Invoices");

        migrationBuilder.DropIndex(
            name: "IX_VendorInvoice_TenantId_ExchangeRateId",
            table: "VendorInvoice");

        migrationBuilder.DropIndex(
            name: "IX_VendorInvoice_ExchangeRateId",
            table: "VendorInvoice");

        migrationBuilder.DropIndex(
            name: "IX_Invoices_TenantId_ExchangeRateId",
            table: "Invoices");

        migrationBuilder.DropIndex(
            name: "IX_Invoices_ExchangeRateId",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "ExchangeRateId",
            table: "VendorInvoice");

        migrationBuilder.DropColumn(
            name: "ExchangeRateId",
            table: "Invoices");
    }
}
