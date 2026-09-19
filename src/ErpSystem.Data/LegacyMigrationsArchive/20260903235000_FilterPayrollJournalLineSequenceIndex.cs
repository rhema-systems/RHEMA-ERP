using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260903235000_FilterPayrollJournalLineSequenceIndex")]
public sealed class FilterPayrollJournalLineSequenceIndex : Migration
{
    private const string IndexName =
        "IX_PayrollJournalLines_TenantId_PayrollRunId_SequenceNo";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: IndexName,
            table: "PayrollJournalLines");

        migrationBuilder.CreateIndex(
            name: IndexName,
            table: "PayrollJournalLines",
            columns: new[] { "TenantId", "PayrollRunId", "SequenceNo" },
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: IndexName,
            table: "PayrollJournalLines");

        // The original unfiltered index cannot coexist with retained soft-delete
        // history that reuses a sequence number.
        migrationBuilder.Sql(
            "DELETE FROM [PayrollJournalLines] WHERE [IsDeleted] = 1;");

        migrationBuilder.CreateIndex(
            name: IndexName,
            table: "PayrollJournalLines",
            columns: new[] { "TenantId", "PayrollRunId", "SequenceNo" },
            unique: true);
    }
}
