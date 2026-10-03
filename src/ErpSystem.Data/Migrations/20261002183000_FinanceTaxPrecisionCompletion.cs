using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ErpSystem.Data;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002183000_FinanceTaxPrecisionCompletion")]
public partial class FinanceTaxPrecisionCompletion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_FinanceSettings_PrecisionGovernance", "FinanceSettings");

        // Historical rows pre-date immutable precision evidence. Preserve that fact as one
        // all-null tuple instead of fabricating currency, raw tax, rounding, allocation, or
        // rounding-policy values. All new posting paths populate the complete tuple.
        migrationBuilder.AddColumn<int>("AllocationSequence", "TaxCalculations", "int", nullable: true);
        migrationBuilder.AddColumn<string>("CurrencyCode", "TaxCalculations", "nvarchar(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<int>("CurrencyDecimalPlaces", "TaxCalculations", "int", nullable: true);
        migrationBuilder.AddColumn<decimal>("RawTaxAmount", "TaxCalculations", "decimal(20,10)", precision: 20, scale: 10, nullable: true);
        migrationBuilder.AddColumn<decimal>("RoundingAdjustment", "TaxCalculations", "decimal(20,10)", precision: 20, scale: 10, nullable: true);
        migrationBuilder.AddColumn<int>("TaxRoundingScope", "TaxCalculations", "int", nullable: true);
        migrationBuilder.AddColumn<int>("TaxRoundingMethod", "TaxCalculations", "int", nullable: true);
        migrationBuilder.AddColumn<decimal>("TaxRoundingIncrement", "TaxCalculations", "decimal(20,4)", precision: 20, scale: 4, nullable: true);

        AlterAmountColumns(migrationBuilder, "decimal(20,4)", 20, 4, "decimal(18,2)", 18, 2);
        migrationBuilder.AlterColumn<decimal>("TaxableAmount", "TaxCalculations", "decimal(20,10)", precision: 20, scale: 10, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,2)", oldPrecision: 18, oldScale: 2);
        AlterRateColumns(migrationBuilder, "decimal(18,6)", 18, 6, "decimal(18,4)", 18, 4);

        migrationBuilder.AddCheckConstraint(
            "CK_FinanceSettings_PrecisionGovernance",
            "FinanceSettings",
            "[UnitPriceDecimalPlaces] BETWEEN 0 AND 6 AND [ExchangeRateInputDecimalPlaces] BETWEEN 6 AND 10 AND [ExchangeRateDisplayDecimalPlaces] BETWEEN 6 AND 10 AND [TaxPercentageDecimalPlaces] BETWEEN 0 AND 6 AND [ReportDisplayDecimalPlaces] BETWEEN 0 AND 4 AND [TaxRoundingMethod] IN (0, 1, 2) AND [TaxRoundingScope] IN (0, 1, 2) AND [InvoiceRoundingMethod] IN (0, 1, 2)");
        migrationBuilder.AddCheckConstraint(
            "CK_TaxCalculations_PrecisionEvidence",
            "TaxCalculations",
            "([CurrencyCode] IS NULL AND [CurrencyDecimalPlaces] IS NULL AND [RawTaxAmount] IS NULL AND [RoundingAdjustment] IS NULL AND [AllocationSequence] IS NULL AND [TaxRoundingScope] IS NULL AND [TaxRoundingMethod] IS NULL AND [TaxRoundingIncrement] IS NULL) OR ([CurrencyCode] IS NOT NULL AND LEN([CurrencyCode]) = 3 AND [CurrencyDecimalPlaces] BETWEEN 0 AND 4 AND [RawTaxAmount] IS NOT NULL AND [RoundingAdjustment] IS NOT NULL AND [AllocationSequence] IS NOT NULL AND [TaxRoundingScope] IN (0, 1, 2) AND [TaxRoundingMethod] IN (0, 1, 2) AND [TaxRoundingIncrement] > 0)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_TaxCalculations_PrecisionEvidence", "TaxCalculations");
        migrationBuilder.DropCheckConstraint("CK_FinanceSettings_PrecisionGovernance", "FinanceSettings");

        AlterRateColumns(migrationBuilder, "decimal(18,4)", 18, 4, "decimal(18,6)", 18, 6);
        migrationBuilder.AlterColumn<decimal>("TaxableAmount", "TaxCalculations", "decimal(18,2)", precision: 18, scale: 2, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(20,10)", oldPrecision: 20, oldScale: 10);
        AlterAmountColumns(migrationBuilder, "decimal(18,2)", 18, 2, "decimal(20,4)", 20, 4);

        migrationBuilder.DropColumn("AllocationSequence", "TaxCalculations");
        migrationBuilder.DropColumn("CurrencyCode", "TaxCalculations");
        migrationBuilder.DropColumn("CurrencyDecimalPlaces", "TaxCalculations");
        migrationBuilder.DropColumn("RawTaxAmount", "TaxCalculations");
        migrationBuilder.DropColumn("RoundingAdjustment", "TaxCalculations");
        migrationBuilder.DropColumn("TaxRoundingScope", "TaxCalculations");
        migrationBuilder.DropColumn("TaxRoundingMethod", "TaxCalculations");
        migrationBuilder.DropColumn("TaxRoundingIncrement", "TaxCalculations");

        migrationBuilder.AddCheckConstraint(
            "CK_FinanceSettings_PrecisionGovernance",
            "FinanceSettings",
            "[UnitPriceDecimalPlaces] BETWEEN 0 AND 6 AND [ExchangeRateInputDecimalPlaces] BETWEEN 6 AND 10 AND [ExchangeRateDisplayDecimalPlaces] BETWEEN 6 AND 10 AND [TaxPercentageDecimalPlaces] BETWEEN 0 AND 4 AND [ReportDisplayDecimalPlaces] BETWEEN 0 AND 4 AND [TaxRoundingMethod] IN (0, 1, 2) AND [TaxRoundingScope] = 0 AND [InvoiceRoundingMethod] IN (0, 1, 2)");
    }

    private static void AlterAmountColumns(MigrationBuilder migrationBuilder, string type, int precision, int scale, string oldType, int oldPrecision, int oldScale)
    {
        foreach (var (table, column) in new[]
        {
            ("Invoices", "TaxAmount"), ("InvoiceLineItem", "TaxAmount"),
            ("VendorInvoice", "TaxAmount"), ("VendorInvoiceLineItem", "TaxAmount"),
            ("TaxCalculations", "BaseAmount"), ("TaxCalculations", "TaxAmount")
        })
            migrationBuilder.AlterColumn<decimal>(column, table, type, precision: precision, scale: scale, nullable: false,
                oldClrType: typeof(decimal), oldType: oldType, oldPrecision: oldPrecision, oldScale: oldScale);
    }

    private static void AlterRateColumns(MigrationBuilder migrationBuilder, string type, int precision, int scale, string oldType, int oldPrecision, int oldScale)
    {
        foreach (var (table, column) in new[]
        {
            ("Taxes", "Rate"), ("TaxRateHistory", "Rate"), ("TaxConfigurationVersions", "Rate"),
            ("InvoiceLineItem", "TaxRate"), ("VendorInvoiceLineItem", "TaxRate"), ("TaxCalculations", "TaxRate")
        })
            migrationBuilder.AlterColumn<decimal>(column, table, type, precision: precision, scale: scale, nullable: false,
                oldClrType: typeof(decimal), oldType: oldType, oldPrecision: oldPrecision, oldScale: oldScale);
    }
}
