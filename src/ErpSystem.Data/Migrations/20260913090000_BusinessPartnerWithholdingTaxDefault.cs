using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260913090000_BusinessPartnerWithholdingTaxDefault")]
public sealed class BusinessPartnerWithholdingTaxDefault : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("DefaultWithholdingTaxId", "BusinessPartners", nullable: true);
        migrationBuilder.CreateIndex("IX_BusinessPartners_DefaultWithholdingTaxId", "BusinessPartners", "DefaultWithholdingTaxId");
        migrationBuilder.AddForeignKey("FK_BusinessPartners_Taxes_DefaultWithholdingTaxId", "BusinessPartners",
            "DefaultWithholdingTaxId", "Taxes", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddColumn<bool>("ApplySupplierWithholdingDefaults", "VendorInvoice", nullable: true);
        migrationBuilder.AddColumn<decimal>("WithholdingTaxRateOverride", "VendorInvoice", type: "decimal(18,4)", nullable: true);
        migrationBuilder.AddColumn<bool>("WithholdingDecisionPending", "VendorInvoice", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_BusinessPartners_Taxes_DefaultWithholdingTaxId", "BusinessPartners");
        migrationBuilder.DropIndex("IX_BusinessPartners_DefaultWithholdingTaxId", "BusinessPartners");
        migrationBuilder.DropColumn("DefaultWithholdingTaxId", "BusinessPartners");
        migrationBuilder.DropColumn("ApplySupplierWithholdingDefaults", "VendorInvoice");
        migrationBuilder.DropColumn("WithholdingTaxRateOverride", "VendorInvoice");
        migrationBuilder.DropColumn("WithholdingDecisionPending", "VendorInvoice");
    }
}
