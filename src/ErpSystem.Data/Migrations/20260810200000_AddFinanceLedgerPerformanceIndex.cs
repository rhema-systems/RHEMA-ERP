using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the ledger access path measured by the NFR-PER Finance workload review. This migration is
/// intentionally narrow because the repository's design-time snapshot has unrelated drift and an
/// automatically generated migration would attempt to recreate tables owned by other modules.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260810200000_AddFinanceLedgerPerformanceIndex")]
public partial class AddFinanceLedgerPerformanceIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Tenant and book lead the key because every supported ledger query is isolated to both.
        // Date supports as-at/range reports, while AccountId remains useful for drill-down without
        // preventing an all-account trial balance from using the same leading index columns.
        migrationBuilder.CreateIndex(
            name: "IX_AccountTransactions_TenantId_BookClassification_TransactionDate_AccountId",
            table: "AccountTransactions",
            columns: new[] { "TenantId", "BookClassification", "TransactionDate", "AccountId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AccountTransactions_TenantId_BookClassification_TransactionDate_AccountId",
            table: "AccountTransactions");
    }
}
