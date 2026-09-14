using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260913030500_InventoryItemPostingAccounts")]
public sealed class InventoryItemPostingAccounts : Migration
{
    private static readonly string[] Columns =
    {
        "InventoryAccountId",
        "InventoryOffsetAccountId",
        "CostOfGoodsSoldAccountId",
        "SalesAccountId",
        "MarkdownsAccountId",
        "SalesReturnsAccountId",
        "InUseAccountId",
        "InServiceAccountId",
        "DamagedAccountId",
        "VarianceAccountId",
        "DropShipItemsAccountId",
        "PurchasePriceVarianceAccountId",
        "UnrealisedPurchasePriceVarianceAccountId",
        "InventoryReturnsAccountId",
        "AssemblyVarianceAccountId",
        "StandardCostRevaluationAccountId",
    };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.AddColumn<Guid>(column, "InventoryItems", nullable: true);
            migrationBuilder.CreateIndex($"IX_InventoryItems_{column}", "InventoryItems", column);
            migrationBuilder.AddForeignKey($"FK_InventoryItems_Accounts_{column}", "InventoryItems", column,
                "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.DropForeignKey($"FK_InventoryItems_Accounts_{column}", "InventoryItems");
            migrationBuilder.DropIndex($"IX_InventoryItems_{column}", "InventoryItems");
            migrationBuilder.DropColumn(column, "InventoryItems");
        }
    }
}
