using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Enforces the Finance manifest rule that behavioural control roles are singleton per tenant/book.
/// Cash and Bank remain repeatable. Attributes are intentionally retained on the executable migration
/// so fast-build migration discovery does not depend on generated historical target-model metadata.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903120000_EnforceFinanceClassificationSystemRoleCardinality")]
public partial class EnforceFinanceClassificationSystemRoleCardinality : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM [AccountClassifications]
                WHERE [IsDeleted] = 0 AND [SystemRole] IS NOT NULL AND [SystemRole] NOT IN (1, 2)
                GROUP BY [TenantId], [AccountingBookId], [SystemRole]
                HAVING COUNT_BIG(*) > 1)
                THROW 51000, 'Duplicate singleton Finance classification system roles must be resolved before this migration can continue.', 1;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_AccountClassifications_TenantId_AccountingBookId_SystemRole",
            table: "AccountClassifications",
            columns: new[] { "TenantId", "AccountingBookId", "SystemRole" },
            unique: true,
            // SQL Server filtered indexes accept conjunctions of simple comparisons, but
            // reject NOT IN in the filter grammar even though it is valid query syntax.
            filter: "[IsDeleted] = 0 AND [SystemRole] IS NOT NULL AND [SystemRole] <> 1 AND [SystemRole] <> 2");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropIndex(
            name: "IX_AccountClassifications_TenantId_AccountingBookId_SystemRole",
            table: "AccountClassifications");
}
