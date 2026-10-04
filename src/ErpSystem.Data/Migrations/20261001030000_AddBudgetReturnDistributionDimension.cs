using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20261001030000_AddBudgetReturnDistributionDimension")]
public partial class AddBudgetReturnDistributionDimension : Migration
{
    private const string LegacyUniqueIndex =
        "IX_BudgetReturns_TenantId_BudgetScenarioId_SegmentValueId";
    private const string DistributionUniqueIndex =
        "IX_BudgetReturns_TenantId_BudgetScenarioId_DistributionDimensionValueId";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: LegacyUniqueIndex,
            table: "BudgetReturns");

        migrationBuilder.AddColumn<Guid>(
            name: "DistributionDimensionValueId",
            table: "BudgetReturns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BudgetReturns_DistributionDimensionValueId",
            table: "BudgetReturns",
            column: "DistributionDimensionValueId");

        migrationBuilder.CreateIndex(
            name: LegacyUniqueIndex,
            table: "BudgetReturns",
            columns: new[] { "TenantId", "BudgetScenarioId", "SegmentValueId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [SegmentValueId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: DistributionUniqueIndex,
            table: "BudgetReturns",
            columns: new[] { "TenantId", "BudgetScenarioId", "DistributionDimensionValueId" },
            unique: true,
            filter: "[IsDeleted] = 0 AND [DistributionDimensionValueId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_BudgetReturns_FinanceDimensionValues_DistributionDimensionValueId",
            table: "BudgetReturns",
            column: "DistributionDimensionValueId",
            principalTable: "FinanceDimensionValues",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BudgetReturns_FinanceDimensionValues_DistributionDimensionValueId",
            table: "BudgetReturns");

        migrationBuilder.DropIndex(
            name: "IX_BudgetReturns_DistributionDimensionValueId",
            table: "BudgetReturns");

        migrationBuilder.DropIndex(
            name: LegacyUniqueIndex,
            table: "BudgetReturns");

        migrationBuilder.DropIndex(
            name: DistributionUniqueIndex,
            table: "BudgetReturns");

        migrationBuilder.DropColumn(
            name: "DistributionDimensionValueId",
            table: "BudgetReturns");

        migrationBuilder.CreateIndex(
            name: LegacyUniqueIndex,
            table: "BudgetReturns",
            columns: new[] { "TenantId", "BudgetScenarioId", "SegmentValueId" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }
}
