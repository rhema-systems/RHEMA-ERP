using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260823011000_AllowProcurementBudgetAcrossPlans")]
public sealed class AllowProcurementBudgetAcrossPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "BudgetId",
            table: "ProcurementPlans",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE planRecord
               SET planRecord.BudgetId = budget.Id
            FROM dbo.ProcurementPlans planRecord
            INNER JOIN dbo.ProcurementBudgets budget
                ON budget.ProcurementPlanId = planRecord.Id
               AND budget.TenantId = planRecord.TenantId
               AND budget.IsDeleted = 0
            WHERE planRecord.BudgetId IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_ProcurementPlans_BudgetId",
            table: "ProcurementPlans",
            column: "BudgetId");

        migrationBuilder.AddForeignKey(
            name: "FK_ProcurementPlans_ProcurementBudgets_BudgetId",
            table: "ProcurementPlans",
            column: "BudgetId",
            principalTable: "ProcurementBudgets",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ProcurementPlans_ProcurementBudgets_BudgetId",
            table: "ProcurementPlans");

        migrationBuilder.DropIndex(
            name: "IX_ProcurementPlans_BudgetId",
            table: "ProcurementPlans");

        migrationBuilder.DropColumn(
            name: "BudgetId",
            table: "ProcurementPlans");
    }
}
