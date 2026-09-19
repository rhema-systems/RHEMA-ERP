using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909213000_LinkLandedCostsToSupplierInvoiceLines")]
public class LinkLandedCostsToSupplierInvoiceLines : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("LandedCostItemId", "VendorInvoiceLineItem", type: "uniqueidentifier", nullable: true);
        migrationBuilder.CreateIndex("IX_VendorInvoiceLineItem_LandedCostItemId", "VendorInvoiceLineItem", "LandedCostItemId",
            unique: true, filter: "[LandedCostItemId] IS NOT NULL AND [IsDeleted] = 0");
        migrationBuilder.AddForeignKey("FK_VendorInvoiceLineItem_LandedCostItems_LandedCostItemId", "VendorInvoiceLineItem", "LandedCostItemId",
            "LandedCostItems", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_VendorInvoiceLineItem_LandedCostItems_LandedCostItemId", "VendorInvoiceLineItem");
        migrationBuilder.DropIndex("IX_VendorInvoiceLineItem_LandedCostItemId", "VendorInvoiceLineItem");
        migrationBuilder.DropColumn("LandedCostItemId", "VendorInvoiceLineItem");
    }
}
