using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260930000200_ApWithholdingNetBasisEvidence")]
public sealed class ApWithholdingNetBasisEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<decimal>(
        name: "WithholdingTaxBaseFunctionalAmount", table: "VendorPaymentAllocation",
        type: "decimal(18,2)", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [VendorPaymentAllocation] WHERE [WithholdingTaxBaseFunctionalAmount] IS NOT NULL) THROW 51000, 'Cannot remove frozen AP withholding evidence. Use an approved retention and rollback plan.', 1;");
        migrationBuilder.DropColumn(
            name: "WithholdingTaxBaseFunctionalAmount", table: "VendorPaymentAllocation");
    }
}
