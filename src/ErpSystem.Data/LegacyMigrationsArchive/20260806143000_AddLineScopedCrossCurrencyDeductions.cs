using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds immutable functional-currency evidence for allocation-level discounts and statutory
/// deductions. Native amounts remain in invoice currency; these columns retain the exact values
/// posted to Finance and used by WHT/VAT-WHT reporting.
///
/// This migration is deliberately hand-scoped. The shared snapshot contains unrelated historical
/// drift, so accepting a broad generated migration would risk changes outside the Finance slice.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260806143000_AddLineScopedCrossCurrencyDeductions")]
public sealed class AddLineScopedCrossCurrencyDeductions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>("DiscountFunctionalAmount", "VendorPaymentAllocation", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("WithholdingTaxFunctionalAmount", "VendorPaymentAllocation", "decimal(18,2)", nullable: false, defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>("DiscountFunctionalAmount", "PaymentAllocation", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("WithholdingTaxAmount", "PaymentAllocation", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("WithholdingTaxFunctionalAmount", "PaymentAllocation", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("VatWithholdingAmount", "PaymentAllocation", "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>("VatWithholdingFunctionalAmount", "PaymentAllocation", "decimal(18,2)", nullable: false, defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("DiscountFunctionalAmount", "VendorPaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxFunctionalAmount", "VendorPaymentAllocation");

        migrationBuilder.DropColumn("DiscountFunctionalAmount", "PaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxAmount", "PaymentAllocation");
        migrationBuilder.DropColumn("WithholdingTaxFunctionalAmount", "PaymentAllocation");
        migrationBuilder.DropColumn("VatWithholdingAmount", "PaymentAllocation");
        migrationBuilder.DropColumn("VatWithholdingFunctionalAmount", "PaymentAllocation");
    }
}
