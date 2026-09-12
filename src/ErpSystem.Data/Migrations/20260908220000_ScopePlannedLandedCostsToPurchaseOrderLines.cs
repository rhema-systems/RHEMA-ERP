using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260908220000_ScopePlannedLandedCostsToPurchaseOrderLines")]
public class ScopePlannedLandedCostsToPurchaseOrderLines : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "PurchaseOrderLandedCostPlanItems", "LandedCostItems" })
        {
            migrationBuilder.AddColumn<Guid>("PurchaseOrderItemId", table, type: "uniqueidentifier", nullable: true);
            migrationBuilder.CreateIndex($"IX_{table}_PurchaseOrderItemId", table, "PurchaseOrderItemId");
            migrationBuilder.AddForeignKey($"FK_{table}_PurchaseOrderItems_PurchaseOrderItemId",
                table, "PurchaseOrderItemId", "PurchaseOrderItems", principalColumn: "Id", onDelete: ReferentialAction.NoAction);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not silently turn line-specific costs into shared costs on rollback.
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [PurchaseOrderLandedCostPlanItems] WHERE [PurchaseOrderItemId] IS NOT NULL) OR EXISTS (SELECT 1 FROM [LandedCostItems] WHERE [PurchaseOrderItemId] IS NOT NULL) THROW 51000, 'Cannot remove scope while line-specific landed costs exist.', 1;");
        foreach (var table in new[] { "PurchaseOrderLandedCostPlanItems", "LandedCostItems" })
        {
            migrationBuilder.DropForeignKey($"FK_{table}_PurchaseOrderItems_PurchaseOrderItemId", table);
            migrationBuilder.DropIndex($"IX_{table}_PurchaseOrderItemId", table);
            migrationBuilder.DropColumn("PurchaseOrderItemId", table);
        }
    }
}
