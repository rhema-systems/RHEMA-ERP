using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260920021000_ConstrainAccountingBookApplicabilityPriority")]
public sealed class ConstrainAccountingBookApplicabilityPriority : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_AccountingBookApplicabilityRules_Priority",
            table: "AccountingBookApplicabilityRules");

        migrationBuilder.AddCheckConstraint(
            name: "CK_AccountingBookApplicabilityRules_Priority",
            table: "AccountingBookApplicabilityRules",
            sql: "[Priority] >= 0 AND [Priority] <= 1000");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_AccountingBookApplicabilityRules_Priority",
            table: "AccountingBookApplicabilityRules");

        migrationBuilder.AddCheckConstraint(
            name: "CK_AccountingBookApplicabilityRules_Priority",
            table: "AccountingBookApplicabilityRules",
            sql: "[Priority] >= 0");
    }
}
