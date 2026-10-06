using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ErpSystem.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261006010000_AllowBankReconciliationRematchAfterUnmatch")]
public class AllowBankReconciliationRematchAfterUnmatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReconciliationMatch_BankStatementLineId",
            table: "ReconciliationMatch");

        migrationBuilder.DropIndex(
            name: "IX_ReconciliationMatch_CashTransactionId",
            table: "ReconciliationMatch");

        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationMatch_BankStatementLineId",
            table: "ReconciliationMatch",
            column: "BankStatementLineId",
            unique: true,
            filter: "[IsDeleted] = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationMatch_CashTransactionId",
            table: "ReconciliationMatch",
            column: "CashTransactionId",
            unique: true,
            filter: "[IsDeleted] = 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_ReconciliationMatch_BankStatementLineId",
            table: "ReconciliationMatch");

        migrationBuilder.DropIndex(
            name: "IX_ReconciliationMatch_CashTransactionId",
            table: "ReconciliationMatch");

        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationMatch_BankStatementLineId",
            table: "ReconciliationMatch",
            column: "BankStatementLineId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ReconciliationMatch_CashTransactionId",
            table: "ReconciliationMatch",
            column: "CashTransactionId");
    }
}
