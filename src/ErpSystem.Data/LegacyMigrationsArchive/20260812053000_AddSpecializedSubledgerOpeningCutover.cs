using ErpSystem.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

/// <summary>
/// Adds the immutable source lineage and transaction-currency evidence required to bring
/// supplier/customer advances and withholding balances across TDC's cutover boundary.
/// This is intentionally a focused manual migration because the shared snapshot contains
/// unrelated module drift; generating a database-wide migration would be unsafe.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260812053000_AddSpecializedSubledgerOpeningCutover")]
public partial class AddSpecializedSubledgerOpeningCutover : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddOpeningSourceColumns(migrationBuilder, "VendorPayment");
        AddOpeningSourceColumns(migrationBuilder, "CustomerPayment");

        // Functional debit/credit remains the GL truth. These nullable values preserve the
        // native-currency quantities that later advance allocations need for realised-FX work.
        migrationBuilder.AddColumn<decimal>(
            name: "TransactionDebitAmount",
            table: "OpeningBalanceLines",
            type: "decimal(18,2)",
            nullable: true);
        migrationBuilder.AddColumn<decimal>(
            name: "TransactionCreditAmount",
            table: "OpeningBalanceLines",
            type: "decimal(18,2)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("TransactionCreditAmount", "OpeningBalanceLines");
        migrationBuilder.DropColumn("TransactionDebitAmount", "OpeningBalanceLines");

        DropOpeningSourceColumns(migrationBuilder, "CustomerPayment");
        DropOpeningSourceColumns(migrationBuilder, "VendorPayment");
    }

    private static void AddOpeningSourceColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OpeningBalanceBatchId",
            table: table,
            type: "uniqueidentifier",
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "OpeningBalanceType",
            table: table,
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);
        migrationBuilder.AddColumn<string>(
            name: "OpeningSourceReference",
            table: table,
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: $"IX_{table}_TenantId_OpeningBalanceBatchId",
            table: table,
            columns: new[] { "TenantId", "OpeningBalanceBatchId" },
            filter: "[OpeningBalanceBatchId] IS NOT NULL");
    }

    private static void DropOpeningSourceColumns(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.DropIndex($"IX_{table}_TenantId_OpeningBalanceBatchId", table);
        migrationBuilder.DropColumn("OpeningSourceReference", table);
        migrationBuilder.DropColumn("OpeningBalanceType", table);
        migrationBuilder.DropColumn("OpeningBalanceBatchId", table);
    }
}
