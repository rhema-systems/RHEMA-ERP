using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the immutable currency/rate evidence required to settle AP and AR invoices from a
/// different payment currency. This migration is intentionally hand-scoped because the merged
/// repository model currently contains unrelated snapshot drift; accepting EF's generated diff
/// would recreate unrelated module tables.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260806095000_AddApArCrossCurrencySettlement")]
public sealed class AddApArCrossCurrencySettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The entity contract already uses six-decimal rates. Align both payment headers so the
        // bank leg cannot be rounded to four decimals while its allocation snapshot keeps six.
        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "VendorPayment", "decimal(18,6)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,4)");
        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "CustomerPayment", "decimal(18,6)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,4)");

        // Header rate ids identify the approved rate-master row; the existing decimal rate is
        // retained as the immutable value used for deterministic posting and reversal.
        migrationBuilder.AddColumn<Guid>("ExchangeRateId", "VendorPayment", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<Guid>("ExchangeRateId", "CustomerPayment", "uniqueidentifier", nullable: true);

        AddAllocationCurrencyEvidence(migrationBuilder, "VendorPaymentAllocation");
        AddAllocationCurrencyEvidence(migrationBuilder, "PaymentAllocation");

        // Realized-FX rows must be understandable without joining back to mutable operational
        // projections, so they retain the payment-side currency evidence as well.
        migrationBuilder.AddColumn<bool>("IsCrossCurrency", "FxRealizedSettlements", "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<string>("PaymentCurrencyCode", "FxRealizedSettlements", "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "GHS");
        migrationBuilder.AddColumn<decimal>("PaymentCurrencyAmount", "FxRealizedSettlements", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<Guid>("PaymentExchangeRateId", "FxRealizedSettlements", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("PaymentExchangeRate", "FxRealizedSettlements", "decimal(18,6)", nullable: false, defaultValue: 1m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("ExchangeRateId", "VendorPayment");
        migrationBuilder.DropColumn("ExchangeRateId", "CustomerPayment");

        DropAllocationCurrencyEvidence(migrationBuilder, "VendorPaymentAllocation");
        DropAllocationCurrencyEvidence(migrationBuilder, "PaymentAllocation");

        migrationBuilder.DropColumn("IsCrossCurrency", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentCurrencyCode", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentCurrencyAmount", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentExchangeRateId", "FxRealizedSettlements");
        migrationBuilder.DropColumn("PaymentExchangeRate", "FxRealizedSettlements");

        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "VendorPayment", "decimal(18,4)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,6)");
        migrationBuilder.AlterColumn<decimal>(
            "ExchangeRate", "CustomerPayment", "decimal(18,4)", nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,6)");
    }

    private static void AddAllocationCurrencyEvidence(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<decimal>("PaymentCurrencyAmount", table, "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>("InvoiceCurrencyCode", table, "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "GHS");
        migrationBuilder.AddColumn<string>("PaymentCurrencyCode", table, "nvarchar(3)", maxLength: 3, nullable: false, defaultValue: "GHS");
        migrationBuilder.AddColumn<bool>("IsCrossCurrency", table, "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<Guid>("InvoiceSettlementExchangeRateId", table, "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("InvoiceSettlementExchangeRate", table, "decimal(18,6)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<Guid>("PaymentExchangeRateId", table, "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<decimal>("PaymentExchangeRate", table, "decimal(18,6)", nullable: false, defaultValue: 1m);
        migrationBuilder.AddColumn<decimal>("PaymentFunctionalAmount", table, "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("SettlementFunctionalAmount", table, "decimal(18,2)", nullable: false, defaultValue: 0m);
    }

    private static void DropAllocationCurrencyEvidence(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropColumn("PaymentCurrencyAmount", table);
        migrationBuilder.DropColumn("InvoiceCurrencyCode", table);
        migrationBuilder.DropColumn("PaymentCurrencyCode", table);
        migrationBuilder.DropColumn("IsCrossCurrency", table);
        migrationBuilder.DropColumn("InvoiceSettlementExchangeRateId", table);
        migrationBuilder.DropColumn("InvoiceSettlementExchangeRate", table);
        migrationBuilder.DropColumn("PaymentExchangeRateId", table);
        migrationBuilder.DropColumn("PaymentExchangeRate", table);
        migrationBuilder.DropColumn("PaymentFunctionalAmount", table);
        migrationBuilder.DropColumn("SettlementFunctionalAmount", table);
    }
}
