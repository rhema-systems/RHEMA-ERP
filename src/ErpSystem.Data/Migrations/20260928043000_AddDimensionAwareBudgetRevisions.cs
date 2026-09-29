using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext)), Migration("20260928043000_AddDimensionAwareBudgetRevisions")]
public partial class AddDimensionAwareBudgetRevisions : Migration
{
    private const string LegacyCellIndex =
        "IX_BudgetRevisionLines_TenantId_BudgetRevisionId_SegmentValueId_AccountId_FiscalPeriodId";
    private const string DimensionCellIndex = "UX_BudgetRevisionLines_Cell";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: LegacyCellIndex,
            table: "BudgetRevisionLines");

        migrationBuilder.AddColumn<Guid>(
            name: "FinanceDimensionSetId",
            table: "BudgetRevisionLines",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_BudgetRevisionLines_FinanceDimensionSetId",
            table: "BudgetRevisionLines",
            column: "FinanceDimensionSetId");

        migrationBuilder.CreateIndex(
            name: DimensionCellIndex,
            table: "BudgetRevisionLines",
            columns: new[]
            {
                "TenantId",
                "BudgetRevisionId",
                "SegmentValueId",
                "AccountId",
                "FiscalPeriodId",
                "FinanceDimensionSetId"
            },
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.AddForeignKey(
            name: "FK_BudgetRevisionLines_FinanceDimensionSets_FinanceDimensionSetId",
            table: "BudgetRevisionLines",
            column: "FinanceDimensionSetId",
            principalTable: "FinanceDimensionSets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_BudgetRevisionLines_FinanceDimensionSets_FinanceDimensionSetId",
            table: "BudgetRevisionLines");

        migrationBuilder.DropIndex(
            name: "IX_BudgetRevisionLines_FinanceDimensionSetId",
            table: "BudgetRevisionLines");

        migrationBuilder.DropIndex(
            name: DimensionCellIndex,
            table: "BudgetRevisionLines");

        migrationBuilder.DropColumn(
            name: "FinanceDimensionSetId",
            table: "BudgetRevisionLines");

        migrationBuilder.CreateIndex(
            name: LegacyCellIndex,
            table: "BudgetRevisionLines",
            columns: new[]
            {
                "TenantId",
                "BudgetRevisionId",
                "SegmentValueId",
                "AccountId",
                "FiscalPeriodId"
            },
            unique: true,
            filter: "[IsDeleted] = 0");
    }
}
