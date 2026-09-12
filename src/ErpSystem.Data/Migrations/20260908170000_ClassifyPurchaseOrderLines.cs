using ErpSystem.Core.Enums;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908170000_ClassifyPurchaseOrderLines")]
public class ClassifyPurchaseOrderLines : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Legacy rows retain their existing stock behaviour; do not reinterpret historic receipts.
        migrationBuilder.AddColumn<int>(name: "LineType", table: "PurchaseOrderItems",
            type: "int", nullable: false, defaultValue: (int)ItemType.StockItem);
        migrationBuilder.AddCheckConstraint(name: "CK_PurchaseOrderItems_LineType",
            table: "PurchaseOrderItems", sql: "[LineType] IN (1, 2, 3, 4)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_PurchaseOrderItems_LineType", "PurchaseOrderItems");
        migrationBuilder.DropColumn("LineType", "PurchaseOrderItems");
    }
}
