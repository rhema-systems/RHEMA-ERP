using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261002213000_FinancePrecisionStorageCorrections")]
public partial class FinancePrecisionStorageCorrections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AlterAmounts(migrationBuilder, "decimal(20,4)", 20, 4, "decimal(18,2)", 18, 2);
        AlterUnitPrices(migrationBuilder, "decimal(20,6)", 20, 6, "decimal(18,2)", 18, 2);

        migrationBuilder.AlterColumn<decimal>("TaxRate", "SupplierDebitNoteTaxComponents",
            type: "decimal(18,6)", precision: 18, scale: 6, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,4)", oldPrecision: 18, oldScale: 4);
        migrationBuilder.AlterColumn<decimal>("TaxableAmount", "SupplierDebitNoteTaxComponents",
            type: "decimal(20,10)", precision: 20, scale: 10, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,2)", oldPrecision: 18, oldScale: 2);

        migrationBuilder.AddColumn<string>("CurrencyCode", "SupplierDebitNoteTaxComponents",
            type: "nvarchar(3)", maxLength: 3, nullable: true);
        migrationBuilder.AddColumn<int>("CurrencyDecimalPlaces", "SupplierDebitNoteTaxComponents",
            type: "int", nullable: true);
        migrationBuilder.AddColumn<decimal>("RawTaxAmount", "SupplierDebitNoteTaxComponents",
            type: "decimal(20,10)", precision: 20, scale: 10, nullable: true);
        migrationBuilder.AddColumn<decimal>("RoundingAdjustment", "SupplierDebitNoteTaxComponents",
            type: "decimal(20,10)", precision: 20, scale: 10, nullable: true);
        migrationBuilder.AddColumn<int>("AllocationSequence", "SupplierDebitNoteTaxComponents",
            type: "int", nullable: true);
        migrationBuilder.AddColumn<int>("TaxRoundingScope", "SupplierDebitNoteTaxComponents",
            type: "int", nullable: true);
        migrationBuilder.AddColumn<int>("TaxRoundingMethod", "SupplierDebitNoteTaxComponents",
            type: "int", nullable: true);
        migrationBuilder.AddColumn<decimal>("TaxRoundingIncrement", "SupplierDebitNoteTaxComponents",
            type: "decimal(20,4)", precision: 20, scale: 4, nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_SupplierDebitNoteTaxComponents_PrecisionEvidence",
            table: "SupplierDebitNoteTaxComponents",
            sql: "([CurrencyCode] IS NULL AND [CurrencyDecimalPlaces] IS NULL AND [RawTaxAmount] IS NULL AND [RoundingAdjustment] IS NULL AND [AllocationSequence] IS NULL AND [TaxRoundingScope] IS NULL AND [TaxRoundingMethod] IS NULL AND [TaxRoundingIncrement] IS NULL) OR ([CurrencyCode] IS NOT NULL AND LEN([CurrencyCode]) = 3 AND [CurrencyDecimalPlaces] BETWEEN 0 AND 4 AND [RawTaxAmount] IS NOT NULL AND [RoundingAdjustment] IS NOT NULL AND [AllocationSequence] IS NOT NULL AND [TaxRoundingScope] IN (0, 1, 2) AND [TaxRoundingMethod] IN (0, 1, 2) AND [TaxRoundingIncrement] > 0)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_SupplierDebitNoteTaxComponents_PrecisionEvidence",
            table: "SupplierDebitNoteTaxComponents");

        foreach (var column in new[]
        {
            "CurrencyCode", "CurrencyDecimalPlaces", "RawTaxAmount", "RoundingAdjustment",
            "AllocationSequence", "TaxRoundingScope", "TaxRoundingMethod", "TaxRoundingIncrement"
        })
            migrationBuilder.DropColumn(column, "SupplierDebitNoteTaxComponents");

        migrationBuilder.AlterColumn<decimal>("TaxableAmount", "SupplierDebitNoteTaxComponents",
            type: "decimal(18,2)", precision: 18, scale: 2, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(20,10)", oldPrecision: 20, oldScale: 10);
        migrationBuilder.AlterColumn<decimal>("TaxRate", "SupplierDebitNoteTaxComponents",
            type: "decimal(18,4)", precision: 18, scale: 4, nullable: false,
            oldClrType: typeof(decimal), oldType: "decimal(18,6)", oldPrecision: 18, oldScale: 6);

        AlterUnitPrices(migrationBuilder, "decimal(18,2)", 18, 2, "decimal(20,6)", 20, 6);
        AlterAmounts(migrationBuilder, "decimal(18,2)", 18, 2, "decimal(20,4)", 20, 4);
    }

    private static void AlterAmounts(
        MigrationBuilder migrationBuilder,
        string type,
        int precision,
        int scale,
        string oldType,
        int oldPrecision,
        int oldScale)
    {
        foreach (var (table, column) in new[]
        {
            ("Invoices", "SubTotal"), ("Invoices", "DiscountAmount"), ("Invoices", "TotalAmount"),
            ("Invoices", "PaidAmount"), ("Invoices", "CreditedAmount"), ("Invoices", "BaseCurrencyAmount"),
            ("Invoices", "EarlyPaymentDiscountAmount"), ("InvoiceLineItem", "DiscountAmount"),
            ("VendorInvoice", "SubTotal"), ("VendorInvoice", "DiscountAmount"), ("VendorInvoice", "TotalAmount"),
            ("VendorInvoice", "PaidAmount"), ("VendorInvoice", "BaseCurrencyAmount"),
            ("VendorInvoice", "EarlyPaymentDiscountAmount"), ("VendorInvoice", "WithholdingTaxAmount"),
            ("VendorInvoiceLineItem", "DiscountAmount"),
            ("SupplierDebitNotes", "DirectInvoiceAppliedAmount"), ("SupplierDebitNotes", "SubTotal"),
            ("SupplierDebitNotes", "TaxAmount"), ("SupplierDebitNotes", "DiscountAmount"),
            ("SupplierDebitNotes", "TotalAmount"), ("SupplierDebitNotes", "BaseCurrencyAmount"),
            ("SupplierDebitNoteLineItems", "TaxAmount"), ("SupplierDebitNoteLineItems", "DiscountAmount"),
            ("SupplierDebitNoteLineItems", "LineTotal"), ("SupplierDebitNoteTaxComponents", "BaseAmount"),
            ("SupplierDebitNoteTaxComponents", "TaxAmount")
        })
            migrationBuilder.AlterColumn<decimal>(column, table, type,
                precision: precision, scale: scale, nullable: false,
                oldClrType: typeof(decimal), oldType: oldType,
                oldPrecision: oldPrecision, oldScale: oldScale);
    }

    private static void AlterUnitPrices(
        MigrationBuilder migrationBuilder,
        string type,
        int precision,
        int scale,
        string oldType,
        int oldPrecision,
        int oldScale)
    {
        foreach (var (table, column) in new[]
        {
            ("InvoiceLineItem", "UnitPrice"),
            ("VendorInvoiceLineItem", "UnitPrice"),
            ("SupplierDebitNoteLineItems", "UnitPrice")
        })
            migrationBuilder.AlterColumn<decimal>(column, table, type,
                precision: precision, scale: scale, nullable: false,
                oldClrType: typeof(decimal), oldType: oldType,
                oldPrecision: oldPrecision, oldScale: oldScale);
    }
}
