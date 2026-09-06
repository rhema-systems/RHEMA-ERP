using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260906010000_AddGovernedSupplierRiskDecisionWithdrawal")]
public sealed class AddGovernedSupplierRiskDecisionWithdrawal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            "CK_ProcurementConfigurationDecisions_Status", "ProcurementConfigurationDecisions");
        migrationBuilder.AddCheckConstraint(
            "CK_ProcurementConfigurationDecisions_Status", "ProcurementConfigurationDecisions",
            "[Status] IN (0, 1, 2, 3) OR ([Status] = 4 AND [DecisionKey] = 'DEC-011')");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Never delete, relabel, or reactivate a withdrawn decision to downgrade.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM [dbo].[ProcurementConfigurationDecisions] WHERE [Status] = 4)
               OR EXISTS (SELECT 1 FROM [dbo].[ProcurementConfigurationRevisions]
                          WHERE [Action] = 'WithdrawDecision' AND [Result] = 'Succeeded')
                THROW 51213, 'DEC-011 withdrawal history must be preserved; rollback requires a reviewed recovery migration.', 1;
            """);
        migrationBuilder.DropCheckConstraint(
            "CK_ProcurementConfigurationDecisions_Status", "ProcurementConfigurationDecisions");
        migrationBuilder.AddCheckConstraint(
            "CK_ProcurementConfigurationDecisions_Status", "ProcurementConfigurationDecisions",
            "[Status] IN (0, 1, 2, 3)");
    }
}
